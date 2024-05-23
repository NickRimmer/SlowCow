using SlowCow.Apps.Shared;
namespace SlowCow.Examples.Launcher.Windows;

public static class UpdateHandler
{
    public static bool UpdateAvailable(string installationPath)
    {
        Log.Information("Checking for updates");

        var updatesPath = Path.Combine(installationPath, Constants.UpdatesFolderName);
        var available = Directory.Exists(updatesPath);

        if (available) Log.Information("Updates available");
        return available;
    }

    public static void ApplyUpdate(string installationPath)
    {
        Log.Information("Applying updates");

        var updatesPath = Path.Combine(installationPath, Constants.UpdatesFolderName);
        if (!Directory.Exists(updatesPath))
        {
            Log.Error("Updates folder not found. Updates cannot be applied");
            return;
        }

        var currentPath = Path.Combine(installationPath, Constants.CurrentFolderName);
        if (!Directory.Exists(currentPath))
        {
            Log.Error("Current path not found. Updates cannot be applied");
            return;
        }

        // backup current files
        var backupPath = Path.Combine(currentPath, Constants.BackupFolderName);
        if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);

        Log.Information("Backing up files");
        if (!CopyFiles(currentPath, backupPath))
        {
            Log.Error("Failed to backup files. Updates cannot be applied");
            return;
        }

        // apply updates
        if (!CopyFiles(updatesPath, currentPath))
        {
            Log.Error("Failed to apply updates");

            // restore files
            if (!CopyFiles(backupPath, currentPath, true))
            {
                Log.Warning("Some files cannot be restored");
            }
            else
            {
                Log.Information("Files restored");
            }
        }
        else
        {
            Log.Information("Updates successfully applied");
        }

        try
        {
            Directory.Delete(backupPath, true);
            Directory.Delete(updatesPath, true);
        }
        catch
        {
            Log.Warning("Failed to delete backup and updates folders");
        }
    }

    private static bool CopyFiles(string sourcePath, string targetPath, bool continueOnError = false)
    {
        var success = true;
        var updatedFiles = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
        foreach (var sourceFilePath in updatedFiles)
        {
            var sourceRelativePath = sourceFilePath.Replace(sourcePath, string.Empty).TrimStart(['\\', '/']);
            var targetFilePath = Path.Combine(targetPath, sourceRelativePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
                File.Copy(sourceFilePath, targetFilePath, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to copy file {SourceFilePath}", sourceFilePath);

                success = false;
                if (!continueOnError) return success;
            }
        }

        return success;
    }
}
