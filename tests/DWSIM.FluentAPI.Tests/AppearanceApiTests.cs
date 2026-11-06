using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Interfaces.Enums.GraphicObjects;
using NUnit.Framework;
using SkiaSharp;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The drawing's appearance through the FluentAPI: background, grid, layout lock and the annotation builders.
    /// </summary>
    [TestFixture]
    public class AppearanceApiTests
    {
        [Test]
        public void TheBackgroundGridAndLockAreSet()
        {
            var fs = Flowsheet.Create("appearance").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables)
                .WithBackgroundGradient("#DCE8F4", "#ffffff").WithGrid(show: true, snap: true).LockLayout();
            var o = fs.Inner.FlowsheetOptions;
            Assert.That(o.FlowsheetBackgroundColor, Is.EqualTo("#DCE8F4"));
            Assert.That(o.FlowsheetBackgroundColor2, Is.EqualTo("#FFFFFF"));
            Assert.That(o.FlowsheetDisplayGrid && o.FlowsheetSnapToGrid && o.FlowsheetLockLayout, Is.True);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            Assert.That(surface.ShowGrid && surface.SnapToGrid, Is.True);

            fs.WithBackground(22, 34, 58);
            Assert.That(o.FlowsheetBackgroundColor, Is.EqualTo("#16223A"));
            Assert.That(o.FlowsheetBackgroundColor2, Is.Empty, "a plain color drops the gradient");

            fs.WithThemeBackground();
            Assert.That(o.FlowsheetBackgroundColor, Is.Empty);
            Assert.Throws<ArgumentException>(() => fs.WithBackground("not a color"));
        }

        [Test]
        public void TheAnnotationBuildersSetWhatTheDrawingUses()
        {
            var fs = Flowsheet.Create("annotations").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);

            var text = fs.AddText("note", "rotated", 10, 10).WithColor("#336699").WithFontStyle(FontStyle.Bold).Rotate(-90).LockPosition();
            Assert.That(text.Object.Rotation, Is.EqualTo(270));
            Assert.That(text.Object.FontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(text.Object.PositionLocked, Is.True);

            var html = fs.AddFormattedText("intro", "<h1>Plant</h1>", 10, 60, 300, 120).WithColor("#202020").WithFontSize(16);
            Assert.That(html.Object.ObjectType, Is.EqualTo(ObjectType.GO_HTMLText));
            Assert.That((html.Object.Width, html.Object.Height), Is.EqualTo((300, 120)));

            var box = fs.AddRectangle("area", 0, 0, 400, 300).WithText("Section 100", 14).WithGradient("#EEF3F8", "#B0C4DE").WithOpacity(80).WithRoundedCorners(false);
            Assert.That(box.Object.GradientMode, Is.True);
            Assert.That(box.Object.Text, Is.EqualTo("Section 100"));
            Assert.That(box.Object.RoundEdges, Is.False);

            var file = Path.Combine(Path.GetTempPath(), "dwsim-appearance-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                using (var bmp = new SKBitmap(200, 100))
                using (var img = SKImage.FromBitmap(bmp))
                using (var data = img.Encode(SKEncodedImageFormat.Png, 90))
                using (var f = File.Create(file)) data.SaveTo(f);

                var pic = fs.AddImage("logo", file, 500, 10, 400, 200);
                pic.WithWidth(100);
                Assert.That(pic.Object.Height, Is.EqualTo(50), "proportions kept");
                pic.KeepProportions(false).WithHeight(80);
                Assert.That(pic.Object.Width, Is.EqualTo(100));
                pic.OriginalSize().FlipHorizontal().Rotate(90);
                Assert.That((pic.Object.Width, pic.Object.Height), Is.EqualTo((200, 100)));
                Assert.That(pic.Object.FlippedH, Is.True);
                Assert.That(pic.Object.Rotation, Is.EqualTo(90));
            }
            finally
            {
                File.Delete(file);
            }

            Assert.That(fs.Annotations.Count, Is.EqualTo(4));
        }
    }
}
