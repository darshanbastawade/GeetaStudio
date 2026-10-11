using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using ContentOS.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GeetaStudio.Api;

public static class DevelopmentSetup
{
    public const string ApiSecretsId = "geetastudio-api-52ed9b61-c102-4716-9bd4-901652979e8c";
    private const string WorkerSecretsId = "dotnet-GeetaStudio.Worker-b45aea23-7d36-4708-83da-a606622b04ef";
    public static async Task RunAsync(IHostEnvironment environment, bool provision, bool seed = false)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Development-only migration command.");
        var database = Environment.GetEnvironmentVariable("GEETA_MIGRATION_DATABASE") ?? "GeetaStudio_Dev";
        var server = Environment.GetEnvironmentVariable("GEETA_MIGRATION_SERVER") ?? "localhost";
        if (database is not ("GeetaStudio_Dev" or "GeetaStudio_M1Tests") ||
            (server != "localhost" && server != "." && !server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Only the explicitly named local development/test databases are permitted.");
        var migration = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true };
        await using var db = new RuntimeDbContext(new DbContextOptionsBuilder<RuntimeDbContext>().UseSqlServer(migration.ConnectionString).Options);
        await db.Database.MigrateAsync();
        await using var core = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>().UseSqlServer(migration.ConnectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "core")).Options);
        await core.Database.MigrateAsync();
        if (!await core.Locales.AnyAsync(l => l.Id == "sa-IN")) { core.Locales.Add(new ContentOS.Domain.ContentLocale { Id = "sa-IN", Name = "Sanskrit (India)" }); await core.SaveChangesAsync(); }
        foreach (var locale in new[] { ("mr-IN", "Marathi (India)"), ("hi-IN", "Hindi (India)") })
            if (!await core.Locales.AnyAsync(x => x.Id == locale.Item1)) { core.Locales.Add(new() { Id = locale.Item1, Name = locale.Item2 }); await core.SaveChangesAsync(); }
        if (seed || provision)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Explicit local seed uses the Windows migration identity.");
            var fixture = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "docs", "fixtures", "M02-universes.json"));
            await CoreSeed.SeedAsync(core, fixture, WindowsIdentity.GetCurrent().Name);
            Console.WriteLine("Core fixtures seeded without overwriting existing records; current Windows identity belongs only to the local tenant.");
        }
        if (!provision) { Console.WriteLine("Dedicated local database migrated without reset/drop."); return; }
        if (database != "GeetaStudio_Dev") throw new InvalidOperationException("Runtime provisioning is only for GeetaStudio_Dev.");
        var master = new SqlConnectionStringBuilder(migration.ConnectionString) { InitialCatalog = "master" };
        await using var admin = new SqlConnection(master.ConnectionString); await admin.OpenAsync();
        await using var mode = new SqlCommand("SELECT CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int)", admin);
        if (Convert.ToInt32(await mode.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Separate runtime SQL logins require mixed authentication; configure approved runtime identities without changing server authentication automatically.");
        foreach (var host in new[] { "Api", "Worker" })
        {
            var login = "GeetaStudio" + host + "_Dev";
            var secretsId = host == "Api" ? ApiSecretsId : WorkerSecretsId;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", secretsId);
            var file = Path.Combine(directory, "secrets.json");
            var values = File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file))! : new Dictionary<string, string>();
            await using var exists = new SqlCommand("SELECT COUNT(*) FROM sys.sql_logins WHERE name = @name", admin);
            exists.Parameters.AddWithValue("@name", login);
            string runtimeConnection;
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) > 0)
            {
                if (!values.TryGetValue("ConnectionStrings:ContentOS", out runtimeConnection!) || new SqlConnectionStringBuilder(runtimeConnection).UserID != login)
                    throw new InvalidOperationException("Existing runtime login has no matching local secret. Preserve it and configure credentials privately.");
            }
            else
            {
                var credential = "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                await using var create = new SqlCommand($"CREATE LOGIN [{login}] WITH " + "PASSWORD" + " = '" + credential + "', CHECK_POLICY = ON;", admin);
                await create.ExecuteNonQueryAsync();
                runtimeConnection = new SqlConnectionStringBuilder(migration.ConnectionString) { IntegratedSecurity = false, UserID = login, Password = credential }.ConnectionString;
                values["ConnectionStrings:ContentOS"] = runtimeConnection;
                Directory.CreateDirectory(directory); await SaveSecretsAsync(file, values);
            }
            await using var grant = new SqlCommand($"IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{login}') CREATE USER [{login}] FOR LOGIN [{login}];", (SqlConnection)db.Database.GetDbConnection());
            await db.Database.OpenConnectionAsync(); await grant.ExecuteNonQueryAsync();
            var tables = host == "Api"
                ? new[] { "Connections", "Profiles", "Revisions", "Routes", "Jobs", "Attempts", "Operations", "Effects", "Budgets", "Reservations", "Outbox", "Inbox", "Audit", "Assets" }
                : new[] { "Jobs", "Attempts", "Operations", "Effects", "Budgets", "Reservations", "Outbox", "Inbox", "Audit" };
            foreach (var table in tables)
            {
                await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[runtime].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                await permission.ExecuteNonQueryAsync();
            }
            if (host == "Worker")
                foreach (var table in new[] { "Connections", "Profiles", "Revisions", "Routes" })
                {
                    // Circuit counters update the connection but cannot change configured identities from worker code.
                    var permissionSql = table == "Connections" ? $"GRANT SELECT ON OBJECT::[runtime].[Connections] TO [{login}]; GRANT UPDATE ([Failures], [CircuitUntilUtc]) ON OBJECT::[runtime].[Connections] TO [{login}];" : $"GRANT SELECT ON OBJECT::[runtime].[{table}] TO [{login}];";
                    await using var permission = new SqlCommand(permissionSql, (SqlConnection)db.Database.GetDbConnection()); await permission.ExecuteNonQueryAsync();
                }
            await using var definitions = new SqlCommand($"GRANT SELECT ON OBJECT::[runtime].[Definitions] TO [{login}];", (SqlConnection)db.Database.GetDbConnection()); await definitions.ExecuteNonQueryAsync();
            if (host == "Api")
            {
                await using var corePermission = new SqlCommand("SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('core') AND name <> '__EFMigrationsHistory'", (SqlConnection)db.Database.GetDbConnection());
                var coreTables = new List<string>();
                await using (var reader = await corePermission.ExecuteReaderAsync()) { while (await reader.ReadAsync()) coreTables.Add(reader.GetString(0)); }
                foreach (var table in coreTables)
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[core].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                // M03 histories are append-only for the API login; no DELETE or schema permissions.
                foreach (var table in new[] { "SourceWork", "SourceEdition", "SourcePermission", "SourceSnapshot", "RemediationTask" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[sources].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "SourceParty", "PermissionLanguage", "PermissionTerritory", "PermissionFormat", "ProvenanceEdge", "RightsDecision", "SourceUse", "SourceRevocation", "SourceCommand", "SourceAudit" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[sources].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "CorpusManifest", "CorpusLocator", "CorpusSourceBinding", "CorpusText", "CorpusCommand" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[corpus].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "CorpusVerse", "CorpusAnnotation" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[corpus].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "EvidenceRubric", "ClaimEvidenceSpan", "ClaimDisagreement", "ClaimContentLink", "EvidenceCommand" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[evidence].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "EvidenceClaim", "ClaimRevision", "ClaimCorrection" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[evidence].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "KnowledgeConcept", "KnowledgeRelation", "KnowledgeIndex", "KnowledgeAlias" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[knowledge].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "KnowledgeChunk", "KnowledgeCommand" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[knowledge].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "AiRouteHead", "AiBudget", "AiRun", "AiAttempt" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[ai].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "AiProfile", "AiRoute" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[ai].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "PromptRevision", "WorkflowDefinition", "WorkflowDecision" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[workflow].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "DurableWorkflow", "WorkflowCheckpoint" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[workflow].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                await using (var readGrant = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[reader].[ReadCollection] TO [{login}]; GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[reader].[ReadEventCount] TO [{login}];", (SqlConnection)db.Database.GetDbConnection())) await readGrant.ExecuteNonQueryAsync();
                foreach (var table in new[] { "ReadPiece", "ReadEvidence", "ReadOperation" })
                {
                    await using var readGrant = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[reader].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection()); await readGrant.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "EditorialItem", "EditorialCopy", "EditorialCorrectionTask", "EditorialTrend" })
                {
                    await using var editorialGrant = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[editorial].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection()); await editorialGrant.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "EditorialApproval", "EditorialAsset", "EditorialCorrectionRecord", "EditorialOperation", "EditorialHistory", "EditorialAttempt" })
                {
                    await using var editorialGrant = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[editorial].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection()); await editorialGrant.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "EvaluationDataset", "EvaluationCandidate", "EvaluationRun", "EvaluationReview", "EvaluationAdvice", "EvaluationOperation" })
                {
                    await using var evaluationGrant = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[evaluation].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await evaluationGrant.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "FactoryPlan", "FactoryBatch", "FactoryWork", "FactoryArtifact" })
                {
                    await using var factoryGrant = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[factory].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await factoryGrant.ExecuteNonQueryAsync();
                }
                await using (var factoryGrant = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[factory].[FactoryOperation] TO [{login}];", (SqlConnection)db.Database.GetDbConnection())) await factoryGrant.ExecuteNonQueryAsync();
                foreach (var table in new[] { "RegistryRevision", "RegistryEpisode" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[registry].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                await using (var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[registry].[RegistryOperation] TO [{login}];", (SqlConnection)db.Database.GetDbConnection())) await permission.ExecuteNonQueryAsync();
                foreach (var table in new[] { "LessonProfile", "LessonMemory", "LessonVariant" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT, UPDATE ON OBJECT::[lessons].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                foreach (var table in new[] { "LessonBundle", "LessonCommand" })
                {
                    await using var permission = new SqlCommand($"GRANT SELECT, INSERT ON OBJECT::[lessons].[{table}] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                    await permission.ExecuteNonQueryAsync();
                }
                await using var coreHistory = new SqlCommand($"GRANT SELECT ON OBJECT::[core].[__EFMigrationsHistory] TO [{login}];", (SqlConnection)db.Database.GetDbConnection());
                await coreHistory.ExecuteNonQueryAsync();
            }
            await using var history = new SqlCommand($"GRANT SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] TO [{login}];", (SqlConnection)db.Database.GetDbConnection()); await history.ExecuteNonQueryAsync();
            values["Runtime:Enabled"] = "true";
            values.TryAdd("Assets:Root", Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".local", "assets")));
            values.TryAdd("Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Warning");
            if (host == "Api")
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This local staff setup uses Windows authentication.");
                values.TryAdd("Staff:Administrator:0", WindowsIdentity.GetCurrent().Name);
            }
            await SaveSecretsAsync(file, values);
            await using var verify = new SqlConnection(runtimeConnection); await verify.OpenAsync();
        }
        Console.WriteLine("Dedicated development database migrated; separate restricted API/worker logins verified; host user secrets configured. No secret values printed.");
    }
    private static async Task SaveSecretsAsync(string file, Dictionary<string, string> values)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(file)!, ".secrets-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(values));
            File.Move(temporary, file, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
