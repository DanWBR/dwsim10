using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A feed defined compound by compound, some by mass flow and some by molar flow, carries every
    /// compound at the flow it was given, whichever order the calls come in.
    /// </summary>
    internal static class CompoundFlowsTest
    {
        public static void Run()
        {
            Check(("Methane", 1.0, true), ("Ethane", 10.0, false), ("Propane", 0.5, true));
            Check(("Ethane", 10.0, false), ("Methane", 1.0, true), ("Propane", 11.0, false));
        }

        private static void Check(params (string Name, double Flow, bool Mass)[] spec)
        {
            var order = string.Join(", ", spec.Select(s => s.Mass ? "mass" : "molar"));

            var fs = Flowsheet.Create("CompoundFlows")
                .WithCompounds("Methane", "Ethane", "Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed").At(300.Kelvin(), 1e6.Pascal());
            foreach (var s in spec)
            {
                if (s.Mass) feed.SetCompoundMassFlow(s.Name, s.Flow);
                else feed.SetCompoundMolarFlow(s.Name, s.Flow);
            }

            var mw = feed.Object.Phases[0].Compounds.Values.ToDictionary(c => c.Name, c => c.ConstantProperties.Molar_Weight);
            var expected = spec.Select(s => (s.Name, Kg: s.Mass ? s.Flow : s.Flow * mw[s.Name] / 1000.0)).ToArray();

            void Compare(string when)
            {
                var errors = new List<string>();
                foreach (var (name, kg) in expected)
                {
                    var c = feed.Object.Phases[0].Compounds[name];
                    var mass = c.MassFlow.GetValueOrDefault();
                    var mol = c.MolarFlow.GetValueOrDefault();
                    var molExpected = kg / mw[name] * 1000.0;
                    Console.WriteLine($"{order}, {when}: {name} {mass:G8} kg/s, {mol:G8} mol/s");
                    if (!(Math.Abs(mass - kg) <= 1e-9 * Math.Max(1.0, kg)))
                        errors.Add($"{name} mass flow {mass} kg/s, expected {kg}");
                    if (!(Math.Abs(mol - molExpected) <= 1e-9 * Math.Max(1.0, molExpected)))
                        errors.Add($"{name} molar flow {mol} mol/s, expected {molExpected}");
                }
                double total = expected.Sum(e => e.Kg);
                if (!(Math.Abs(feed.MassFlowKgPerSecond - total) <= 1e-9 * total))
                    errors.Add($"total mass flow {feed.MassFlowKgPerSecond} kg/s, expected {total}");
                if (errors.Count > 0)
                    throw new Exception($"{order}, {when}: " + string.Join("; ", errors));
            }

            Compare("before solving");
            fs.Solve();
            Compare("after solving");
        }
    }
}
