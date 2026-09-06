using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArcCreate.Compose.Project
{
    public class RecentFileHistoryEntry
    {
        public string DirectoryPath { get; set; }

        public string ChartPath { get; set; }
    }

    public static class DirectFileProjectResolver
    {
        public static string ResolveHistoryDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }

                string directory = Path.GetDirectoryName(fullPath);
                return Directory.Exists(directory) ? directory : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        public static string[] NormalizeHistoryDirectories(IEnumerable<string> paths, int maxCount)
        {
            if (paths == null || maxCount <= 0)
            {
                return Array.Empty<string>();
            }

            return paths
                .Select(ResolveHistoryDirectory)
                .Where(path => path != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .ToArray();
        }

        public static RecentFileHistoryEntry[] NormalizeHistoryEntries(
            IEnumerable<RecentFileHistoryEntry> entries,
            int maxCount)
        {
            if (entries == null || maxCount <= 0)
            {
                return Array.Empty<RecentFileHistoryEntry>();
            }

            List<RecentFileHistoryEntry> normalized = new List<RecentFileHistoryEntry>();
            foreach (RecentFileHistoryEntry entry in entries)
            {
                string directory = ResolveHistoryDirectory(entry?.DirectoryPath ?? entry?.ChartPath);
                if (directory == null
                 || normalized.Any(item => item.DirectoryPath.Equals(
                     directory,
                     StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string chartPath = NormalizeHistoryChartPath(entry?.ChartPath, directory);
                normalized.Add(new RecentFileHistoryEntry
                {
                    DirectoryPath = directory,
                    ChartPath = chartPath,
                });

                if (normalized.Count >= maxCount)
                {
                    break;
                }
            }

            return normalized.ToArray();
        }

        public static string ResolveHistoryOpenPath(RecentFileHistoryEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            string directory = ResolveHistoryDirectory(entry.DirectoryPath ?? entry.ChartPath);
            if (directory == null)
            {
                return null;
            }

            return NormalizeHistoryChartPath(entry.ChartPath, directory) ?? directory;
        }

        public static bool IsSupportedDrop(string path)
        {
            if (Directory.Exists(path))
            {
                return true;
            }

            string extension = Path.GetExtension(path);
            if (extension.Equals(".aff", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(path).Equals("preview.ogg", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveAudioForChart(string chartPath)
        {
            string specificAudio = Path.ChangeExtension(chartPath, ".ogg");
            if (File.Exists(specificAudio)
             && !Path.GetFileName(specificAudio).Equals("preview.ogg", StringComparison.OrdinalIgnoreCase))
            {
                return specificAudio;
            }

            string baseAudio = Path.Combine(Path.GetDirectoryName(chartPath), "base.ogg");
            return File.Exists(baseAudio) ? baseAudio : null;
        }

        public static string[] FindChartsForAudio(string audioPath)
        {
            string directory = Path.GetDirectoryName(audioPath);
            string audioName = Path.GetFileNameWithoutExtension(audioPath);
            if (!audioName.Equals("base", StringComparison.OrdinalIgnoreCase))
            {
                string matchingChart = Path.Combine(directory, audioName + ".aff");
                if (File.Exists(matchingChart))
                {
                    return new[] { matchingChart };
                }
            }

            return Directory.GetFiles(directory, "*.aff")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] FindLoadableChartsInDirectory(string directory)
        {
            return Directory.GetFiles(directory, "*.aff")
                .Where(path => ResolveAudioForChart(path) != null)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string ResolveJacketForChart(string chartPath, string preferredJacketPath = null)
        {
            string chartJacket = Path.ChangeExtension(chartPath, ".jpg");
            if (File.Exists(chartJacket))
            {
                return chartJacket;
            }

            string directory = Path.GetDirectoryName(chartPath);
            string chartName = Path.GetFileNameWithoutExtension(chartPath);
            string highResolutionChartJacket = Path.Combine(directory, $"1080_{chartName}.jpg");
            if (File.Exists(highResolutionChartJacket))
            {
                return highResolutionChartJacket;
            }

            string baseJacket = Path.Combine(directory, "base.jpg");
            if (File.Exists(baseJacket))
            {
                return baseJacket;
            }

            string highResolutionBaseJacket = Path.Combine(directory, "1080_base.jpg");
            if (File.Exists(highResolutionBaseJacket))
            {
                return highResolutionBaseJacket;
            }

            return !string.IsNullOrEmpty(preferredJacketPath) && File.Exists(preferredJacketPath)
                ? preferredJacketPath
                : null;
        }

        public static bool AreSameFile(string firstPath, string secondPath)
        {
            if (string.IsNullOrEmpty(firstPath) || string.IsNullOrEmpty(secondPath))
            {
                return false;
            }

            return Path.GetFullPath(firstPath).Equals(
                Path.GetFullPath(secondPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHistoryChartPath(string chartPath, string directory)
        {
            if (string.IsNullOrWhiteSpace(chartPath))
            {
                return null;
            }

            try
            {
                string fullChartPath = Path.IsPathRooted(chartPath)
                    ? Path.GetFullPath(chartPath)
                    : Path.GetFullPath(Path.Combine(directory, chartPath));
                return File.Exists(fullChartPath)
                    && Path.GetExtension(fullChartPath).Equals(".aff", StringComparison.OrdinalIgnoreCase)
                    && Path.GetDirectoryName(fullChartPath).Equals(directory, StringComparison.OrdinalIgnoreCase)
                        ? fullChartPath
                        : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }
    }
}
