using System;
using System.IO;
using System.Linq;
using ArcCreate.Gameplay.Data;
using UnityEngine;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Holds the miss log that was dropped onto the editor. Loading it selects the notes that were missed in the
    /// chart that is open, and "next miss" / "previous miss" move the playback position from one miss to the next.
    /// </summary>
    public static class MissLogSession
    {
        /// <summary>
        /// How long before a missed note the playback position is placed, so the approach to it can be watched.
        /// </summary>
        public const int LeadMilliseconds = 1000;

        private static readonly string[] DifficultyNames = { "PAST", "PRESENT", "FUTURE", "BEYOND", "ETERNAL" };

        /// <summary>
        /// Gets the miss log that was loaded last, or null.
        /// </summary>
        public static MissLogFile File { get; private set; }

        /// <summary>
        /// Gets the result of matching that log against the chart that was open when it was loaded, or null.
        /// </summary>
        public static MissMatchResult Result { get; private set; }

        /// <summary>
        /// Reads a miss log file, selects the missed notes of the open chart and jumps to the first miss.
        /// Every problem is shown to the user; nothing is skipped silently.
        /// </summary>
        /// <param name="path">The path of the dropped miss log file.</param>
        public static void Load(string path)
        {
            Debug.Log($"ミス記録を読み込みます: \"{path}\"（譜面の読み込み済み: {Services.Gameplay?.IsLoaded ?? false}）");
            if (!(Services.Gameplay?.IsLoaded ?? false))
            {
                Services.Popups.Notify(
                    Popups.Severity.Error,
                    "先に、ミスした譜面（AFF）を開いてください。\nミス記録は、開いている譜面の上に重ねて表示します。");
                return;
            }

            MissLogFile file;
            try
            {
                file = MissLogParser.Parse(System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8));
            }
            catch (MissLogFormatException error)
            {
                Services.Popups.Notify(Popups.Severity.Error, $"ミス記録を読み込めません。\n{Path.GetFileName(path)}\n{error.Message}");
                return;
            }
            catch (IOException error)
            {
                Services.Popups.Notify(Popups.Severity.Error, $"ミス記録を開けません。\n{path}\n{error.Message}");
                return;
            }

            MissMatchResult result = MissLogMatcher.Match(
                file,
                Services.Gameplay.Chart.GetAll<Tap>(),
                Services.Gameplay.Chart.GetAll<Hold>(),
                Services.Gameplay.Chart.GetAll<Arc>(),
                Services.Gameplay.Chart.GetAll<ArcTap>());

            File = file;
            Result = result;
            Services.Selection.SetSelection(result.AllNotes);
            Debug.Log($"ミス記録: {file.Records.Count}行、ミスしたノーツ{result.Missed.Count}本、譜面に見つからない行{result.Unmatched.Count}件、選択したノーツ{Services.Selection.SelectedNotes.Count}本");

            string warning = DescribeMismatch(file);
            Services.Popups.Notify(
                warning.Length > 0 || result.Unmatched.Count > 0 ? Popups.Severity.Warning : Popups.Severity.Info,
                Summarize(file, result) + warning);

            if (result.Missed.Count > 0)
            {
                Services.Gameplay.Audio.ChartTiming = Math.Max(0, result.Missed[0].Timing - LeadMilliseconds);
            }
        }

        /// <summary>
        /// Moves the playback position to the next or the previous missed note, measured from the current position.
        /// </summary>
        /// <param name="direction">+1 for the next miss, -1 for the previous miss.</param>
        public static void Step(int direction)
        {
            if (Result == null || Result.Missed.Count == 0)
            {
                Services.Popups.Notify(Popups.Severity.Info, "ミス記録が読み込まれていません。ミス記録のtxtファイルを、この画面へドロップしてください。");
                return;
            }

            int current = Services.Gameplay.Audio.ChartTiming;
            MissedNote target = direction > 0
                ? Result.Missed.FirstOrDefault(m => m.Timing - LeadMilliseconds > current)
                : Result.Missed.LastOrDefault(m => m.Timing - LeadMilliseconds < current);
            if (target == null)
            {
                Services.Popups.Notify(Popups.Severity.Info, direction > 0 ? "これが最後のミスです。" : "これが最初のミスです。");
                return;
            }

            Services.Gameplay.Audio.ChartTiming = Math.Max(0, target.Timing - LeadMilliseconds);
            int index = Result.Missed.IndexOf(target) + 1;
            Services.Popups.Notify(Popups.Severity.Info, $"ミス {index}/{Result.Missed.Count}: {FormatTime(target.Timing)}  {Describe(target)}");
        }

        /// <summary>
        /// Formats a chart time as mm:ss.mmm.
        /// </summary>
        /// <param name="milliseconds">The chart time.</param>
        /// <returns>The text.</returns>
        public static string FormatTime(int milliseconds)
        {
            int minutes = milliseconds / 60000;
            int seconds = (milliseconds / 1000) % 60;
            return $"{minutes:00}:{seconds:00}.{milliseconds % 1000:000}";
        }

        private static string Describe(MissedNote missed)
        {
            MissRecord first = missed.Records[0];
            string lane = first.Lane > 0 ? $" レーン{first.Lane}" : string.Empty;
            int lost = missed.Records.Count(r => r.Kind == MissKind.Lost);
            int far = missed.Records.Count(r => r.Kind == MissKind.Far);
            string counts = far > 0 && lost > 0 ? $"FAR{far}・LOST{lost}" : far > 0 ? "FAR" : lost > 1 ? $"LOST{lost}粒" : "LOST";
            return $"{first.NoteType}{lane} {counts}";
        }

        private static string Summarize(MissLogFile file, MissMatchResult result)
        {
            string text = $"ミス記録を読み込みました: {file.Title} {file.Difficulty}\n"
                + $"{file.Date}  速度 {file.Speed}\n"
                + $"ミス {file.Records.Count}行 → ノーツ {result.AllNotes.Count()}本を選択"
                + "（次のミス: Alt+N、前のミス: Alt+P）";
            if (result.Unmatched.Count > 0)
            {
                text += $"\n譜面に見つからなかった行: {result.Unmatched.Count}件（例: {result.Unmatched[0].LineNumber}行目、{FormatTime(result.Unmatched[0].NoteMs)}）";
            }

            return text;
        }

        // The log names the song folder and the difficulty; the open chart should be the same one.
        private static string DescribeMismatch(MissLogFile file)
        {
            string chartPath = Services.Project.CurrentChart?.ChartPath;
            if (string.IsNullOrEmpty(chartPath))
            {
                return string.Empty;
            }

            string message = string.Empty;
            string folder = Path.GetFileName(Path.GetDirectoryName(chartPath));
            if (!string.IsNullOrEmpty(file.SongId) && !string.Equals(folder, file.SongId, StringComparison.OrdinalIgnoreCase))
            {
                message += $"\n注意: 開いている曲のフォルダ（{folder}）と、ミス記録の曲（{file.SongId}）が違います。";
            }

            string difficultyName = file.Difficulty.Split(' ')[0];
            int logIndex = Array.IndexOf(DifficultyNames, difficultyName);
            if (logIndex >= 0 && int.TryParse(Path.GetFileNameWithoutExtension(chartPath), out int chartIndex) && chartIndex != logIndex)
            {
                message += $"\n注意: 開いている譜面（{Path.GetFileName(chartPath)}）と、ミス記録の難易度（{file.Difficulty}）が違うようです。";
            }

            return message;
        }
    }
}
