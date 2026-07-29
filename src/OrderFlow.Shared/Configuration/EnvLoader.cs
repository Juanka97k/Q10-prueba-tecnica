using System;
using System.IO;

namespace OrderFlow.Shared.Configuration;

public static class EnvLoader
{
    public static void Load(string? searchStartingPath = null)
    {
        var startingDir = searchStartingPath ?? Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(startingDir);
        string? envFile = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                envFile = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (envFile == null) return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}
