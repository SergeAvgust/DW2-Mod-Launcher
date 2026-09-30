using System;
using DW2ModLauncher.Core.Services.Publishing;
using Xunit;

namespace DW2ModLauncher.Tests
{
    public class ModWorkshopIdWatcherTests
    {
        [Fact]
        public void WaitForWorkshopId_ReturnsId_AsSoonAsScreenTextParses()
        {
            int reads = 0;
            Func<string> readScreenText = () =>
            {
                reads++;
                return reads < 3 ? "Publishing mods\\Foo" : "Success: Created Workshop item 42 (OK)";
            };

            long? result = ModWorkshopIdWatcher.WaitForWorkshopId(() => false, readScreenText, TimeSpan.FromSeconds(5), TimeSpan.Zero);

            Assert.Equal(42L, result);
            Assert.Equal(3, reads);
        }

        [Fact]
        public void WaitForWorkshopId_ReturnsNull_WhenProcessExitsFirst()
        {
            long? result = ModWorkshopIdWatcher.WaitForWorkshopId(() => true, () => "irrelevant", TimeSpan.FromSeconds(5), TimeSpan.Zero);

            Assert.Null(result);
        }

        [Fact]
        public void WaitForWorkshopId_ReturnsNull_WhenTimeoutElapsesWithoutAMatch()
        {
            long? result = ModWorkshopIdWatcher.WaitForWorkshopId(() => false, () => "no id here", TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10));

            Assert.Null(result);
        }
    }
}
