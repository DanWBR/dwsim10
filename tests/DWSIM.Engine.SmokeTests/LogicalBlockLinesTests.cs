//    The dashed lines of the logical blocks (Spec, Adjust, Information Carrier, controllers) run to the objects
//    they link, in the flowsheet's coordinates (https://github.com/DanWBR/dwsim10/issues/116). The surface draws each
//    object on a canvas flipped, rotated and zoomed about the object's center; the lines are drawn after undoing that.

using DWSIM.Automation.FluentAPI;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes;
using DWSIM.GlobalSettings;
using NUnit.Framework;
using SkiaSharp;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class LogicalBlockLinesTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
        }

        // the surface's transform of an object (DesignSurface), then the object's own undoing: the canvas is back where
        // the flowsheet's coordinates are, whatever the flip, the rotation and the object zoom
        [Test]
        public void UndoingAnObjectsTransformRestoresTheFlowsheetsCoordinates(
            [Values(false, true)] bool flippedH, [Values(false, true)] bool flippedV,
            [Values(0, 90, 180, 37)] int rotation, [Values(1.0, 1.5)] double zoom)
        {
            var fs = Flowsheet.Create("LogicalBlockLines");
            fs.Inner.FlowsheetOptions.CurrentFlowsheetObjectZoomLevel = zoom;
            var spec = new SpecGraphic(300, 200, 20, 20) { FlippedH = flippedH, FlippedV = flippedV, Rotation = rotation, Flowsheet = fs.Inner };

            using var bitmap = new SKBitmap(100, 100);
            using var canvas = new SKCanvas(bitmap);
            canvas.Translate(15, -40);
            canvas.Scale(0.83f);
            var m0 = canvas.TotalMatrix;

            float cx = spec.X + spec.Width / 2f, cy = spec.Y + spec.Height / 2f;
            if (flippedV && !flippedH) canvas.Scale(1, -1, cx, cy);
            else if (flippedH && !flippedV) canvas.Scale(-1, 1, cx, cy);
            else if (flippedH && flippedV) canvas.Scale(-1, -1, cx, cy);
            if (rotation != 0) canvas.RotateDegrees(rotation, cx, cy);
            canvas.Scale((float)zoom, (float)zoom, cx, cy);

            spec.UnTransformCanvas(canvas);
            var m = canvas.TotalMatrix;
            foreach (var (a, b) in new[] { (m.ScaleX, m0.ScaleX), (m.SkewX, m0.SkewX), (m.TransX, m0.TransX),
                                           (m.SkewY, m0.SkewY), (m.ScaleY, m0.ScaleY), (m.TransY, m0.TransY) })
                Assert.That(a, Is.EqualTo(b).Within(1e-3));
        }
    }
}
