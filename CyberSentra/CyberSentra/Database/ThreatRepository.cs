using CyberSentra.Database;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace CyberSentra.Database
{
    public static class ThreatRepository
    {
        public static void SaveThreats(IEnumerable<ThreatInfo> threats)
        {
            using var conn = DatabaseContext.GetConnection();

            foreach (var t in threats)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO Threats (Time, Technique, Name, Tactic, Severity, Details)
VALUES ($time, $tech, $name, $tactic, $severity, $details);
";
                cmd.Parameters.AddWithValue("$time", t.Time);
                cmd.Parameters.AddWithValue("$tech", t.Technique);
                cmd.Parameters.AddWithValue("$name", t.Name);
                cmd.Parameters.AddWithValue("$tactic", t.Tactic);
                cmd.Parameters.AddWithValue("$severity", t.Severity);
                cmd.Parameters.AddWithValue("$details", t.Details);
                cmd.ExecuteNonQuery();
            }
        }
        

        public static List<ThreatInfo> LoadThreatHistory()
        {
            var list = new List<ThreatInfo>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Time,Technique,Name,Tactic,Severity,Details FROM Threats ORDER BY Id DESC LIMIT 500";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ThreatInfo
                {
                    Time = reader.GetString(0),
                    Technique = reader.GetString(1),
                    Name = reader.GetString(2),
                    Tactic = reader.GetString(3),
                    Severity = reader.GetString(4),
                    Details = reader.GetString(5)
                });
            }

            return list;
        }
    }
}
