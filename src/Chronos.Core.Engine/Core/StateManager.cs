using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace Chronos.Core.Engine.Core;

/// <summary>Represents a cron job.</summary>
internal sealed record CronJob(string JobId, string CronExpression, string Command, bool Enabled);

/// <summary>Represents a one‑off schedule.</summary>
internal sealed record Schedule(string ScheduleId, DateTime ScheduledTimeUtc, string Command, bool Repeat);

internal sealed record QueuedMessage(long Id, string MessageType, string PayloadJson, DateTime CreatedAtUtc);

/// <summary>Manages persistence of engine state using SQLite.</summary>
internal interface IStateManager
{
    /// <summary>Gets the engine's unique ID.</summary>
    string EngineId { get; }

    /// <summary>Deletes the live trading state.</summary>
    Task DeleteLiveStateAsync(CancellationToken cancellationToken);

    /// <summary>Sets the current session ID.</summary>
    Task SetSessionIdAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Loads all state from the database.</summary>
    Task LoadStateAsync(CancellationToken cancellationToken);

    /// <summary>Persists all current state to the database.</summary>
    Task SaveStateAsync(CancellationToken cancellationToken);

    /// <summary>Saves live trading state.</summary>
    Task SaveLiveStateAsync(LiveState liveState, CancellationToken cancellationToken);

    /// <summary>Loads live trading state.</summary>
    Task<LiveState?> LoadLiveStateAsync(CancellationToken cancellationToken);

    /// <summary>Saves an optimization state snapshot.</summary>
    Task SaveOptimizationStateAsync(string optimizationId, OptimizationState state, CancellationToken cancellationToken);

    /// <summary>Loads an optimization state snapshot.</summary>
    Task<OptimizationState?> LoadOptimizationStateAsync(string optimizationId, CancellationToken cancellationToken);

    /// <summary>Saves a cron job.</summary>
    Task SaveCronJobAsync(CronJob job, CancellationToken cancellationToken);

    /// <summary>Deletes a cron job.</summary>
    Task DeleteCronJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Loads all cron jobs.</summary>
    Task<List<CronJob>> LoadCronJobsAsync(CancellationToken cancellationToken);

    /// <summary>Saves a one‑off schedule.</summary>
    Task SaveScheduleAsync(Schedule schedule, CancellationToken cancellationToken);

    /// <summary>Deletes a schedule.</summary>
    Task DeleteScheduleAsync(string scheduleId, CancellationToken cancellationToken);

    /// <summary>Loads all schedules.</summary>
    Task<List<Schedule>> LoadSchedulesAsync(CancellationToken cancellationToken);

    /// <summary>Saves the extension manifest.</summary>
    Task SaveExtensionManifestAsync(object manifest, CancellationToken cancellationToken);

    /// <summary>Loads the extension manifest.</summary>
    Task<object?> LoadExtensionManifestAsync(CancellationToken cancellationToken);

    /// <summary>Sets an arbitrary metadata key-value pair (e.g. Admin Configs).</summary>
    Task SetMetadataAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>Gets an arbitrary metadata value.</summary>
    Task<string?> GetMetadataAsync(string key, CancellationToken cancellationToken);

    /// <summary>Enqueues an outgoing message to be sent later.</summary>
    Task EnqueueOutgoingMessageAsync(string messageType, string payloadJson, CancellationToken cancellationToken);

    /// <summary>Gets all pending outgoing messages.</summary>
    Task<IReadOnlyList<QueuedMessage>> GetPendingOutgoingMessagesAsync(CancellationToken cancellationToken);

    /// <summary>Deletes a queued message by ID.</summary>
    Task DeleteOutgoingMessageAsync(long id, CancellationToken cancellationToken);
}

/// <summary>Default implementation of state persistence.</summary>
internal sealed class StateManager : IStateManager, IDisposable
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private string _engineId = string.Empty;

    /// <inheritdoc/>
    public string EngineId => _engineId;

    /// <summary>Initializes a new instance.</summary>
    public StateManager()
    {
        _databasePath = Path.Combine("state", "engine_state.db");
        Directory.CreateDirectory("state");

        if (!File.Exists(_databasePath))
        {
            InitializeDatabase();
        }
        else
        {
            _engineId = LoadEngineId();
        }
    }

    /// <summary>Initialises the SQLite database with required tables.</summary>
    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        string createTablesSql = @"
            CREATE TABLE IF NOT EXISTS Metadata (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Tasks (TaskId TEXT PRIMARY KEY, TaskType TEXT NOT NULL, State TEXT NOT NULL, Config TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS LiveState (TaskId TEXT PRIMARY KEY, StateJson TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS OptimizationStates (OptimizationId TEXT PRIMARY KEY, StateJson TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS CronJobs (JobId TEXT PRIMARY KEY, CronExpression TEXT NOT NULL, Command TEXT NOT NULL, Enabled INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS Schedules (ScheduleId TEXT PRIMARY KEY, ScheduledTimeUtc TEXT NOT NULL, Command TEXT NOT NULL, Repeat INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ExtensionManifest (ManifestType TEXT PRIMARY KEY, ManifestJson TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS QueuedMessages (Id INTEGER PRIMARY KEY AUTOINCREMENT, MessageType TEXT NOT NULL, PayloadJson TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL, Sent INTEGER NOT NULL DEFAULT 0);
        ";

        using var command = new SqliteCommand(createTablesSql, connection);
        command.ExecuteNonQuery();

        string newId = $"eng_{Guid.NewGuid():N}";
        using var insertCmd = new SqliteCommand("INSERT INTO Metadata (Key, Value) VALUES ('EngineId', @Value)", connection);
        insertCmd.Parameters.AddWithValue("@Value", newId);
        insertCmd.ExecuteNonQuery();

        _engineId = newId;
    }

    /// <inheritdoc/>
    public async Task DeleteLiveStateAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("DELETE FROM LiveState WHERE TaskId = 'current'", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task EnqueueOutgoingMessageAsync(string messageType, string payloadJson, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = new SqliteCommand(
                "INSERT INTO QueuedMessages (MessageType, PayloadJson, CreatedAtUtc, Sent) VALUES (@Type, @Json, @Created, 0)",
                connection);
            cmd.Parameters.AddWithValue("@Type", messageType);
            cmd.Parameters.AddWithValue("@Json", payloadJson);
            cmd.Parameters.AddWithValue("@Created", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<QueuedMessage>> GetPendingOutgoingMessagesAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = new SqliteCommand(
                "SELECT Id, MessageType, PayloadJson, CreatedAtUtc FROM QueuedMessages WHERE Sent = 0 ORDER BY Id ASC",
                connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var list = new List<QueuedMessage>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new QueuedMessage(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture)
                ));
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteOutgoingMessageAsync(long id, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = new SqliteCommand("DELETE FROM QueuedMessages WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Loads the engine ID from the database.</summary>
    private string LoadEngineId()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var selectCmd = new SqliteCommand("SELECT Value FROM Metadata WHERE Key = 'EngineId'", connection);
        string? result = selectCmd.ExecuteScalar() as string;

        if (string.IsNullOrEmpty(result))
        {
            result = $"eng_{Guid.NewGuid():N}";
            using var insertCmd = new SqliteCommand("INSERT OR REPLACE INTO Metadata (Key, Value) VALUES ('EngineId', @Value)", connection);
            insertCmd.Parameters.AddWithValue("@Value", result);
            insertCmd.ExecuteNonQuery();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task SetSessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        await SetMetadataAsync("SessionId", sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task LoadStateAsync(CancellationToken cancellationToken)
    {
        // For now, nothing more to load – live/optimization states are loaded on demand.
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SaveStateAsync(CancellationToken cancellationToken)
    {
        // Save all active tasks – we rely on individual save methods for live/optimization.
        // This method can be used as a global checkpoint.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveLiveStateAsync(LiveState liveState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(liveState);

        string json = JsonSerializer.Serialize(liveState, _jsonOptions);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO LiveState (TaskId, StateJson) VALUES ('current', @Json)", connection);
            cmd.Parameters.AddWithValue("@Json", json);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<LiveState?> LoadLiveStateAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT StateJson FROM LiveState WHERE TaskId = 'current'", connection);
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is string json)
            {
                return JsonSerializer.Deserialize<LiveState>(json, _jsonOptions);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveOptimizationStateAsync(string optimizationId, OptimizationState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(optimizationId);
        ArgumentNullException.ThrowIfNull(state);

        string json = JsonSerializer.Serialize(state, _jsonOptions);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO OptimizationStates (OptimizationId, StateJson) VALUES (@Id, @Json)", connection);
            cmd.Parameters.AddWithValue("@Id", optimizationId);
            cmd.Parameters.AddWithValue("@Json", json);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<OptimizationState?> LoadOptimizationStateAsync(string optimizationId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT StateJson FROM OptimizationStates WHERE OptimizationId = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", optimizationId);
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is string json)
            {
                return JsonSerializer.Deserialize<OptimizationState>(json, _jsonOptions);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveCronJobAsync(CronJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO CronJobs (JobId, CronExpression, Command, Enabled) VALUES (@Id, @Cron, @Cmd, @Enabled)", connection);
            cmd.Parameters.AddWithValue("@Id", job.JobId);
            cmd.Parameters.AddWithValue("@Cron", job.CronExpression);
            cmd.Parameters.AddWithValue("@Cmd", job.Command);
            cmd.Parameters.AddWithValue("@Enabled", job.Enabled ? 1 : 0);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task DeleteCronJobAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("DELETE FROM CronJobs WHERE JobId = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", jobId);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<List<CronJob>> LoadCronJobsAsync(CancellationToken cancellationToken)
    {
        List<CronJob> jobs = new List<CronJob>();
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT JobId, CronExpression, Command, Enabled FROM CronJobs", connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                jobs.Add(new CronJob(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) == 1
                ));
            }
            return jobs;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveScheduleAsync(Schedule schedule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO Schedules (ScheduleId, ScheduledTimeUtc, Command, Repeat) VALUES (@Id, @Time, @Cmd, @Repeat)", connection);
            cmd.Parameters.AddWithValue("@Id", schedule.ScheduleId);
            cmd.Parameters.AddWithValue("@Time", schedule.ScheduledTimeUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@Cmd", schedule.Command);
            cmd.Parameters.AddWithValue("@Repeat", schedule.Repeat ? 1 : 0);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task DeleteScheduleAsync(string scheduleId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("DELETE FROM Schedules WHERE ScheduleId = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", scheduleId);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<List<Schedule>> LoadSchedulesAsync(CancellationToken cancellationToken)
    {
        List<Schedule> schedules = new List<Schedule>();
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT ScheduleId, ScheduledTimeUtc, Command, Repeat FROM Schedules", connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                schedules.Add(new Schedule(
                    reader.GetString(0),
                    DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                    reader.GetString(2),
                    reader.GetInt32(3) == 1
                ));
            }
            return schedules;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveExtensionManifestAsync(object manifest, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(manifest, _jsonOptions);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO ExtensionManifest (ManifestType, ManifestJson) VALUES ('Default', @Json)", connection);
            cmd.Parameters.AddWithValue("@Json", json);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<object?> LoadExtensionManifestAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT ManifestJson FROM ExtensionManifest WHERE ManifestType = 'Default'", connection);
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is string json)
            {
                return JsonSerializer.Deserialize<object>(json, _jsonOptions);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SetMetadataAsync(string key, string value, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO Metadata (Key, Value) VALUES (@Key, @Value)", connection);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetMetadataAsync(string key, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = new SqliteCommand("SELECT Value FROM Metadata WHERE Key = @Key", connection);
            cmd.Parameters.AddWithValue("@Key", key);
            return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lock.Dispose();
    }
}
