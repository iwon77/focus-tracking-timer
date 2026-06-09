using System.Collections.ObjectModel;
using System.Globalization;
using FocusTrackingTimer.Core.Tracking;
using Microsoft.Data.Sqlite;

namespace FocusTrackingTimer.Core.Persistence;

public sealed class SqliteProjectTimerStore
{
    public SqliteProjectTimerStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public ProjectTimerEngineState LoadState()
    {
        using SqliteConnection connection = OpenConnection();
        EnsureSchema(connection);

        List<ProjectState> projects = LoadProjects(connection);
        List<ProjectTimerRecord> completedRecords = LoadCompletedRecords(connection);
        return new ProjectTimerEngineState(projects, completedRecords);
    }

    public void SaveState(ProjectTimerEngineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using SqliteConnection connection = OpenConnection();
        EnsureSchema(connection);
        using SqliteTransaction transaction = connection.BeginTransaction();

        ExecuteNonQuery(
            connection,
            transaction,
            """
            DELETE FROM focus_segments;
            DELETE FROM completed_records;
            DELETE FROM registered_programs;
            DELETE FROM projects;
            """);

        SaveProjectCatalog(connection, transaction, state.Projects);

        for (int recordIndex = 0; recordIndex < state.CompletedRecords.Count; recordIndex++)
        {
            ProjectTimerRecord record = state.CompletedRecords[recordIndex];
            InsertCompletedRecord(connection, transaction, record, recordIndex);

            for (int segmentIndex = 0; segmentIndex < record.FocusSegments.Count; segmentIndex++)
            {
                InsertFocusSegment(connection, transaction, record.FocusSegments[segmentIndex], recordIndex, segmentIndex);
            }
        }

        transaction.Commit();
    }

    public void SaveProjectCatalog(IEnumerable<ProjectState> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        ReadOnlyCollection<ProjectState> projectList = new(projects.ToList());

        using SqliteConnection connection = OpenConnection();
        EnsureSchema(connection);
        using SqliteTransaction transaction = connection.BeginTransaction();

        SaveProjectCatalog(connection, transaction, projectList);
        transaction.Commit();
    }

    public void AppendCompletedRecord(ProjectTimerRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using SqliteConnection connection = OpenConnection();
        EnsureSchema(connection);
        using SqliteTransaction transaction = connection.BeginTransaction();

        int nextRecordOrder = GetNextRecordOrder(connection, transaction);
        InsertCompletedRecord(connection, transaction, record, nextRecordOrder);

        for (int segmentIndex = 0; segmentIndex < record.FocusSegments.Count; segmentIndex++)
        {
            InsertFocusSegment(connection, transaction, record.FocusSegments[segmentIndex], nextRecordOrder, segmentIndex);
        }

        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        string? directoryPath = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath
        }.ToString());

        connection.Open();

        using SqliteCommand pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        _ = pragmaCommand.ExecuteNonQuery();

        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        ExecuteNonQuery(
            connection,
            transaction: null,
            """
            CREATE TABLE IF NOT EXISTS projects (
                project_id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                sort_order INTEGER NOT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL DEFAULT '',
                is_pinned INTEGER NOT NULL DEFAULT 0,
                memo TEXT NOT NULL DEFAULT '',
                memo_updated_at TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS registered_programs (
                project_id TEXT NOT NULL,
                process_name TEXT NOT NULL,
                display_name TEXT NOT NULL,
                initial_display_name TEXT NOT NULL,
                registered_at TEXT NOT NULL,
                sort_order INTEGER NOT NULL,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (project_id, process_name),
                FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS completed_records (
                record_order INTEGER NOT NULL PRIMARY KEY,
                project_id TEXT NOT NULL,
                project_name TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                FOREIGN KEY (project_id) REFERENCES projects(project_id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS focus_segments (
                record_order INTEGER NOT NULL,
                segment_order INTEGER NOT NULL,
                process_name TEXT NOT NULL,
                display_name TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                PRIMARY KEY (record_order, segment_order),
                FOREIGN KEY (record_order) REFERENCES completed_records(record_order) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_completed_records_project_started_at
            ON completed_records (project_id, started_at);

            CREATE INDEX IF NOT EXISTS ix_completed_records_started_at
            ON completed_records (started_at);

            CREATE INDEX IF NOT EXISTS ix_focus_segments_record_order_started_at
            ON focus_segments (record_order, started_at);

            CREATE INDEX IF NOT EXISTS ix_focus_segments_started_at
            ON focus_segments (started_at);
            """);

        EnsureColumn(connection, "projects", "is_deleted", "ALTER TABLE projects ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(connection, "projects", "created_at", "ALTER TABLE projects ADD COLUMN created_at TEXT NOT NULL DEFAULT '';");
        EnsureColumn(connection, "projects", "is_pinned", "ALTER TABLE projects ADD COLUMN is_pinned INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(connection, "projects", "memo", "ALTER TABLE projects ADD COLUMN memo TEXT NOT NULL DEFAULT '';");
        EnsureColumn(connection, "projects", "memo_updated_at", "ALTER TABLE projects ADD COLUMN memo_updated_at TEXT NOT NULL DEFAULT '';");
        EnsureColumn(connection, "registered_programs", "is_pinned", "ALTER TABLE registered_programs ADD COLUMN is_pinned INTEGER NOT NULL DEFAULT 0;");
    }

    private static List<ProjectState> LoadProjects(SqliteConnection connection)
    {
        List<ProjectStateRow> rows = [];

        using (SqliteCommand projectCommand = connection.CreateCommand())
        {
            projectCommand.CommandText =
                """
                SELECT project_id, name, is_deleted, created_at, is_pinned, memo, memo_updated_at
                FROM projects
                ORDER BY sort_order;
                """;

            using SqliteDataReader reader = projectCommand.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ProjectStateRow(
                    ParseGuid(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetInt32(2) != 0,
                    ParseOptionalDateTimeOffset(reader.GetString(3)),
                    reader.GetInt32(4) != 0,
                    reader.GetString(5),
                    ParseOptionalDateTimeOffset(reader.GetString(6)),
                    []));
            }
        }

        Dictionary<Guid, List<RegisteredProgramInfo>> registrationsByProjectId = rows.ToDictionary(
            row => row.Id,
            row => row.RegisteredPrograms);

        using (SqliteCommand registrationCommand = connection.CreateCommand())
        {
            registrationCommand.CommandText =
                """
                SELECT project_id, process_name, display_name, initial_display_name, registered_at, is_pinned
                FROM registered_programs
                ORDER BY project_id, sort_order;
                """;

            using SqliteDataReader reader = registrationCommand.ExecuteReader();
            while (reader.Read())
            {
                Guid projectId = ParseGuid(reader.GetString(0));
                if (!registrationsByProjectId.TryGetValue(projectId, out List<RegisteredProgramInfo>? registrations))
                {
                    throw new InvalidOperationException("Registered program references a missing project.");
                }

                registrations.Add(new RegisteredProgramInfo(
                    new TrackedApplication(reader.GetString(1), reader.GetString(2)),
                    ParseDateTimeOffset(reader.GetString(4)),
                    reader.GetString(3),
                    reader.GetInt32(5) != 0));
            }
        }

        return [.. rows.Select(row => new ProjectState(
            row.Id,
            row.Name,
            row.RegisteredPrograms,
            row.IsDeleted,
            row.CreatedAt,
            row.IsPinned,
            row.Memo,
            row.MemoUpdatedAt))];
    }

    private static List<ProjectTimerRecord> LoadCompletedRecords(SqliteConnection connection)
    {
        List<CompletedRecordRow> rows = [];

        using (SqliteCommand recordCommand = connection.CreateCommand())
        {
            recordCommand.CommandText =
                """
                SELECT record_order, project_id, project_name, started_at, ended_at
                FROM completed_records
                ORDER BY record_order;
                """;

            using SqliteDataReader reader = recordCommand.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new CompletedRecordRow(
                    reader.GetInt32(0),
                    ParseGuid(reader.GetString(1)),
                    reader.GetString(2),
                    ParseDateTimeOffset(reader.GetString(3)),
                    ParseDateTimeOffset(reader.GetString(4)),
                    []));
            }
        }

        Dictionary<int, List<ProgramFocusSegment>> segmentsByRecordOrder = rows.ToDictionary(
            row => row.RecordOrder,
            row => row.FocusSegments);

        using (SqliteCommand segmentCommand = connection.CreateCommand())
        {
            segmentCommand.CommandText =
                """
                SELECT record_order, process_name, display_name, started_at, ended_at
                FROM focus_segments
                ORDER BY record_order, segment_order;
                """;

            using SqliteDataReader reader = segmentCommand.ExecuteReader();
            while (reader.Read())
            {
                int recordOrder = reader.GetInt32(0);
                if (!segmentsByRecordOrder.TryGetValue(recordOrder, out List<ProgramFocusSegment>? segments))
                {
                    throw new InvalidOperationException("Focus segment references a missing completed record.");
                }

                segments.Add(new ProgramFocusSegment(
                    new TrackedApplication(reader.GetString(1), reader.GetString(2)),
                    ParseDateTimeOffset(reader.GetString(3)),
                    ParseDateTimeOffset(reader.GetString(4))));
            }
        }

        return [.. rows.Select(row => new ProjectTimerRecord(
            row.ProjectId,
            row.ProjectName,
            row.StartedAt,
            row.EndedAt,
            row.FocusSegments))];
    }

    private static void InsertProject(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectState project,
        int sortOrder)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO projects (project_id, name, sort_order, is_deleted, created_at, is_pinned, memo, memo_updated_at)
            VALUES ($project_id, $name, $sort_order, $is_deleted, $created_at, $is_pinned, $memo, $memo_updated_at);
            """;
        _ = command.Parameters.AddWithValue("$project_id", project.Id.ToString("D"));
        _ = command.Parameters.AddWithValue("$name", project.Name);
        _ = command.Parameters.AddWithValue("$sort_order", sortOrder);
        _ = command.Parameters.AddWithValue("$is_deleted", project.IsDeleted ? 1 : 0);
        _ = command.Parameters.AddWithValue("$created_at", FormatDateTimeOffset(project.CreatedAt));
        _ = command.Parameters.AddWithValue("$is_pinned", project.IsPinned ? 1 : 0);
        _ = command.Parameters.AddWithValue("$memo", project.Memo);
        _ = command.Parameters.AddWithValue("$memo_updated_at", FormatDateTimeOffset(project.MemoUpdatedAt));
        _ = command.ExecuteNonQuery();
    }

    private static void UpsertProject(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectState project,
        int sortOrder)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO projects (project_id, name, sort_order, is_deleted, created_at, is_pinned, memo, memo_updated_at)
            VALUES ($project_id, $name, $sort_order, $is_deleted, $created_at, $is_pinned, $memo, $memo_updated_at)
            ON CONFLICT(project_id) DO UPDATE SET
                name = excluded.name,
                sort_order = excluded.sort_order,
                is_deleted = excluded.is_deleted,
                created_at = excluded.created_at,
                is_pinned = excluded.is_pinned,
                memo = excluded.memo,
                memo_updated_at = excluded.memo_updated_at;
            """;
        _ = command.Parameters.AddWithValue("$project_id", project.Id.ToString("D"));
        _ = command.Parameters.AddWithValue("$name", project.Name);
        _ = command.Parameters.AddWithValue("$sort_order", sortOrder);
        _ = command.Parameters.AddWithValue("$is_deleted", project.IsDeleted ? 1 : 0);
        _ = command.Parameters.AddWithValue("$created_at", FormatDateTimeOffset(project.CreatedAt));
        _ = command.Parameters.AddWithValue("$is_pinned", project.IsPinned ? 1 : 0);
        _ = command.Parameters.AddWithValue("$memo", project.Memo);
        _ = command.Parameters.AddWithValue("$memo_updated_at", FormatDateTimeOffset(project.MemoUpdatedAt));
        _ = command.ExecuteNonQuery();
    }

    private static void InsertRegisteredProgram(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        RegisteredProgramInfo registration,
        int sortOrder)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO registered_programs (
                project_id,
                process_name,
                display_name,
                initial_display_name,
                registered_at,
                sort_order,
                is_pinned)
            VALUES (
                $project_id,
                $process_name,
                $display_name,
                $initial_display_name,
                $registered_at,
                $sort_order,
                $is_pinned);
            """;
        _ = command.Parameters.AddWithValue("$project_id", projectId.ToString("D"));
        _ = command.Parameters.AddWithValue("$process_name", registration.Program.ProcessName);
        _ = command.Parameters.AddWithValue("$display_name", registration.Program.DisplayName);
        _ = command.Parameters.AddWithValue("$initial_display_name", registration.InitialDisplayName);
        _ = command.Parameters.AddWithValue("$registered_at", FormatDateTimeOffset(registration.RegisteredAt));
        _ = command.Parameters.AddWithValue("$sort_order", sortOrder);
        _ = command.Parameters.AddWithValue("$is_pinned", registration.IsPinned ? 1 : 0);
        _ = command.ExecuteNonQuery();
    }

    private static void InsertCompletedRecord(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectTimerRecord record,
        int recordOrder)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO completed_records (
                record_order,
                project_id,
                project_name,
                started_at,
                ended_at)
            VALUES (
                $record_order,
                $project_id,
                $project_name,
                $started_at,
                $ended_at);
            """;
        _ = command.Parameters.AddWithValue("$record_order", recordOrder);
        _ = command.Parameters.AddWithValue("$project_id", record.ProjectId.ToString("D"));
        _ = command.Parameters.AddWithValue("$project_name", record.ProjectName);
        _ = command.Parameters.AddWithValue("$started_at", FormatDateTimeOffset(record.StartedAt));
        _ = command.Parameters.AddWithValue("$ended_at", FormatDateTimeOffset(record.EndedAt));
        _ = command.ExecuteNonQuery();
    }

    private static void InsertFocusSegment(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProgramFocusSegment segment,
        int recordOrder,
        int segmentOrder)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO focus_segments (
                record_order,
                segment_order,
                process_name,
                display_name,
                started_at,
                ended_at)
            VALUES (
                $record_order,
                $segment_order,
                $process_name,
                $display_name,
                $started_at,
                $ended_at);
            """;
        _ = command.Parameters.AddWithValue("$record_order", recordOrder);
        _ = command.Parameters.AddWithValue("$segment_order", segmentOrder);
        _ = command.Parameters.AddWithValue("$process_name", segment.Program.ProcessName);
        _ = command.Parameters.AddWithValue("$display_name", segment.Program.DisplayName);
        _ = command.Parameters.AddWithValue("$started_at", FormatDateTimeOffset(segment.StartedAt));
        _ = command.Parameters.AddWithValue("$ended_at", FormatDateTimeOffset(segment.EndedAt));
        _ = command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        _ = command.ExecuteNonQuery();
    }

    private static void SaveProjectCatalog(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ReadOnlyCollection<ProjectState> projects)
    {
        for (int projectIndex = 0; projectIndex < projects.Count; projectIndex++)
        {
            ProjectState project = projects[projectIndex];
            UpsertProject(connection, transaction, project, projectIndex);
            DeleteRegisteredPrograms(connection, transaction, project.Id);

            for (int programIndex = 0; programIndex < project.RegisteredPrograms.Count; programIndex++)
            {
                InsertRegisteredProgram(connection, transaction, project.Id, project.RegisteredPrograms[programIndex], programIndex);
            }
        }
    }

    private static void DeleteRegisteredPrograms(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM registered_programs WHERE project_id = $project_id;";
        _ = command.Parameters.AddWithValue("$project_id", projectId.ToString("D"));
        _ = command.ExecuteNonQuery();
    }

    private static int GetNextRecordOrder(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(record_order) + 1, 0) FROM completed_records;";
        object? result = command.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string alterSql)
    {
        bool columnExists = false;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info({tableName});";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (columnExists)
        {
            return;
        }

        ExecuteNonQuery(
            connection,
            transaction: null,
            alterSql);
    }

    private static string FormatDateTimeOffset(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseOptionalDateTimeOffset(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ParseDateTimeOffset(value);
    }

    private static Guid ParseGuid(string value)
    {
        return Guid.ParseExact(value, "D");
    }

    private sealed record ProjectStateRow(
        Guid Id,
        string Name,
        bool IsDeleted,
        DateTimeOffset? CreatedAt,
        bool IsPinned,
        string Memo,
        DateTimeOffset? MemoUpdatedAt,
        List<RegisteredProgramInfo> RegisteredPrograms);

    private sealed record CompletedRecordRow(
        int RecordOrder,
        Guid ProjectId,
        string ProjectName,
        DateTimeOffset StartedAt,
        DateTimeOffset EndedAt,
        List<ProgramFocusSegment> FocusSegments);
}
