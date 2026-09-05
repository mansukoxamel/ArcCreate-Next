using System;
using System.IO;
using System.Linq;

namespace ArcCreate.Compose.Project
{
    public static class DirectFileProjectResolver
    {
        public static bool IsSupportedDrop(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(".aff", StringComparison.OrdinalIgnoreCase))
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
    }
}
