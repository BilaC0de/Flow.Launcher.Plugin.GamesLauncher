using Flow.Launcher.Plugin;
using GamesLauncher.Common;
using GamesLauncher.Platforms.SyncEngines.Common.Interfaces;

namespace GamesLauncher.Platforms.SyncEngines
{
    public class ShortcutsSyncEngine : ISyncEngine
    {
        public string PlatformName => "Shortcut";
        public IEnumerable<Game> SynchronizedGames { get; private set; } = Array.Empty<Game>();

        private readonly IPublicAPI publicApi;
        private readonly DirectoryInfo shortcutsDirectory;

        public ShortcutsSyncEngine(IPublicAPI publicApi)
        {
            this.publicApi = publicApi;
            shortcutsDirectory = Directory.CreateDirectory(Paths.CustomShortcutsDirectory);
        }

        public async Task SynchronizeGames()
        {
            string[] shortcutExtensions = { "*.url", "*.lnk" };
            var syncedGames = new List<Game>();

            foreach (var shortcutExtension in shortcutExtensions)
            {
                foreach (var shortcut in shortcutsDirectory.EnumerateFiles(shortcutExtension, SearchOption.AllDirectories))
                {
                    syncedGames.Add(new Game(
                        title: Path.GetFileNameWithoutExtension(shortcut.FullName),
                        runTask: GetGameRunTask(shortcut.FullName),
                        iconPath: await GetIconPath(shortcut),
                        iconDelegate: null,
                        platform: PlatformName,
                        uninstallAction: new(
                            GetShortcutDeleteTask(shortcut.FullName),
                            "Delete Shortcut"
                            )
                        ));
                }
            }

            SynchronizedGames = syncedGames;
        }

        private Func<Task> GetGameRunTask(string fullPath)
        {
            return async () =>
            {
                var directory = Path.GetDirectoryName(fullPath);
                var fileShortcut = Path.GetFileName(fullPath);
                publicApi.ShellRun($"cd /d \"{directory}\" && start \"\" \"{fileShortcut}\"");
                await Task.CompletedTask;
            };
        }

        private Func<Task> GetShortcutDeleteTask(string fullPath)
        {
            return async () =>
            {
                File.Delete(fullPath);
                await SynchronizeGames();
            };
        }

        private static async Task<string?> GetIconPath(FileInfo fileInfo)
        {
            if (fileInfo.Extension == ".url")
            {
                await foreach (var line in File.ReadLinesAsync(fileInfo.FullName))
                {
                    if (line.Trim().StartsWith("IconFile="))
                    {
                        var iconPath = line.Replace("IconFile=", "").Trim();
                        // Remplacer les variables d'environnement
                        return Environment.ExpandEnvironmentVariables(iconPath);
                    }
                }
            }
            else if (fileInfo.Extension == ".lnk")
            {
                // D'abord, vérifier s'il y a une IconLocation personnalisée définie
                var iconLocation = GetLnkIconLocation(fileInfo.FullName);
                if (!string.IsNullOrEmpty(iconLocation) && File.Exists(iconLocation))
                {
                    return Environment.ExpandEnvironmentVariables(iconLocation);
                }

                // Si pas d'IconLocation, extraire l'exécutable cible
                var targetPath = GetLnkTargetPath(fileInfo.FullName);
                if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    return Environment.ExpandEnvironmentVariables(targetPath);
                }
            }

            return fileInfo.FullName;
        }

        private static string? GetLnkIconLocation(string lnkFilePath)
        {
            try
            {
                // Utiliser COM (WScript.Shell) pour lire les propriétés du raccourci
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(lnkFilePath);
                string iconLocation = shortcut.IconLocation;
                
                // Libérer les ressources COM
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                
                return string.IsNullOrEmpty(iconLocation) ? null : iconLocation;
            }
            catch
            {
                return null;
            }
        }

        private static string? GetLnkTargetPath(string lnkFilePath)
        {
            try
            {
                // Utiliser COM (WScript.Shell) pour lire les propriétés du raccourci
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(lnkFilePath);
                string targetPath = shortcut.TargetPath;
                
                // Libérer les ressources COM
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                
                return targetPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
