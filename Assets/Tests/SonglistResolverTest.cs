using System;
using System.IO;
using ArcCreate.Compose.Project;
using ArcCreate.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class SonglistResolverTest
    {
        private string libraryDirectory;
        private string songDirectory;

        [SetUp]
        public void SetUp()
        {
            libraryDirectory = Path.Combine(Path.GetTempPath(), $"ArcCreateNext-Songlist-{Guid.NewGuid():N}");
            songDirectory = Path.Combine(libraryDirectory, "testsong");
            Directory.CreateDirectory(songDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(libraryDirectory, true);
        }

        [Test]
        public void Lookup_FindsOfficialSongByRelativeParentAndFolderName()
        {
            WriteSonglist("songlist", "testsong", "Official title");

            SonglistLookupResult result = SonglistResolver.Lookup(songDirectory);

            Assert.That(result.Status, Is.EqualTo(SonglistLookupStatus.Found));
            Assert.That(result.Song.Id, Is.EqualTo("testsong"));
            Assert.That(result.Song.GetDisplayTitle(), Is.EqualTo("日本語タイトル"));
            Assert.That(result.Song.Artist, Is.EqualTo("Composer"));
            Assert.That(result.Song.BpmBase, Is.EqualTo(180));
            Assert.That(result.Song.Side, Is.EqualTo(1));
        }

        [Test]
        public void Lookup_FindsCustomSongWhenOfficialHasNoMatchingId()
        {
            WriteSonglist("songlist", "another", "Official title");
            WriteSonglist("songlist.arcnext.json", "testsong", "Custom title");

            SonglistLookupResult result = SonglistResolver.Lookup(songDirectory);

            Assert.That(result.Status, Is.EqualTo(SonglistLookupStatus.Found));
            Assert.That(result.Song.GetDisplayTitle(), Is.EqualTo("日本語タイトル"));
        }

        [Test]
        public void Lookup_ReturnsNotFoundWithoutListsOrMatchingId()
        {
            Assert.That(SonglistResolver.Lookup(songDirectory).Status, Is.EqualTo(SonglistLookupStatus.NotFound));

            WriteSonglist("songlist", "another", "Other title");
            Assert.That(SonglistResolver.Lookup(songDirectory).Status, Is.EqualTo(SonglistLookupStatus.NotFound));
        }

        [Test]
        public void Lookup_DetectsDuplicateIdWithoutChoosingEitherEntry()
        {
            WriteSonglist("songlist", "testsong", "Official title");
            WriteSonglist("songlist.arcnext.json", "testsong", "Custom title");

            SonglistLookupResult result = SonglistResolver.Lookup(songDirectory);

            Assert.That(result.Status, Is.EqualTo(SonglistLookupStatus.DuplicateId));
            Assert.That(result.Song, Is.Null);
            StringAssert.Contains("testsong", result.Error);
        }

        [Test]
        public void Lookup_ReportsMalformedJsonInsteadOfHidingIt()
        {
            File.WriteAllText(Path.Combine(libraryDirectory, "songlist"), "{broken");

            SonglistLookupResult result = SonglistResolver.Lookup(songDirectory);

            Assert.That(result.Status, Is.EqualTo(SonglistLookupStatus.Invalid));
            Assert.That(result.Error, Is.Not.Empty);
        }

        [TestCase(0, "Past")]
        [TestCase(1, "Present")]
        [TestCase(2, "Future")]
        [TestCase(3, "Beyond")]
        [TestCase(4, "Eternal")]
        public void DifficultyMapping_UsesArcaeaNames(int ratingClass, string name)
        {
            Assert.That(SonglistResolver.GetDifficultyName(ratingClass), Is.EqualTo(name));
        }

        [TestCase(0, "light")]
        [TestCase(1, "conflict")]
        [TestCase(2, "colorless")]
        public void SideMapping_UsesExistingArcCreateSkinNames(int side, string name)
        {
            Assert.That(SonglistResolver.GetSideName(side), Is.EqualTo(name));
        }

        [Test]
        public void FindDifficulty_UsesAffNumberAsRatingClass()
        {
            WriteSonglist("songlist", "testsong", "Official title");
            SonglistSong song = SonglistResolver.Lookup(songDirectory).Song;

            SonglistDifficulty difficulty = SonglistResolver.FindDifficulty(song, "2.aff");

            Assert.That(difficulty, Is.Not.Null);
            Assert.That(difficulty.Rating, Is.EqualTo(9));
            Assert.That(difficulty.RatingPlus, Is.True);
            Assert.That(SonglistResolver.FindDifficulty(song, "custom.aff"), Is.Null);
        }

        [Test]
        public void ApplyMetadata_SetsSongAndDifficultyDataAndFallsBackToSideBackground()
        {
            WriteSonglist("songlist", "testsong", "Official title");
            SonglistSong song = SonglistResolver.Lookup(songDirectory).Song;
            ChartSettings chart = new ChartSettings
            {
                ChartPath = "2.aff",
                Title = "Untitled",
                Composer = "N/A",
                BackgroundPath = "old.jpg",
            };

            SonglistResolver.ApplyMetadata(chart, song);

            Assert.That(chart.Title, Is.EqualTo("日本語タイトル"));
            Assert.That(chart.Composer, Is.EqualTo("Composer"));
            Assert.That(chart.BaseBpm, Is.EqualTo(180));
            Assert.That(chart.BpmText, Is.EqualTo("180"));
            Assert.That(chart.PreviewStart, Is.EqualTo(1000));
            Assert.That(chart.PreviewEnd, Is.EqualTo(5000));
            Assert.That(chart.Difficulty, Is.EqualTo("Future 9+"));
            Assert.That(chart.ChartConstant, Is.EqualTo(9.7).Within(0.0001));
            Assert.That(chart.Charter, Is.EqualTo("Charter"));
            Assert.That(chart.Illustrator, Is.EqualTo("Illustrator"));
            Assert.That(chart.Skin.Side, Is.EqualTo("conflict"));
            Assert.That(chart.BackgroundPath, Is.Null);
        }

        private void WriteSonglist(string fileName, string id, string englishTitle)
        {
            string json = $@"{{
  ""songs"": [{{
    ""id"": ""{id}"",
    ""title_localized"": {{ ""en"": ""{englishTitle}"", ""ja"": ""日本語タイトル"" }},
    ""artist"": ""Composer"",
    ""bpm"": ""180"",
    ""bpm_base"": 180,
    ""audioPreview"": 1000,
    ""audioPreviewEnd"": 5000,
    ""side"": 1,
    ""bg"": ""missing_background_is_allowed"",
    ""difficulties"": [{{
      ""ratingClass"": 2,
      ""rating"": 9,
      ""ratingPlus"": true,
      ""chartDesigner"": ""Charter"",
      ""jacketDesigner"": ""Illustrator""
    }}]
  }}]
}}";
            File.WriteAllText(Path.Combine(libraryDirectory, fileName), json);
        }
    }
}
