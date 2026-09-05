using System;
using System.IO;
using System.Linq;

namespace ArcCreate.Compose.Project
{
    public static class DirectFileProjectResolver
    {
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

            string baseJacket = Path.Combine(Path.GetDirectoryName(chartPath), "base.jpg");
            if (File.Exists(baseJacket))
            {
                return baseJacket;
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
    }
}
