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
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return
            [
                Path.Combine(local, "PartFinder", "setup-state.json"),
                Path.Combine(roaming, "PartFinder", "setup-state.json"),
            ];
        }
    }

    public static string FindExistingSetupStatePath()
    {
        foreach (var p in SetupStateCandidatePaths)
        {
            try
            {
                if (File.Exists(p))
                {
                    return p;
                }
            }
            catch
            {
                // Ignore inaccessible paths and continue.
            }
        }

        return SetupStateFilePath;
    }
}
