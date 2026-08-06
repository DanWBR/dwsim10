using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Water absorbing ammonia out of a nitrogen stream: liquid in at the top, gas in
    /// at the bottom, ten stages, NRTL. Needs no licence and no external file.
    /// <para>
    /// Guards two defects that made every API-built absorber unsolvable. The top
    /// product used to be connected as a Distillate, which is a liquid drawn off stage
    /// 0 at rate LSS(0) - zero here - so the closing mass balance never subtracted the
    /// overhead and threw a 0.64 % error on water even though the converged profiles
    /// were exact. And firstF, which seeds the internal liquid traffic, used to be
    /// read from the order the feeds sat in rather than from their stage positions, so
    /// connecting the gas first started L off at the gas rate. The column is therefore
    /// built twice, once in each connection order, and the two must agree.
    /// </para>
    /// </summary>
    internal static class AbsorberTest
    {
        private static int _passed, _failed;

        public static void Run()
        {
            _passed = 0;
            _failed = 0;

            var a = Solve(gasConnectedFirst: false);
            var b = Solve(gasConnectedFirst: true);

            Check("converges with the liquid feed connected first", a.ok, a.detail);
            Check("converges with the gas feed connected first", b.ok, b.detail);

            if (a.ok && b.ok)
            {
                Check("connection order does not change the answer",
                    Math.Abs(a.ammoniaOut - b.ammoniaOut) < 1e-10,
                    $"y(NH3) = {a.ammoniaOut:E6} vs {b.ammoniaOut:E6}");

                // Absorption has to actually happen, and cannot exceed what came in.
                Check("ammonia is absorbed", a.ammoniaOut < 0.05 && a.ammoniaOut > 0.0,
                    $"y(NH3) = {a.ammoniaOut:E4} against 0.05 in the feed gas");
            }

            foreach (var method in new[]
            {
                "Wang-Henke (Bubble Point)",
                "Burningham-Otto (Sum Rates)",
                "Simultaneous Correction",
                "Newton-Raphson (Naphtali-Sandholm)",
            })
            {
                var r = Solve(false, method);
                Check($"converges with {method}", r.ok, r.detail);
            }

            Console.WriteLine();
            Console.WriteLine($"  Absorber results: {_passed} passed, {_failed} failed");
            if (_failed > 0) throw new Exception($"{_failed} absorber test(s) failed.");
        }

        private static (bool ok, double ammoniaOut, string detail) Solve(
            bool gasConnectedFirst, string method = null)
        {
            var fs = Flowsheet.Create("FluentAbsorberTest")
                .WithCompounds("Water", "Ammonia", "Nitrogen")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var water = fs.AddMaterialStream("lean water")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(100.MolPerSecond());
            water.WithComposition(c =>
            {
                c.Mole("Water", 1.0); c.Mole("Ammonia", 0.0); c.Mole("Nitrogen", 0.0);
            });

            var gas = fs.AddMaterialStream("sour gas")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(20.MolPerSecond());
            gas.WithComposition(c =>
            {
                c.Mole("Water", 0.0); c.Mole("Ammonia", 0.05); c.Mole("Nitrogen", 0.95);
            });

            var top = fs.AddMaterialStream("clean gas");
            var bottoms = fs.AddMaterialStream("rich water");

            var col = fs.AddAbsorptionColumn("ABS-1")
                .WithNumberOfStages(10)
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal());

            if (gasConnectedFirst)
            {
                col.Object.ConnectFeed(gas.Object, 9);
                col.Object.ConnectFeed(water.Object, 0);
            }
            else
            {
                col.Object.ConnectFeed(water.Object, 0);
                col.Object.ConnectFeed(gas.Object, 9);
            }
            col.WithTopProduct(top).WithBottoms(bottoms);

            col.Object.MaxIterations = 200;
            if (method != null) col.Object.SolvingMethodName = method;

            var errors = fs.TrySolve();

            if (!col.Object.Calculated)
                return (false, 0.0, errors.Count > 0 ? errors[0].Message : col.Object.ErrorMessage ?? "?");

            double y = top.OverallMoleFraction("Ammonia");
            return (true, y, $"y(NH3) = {y:E4}, Ttop = {col.Object.Tf[0]:F1} K");
        }

        private static void Check(string name, bool condition, string detail)
        {
            if (condition)
            {
                Console.WriteLine($"    [PASS] {name}");
                _passed++;
            }
            else
            {
                Console.WriteLine($"    [FAIL] {name} - {detail}");
                _failed++;
            }
        }
    }
}
