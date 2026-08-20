using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Natural layout on a mixer -> heater -> splitter -> recycle loop. The forward chain must run
    /// left to right and upright, and the recycle's return path must drop onto a row below, mirrored
    /// horizontally and running right to left, so the loop closes as a rectangle. Guards the
    /// flow-orientation pass and the rectangular recycle layout; needs no licence, no solve.
    /// </summary>
    internal static class RecycleLayoutTest
    {
        private static int _passed, _failed;

        public static void Run()
        {
            _passed = 0;
            _failed = 0;

            var fs = Flowsheet.Create("RecycleLayout")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed    = fs.AddMaterialStream("feed").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(100.KgPerSecond());
            var sMix    = fs.AddMaterialStream("s-mix");
            var sHot    = fs.AddMaterialStream("s-hot");
            var sProd   = fs.AddMaterialStream("product");
            var sRec    = fs.AddMaterialStream("s-rec");
            var sRecOut = fs.AddMaterialStream("s-rec-out");

            var mix   = fs.AddMixer("MIX-1").ConnectFeed(feed, 0).ConnectFeed(sRecOut, 1).ConnectProduct(sMix, 0);
            var heat  = fs.AddHeater("HEAT-1").ConnectFeed(sMix, 0).ConnectProduct(sHot, 0);
            var split = fs.AddSplitter("SPLIT-1").ConnectFeed(sHot, 0).ConnectProduct(sProd, 0).ConnectProduct(sRec, 1);
            var rec   = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1").ConnectFeed(sRec, 0).ConnectProduct(sRecOut, 0);

            fs.NaturalLayout();

            var gFeed    = feed.Object.GraphicObject;
            var gMix     = mix.Object.GraphicObject;
            var gSplit   = split.Object.GraphicObject;
            var gRec     = rec.Object.GraphicObject;
            var gSRec    = sRec.Object.GraphicObject;
            var gSRecOut = sRecOut.Object.GraphicObject;

            // the forward chain runs left to right, upright
            Check("forward chain is left to right",
                gFeed.X < gMix.X && gMix.X < gSplit.X,
                $"feed={gFeed.X}, mix={gMix.X}, split={gSplit.X}");
            Check("forward objects are not mirrored",
                !gMix.FlippedH && !gSplit.FlippedH,
                $"mix.FlipH={gMix.FlippedH}, split.FlipH={gSplit.FlippedH}");

            // the recycle return sits on a row below the main chain
            var chainY = gMix.Y;
            Check("recycle return is on a lower row",
                gRec.Y > chainY + gMix.Height && gSRec.Y > chainY && gSRecOut.Y > chainY,
                $"chainY={chainY}, rec={gRec.Y}, sRec={gSRec.Y}, sRecOut={gSRecOut.Y}");

            // and it is mirrored horizontally, running right to left back to the mixer
            Check("recycle return is mirrored horizontally",
                gRec.FlippedH && gSRec.FlippedH && gSRecOut.FlippedH,
                $"rec={gRec.FlippedH}, sRec={gSRec.FlippedH}, sRecOut={gSRecOut.FlippedH}");
            Check("recycle return runs right to left",
                gSRec.X > gRec.X && gRec.X > gSRecOut.X,
                $"sRec={gSRec.X}, rec={gRec.X}, sRecOut={gSRecOut.X}");

            Console.WriteLine($"  Recycle layout results: {_passed} passed, {_failed} failed");
            if (_failed > 0) throw new Exception($"{_failed} recycle-layout check(s) failed.");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _passed++; Console.WriteLine($"  PASS {name}"); }
            else { _failed++; Console.WriteLine($"  FAIL {name}: {detail}"); }
        }
    }
}
