using System;
using MySqlConnector;
using TUnit.Core;

namespace Quartermaster.Server.Tests.Infrastructure;

public static class GlobalTestHooks {
    [Before(Assembly)]
    public static void VerifyMySqlAvailable() {
        try {
            using var conn = new MySqlConnection("server=localhost;user id=root;");
            conn.Open();
        } catch (Exception ex) {
            throw new InvalidOperationException(
                "MySQL is not reachable at localhost (user 'root', no password). " +
                "Tests require a local MySQL instance. Start MySQL and retry. " +
                $"Underlying error: {ex.Message}", ex);
        }
    }

    [After(Assembly)]
    public static void DropWorkerDatabases() {
        var keep = Environment.GetEnvironmentVariable("QM_TEST_KEEP_DB");
        if (keep == "1" || keep?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            return;

        TestDatabaseFixture.DropAllWorkerDatabases();
    }
}
