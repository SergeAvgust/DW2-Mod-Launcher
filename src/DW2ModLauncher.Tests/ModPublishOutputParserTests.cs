using DW2ModLauncher.Core.Services.Publishing;
using Xunit;

namespace DW2ModLauncher.Tests
{
    public class ModPublishOutputParserTests
    {
        // Captured verbatim from a real "DistantWorlds2.exe --ugc-publish mods/GalCivMusic" run.
        private const string RealSampleOutput =
            "Publishing mods\\GalCivMusic\r\n\r\n" +
            "Setting breakpad minidump AppID = 1531540\r\n" +
            "SteamInternal_SetMinidumpSteamID:  Caching Steam ID:  76561199555527063 [API loaded no]\r\n\r\n" +
            "Using Workshop ID 3807392576\r\n" +
            "Title: Galactic Civilizations II Music Compilation\r\n" +
            "Description: Music from Galactic Civilizations II game\r\n" +
            "Preview Image: poster.jpg\r\n" +
            "Version: 1.0.0\r\n" +
            "Progress: 100.00%\r\n" +
            "Success: Created Workshop item 3807392576 (OK)\r\n" +
            "URL: http://steamcommunity.com/sharedfiles/filedetails/?source=Facepunch.Steamworks&id=3807392576\r\n" +
            "Press any key to exit.";

        [Fact]
        public void TryParseWorkshopId_ParsesRealSampleOutput()
        {
            bool found = ModPublishOutputParser.TryParseWorkshopId(RealSampleOutput, out long workshopId);

            Assert.True(found);
            Assert.Equal(3807392576L, workshopId);
        }

        [Fact]
        public void TryParseWorkshopId_FallsBackToUrl_WhenSuccessLineMissing()
        {
            string text = "URL: http://steamcommunity.com/sharedfiles/filedetails/?id=123456789\r\nPress any key to exit.";

            bool found = ModPublishOutputParser.TryParseWorkshopId(text, out long workshopId);

            Assert.True(found);
            Assert.Equal(123456789L, workshopId);
        }

        [Fact]
        public void TryParseWorkshopId_ReturnsFalse_WhenNothingMatches()
        {
            bool found = ModPublishOutputParser.TryParseWorkshopId("Publishing mods\\Foo\r\nPress any key to exit.", out long workshopId);

            Assert.False(found);
            Assert.Equal(0, workshopId);
        }

        [Fact]
        public void TryParseWorkshopId_ReturnsFalse_ForNullOrEmpty()
        {
            Assert.False(ModPublishOutputParser.TryParseWorkshopId(null, out _));
            Assert.False(ModPublishOutputParser.TryParseWorkshopId("", out _));
        }
    }
}
