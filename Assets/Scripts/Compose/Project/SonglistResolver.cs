using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcCreate.Data;
using Newtonsoft.Json;

namespace ArcCreate.Compose.Project
{
    public enum SonglistLookupStatus
    {
        NotFound,
        Found,
        DuplicateId,
        Invalid,
    }

    public sealed class SonglistLookupResult
    {
        public SonglistLookupStatus Status { get; set; }

        public SonglistSong Song { get; set; }

        public string Error { get; set; }
    }

    public sealed class SonglistSong
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title_localized")]
        public Dictionary<string, string> TitleLocalized { get; set; }

        [JsonProperty("artist")]
        public string Artist { get; set; }

        [JsonProperty("bpm")]
        public string Bpm { get; set; }

        [JsonProperty("bpm_base")]
        public float BpmBase { get; set; }

        [JsonProperty("audioPreview")]
        public int AudioPreview { get; set; }

        [JsonProperty("audioPreviewEnd")]
        public int AudioPreviewEnd { get; set; }

        [JsonProperty("side")]
        public int Side { get; set; }

        [JsonProperty("bg")]
        public string Background { get; set; }

        [JsonProperty("difficulties")]
        public List<SonglistDifficulty> Difficulties { get; set; }

        public string GetDisplayTitle()
        {
            if (TitleLocalized == null || TitleLocalized.Count == 0)
            {
                return null;
            }

            if (TitleLocalized.TryGetValue("ja", out string japanese)
             && !string.IsNullOrWhiteSpace(japanese))
            {
                return japanese;
            }

            if (TitleLocalized.TryGetValue("en", out string english)
             && !string.IsNullOrWhiteSpace(english))
            {
                return english;
            }

            return TitleLocalized.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }

    public sealed class SonglistDifficulty
    {
        [JsonProperty("ratingClass")]
        public int RatingClass { get; set; }

        [JsonProperty("rating")]
        public int Rating { get; set; }

        [JsonProperty("ratingPlus")]
        public bool RatingPlus { get; set; }

        [JsonProperty("chartDesigner")]
        public string ChartDesigner { get; set; }

        [JsonProperty("jacketDesigner")]
        public string JacketDesigner { get; set; }

        [JsonProperty("bg")]
        public string Background { get; set; }
    }

    public static class SonglistResolver
    {
        public const string OfficialFileName = "songlist";
        public const string CustomFileName = "songlist.arcnext.json";

        public static SonglistLookupResult Lookup(string songDirectory)
        {
            try
            {
                string fullSongDirectory = Path.GetFullPath(songDirectory);
                DirectoryInfo song = new DirectoryInfo(fullSongDirectory);
                if (!song.Exists || song.Parent == null)
                {
                    return NotFound();
                }

                string officialPath = Path.Combine(song.Parent.FullName, OfficialFileName);
                string customPath = Path.Combine(song.Parent.FullName, CustomFileName);
                SonglistSong official = ReadSong(officialPath, song.Name);
                SonglistSong custom = ReadSong(customPath, song.Name);

                // TODO: The policy for choosing between official and custom entries with the
                // same id is deliberately unresolved. Never merge or overwrite either entry.
                if (official != null && custom != null)
                {
                    return new SonglistLookupResult
                    {
                        Status = SonglistLookupStatus.DuplicateId,
                        Error = $"本家とArcCreate Nextのsonglistに同じIDがあります: {song.Name}",
                    };
                }

                SonglistSong found = official ?? custom;
                return found == null
                    ? NotFound()
                    : new SonglistLookupResult { Status = SonglistLookupStatus.Found, Song = found };
            }
            catch (Exception exception) when (
                exception is IOException
             || exception is UnauthorizedAccessException
             || exception is JsonException
             || exception is ArgumentException
             || exception is NotSupportedException)
            {
                return new SonglistLookupResult
                {
                    Status = SonglistLookupStatus.Invalid,
                    Error = $"songlistを読み込めませんでした: {exception.Message}",
                };
            }
        }

        public static SonglistDifficulty FindDifficulty(SonglistSong song, string chartPath)
        {
            if (song?.Difficulties == null
             || !int.TryParse(Path.GetFileNameWithoutExtension(chartPath), out int ratingClass))
            {
                return null;
            }

            return song.Difficulties.FirstOrDefault(difficulty => difficulty.RatingClass == ratingClass);
        }

        public static string GetDifficultyName(int ratingClass)
        {
            switch (ratingClass)
            {
                case 0:
                    return "Past";
                case 1:
                    return "Present";
                case 2:
                    return "Future";
                case 3:
                    return "Beyond";
                case 4:
                    return "Eternal";
                default:
                    return null;
            }
        }

        public static string GetSideName(int side)
        {
            switch (side)
            {
                case 0:
                    return "light";
                case 1:
                    return "conflict";
                case 2:
                    return "colorless";
                default:
                    return null;
            }
        }

        public static void ApplyMetadata(ChartSettings chart, SonglistSong song)
        {
            string title = song.GetDisplayTitle();
            if (!string.IsNullOrWhiteSpace(title))
            {
                chart.Title = title;
            }

            if (!string.IsNullOrWhiteSpace(song.Artist))
            {
                chart.Composer = song.Artist;
            }

            if (song.BpmBase > 0)
            {
                chart.BaseBpm = song.BpmBase;
            }

            chart.BpmText = string.IsNullOrWhiteSpace(song.Bpm) ? chart.BaseBpm.ToString() : song.Bpm;
            chart.PreviewStart = song.AudioPreview;
            chart.PreviewEnd = song.AudioPreviewEnd;

            string side = GetSideName(song.Side);
            if (side != null)
            {
                chart.Skin = chart.Skin ?? new SkinSettings();
                chart.Skin.Side = side;
            }

            SonglistDifficulty difficulty = FindDifficulty(song, chart.ChartPath);
            if (difficulty == null)
            {
                return;
            }

            string difficultyName = GetDifficultyName(difficulty.RatingClass);
            if (difficultyName != null)
            {
                chart.Difficulty = $"{difficultyName} {difficulty.Rating}{(difficulty.RatingPlus ? "+" : string.Empty)}";
            }

            // The official songlist exposes only the displayed rating, not the exact chart constant.
            chart.ChartConstant = difficulty.Rating + (difficulty.RatingPlus ? 0.7 : 0);
            chart.Charter = difficulty.ChartDesigner;
            chart.Illustrator = difficulty.JacketDesigner;

            // bg is metadata, not a guaranteed file beside the song. Keep BackgroundPath empty
            // when no real file is resolved so GameplayLoader uses the side's standard background.
            chart.BackgroundPath = null;
        }

        private static SonglistSong ReadSong(string path, string id)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            SonglistDocument document = JsonConvert.DeserializeObject<SonglistDocument>(File.ReadAllText(path));
            if (document?.Songs == null)
            {
                throw new JsonSerializationException($"{Path.GetFileName(path)}にsongs配列がありません。");
            }

            return document.Songs.FirstOrDefault(song =>
                string.Equals(song.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static SonglistLookupResult NotFound()
        {
            return new SonglistLookupResult { Status = SonglistLookupStatus.NotFound };
        }

        private sealed class SonglistDocument
        {
            [JsonProperty("songs")]
            public List<SonglistSong> Songs { get; set; }
        }
    }
}
