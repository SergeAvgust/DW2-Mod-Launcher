using System.IO;
using DW2ModLauncher.Core.Services.Publishing;
using Xunit;

namespace DW2ModLauncher.Tests
{
    public class ModPublishCommandBuilderTests
    {
        [Fact]
        public void GetModsRelativePath_ReturnsModsSlashFolder_ForDirectChildOfGameModsFolder()
        {
            string gameRoot = Path.Combine(Path.GetTempPath(), "dw2-game-" + System.Guid.NewGuid().ToString("N"));
            string modFolder = Path.Combine(gameRoot, "mods", "MyMod");

            string relative = ModPublishCommandBuilder.GetModsRelativePath(gameRoot, modFolder);

            Assert.Equal("mods/MyMod", relative);
        }

        [Fact]
        public void GetModsRelativePath_UsesForwardSlashes_ForNestedFolder()
        {
            string gameRoot = Path.Combine(Path.GetTempPath(), "dw2-game-" + System.Guid.NewGuid().ToString("N"));
            string modFolder = Path.Combine(gameRoot, "mods", "Group", "MyMod");

            string relative = ModPublishCommandBuilder.GetModsRelativePath(gameRoot, modFolder);

            Assert.Equal("mods/Group/MyMod", relative);
        }

        [Fact]
        public void GetModsRelativePath_ReturnsNull_WhenModIsOutsideGameModsFolder()
        {
            string gameRoot = Path.Combine(Path.GetTempPath(), "dw2-game-" + System.Guid.NewGuid().ToString("N"));
            string modFolder = Path.Combine(Path.GetTempPath(), "workshop-content", "12345");

            string relative = ModPublishCommandBuilder.GetModsRelativePath(gameRoot, modFolder);

            Assert.Null(relative);
        }

        [Fact]
        public void GetModsRelativePath_ReturnsNull_WhenModIsTheModsFolderItself()
        {
            string gameRoot = Path.Combine(Path.GetTempPath(), "dw2-game-" + System.Guid.NewGuid().ToString("N"));
            string modFolder = Path.Combine(gameRoot, "mods");

            string relative = ModPublishCommandBuilder.GetModsRelativePath(gameRoot, modFolder);

            Assert.Null(relative);
        }

        [Fact]
        public void BuildArguments_QuotesPathsContainingSpaces()
        {
            Assert.Equal("--ugc-publish \"mods/My Mod\"", ModPublishCommandBuilder.BuildArguments("mods/My Mod"));
            Assert.Equal("--ugc-publish mods/MyMod", ModPublishCommandBuilder.BuildArguments("mods/MyMod"));
        }
    }
}
