//    Position lock, layout lock and the appearance descriptors of the flowsheet drawing.
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Drawing.SkiaSharp.Appearance;
using DWSIM.Drawing.SkiaSharp.GraphicObjects;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using NUnit.Framework;
using SkiaSharp;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A locked object stays where it is when the flowsheet is dragged, aligned, snapped or laid out,
    /// and moves with everything else when the view is panned or zoomed. The lock is saved with the
    /// object and a copy comes out unlocked. The appearance descriptors list the properties each kind
    /// of object has and apply their side effects.
    /// </summary>
    [TestFixture]
    public class FlowsheetLayoutLockTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        private sealed class Case
        {
            public FluentFlowsheet Fs;
            public GraphicsSurface Surface;
            public IGraphicObject Feed, Heater, Product, Mixer, Feed2;
        }

        // FEED -> H-1 -> OUT -> MIX-1 <- FEED2
        private static Case Build()
        {
            var fs = FluentFlowsheet.Create("LockProbe").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("FEED").PositionAt(50, 100);
            var heater = fs.AddHeater("H-1").PositionAt(200, 100).ConnectFeed(feed);
            var product = heater.ConnectNewProduct("OUT").PositionAt(350, 100);
            var feed2 = fs.AddMaterialStream("FEED2").PositionAt(350, 250);
            var mixer = fs.AddMixer("MIX-1").PositionAt(500, 160).ConnectFeed(product).ConnectFeed(feed2, 1);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            surface.Zoom = 1.0f;
            surface.SnapToGrid = false;
            surface.LockLayout = false;
            return new Case
            {
                Fs = fs, Surface = surface,
                Feed = feed.Object.GraphicObject, Heater = heater.Object.GraphicObject,
                Product = product.Object.GraphicObject, Mixer = mixer.Object.GraphicObject,
                Feed2 = feed2.Object.GraphicObject
            };
        }

        private static (float X, float Y) Pos(IGraphicObject g) => (g.X, g.Y);

        private static Dictionary<IGraphicObject, (float X, float Y)> Positions(Case c) =>
            new[] { c.Feed, c.Heater, c.Product, c.Mixer, c.Feed2 }.ToDictionary(g => g, Pos);

        /// <summary>Presses on the heater, adds the feed to the selection and drags by (dx, dy).</summary>
        private static void DragHeaterWithFeed(Case c, int dx, int dy)
        {
            var h = c.Heater;
            int x0 = (int)(h.X + h.Width / 2), y0 = (int)(h.Y + h.Height / 2);
            c.Surface.InputPress(x0, y0);
            Assert.That(c.Surface.SelectedObject, Is.SameAs(h), "the press did not hit the heater");
            c.Surface.SelectedObjects[c.Feed.Name] = c.Feed;
            c.Surface.InputMove(x0 + dx, y0 + dy);
            c.Surface.InputRelease();
        }

        [Test]
        public void DraggingAMixedSelectionMovesOnlyTheUnlockedObjects()
        {
            var c = Build();
            c.Feed.PositionLocked = true;
            var feed0 = Pos(c.Feed);
            var heater0 = Pos(c.Heater);

            DragHeaterWithFeed(c, 30, 20);

            Assert.That(Pos(c.Feed), Is.EqualTo(feed0), "a locked object moved with the drag");
            Assert.That(c.Heater.X, Is.EqualTo(heater0.X + 30).Within(0.01));
            Assert.That(c.Heater.Y, Is.EqualTo(heater0.Y + 20).Within(0.01));
        }

        [Test]
        public void LockLayoutFreezesEveryObjectAndIsStoredInTheOptions()
        {
            var c = Build();
            c.Surface.LockLayout = true;
            Assert.That(c.Fs.Inner.FlowsheetOptions.FlowsheetLockLayout, Is.True, "the surface flag is not kept in the flowsheet options");
            var before = Positions(c);

            DragHeaterWithFeed(c, 40, 40);
            c.Surface.SelectedObjects.Clear();
            foreach (var g in before.Keys) c.Surface.SelectedObjects[g.Name] = g;
            c.Surface.AlignSelectedObjects(GraphicsSurface.AlignDirection.Tops);
            c.Surface.AutoArrange();
            c.Surface.OrthogonalArrange();
            c.Surface.ApplyNaturalLayout(new List<string> { c.Feed.Name, c.Heater.Name, c.Product.Name, c.Feed2.Name, c.Mixer.Name }, 75);
            c.Surface.RestoreLayout();

            foreach (var kv in before)
                Assert.That(Pos(kv.Key), Is.EqualTo(kv.Value), kv.Key.Tag + " moved while the layout was locked");
            Assert.That(c.Feed.PositionLocked, Is.False, "Lock Layout must not change the objects' own lock");
        }

        [Test]
        public void SnapToGridLeavesALockedObjectOffTheGrid()
        {
            var c = Build();
            c.Surface.SnapToGrid = true;
            c.Surface.GridSize = 20;
            c.Feed.X = 53; c.Feed.Y = 107;
            c.Feed.PositionLocked = true;

            DragHeaterWithFeed(c, 7, 3);

            Assert.That(Pos(c.Feed), Is.EqualTo((53f, 107f)), "a locked object was snapped to the grid");
            var hc = c.Heater.X + c.Heater.Width / 2.0;
            Assert.That(Math.Abs(hc / 20.0 - Math.Round(hc / 20.0)), Is.LessThan(0.051), "the unlocked object was not snapped");
        }

        [Test]
        public void AligningLinesTheOthersUpWithTheLockedObject()
        {
            var c = Build();
            c.Feed.PositionLocked = true;
            c.Feed.Y = 300;
            c.Surface.SelectedObjects.Clear();
            c.Surface.SelectedObjects[c.Feed.Name] = c.Feed;
            c.Surface.SelectedObjects[c.Heater.Name] = c.Heater;
            c.Surface.SelectedObject = c.Heater;

            c.Surface.AlignSelectedObjects(GraphicsSurface.AlignDirection.Tops);
            Assert.That(c.Feed.Y, Is.EqualTo(300f), "the locked object moved");
            Assert.That(c.Heater.Y, Is.EqualTo(300f), "the unlocked object did not line up with the locked one");

            var feedX = c.Feed.X;
            c.Surface.AlignSelectedObjects(GraphicsSurface.AlignDirection.Lefts);
            Assert.That(c.Feed.X, Is.EqualTo(feedX));
            Assert.That(c.Heater.X, Is.EqualTo(feedX));
        }

        [TestCase("auto")]
        [TestCase("orthogonal")]
        [TestCase("natural")]
        public void TheAutomaticLayoutsLeaveALockedObjectInPlace(string layout)
        {
            var c = Build();
            c.Heater.PositionLocked = true;
            var heater0 = Pos(c.Heater);
            var others0 = Positions(c).Where(kv => kv.Key != c.Heater).ToList();

            switch (layout)
            {
                case "auto": c.Surface.AutoArrange(); break;
                case "orthogonal": c.Surface.OrthogonalArrange(); break;
                default:
                    c.Surface.ApplyNaturalLayout(new List<string> { c.Feed.Name, c.Heater.Name, c.Product.Name, c.Feed2.Name, c.Mixer.Name }, 75);
                    break;
            }

            Assert.That(Pos(c.Heater), Is.EqualTo(heater0), "the locked object was moved by the " + layout + " layout");
            Assert.That(others0.Any(kv => Pos(kv.Key) != kv.Value), Is.True, "the " + layout + " layout moved nothing at all");

            // undoing the layout puts the others back and still leaves the locked object alone
            c.Surface.RestoreLayout();
            Assert.That(Pos(c.Heater), Is.EqualTo(heater0));
            foreach (var kv in others0)
            {
                Assert.That(kv.Key.X, Is.EqualTo(kv.Value.X).Within(1.0), kv.Key.Tag + " X after Restore Layout");
                Assert.That(kv.Key.Y, Is.EqualTo(kv.Value.Y).Within(1.0), kv.Key.Tag + " Y after Restore Layout");
            }
        }

        [Test]
        public void PanningZoomingAndCenteringStillMoveLockedObjects()
        {
            var c = Build();
            c.Feed.PositionLocked = true;
            c.Surface.LockLayout = true;

            // pan by dragging the empty canvas
            var feed0 = Pos(c.Feed);
            c.Surface.InputPress(2000, 2000);
            Assert.That(c.Surface.SelectedObject, Is.Null, "the press hit an object instead of the canvas");
            c.Surface.InputMove(2050, 2030);
            c.Surface.InputRelease();
            Assert.That(c.Feed.X, Is.EqualTo(feed0.X + 50).Within(0.01), "panning did not move a locked object");
            Assert.That(c.Feed.Y, Is.EqualTo(feed0.Y + 30).Within(0.01));

            var feed1 = Pos(c.Feed);
            c.Surface.OffsetAll(-10, 15);
            Assert.That(Pos(c.Feed), Is.EqualTo((feed1.X - 10, feed1.Y + 15)), "OffsetAll did not move a locked object");

            // zoom to fit shifts every object by the same amount, locked or not
            var dxFeedHeater = c.Heater.X - c.Feed.X;
            c.Surface.ZoomAll(800, 600);
            c.Surface.Center(800, 600);
            Assert.That(c.Heater.X - c.Feed.X, Is.EqualTo(dxFeedHeater).Within(0.01), "zoom or centre moved the objects apart");
        }

        [Test]
        public void ThePositionLockIsSavedAndACopyComesOutUnlocked()
        {
            var c = Build();
            var go = (GraphicObject)c.Heater;
            go.PositionLocked = true;

            var data = go.SaveData();
            Assert.That(data.Any(e => e.Name.LocalName == "PositionLocked" && e.Value == "true"), Is.True,
                "PositionLocked is not written to the file");
            var loaded = (GraphicObject)Activator.CreateInstance(go.GetType());
            loaded.LoadData(data);
            Assert.That(loaded.PositionLocked, Is.True, "PositionLocked did not survive a save and load");

            Assert.That(go.Clone().PositionLocked, Is.False, "a clone kept the lock");

            // an object saved before the lock existed has no element and loads unlocked
            var old = go.SaveData().Where(e => e.Name.LocalName != "PositionLocked").ToList();
            var fromOld = (GraphicObject)Activator.CreateInstance(go.GetType());
            fromOld.LoadData(old);
            Assert.That(fromOld.PositionLocked, Is.False);
        }

        [Test]
        public void APastedObjectComesOutUnlocked()
        {
            var c = Build();
            var note = new TextGraphic(400, 400, "note") { Name = "NOTE-1", Tag = "NOTE-1", PositionLocked = true };
            var data = new List<System.Xml.Linq.XElement> { new System.Xml.Linq.XElement("GraphicObject", note.SaveData()) };
            var excs = new ConcurrentBag<Exception>();
            var fb = (DWSIM.FlowsheetBase.FlowsheetBase)c.Fs.Inner;

            fb.AddGraphicObjects(data, excs, "P1_", 40, false);

            var pasted = c.Fs.Inner.GraphicObjects.Values.FirstOrDefault(g => g.Name == "P1_NOTE-1");
            Assert.That(pasted, Is.Not.Null, "the pasted note is not on the flowsheet: " + string.Join("; ", excs.Select(e => e.Message)));
            Assert.That(pasted.PositionLocked, Is.False, "a pasted copy kept the lock");
        }

        [Test]
        public void AnOldSampleLoadsWithNothingLocked()
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null);
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", "ExtractiveDistillation.dwxmz"));

            Assert.That(flowsheet.GraphicObjects.Count, Is.GreaterThan(0));
            Assert.That(flowsheet.GraphicObjects.Values.Any(g => g.PositionLocked), Is.False);
            Assert.That(flowsheet.FlowsheetOptions.FlowsheetLockLayout, Is.False);
        }

        // ------------------------------------------------------------------ appearance descriptors

        private static string[] Keys(IGraphicObject g) => AppearanceDescriptors.ForObject(g).Select(d => d.Key).ToArray();

        [Test]
        public void TheDescriptorsListThePropertiesOfEachKindOfObject()
        {
            var c = Build();

            var stream = Keys(c.Feed);
            Assert.That(stream, Is.SupersetOf(new[] { "OverrideColors", "LineColor", "FontSize", "FontStyle", "DrawLabel",
                "Width", "Height", "Rotation", "FlippedH", "FlippedV", "X", "Y", "PositionLocked" }));
            Assert.That(stream, Has.None.AnyOf("FillColor", "Text", "Opacity", "BorderColor", "Size"));

            var text = new TextGraphic(10, 10, "hello");
            Assert.That(text.ObjectType, Is.EqualTo(ObjectType.GO_Text));
            var textKeys = Keys(text);
            Assert.That(textKeys, Is.SupersetOf(new[] { "Text", "Color", "Size", "FontStyle", "X", "Y", "PositionLocked" }));
            Assert.That(textKeys, Has.None.AnyOf("Width", "LineColor", "Rotation", "FontSize"));

            var rect = new RectangleGraphic(new SKPoint(10, 10), "box");
            Assert.That(rect.ObjectType, Is.EqualTo(ObjectType.GO_Rectangle));
            Assert.That(Keys(rect), Is.SupersetOf(new[] { "GradientMode", "FillColor", "GradientColor1", "GradientColor2",
                "Opacity", "LineWidth", "RoundEdges", "Text", "FontColor", "FontSize", "Width", "Height" }));
            Assert.That(Keys(rect), Has.None.AnyOf("Rotation", "LineColor", "OverrideColors"));

            var table = new TableGraphic(10, 10);
            Assert.That(table.ObjectType, Is.EqualTo(ObjectType.GO_Table));
            Assert.That(Keys(table), Is.SupersetOf(new[] { "BorderColor", "TextColor", "Padding", "FontSize", "FontStyle" }));
            Assert.That(Keys(table), Has.None.AnyOf("Width", "LineColor", "Rotation"));

            // every descriptor names a property its objects really have, and the groups come in order
            foreach (var g in new IGraphicObject[] { c.Feed, c.Heater, c.Mixer, text, rect, table })
            {
                var ds = AppearanceDescriptors.ForObject(g);
                Assert.That(ds.Select(d => (int)d.Group), Is.Ordered, g.GetType().Name);
                foreach (var d in ds)
                {
                    Assert.That(d.GetValue(g), Is.Not.Null, g.GetType().Name + "." + d.Key);
                    Assert.That(d.Help, Is.Not.Empty, d.Key);
                    Assert.That(d.DisplayName + d.Help, Does.Not.Contain("\u2014"), "em dash in " + d.Key);
                }
            }
        }

        [Test]
        public void SettingThroughTheDescriptorsAppliesTheSideEffects()
        {
            var c = Build();
            var heater = (ShapeGraphic)c.Heater;
            heater.OverrideColors = false;

            var lineColor = AppearanceDescriptors.Find("LineColor");
            Assert.That(lineColor.SetValue(heater, new SKColor(10, 20, 30)), Is.True);
            Assert.That(heater.LineColor, Is.EqualTo(new SKColor(10, 20, 30)));
            Assert.That(heater.OverrideColors, Is.True, "a line color must turn custom colors on, or the status colors overwrite it");
            heater.UpdateStatus();
            Assert.That(heater.LineColor, Is.EqualTo(new SKColor(10, 20, 30)), "the status colors overwrote the custom line color");

            // a hex string works too
            Assert.That(lineColor.SetValue(heater, "#FF102030"), Is.True);
            Assert.That(heater.LineColor, Is.EqualTo(new SKColor(0x10, 0x20, 0x30)));

            // numbers are clamped and converted to the property's own type
            var rect = new RectangleGraphic(new SKPoint(10, 10), "box");
            AppearanceDescriptors.Find("Opacity").SetValue(rect, 999.0);
            Assert.That(rect.Opacity, Is.EqualTo(255));
            AppearanceDescriptors.Find("FontSize").SetValue(heater, "12,5");
            Assert.That(heater.FontSize, Is.EqualTo(12.5).Within(1e-9));
            AppearanceDescriptors.Find("FontStyle").SetValue(heater, FontStyle.Bold);
            Assert.That(heater.FontStyle, Is.EqualTo(FontStyle.Bold));

            // X and Y are left alone while the object is locked
            heater.PositionLocked = true;
            var x0 = heater.X;
            Assert.That(AppearanceDescriptors.Find("X").SetValue(heater, x0 + 100), Is.False);
            Assert.That(heater.X, Is.EqualTo(x0));
            heater.PositionLocked = false;
            Assert.That(AppearanceDescriptors.Find("X").SetValue(heater, x0 + 100), Is.True);
            Assert.That(heater.X, Is.EqualTo(x0 + 100).Within(0.01));

            // the common value of several objects is blank when they differ
            var fontSize = AppearanceDescriptors.Find("FontSize");
            ((ShapeGraphic)c.Mixer).FontSize = 12.5;
            Assert.That(fontSize.GetCommonValue(new[] { c.Heater, c.Mixer }), Is.EqualTo(12.5));
            ((ShapeGraphic)c.Mixer).FontSize = 9;
            Assert.That(fontSize.GetCommonValue(new[] { c.Heater, c.Mixer }), Is.Null);
        }

        [Test]
        public void ResetToDefaultsKeepsSizePositionTextAndLock()
        {
            var c = Build();
            var heater = (ShapeGraphic)c.Heater;
            var fresh = (ShapeGraphic)Activator.CreateInstance(heater.GetType());
            heater.OverrideColors = true;
            heater.LineColor = SKColors.Red;
            heater.FontSize = 20;
            heater.FlippedH = true;
            heater.PositionLocked = true;
            heater.Width = 77;
            var pos = Pos(heater);

            AppearanceDescriptors.ResetToDefaults(heater);

            Assert.That(heater.OverrideColors, Is.EqualTo(fresh.OverrideColors));
            Assert.That(heater.FontSize, Is.EqualTo(fresh.FontSize));
            Assert.That(heater.FlippedH, Is.EqualTo(fresh.FlippedH));
            Assert.That(heater.PositionLocked, Is.True);
            Assert.That(heater.Width, Is.EqualTo(77));
            Assert.That(Pos(heater), Is.EqualTo(pos));

            var rect = new RectangleGraphic(new SKPoint(10, 10), "keep me") { Opacity = 10 };
            AppearanceDescriptors.ResetToDefaults(rect);
            Assert.That(rect.Text, Is.EqualTo("keep me"));
            Assert.That(rect.Opacity, Is.EqualTo(new RectangleGraphic().Opacity));
        }
    }
}
