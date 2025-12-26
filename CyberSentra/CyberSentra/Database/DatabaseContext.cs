using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace CyberSentra.Database
{
    public static class DatabaseContext
    {
        private static string DbPath =>
            Path.Combine(AppContext.BaseDirectory, "cybersentra.db");

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();
            return conn;
        }


        public static void Initialize()
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            // ---- Create tables ----
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Events (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Time TEXT,
                    Type TEXT,
                    Severity TEXT,
                    User TEXT,
                    Process TEXT,
                    Details TEXT,
                    Source TEXT
                );

                CREATE TABLE IF NOT EXISTS Threats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Time TEXT,
                    Technique TEXT,
                    Name TEXT,
                    Tactic TEXT,
                    Severity TEXT,
                    Details TEXT
                );

                CREATE TABLE IF NOT EXISTS MlAnomalies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunKey TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UserWindow TEXT NOT NULL,
                    Score REAL NOT NULL,
                    IsAnomaly INTEGER NOT NULL,
                    F0_TotalEvents REAL NOT NULL,
                    F1_FailedLogins REAL NOT NULL,
                    F2_ErrorsFailures REAL NOT NULL,
                    F3_Warnings REAL NOT NULL,
                    F4_UniqueProcesses REAL NOT NULL,
                    F5_UniqueSources REAL NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_MlAnomalies_CreatedUtc ON MlAnomalies(CreatedUtc);
                CREATE INDEX IF NOT EXISTS IX_MlAnomalies_UserWindow ON MlAnomalies(UserWindow);
                CREATE INDEX IF NOT EXISTS IX_MlAnomalies_RunKey ON MlAnomalies(RunKey);

                CREATE UNIQUE INDEX IF NOT EXISTS UX_MlAnomalies_RunKey_UserWindow
                ON MlAnomalies(RunKey, UserWindow);
                ";
                cmd.ExecuteNonQuery();
            }

            // ---- Migration safety: add RunKey column if old DB exists ----
            try
            {
                using var check = conn.CreateCommand();
                check.CommandText = "PRAGMA table_info(MlAnomalies);";
                using var r = check.ExecuteReader();

                bool hasRunKey = false;
                while (r.Read())
                {
                    var colName = r.GetString(1);
                    if (string.Equals(colName, "RunKey", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRunKey = true;
                        break;
                    }
                }

                if (!hasRunKey)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE MlAnomalies ADD COLUMN RunKey TEXT NOT NULL DEFAULT '';";
                    alter.ExecuteNonQuery();

                    using var uniq = conn.CreateCommand();
                    uniq.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_MlAnomalies_RunKey_UserWindow ON MlAnomalies(RunKey, UserWindow);";
                    uniq.ExecuteNonQuery();

                    using var ix = conn.CreateCommand();
                    ix.CommandText = "CREATE INDEX IF NOT EXISTS IX_MlAnomalies_RunKey ON MlAnomalies(RunKey);";
                    ix.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB] MlAnomalies migration: " + ex.Message);
            }
        }

    }
}
