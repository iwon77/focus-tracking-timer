using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.Core.Tests;

public class ProjectTimerEngineTests
{
    [Fact]
    public void StopProjectRecordsWallClockTimeButTotalDurationUsesRegisteredProgramFocusOnly()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("unity", "Unity")));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("unity", startedAt.AddMinutes(5));
        engine.ObserveFocusedProgram("code", startedAt.AddMinutes(20));
        engine.ObserveFocusedProgram(null, startedAt.AddMinutes(25));
        ProjectTimerRecord record = engine.StopProject(startedAt.AddMinutes(40));

        Assert.Equal(TimeSpan.FromMinutes(40), record.WallClockDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), record.TotalDuration);
        Assert.Collection(
            record.ProgramSummaries.OrderBy(summary => summary.Program.ProcessName),
            summary =>
            {
                Assert.Equal("code", summary.Program.ProcessName);
                Assert.Equal(TimeSpan.FromMinutes(5), summary.FocusDuration);
            },
            summary =>
            {
                Assert.Equal("unity", summary.Program.ProcessName);
                Assert.Equal(TimeSpan.FromMinutes(15), summary.FocusDuration);
            });
    }

    [Fact]
    public void CurrentRunDurationOnlyIncreasesWhileRegisteredProgramIsFocused()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Study", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        engine.StartProject(project.Id, startedAt);
        Assert.Equal(TimeSpan.Zero, engine.GetCurrentRunDuration(project.Id, startedAt.AddMinutes(10)));

        engine.ObserveFocusedProgram("code", startedAt.AddMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(5), engine.GetCurrentRunDuration(project.Id, startedAt.AddMinutes(15)));

        engine.ObserveFocusedProgram("unknown", startedAt.AddMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(5), engine.GetCurrentRunDuration(project.Id, startedAt.AddMinutes(30)));
    }

    [Fact]
    public void CurrentWallClockDurationIncreasesAfterProjectStarts()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Study", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        engine.StartProject(project.Id, startedAt);

        Assert.Equal(TimeSpan.FromMinutes(10), engine.GetCurrentWallClockDuration(project.Id, startedAt.AddMinutes(10)));
        Assert.Equal(TimeSpan.Zero, engine.GetCurrentRunDuration(project.Id, startedAt.AddMinutes(10)));
    }

    [Fact]
    public void GetProjectTotalDurationIncludesCompletedAndActiveFocusDurations()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Study", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("code", startedAt);
        engine.StopProject(startedAt.AddMinutes(10));
        engine.StartProject(project.Id, startedAt.AddMinutes(20));
        engine.ObserveFocusedProgram("code", startedAt.AddMinutes(25));

        TimeSpan totalDuration = engine.GetProjectTotalDuration(project.Id, startedAt.AddMinutes(35));

        Assert.Equal(TimeSpan.FromMinutes(20), totalDuration);
    }

    [Fact]
    public void CurrentSessionProgramSummariesDoNotIncludePreviousSessions()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Study", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset firstStartedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondStartedAt = firstStartedAt.AddHours(1);

        engine.StartProject(project.Id, firstStartedAt);
        engine.ObserveFocusedProgram("code", firstStartedAt);
        engine.StopProject(firstStartedAt.AddMinutes(20));
        engine.StartProject(project.Id, secondStartedAt);

        IReadOnlyList<ProgramFocusSummary> summaries = engine.GetCurrentSessionProgramSummaries(
            project.Id,
            secondStartedAt.AddMinutes(5));

        ProgramFocusSummary summary = Assert.Single(summaries);
        Assert.Equal("code", summary.Program.ProcessName);
        Assert.Equal(TimeSpan.Zero, summary.FocusDuration);
    }

    [Fact]
    public void GetProgramSummariesReturnsRegisteredProgramsWithZeroDuration()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Prototype", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("dotnet", ".NET")));

        IReadOnlyList<ProgramFocusSummary> summaries = engine.GetProgramSummaries(project.Id, DateTimeOffset.Now);

        Assert.Single(summaries);
        Assert.Equal("dotnet", summaries[0].Program.ProcessName);
        Assert.Equal(TimeSpan.Zero, summaries[0].FocusDuration);
    }

    [Fact]
    public void ProgramSummariesCanUseRegisteredOrderOrMostUsedOrder()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("unity", "Unity")));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("code", startedAt);
        engine.StopProject(startedAt.AddMinutes(10));

        IReadOnlyList<ProgramFocusSummary> registeredOrder = engine.GetProgramSummaries(
            project.Id,
            startedAt.AddMinutes(10),
            ProgramSortMode.Registered);
        IReadOnlyList<ProgramFocusSummary> mostUsedOrder = engine.GetProgramSummaries(
            project.Id,
            startedAt.AddMinutes(10),
            ProgramSortMode.MostUsed);

        Assert.Equal(["unity", "code"], registeredOrder.Select(summary => summary.Program.ProcessName));
        Assert.Equal(["code", "unity"], mostUsedOrder.Select(summary => summary.Program.ProcessName));
    }

    [Fact]
    public void ProgramOrderCanBeMovedManually()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("unity", "Unity")));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));

        Assert.True(engine.TryMoveProgram(project.Id, "code", -1));

        IReadOnlyList<ProgramFocusSummary> manualOrder = engine.GetProgramSummaries(
            project.Id,
            DateTimeOffset.Now,
            ProgramSortMode.Manual);

        Assert.Equal(["code", "unity"], manualOrder.Select(summary => summary.Program.ProcessName));
    }

    [Fact]
    public void RemovedProgramDoesNotAppearInProgramSummaries()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);
        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("code", startedAt);
        engine.StopProject(startedAt.AddMinutes(10));

        Assert.True(engine.TryRemoveProgram(project.Id, "code"));

        Assert.Empty(engine.GetProgramSummaries(project.Id, startedAt.AddMinutes(10)));
        Assert.Single(engine.GetRecentRecords(1, project.Id)[0].ProgramSummaries);
    }

    [Fact]
    public void RegisteredProgramInfoKeepsRegistrationTime()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        DateTimeOffset registeredAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code"), registeredAt));

        RegisteredProgramInfo registration = Assert.Single(engine.GetRegisteredProgramInfos(project.Id));
        Assert.Equal("code", registration.Program.ProcessName);
        Assert.Equal("Code", registration.InitialDisplayName);
        Assert.Equal(registeredAt, registration.RegisteredAt);
    }

    [Fact]
    public void ProjectCanBeRenamed()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));

        Assert.True(engine.TryRenameProject(project.Id, "Deep Work"));

        Assert.Equal("Deep Work", project.Name);
    }

    [Fact]
    public void ProjectRenameRejectsDuplicateName()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition work));
        Assert.True(engine.TryAddProject("Study", out _));

        Assert.False(engine.TryRenameProject(work.Id, "Study"));

        Assert.Equal("Work", work.Name);
    }

    [Fact]
    public void ProjectCanBeRemovedWithItsRecords()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        DateTimeOffset startedAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);
        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("code", startedAt);
        engine.StopProject(startedAt.AddMinutes(10));

        Assert.True(engine.TryRemoveProject(project.Id));

        Assert.Empty(engine.Projects);
        Assert.Empty(engine.CompletedRecords);
    }

    [Fact]
    public void UpdatingDisplayNameKeepsProcessNameAndRegistrationTime()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        DateTimeOffset registeredAt = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code"), registeredAt));

        Assert.True(engine.TryUpdateProgram(project.Id, "code", new TrackedApplication("code", "Coding Tool")));

        RegisteredProgramInfo registration = Assert.Single(engine.GetRegisteredProgramInfos(project.Id));
        Assert.Equal("code", registration.Program.ProcessName);
        Assert.Equal("Coding Tool", registration.Program.DisplayName);
        Assert.Equal("Code", registration.InitialDisplayName);
        Assert.Equal(registeredAt, registration.RegisteredAt);
    }

    [Fact]
    public void GetDailyDurationSummariesSplitFocusDurationsAcrossDateBoundaries()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition project));
        Assert.True(engine.TryRegisterProgram(project.Id, new TrackedApplication("code", "Code")));
        TimeSpan localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 6, 1, 23, 50, 0));
        DateTimeOffset startedAt = new(2026, 6, 1, 23, 50, 0, localOffset);
        DateTimeOffset endedAt = new(2026, 6, 2, 0, 20, 0, localOffset);

        engine.StartProject(project.Id, startedAt);
        engine.ObserveFocusedProgram("code", startedAt);
        engine.StopProject(endedAt);

        IReadOnlyList<DailyDurationSummary> summaries = engine.GetDailyDurationSummaries(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            endedAt);

        Assert.Collection(
            summaries,
            summary => Assert.Equal(TimeSpan.FromMinutes(10), summary.TotalDuration),
            summary => Assert.Equal(TimeSpan.FromMinutes(20), summary.TotalDuration),
            summary => Assert.Equal(TimeSpan.Zero, summary.TotalDuration));
    }

    [Fact]
    public void GetDailyProjectDurationSummariesReturnPerProjectBreakdownForTheSameDay()
    {
        ProjectTimerEngine engine = new();
        Assert.True(engine.TryAddProject("Work", out ProjectDefinition work));
        Assert.True(engine.TryAddProject("Study", out ProjectDefinition study));
        Assert.True(engine.TryRegisterProgram(work.Id, new TrackedApplication("code", "Code")));
        Assert.True(engine.TryRegisterProgram(study.Id, new TrackedApplication("word", "Word")));
        TimeSpan localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 6, 3, 18, 0, 0));
        DateTimeOffset observedAt = new(2026, 6, 3, 18, 0, 0, localOffset);

        engine.StartProject(work.Id, observedAt.AddHours(-4));
        engine.ObserveFocusedProgram("code", observedAt.AddHours(-4));
        engine.StopProject(observedAt.AddHours(-3).AddMinutes(-30));

        engine.StartProject(study.Id, observedAt.AddHours(-2));
        engine.ObserveFocusedProgram("word", observedAt.AddHours(-2));
        engine.StopProject(observedAt.AddHours(-1).AddMinutes(-40));

        IReadOnlyList<DailyProjectDurationSummary> summaries = engine.GetDailyProjectDurationSummaries(
            new DateOnly(2026, 6, 3),
            new DateOnly(2026, 6, 3),
            observedAt);

        Assert.Collection(
            summaries,
            summary =>
            {
                Assert.Equal(new DateOnly(2026, 6, 3), summary.Date);
                Assert.Equal(work.Id, summary.ProjectId);
                Assert.Equal("Work", summary.ProjectName);
                Assert.Equal(TimeSpan.FromMinutes(30), summary.TotalDuration);
            },
            summary =>
            {
                Assert.Equal(new DateOnly(2026, 6, 3), summary.Date);
                Assert.Equal(study.Id, summary.ProjectId);
                Assert.Equal("Study", summary.ProjectName);
                Assert.Equal(TimeSpan.FromMinutes(20), summary.TotalDuration);
            });
    }
}
