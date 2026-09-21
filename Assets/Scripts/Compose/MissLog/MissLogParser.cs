using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Reads the miss log text files written by Xynapse ("# Xynapse miss log v1/v2/v3").
    /// The columns are read by the names in the "# note_ms ..." header row, so older versions
    /// (without the lane or chart_info columns) still load.
    /// </summary>
    public static class MissLogParser
    {
        private const string HeaderPrefix = "# Xynapse miss log v";
        private const string ColumnHeaderStart = "# note_ms";

        /// <summary>
        /// Gets a value indicating whether the text starts like a Xynapse miss log.
        /// </summary>
        /// <param name="firstLine">The first line of the file.</param>
        /// <returns>True when the file is a miss log.</returns>
        public static bool IsMissLog(string firstLine)
        {
            return firstLine != null && firstLine.TrimStart('﻿').StartsWith(HeaderPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Parses a whole miss log file.
        /// </summary>
        /// <param name="text">The file content.</param>
        /// <returns>The parsed file.</returns>
        /// <exception cref="MissLogFormatException">Thrown with the line number and reason when the file is malformed.</exception>
        public static MissLogFile Parse(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || !IsMissLog(lines[0]))
            {
                throw new MissLogFormatException("ミス記録ファイルではありません（先頭が「# Xynapse miss log v」ではありません）。");
            }

            MissLogFile file = new MissLogFile();
            string versionText = lines[0].TrimStart('﻿').Substring(HeaderPrefix.Length).Trim();
            if (!int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
            {
                throw new MissLogFormatException($"1行目: 形式の版を読めません。\n{lines[0]}");
            }

            file.Version = version;

            Dictionary<string, int> columns = null;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                int lineNumber = i + 1;
                if (line.StartsWith(ColumnHeaderStart, StringComparison.Ordinal))
                {
                    columns = ParseColumnHeader(line);
                    continue;
                }

                if (line[0] == '#')
                {
                    continue;
                }

                if (columns == null)
                {
                    ReadHeaderField(file, line);
                    continue;
                }

                file.Records.Add(ParseRecord(line, lineNumber, columns));
            }

            if (columns == null)
            {
                throw new MissLogFormatException("列の見出し行（# note_ms …）が見つかりません。");
            }

            return file;
        }

        private static Dictionary<string, int> ParseColumnHeader(string line)
        {
            string[] names = line.Substring(2).Split('\t');
            Dictionary<string, int> columns = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++)
            {
                columns[names[i].Trim()] = i;
            }

            return columns;
        }

        private static void ReadHeaderField(MissLogFile file, string line)
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                return;
            }

            string key = line.Substring(0, colon).Trim();
            string value = line.Substring(colon + 1).Trim();
            switch (key)
            {
                case "song":
                    file.SongId = value;
                    break;
                case "title":
                    file.Title = value;
                    break;
                case "difficulty":
                    file.Difficulty = value;
                    break;
                case "date":
                    file.Date = value;
                    break;
                case "speed":
                    file.Speed = value;
                    break;
            }
        }

        private static MissRecord ParseRecord(string line, int lineNumber, Dictionary<string, int> columns)
        {
            string[] fields = line.Split('\t');
            string Get(string name)
            {
                return columns.TryGetValue(name, out int index) && index < fields.Length ? fields[index].Trim() : null;
            }

            int RequireInt(string name)
            {
                string value = Get(name);
                if (value == null || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    throw new MissLogFormatException($"{lineNumber}行目: 「{name}」の列を数値として読めません。\n{line}");
                }

                return number;
            }

            MissRecord record = new MissRecord
            {
                LineNumber = lineNumber,
                NoteMs = RequireInt("note_ms"),
                JudgedMs = Get("judged_ms") == null ? 0 : RequireInt("judged_ms"),
                ChartInfo = Get("chart_info") ?? string.Empty,
            };

            switch (Get("kind"))
            {
                case "LOST":
                    record.Kind = MissKind.Lost;
                    break;
                case "FAR":
                    record.Kind = MissKind.Far;
                    break;
                default:
                    throw new MissLogFormatException($"{lineNumber}行目: 「kind」がLOSTでもFARでもありません。\n{line}");
            }

            switch (Get("note_type"))
            {
                case "Tap":
                    record.NoteType = MissNoteType.Tap;
                    break;
                case "Hold":
                    record.NoteType = MissNoteType.Hold;
                    break;
                case "ArcTap":
                    record.NoteType = MissNoteType.ArcTap;
                    break;
                case "Arc":
                    record.NoteType = MissNoteType.Arc;
                    break;
                default:
                    throw new MissLogFormatException($"{lineNumber}行目: 「note_type」を読めません（Tap・Hold・ArcTap・Arcのどれかのはずです）。\n{line}");
            }

            record.Lane = ReadLane(Get("lane"), record.ChartInfo);
            return record;
        }

        // v3 has a "lane" column read from the note itself ("1".."4", "-" or "?").
        // Older versions only have the chart file's "lane N" text; it is used only when it names a single lane.
        private static int ReadLane(string laneColumn, string chartInfo)
        {
            if (laneColumn != null)
            {
                return int.TryParse(laneColumn, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lane) ? lane : 0;
            }

            if (chartInfo.Length > 0 && !chartInfo.Contains("/"))
            {
                System.Text.RegularExpressions.Match match =
                    System.Text.RegularExpressions.Regex.Match(chartInfo, @"^lane (\d)");
                if (match.Success)
                {
                    return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }

            return 0;
        }
    }
}
