using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies EF migrations. If the DB was created with EnsureCreated (no history)
    /// but already has schema, baselines pending migrations so existing data is kept.
    /// </summary>
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();

        if (applied.Count == 0 && pending.Count > 0 && await HasLegacySchemaAsync(db, ct))
        {
            await EnsureHistoryTableAsync(db, ct);
            foreach (var migrationId in pending)
                await InsertHistoryAsync(db, migrationId, ct);
            return;
        }

        await db.Database.MigrateAsync(ct);
    }

    private static async Task<bool> HasLegacySchemaAsync(AppDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Invoices','Customers','CompanySettings');";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return count >= 3;
    }

    private static Task EnsureHistoryTableAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            ct);

    private static Task InsertHistoryAsync(AppDbContext db, string migrationId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ({migrationId}, {ProductVersion()});
            """,
            ct);

    private static string ProductVersion()
    {
        var asm = typeof(DbContext).Assembly.GetName().Version;
        return asm is null ? "10.0.13" : $"{asm.Major}.{asm.Minor}.{asm.Build}";
    }
}
