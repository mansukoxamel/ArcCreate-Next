using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcCreate.EditorScripts
{
    internal static class WindowsBuild
    {
        private const string BuildDirectory = "Build/ArcCreateNext";
        private const string SkinDirectoryName = "Skin";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Compose.unity",
            "Assets/Scenes/Gameplay.unity",
        };

        [MenuItem("ArcCreate Next/Build Windows")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildDirectory = Path.Combine(projectRoot, BuildDirectory);
            string skinDirectory = Path.Combine(buildDirectory, SkinDirectoryName);
            string backupDirectory = BackupSkinDirectory(projectRoot, skinDirectory);
            Exception buildException = null;
            Exception restoreException = null;

            try
            {
                PlayerSettings.SetManagedStrippingLevel(
                    NamedBuildTarget.Standalone,
                    ManagedStrippingLevel.Low);
                AssetDatabase.SaveAssets();

                BuildReport report = BuildPipeline.BuildPlayer(
                    Scenes,
                    Path.Combine(buildDirectory, "ArcCreateNext.exe"),
                    BuildTarget.StandaloneWindows64,
                    BuildOptions.None);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
                }
            }
            catch (Exception exception)
            {
                buildException = exception;
            }

            try
            {
                RestoreSkinDirectory(skinDirectory, backupDirectory);
            }
            catch (Exception exception)
            {
                restoreException = exception;
            }

            if (buildException != null && restoreException != null)
            {
                throw new AggregateException(
                    "Windows build failed and the external Skin directory could not be restored. " +
                    $"The backup remains at: {backupDirectory}",
                    buildException,
                    restoreException);
            }

            if (restoreException != null)
            {
                throw new InvalidOperationException(
                    "The Windows build completed, but the external Skin directory could not be restored. " +
                    $"The backup remains at: {backupDirectory}",
                    restoreException);
            }

            if (buildException != null)
            {
                throw buildException;
            }
        }

        private static string BackupSkinDirectory(string projectRoot, string skinDirectory)
        {
            if (!Directory.Exists(skinDirectory))
            {
                return null;
            }

            string backupRoot = Path.Combine(projectRoot, ".temporary", "BuildSkinBackups");
            string backupDirectory = Path.Combine(
                backupRoot,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(skinDirectory, backupDirectory);
            VerifyDirectoryCopy(skinDirectory, backupDirectory);
            Debug.Log($"Backed up external Skin directory to: {backupDirectory}");
            return backupDirectory;
        }

        private static void RestoreSkinDirectory(string skinDirectory, string backupDirectory)
        {
            if (string.IsNullOrEmpty(backupDirectory))
            {
                return;
            }

            CopyDirectory(backupDirectory, skinDirectory);
            VerifyDirectoryCopy(backupDirectory, skinDirectory);
            Directory.Delete(backupDirectory, true);
            Debug.Log($"Restored external Skin directory to: {skinDirectory}");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destinationFile, true);
            }

            foreach (string sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubdirectory = Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(sourceSubdirectory));
                CopyDirectory(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static void VerifyDirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (!File.Exists(destinationFile) || !FilesAreIdentical(sourceFile, destinationFile))
                {
                    throw new IOException(
                        $"External Skin file backup verification failed: {sourceFile} -> {destinationFile}");
                }
            }

            foreach (string sourceSubdirectory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubdirectory = Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(sourceSubdirectory));
                if (!Directory.Exists(destinationSubdirectory))
                {
                    throw new DirectoryNotFoundException(
                        $"External Skin directory backup verification failed: {destinationSubdirectory}");
                }

                VerifyDirectoryCopy(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static bool FilesAreIdentical(string firstPath, string secondPath)
        {
            var firstInfo = new FileInfo(firstPath);
            var secondInfo = new FileInfo(secondPath);
            if (firstInfo.Length != secondInfo.Length)
            {
                return false;
            }

            using (SHA256 sha256 = SHA256.Create())
            using (FileStream firstStream = File.OpenRead(firstPath))
            using (FileStream secondStream = File.OpenRead(secondPath))
            {
                byte[] firstHash = sha256.ComputeHash(firstStream);
                byte[] secondHash = sha256.ComputeHash(secondStream);
                if (firstHash.Length != secondHash.Length)
                {
                    return false;
                }

                for (int i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
