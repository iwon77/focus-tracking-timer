using FocusTrackingTimer.Core.Persistence;
using FocusTrackingTimer.Core.Tracking;
using Microsoft.Data.Sqlite;

namespace FocusTrackingTimer.Core.Tests;

public sealed class SqliteProjectTimerStoreTests
{
    [Fact]
    public void SaveAndLoadStateRoundTripsProjectsProgramsAndCompletedRecords()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "FocusTrackingTimer.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(tempDirectory, "focus-tracking-timer.db");

        try
        {
            ProjectTimerEngine engine = new();
            Assert.True(engine.TryAddProject("Work", out ProjectDefinition workProject));
            Assert.True(engine.TryAddProject("Study", out ProjectDefinition studyProject));
            Assert.True(engine.TrySetProjectPinned(studyProject.Id, true));
            engine.UpdateProjectMemo(studyProject.Id, "Study memo");

            DateTimeOffset codeRegisteredAt = new(2026, 6, 4, 9, 0, 0, TimeSpan.Zero);
            DateTimeOffset docsRegisteredAt = new(2026, 6, 4, 9, 5, 0, TimeSpan.Zero);
            Assert.True(engine.TryRegisterProgram(workProject.Id, new TrackedApplication("code", "Code"), codeRegisteredAt));
            Assert.True(engine.TryRegisterProgram(workProject.Id, new TrackedApplication("chrome", "Chrome"), docsRegisteredAt));
            Assert.True(engine.TryUpdateProgram(workProject.Id, "code", new TrackedApplication("code", "Visual Studio Code")));
            Assert.True(engine.TrySetProgramPinned(workProject.Id, "chrome", true));

            DateTimeOffset startedAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
            engine.StartProject(workProject.Id, startedAt);
            engine.ObserveFocusedProgram("code", startedAt);
            engine.ObserveFocusedProgram("chrome", startedAt.AddMinutes(15));
            ProjectTimerRecord record = engine.StopProject(startedAt.AddMinutes(25));

            SqliteProjectTimerStore store = new(databasePath);
            store.SaveState(engine.CreateStateSnapshot());
            ProjectTimerEngineState loadedState = store.LoadState();

            Assert.Equal(2, loadedState.Projects.Count);

            ProjectState loadedWorkProject = Assert.Single(loadedState.Projects, project => project.Name == "Work");
            Assert.Collection(
                loadedWorkProject.RegisteredPrograms,
                registration =>
                {
                    Assert.Equal("chrome", registration.Program.ProcessName);
                    Assert.Equal("Chrome", registration.Program.DisplayName);
                    Assert.Equal("Chrome", registration.InitialDisplayName);
                    Assert.Equal(docsRegisteredAt, registration.RegisteredAt);
                    Assert.True(registration.IsPinned);
                },
                registration =>
                {
                    Assert.Equal("code", registration.Program.ProcessName);
                    Assert.Equal("Visual Studio Code", registration.Program.DisplayName);
                    Assert.Equal("Code", registration.InitialDisplayName);
                    Assert.Equal(codeRegisteredAt, registration.RegisteredAt);
                });

            ProjectState loadedStudyProject = Assert.Single(loadedState.Projects, project => project.Name == "Study");
            Assert.True(loadedStudyProject.IsPinned);
            Assert.Equal("Study memo", loadedStudyProject.Memo);

            ProjectTimerRecord loadedRecord = Assert.Single(loadedState.CompletedRecords);
            Assert.Equal(record.ProjectId, loadedRecord.ProjectId);
            Assert.Equal(record.ProjectName, loadedRecord.ProjectName);
            Assert.Equal(record.StartedAt, loadedRecord.StartedAt);
            Assert.Equal(record.EndedAt, loadedRecord.EndedAt);
            Assert.Collection(
                loadedRecord.FocusSegments,
                segment =>
                {
                    Assert.Equal("code", segment.Program.ProcessName);
                    Assert.Equal("Visual Studio Code", segment.Program.DisplayName);
                    Assert.Equal(startedAt, segment.StartedAt);
                    Assert.Equal(startedAt.AddMinutes(15), segment.EndedAt);
                },
                segment =>
                {
                    Assert.Equal("chrome", segment.Program.ProcessName);
                    Assert.Equal("Chrome", segment.Program.DisplayName);
                    Assert.Equal(startedAt.AddMinutes(15), segment.StartedAt);
                    Assert.Equal(startedAt.AddMinutes(25), segment.EndedAt);
                });

            ProjectTimerEngine restoredEngine = new();
            restoredEngine.ReplaceState(loadedState);

            Assert.Equal(2, restoredEngine.Projects.Count);
            Assert.Equal(TimeSpan.FromMinutes(25), restoredEngine.GetProjectTotalDuration(workProject.Id, startedAt.AddMinutes(25)));
            Assert.Equal(TimeSpan.Zero, restoredEngine.GetProjectTotalDuration(studyProject.Id, startedAt.AddMinutes(25)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadStateKeepsDeletedProjectsForStatisticsWithoutRestoringThemToActiveList()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "FocusTrackingTimer.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(tempDirectory, "focus-tracking-timer.db");

        try
        {
            ProjectTimerEngine engine = new();
            Assert.True(engine.TryAddProject("Archive", out ProjectDefinition archiveProject));
            Assert.True(engine.TryRegisterProgram(archiveProject.Id, new TrackedApplication("code", "Code")));

            DateTimeOffset startedAt = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);
            engine.StartProject(archiveProject.Id, startedAt);
            engine.ObserveFocusedProgram("code", startedAt);
            engine.StopProject(startedAt.AddMinutes(30));
            Assert.True(engine.TryRemoveProject(archiveProject.Id));

            SqliteProjectTimerStore store = new(databasePath);
            store.SaveState(engine.CreateStateSnapshot());
            ProjectTimerEngineState loadedState = store.LoadState();

            ProjectState deletedProject = Assert.Single(loadedState.Projects);
            Assert.True(deletedProject.IsDeleted);
            Assert.Single(loadedState.CompletedRecords);

            ProjectTimerEngine restoredEngine = new();
            restoredEngine.ReplaceState(loadedState);

            Assert.Empty(restoredEngine.Projects);
            DailyProjectDurationSummary summary = Assert.Single(restoredEngine.GetDailyProjectDurationSummaries(
                new DateOnly(2026, 6, 5),
                new DateOnly(2026, 6, 5),
                startedAt.AddMinutes(30)));
            Assert.Equal(archiveProject.Id, summary.ProjectId);
            Assert.Equal("Archive", summary.ProjectName);
            Assert.Equal(TimeSpan.FromMinutes(30), summary.TotalDuration);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
