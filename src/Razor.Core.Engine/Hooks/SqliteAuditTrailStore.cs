// -----------------------------------------------------------------------------
// <copyright file="SqliteAuditTrailStore.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

using Microsoft.Data.Sqlite;

namespace Razor.Core.Engine.Hooks;

/// <summary>
/// Append‑only, immutable store for <see cref="AuditTrailRecord"/> entries backed by SQLite.
/// Records can only be inserted; no update or delete path is exposed, guaranteeing the
/// audit log cannot be tampered with after the fact.
/// </summary>
internal sealed class SqliteAuditTrailStore : IDisposable
{
    private readonly string _connectionString;
    private readonly Lock _lock = new();
    private bool _initialized;

    /// <summary>
    /// Creates a new SQLite‑backed audit trail store.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file. Defaults to <c>./audit_trail.db</c>.</param>
    public SqliteAuditTrailStore(string? databasePath = null)
    {
        var path = string.IsNullOrWhiteSpace(databasePath) ? "./audit_trail.db" : databasePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    /// <summary>
    /// Appends an immutable audit record. This is the only mutating operation supported.
    /// </summary>
    /// <param name="record">The record to persist.</param>
    public void Append(AuditTrailRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        EnsureInitialized();

        // Insert‑only: the schema has no UPDATE/DELETE path, so once written a
        // record can never be modified or removed.
        const string sql =
            "INSERT INTO AuditTrail (TimestampUtc, TaskId, OrderId, Symbol, Side, Quantity, Price, Status, RejectionReason) " +
            "VALUES (@TimestampUtc, @TaskId, @OrderId, @Symbol, @Side, @Quantity, @Price, @Status, @RejectionReason);";

        lock (_lock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.Add(new SqliteParameter("@TimestampUtc", record.TimestampUtc));
            command.Parameters.Add(new SqliteParameter("@TaskId", record.TaskId));
            command.Parameters.Add(new SqliteParameter("@OrderId", record.OrderId));
            command.Parameters.Add(new SqliteParameter("@Symbol", record.Symbol));
            command.Parameters.Add(new SqliteParameter("@Side", record.Side));
            command.Parameters.Add(new SqliteParameter("@Quantity", record.Quantity));
            command.Parameters.Add(new SqliteParameter("@Price", record.Price));
            command.Parameters.Add(new SqliteParameter("@Status", record.Status));
            command.Parameters.Add(new SqliteParameter("@RejectionReason", record.RejectionReason));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Returns all recorded audit trail entries in insertion order.
    /// </summary>
    /// <returns>The immutable list of records.</returns>
    public IReadOnlyList<AuditTrailRecord> ReadAll()
    {
        EnsureInitialized();

        const string sql = "SELECT TimestampUtc, TaskId, OrderId, Symbol, Side, Quantity, Price, Status, RejectionReason " +
                            "FROM AuditTrail ORDER BY RowId ASC;";

        var results = new List<AuditTrailRecord>();
        lock (_lock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new AuditTrailRecord
                {
                    TimestampUtc = reader.GetString(0),
                    TaskId = reader.GetString(1),
                    OrderId = reader.GetInt64(2),
                    Symbol = reader.GetString(3),
                    Side = reader.GetString(4),
                    Quantity = reader.GetDouble(5),
                    Price = reader.GetDouble(6),
                    Status = reader.GetString(7),
                    RejectionReason = reader.GetString(8)
                });
            }
        }

        return results.AsReadOnly();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS AuditTrail (" +
                "RowId INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "TimestampUtc TEXT NOT NULL, " +
                "TaskId TEXT NOT NULL, " +
                "OrderId INTEGER NOT NULL, " +
                "Symbol TEXT NOT NULL, " +
                "Side TEXT NOT NULL, " +
                "Quantity REAL NOT NULL, " +
                "Price REAL NOT NULL, " +
                "Status TEXT NOT NULL, " +
                "RejectionReason TEXT NOT NULL);", connection);
            command.ExecuteNonQuery();
            _initialized = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged handles are retained; the lock is released naturally.
    }
}
