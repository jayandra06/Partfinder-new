namespace PartFinder.Services;

internal static class SetupPaths
{
    public static string SetupStateFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "setup-state.json");

    /// <summary>
    /// Older builds or packaging modes may have written setup-state.json in different app-data roots.
    /// Try these before deciding setup is missing.
    /// </summary>
    public static IReadOnlyList<string> SetupStateCandidatePaths
    {
        get
        {
            var paths = new List<string>();
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            paths.Add(Path.Combine(local, "PartFinder", "setup-state.json"));
            paths.Add(Path.Combine(roaming, "PartFinder", "setup-state.json"));

            try
            {
                var packagesDir = Path.Combine(local, "Packages");
                if (Directory.Exists(packagesDir))
                {
                    foreach (var pkgDir in Directory.EnumerateDirectories(packagesDir, "*PartFinder*"))
                    {
                        var candidate = Path.Combine(pkgDir, "LocalCache", "Local", "PartFinder", "setup-state.json");
                        if (File.Exists(candidate) && !paths.Contains(candidate))
                        {
                            paths.Add(candidate);
                        }
                    }

                    foreach (var pkgDir in Directory.EnumerateDirectories(packagesDir, "608d9293*"))
                    {
                        var candidate = Path.Combine(pkgDir, "LocalCache", "Local", "PartFinder", "setup-state.json");
                        if (File.Exists(candidate) && !paths.Contains(candidate))
                        {
                            paths.Add(candidate);
                        }
                    }
                }
            }
            catch
            {
                // Best effort
            }

            return paths;
        }
    }

    /// <summary>
    /// Single authoritative setup-state.json: canonical LocalAppData file when present,
    /// otherwise the most recently written legacy copy (Packages / Roaming).
    /// </summary>
    public static string GetAuthoritativeSetupStatePath()
    {
        try
        {
            if (File.Exists(SetupStateFilePath))
            {
                return SetupStateFilePath;
            }
        }
        catch
        {
            // Fall through to legacy scan.
        }

        string? newestPath = null;
        var newestUtc = DateTime.MinValue;
        foreach (var p in SetupStateCandidatePaths)
        {
            if (string.Equals(p, SetupStateFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (!File.Exists(p))
                {
                    continue;
                }

                var written = File.GetLastWriteTimeUtc(p);
                if (written >= newestUtc)
                {
                    newestUtc = written;
                    newestPath = p;
                }
            }
            catch
            {
                // Ignore inaccessible paths and continue.
            }
        }

        return newestPath ?? SetupStateFilePath;
    }

    public static string FindExistingSetupStatePath() => GetAuthoritativeSetupStatePath();

    /// <summary>
    /// Copies the active legacy setup file into the canonical path so reads/writes stay consistent.
    /// </summary>
    public static void MigrateToCanonicalSetupStateIfNeeded()
    {
        var source = GetAuthoritativeSetupStatePath();
        if (string.Equals(source, SetupStateFilePath, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(source))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SetupStateFilePath)!);
            File.Copy(source, SetupStateFilePath, overwrite: true);
        }
        catch
        {
            // Best effort — caller still reads via GetAuthoritativeSetupStatePath().
        }
    }

    /// <summary>Removes duplicate setup-state.json copies outside the canonical path.</summary>
    public static void PurgeLegacySetupStateFiles()
    {
        foreach (var p in SetupStateCandidatePaths)
        {
            if (string.Equals(p, SetupStateFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.Exists(p))
                {
                    File.Delete(p);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }

    public static void ClearAllSetupStateFiles()
    {
        foreach (var p in SetupStateCandidatePaths)
        {
            try
            {
                if (File.Exists(p))
                {
                    File.Delete(p);
                }
            }
            catch
            {
                // Ignore failures and continue; best-effort logout cleanup.
            }
        }
    }
}
