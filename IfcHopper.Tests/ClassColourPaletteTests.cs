using System;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class ClassColourPaletteTests
    {
        [Fact]
        public void AutomaticColours_AreStableAcrossPalettesOrderAndCase()
        {
            var first = new ClassColourPalette();
            var wall = first.GetColour("IfcWall");
            var slab = first.GetColour("IfcSlab");
            var second = new ClassColourPalette();
            Assert.Equal(slab, second.GetColour("IFCSLAB"));
            Assert.Equal(wall, second.GetColour(" ifcwall "));
            Assert.NotEqual(wall, slab);
            Assert.Equal(0, wall.Transparency);
            Assert.InRange(wall.Red, 64 / 255.0, 223 / 255.0);
        }

        [Fact]
        public void PartialOverrides_AreExactCaseInsensitiveAndPreserveTransparency()
        {
            var colour = new Colour(1, 0, 0, 0.5);
            var classes = new[] { " ifcwall " };
            var values = new[] { colour };
            var palette = new ClassColourPalette(classes, values);
            classes[0] = "IfcSlab";
            values[0] = new Colour(0, 1, 0);
            Assert.Same(colour, palette.GetColour("IFCWALL"));
            Assert.Equal(new ClassColourPalette().GetColour("IfcSlab"), palette.GetColour("IfcSlab"));
            Assert.Equal(new ClassColourPalette().GetColour("IfcWallStandardCase"), palette.GetColour("IfcWallStandardCase"));
        }

        [Fact]
        public void WildcardFallback_AppliesOnlyWithoutAnExactOverrideRegardlessOfOrder()
        {
            var red = new Colour(1, 0, 0);
            var grey = new Colour(.5, .5, .5, .6);
            foreach (var palette in new[]
            {
                new ClassColourPalette(new[] { "*", "IfcWall" }, new[] { grey, red }),
                new ClassColourPalette(new[] { "IfcWall", " * " }, new[] { red, grey })
            })
            {
                Assert.Same(red, palette.GetColour("ifcwall"));
                Assert.Same(grey, palette.GetColour("IfcSlab"));
                Assert.Same(grey, palette.GetColour("IfcWallStandardCase"));
            }
            Assert.Throws<ArgumentException>(() => new ClassColourPalette(new[] { "*", " * " }, new[] { red, grey }));
        }

        [Fact]
        public void InvalidOverrides_AreRejected()
        {
            var red = new Colour(1, 0, 0);
            Assert.Throws<ArgumentException>(() => new ClassColourPalette(new[] { "IfcWall" }));
            Assert.Throws<ArgumentException>(() => new ClassColourPalette(null, new[] { red }));
            Assert.Throws<ArgumentException>(() => new ClassColourPalette(new[] { "IfcWall", "ifcwall" }, new[] { red, red }));
            Assert.Throws<ArgumentException>(() => new ClassColourPalette(new[] { "IfcWall" }, new Colour[] { null }));
            foreach (var invalid in new[] { null, " ", "IfcNotAClass", "!IfcWall" })
            {
                Assert.Throws<ArgumentException>(() => new ClassColourPalette(new[] { invalid }, new[] { red }));
                Assert.Throws<ArgumentException>(() => new ClassColourPalette().GetColour(invalid));
            }
        }
    }
}
