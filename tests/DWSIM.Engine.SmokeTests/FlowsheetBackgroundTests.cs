//    Background color of the flowsheet drawing.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Drawing.SkiaSharp.Appearance;
using NUnit.Framework;
using SkiaSharp;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A flowsheet can have its own background color. The drawing clears to it, lines and text turn light
    /// on a dark color and dark on a light one, and the color is saved with the flowsheet; a flowsheet
    /// without one follows the interface theme as before.
    /// </summary>
    [TestFixture]
    public class FlowsheetBackgroundTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            DWSIM.GlobalSettings.Settings.DarkMode = false;
        }

        // FEED -> H-1 -> OUT, well away from the corner the tests read
        private static (FluentFlowsheet fs, GraphicsSurface surface) Build()
        {
            var fs = FluentFlowsheet.Create("BackgroundProbe").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("FEED").PositionAt(150, 150);
            var heater = fs.AddHeater("H-1").PositionAt(300, 150).ConnectFeed(feed);
            heater.ConnectNewProduct("OUT").PositionAt(450, 150);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            surface.Flowsheet = fs.Inner;
            surface.Zoom = 1.0f;
            return (fs, surface);
        }

        private static SKColor CornerAfterDrawing(GraphicsSurface surface)
        {
            using var bitmap = new SKBitmap(600, 300);
            using (var canvas = new SKCanvas(bitmap)) surface.UpdateCanvas(canvas);
            return bitmap.GetPixel(2, 2);
        }

        // A vertical gradient runs from the top color at the top of the view to the bottom color at its
        // bottom, whatever the zoom, and the lines follow the mean of the two ends.
        [Test]
        public void AGradientRunsDownTheView()
        {
            var (fs, surface) = Build();
            var top = new SKColor(0xEC, 0xF2, 0xF8);
            var bottom = new SKColor(0xB0, 0xC4, 0xDE);
            FlowsheetBackground.SetColor(fs.Inner.FlowsheetOptions, top);
            FlowsheetBackground.SetGradientColor(fs.Inner.FlowsheetOptions, bottom);

            foreach (var zoom in new[] { 1.0f, 2.5f })
            {
                surface.Zoom = zoom;
                using var bitmap = new SKBitmap(600, 300);
                using (var canvas = new SKCanvas(bitmap)) surface.UpdateCanvas(canvas);
                Assert.That(Distance(bitmap.GetPixel(2, 0), top), Is.LessThan(4), "top of the view, zoom " + zoom);
                Assert.That(Distance(bitmap.GetPixel(2, 299), bottom), Is.LessThan(4), "bottom of the view, zoom " + zoom);
            }
            Assert.That(GraphicsSurface.DarkCanvas, Is.False);

            // dark ends turn the lines light; a dark and a light end are flagged
            Assert.That(FlowsheetBackground.IsDark(new SKColor(0x16, 0x22, 0x3A), new SKColor(0x1E, 0x1E, 0x1E)), Is.True);
            Assert.That(FlowsheetBackground.ContrastDiffers(top, new SKColor(0x16, 0x22, 0x3A)), Is.True);
            Assert.That(FlowsheetBackground.ContrastDiffers(top, bottom), Is.False);

            // following the theme again drops the gradient too
            FlowsheetBackground.SetColor(fs.Inner.FlowsheetOptions, null);
            Assert.That(fs.Inner.FlowsheetOptions.FlowsheetBackgroundColor2, Is.Empty);
            surface.Zoom = 1.0f;
            Assert.That(CornerAfterDrawing(surface), Is.EqualTo(SKColors.White));
        }

        private static int Distance(SKColor a, SKColor b) =>
            System.Math.Max(System.Math.Abs(a.Red - b.Red), System.Math.Max(System.Math.Abs(a.Green - b.Green), System.Math.Abs(a.Blue - b.Blue)));

        [Test]
        public void WithoutAColorTheDrawingFollowsTheTheme()
        {
            var (fs, surface) = Build();
            Assert.That(fs.Inner.FlowsheetOptions.FlowsheetBackgroundColor, Is.Empty);

            Assert.That(CornerAfterDrawing(surface), Is.EqualTo(SKColors.White));
            Assert.That(GraphicsSurface.DarkCanvas, Is.False);
            Assert.That(GraphicsSurface.ForegroundColor, Is.EqualTo(SKColors.Black));
        }

        [Test]
        public void ADarkColorTurnsTheLinesLight()
        {
            var (fs, surface) = Build();
            var navy = new SKColor(0x16, 0x22, 0x3A);
            FlowsheetBackground.SetColor(fs.Inner.FlowsheetOptions, navy);
            Assert.That(fs.Inner.FlowsheetOptions.FlowsheetBackgroundColor, Is.EqualTo("#16223A"));

            Assert.That(CornerAfterDrawing(surface), Is.EqualTo(navy));
            Assert.That(GraphicsSurface.DarkCanvas, Is.True, "lines and text follow the dark background");
            Assert.That(GraphicsSurface.ForegroundColor, Is.EqualTo(GraphicsSurface.DarkForegroundColor));

            // a light color brings the dark lines back, and clearing it returns to the theme
            var paper = new SKColor(0xFA, 0xF6, 0xEC);
            FlowsheetBackground.SetColor(fs.Inner.FlowsheetOptions, paper);
            Assert.That(CornerAfterDrawing(surface), Is.EqualTo(paper));
            Assert.That(GraphicsSurface.DarkCanvas, Is.False);

            FlowsheetBackground.SetColor(fs.Inner.FlowsheetOptions, null);
            Assert.That(CornerAfterDrawing(surface), Is.EqualTo(SKColors.White));
        }

        [Test]
        public void TheColorIsSavedWithTheFlowsheet()
        {
            var options = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables();
            options.FlowsheetBackgroundColor = "#1E1E1E";
            options.FlowsheetBackgroundColor2 = "#16223A";
            var data = options.SaveData();

            var loaded = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables();
            loaded.LoadData(data);
            Assert.That(loaded.FlowsheetBackgroundColor, Is.EqualTo("#1E1E1E"));
            Assert.That(loaded.FlowsheetBackgroundColor2, Is.EqualTo("#16223A"));

            // a file saved before the option existed follows the theme
            var old = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables();
            old.LoadData(data.Where(e => !e.Name.LocalName.StartsWith("FlowsheetBackgroundColor")).ToList());
            Assert.That(old.FlowsheetBackgroundColor, Is.Empty);
            Assert.That(old.FlowsheetBackgroundColor2, Is.Empty);
        }

        [Test]
        public void TheContrastSwitchesWhereBlackAndWhiteReadEqually()
        {
            Assert.That(FlowsheetBackground.IsDark(SKColors.White), Is.False);
            Assert.That(FlowsheetBackground.IsDark(new SKColor(0xF0, 0xF0, 0xF0)), Is.False);
            Assert.That(FlowsheetBackground.IsDark(new SKColor(0x1E, 0x1E, 0x1E)), Is.True);
            Assert.That(FlowsheetBackground.IsDark(new SKColor(0x80, 0x80, 0x80)), Is.False, "mid grey reads better with black");
            Assert.That(FlowsheetBackground.IsDark(new SKColor(0x00, 0x00, 0xC0)), Is.True, "saturated blue reads better with white");
        }
    }
}
