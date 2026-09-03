//    Building the phase envelope across a range of compositions.
//
//    A guard on the phase-envelope generator: whatever the composition, the dew curve of a mixture
//    with an analytical critical point (the cubic packages) must reach that critical point, not stop
//    short of it. The dew line is traced by stepping temperature, which cannot cross the
//    cricondentherm, so near the critical point it is finished along its retrograde branch in
//    pressure - and the dew temperature may rise or fall to the critical point depending on the
//    mixture. These sweeps cover both directions.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class PhaseEnvelopeSweepTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.Thermodynamics.PropertyPackages.PropertyPackage PR78(string[] compounds, double[] fractions)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            foreach (var c in compounds) fs.AddCompound(c);

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinson1978PropertyPackage { Flowsheet = fs };

            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            ms.SetOverallComposition(fractions);
            pp.CurrentMaterialStream = ms;

            return pp;
        }

        private static List<double> ToList(object o) => ((IEnumerable)o).Cast<object>().Select(Convert.ToDouble).ToList();

        /// <summary>Bubble/dew point counts and how close the dew curve gets to the mixture critical point.</summary>
        private static (int nb, int nd, double closestDewRel, double tc, double pc) Envelope(
            DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp)
        {
            var cps = pp.DW_CalculateCriticalPoints();
            double tc = 0.0, pc = 0.0;
            if (cps.Count > 0) { tc = cps[0][0]; pc = cps[0][1]; }

            var opts = new DWSIM.Thermodynamics.PropertyPackages.PhaseEnvelopeOptions();
            var raw = (object[])pp.DW_ReturnPhaseEnvelope(opts, null);

            var bubT = ToList(raw[0]);
            var dewT = ToList(raw[5]);
            var dewP = ToList(raw[6]);

            double rel = double.MaxValue;
            if (tc > 0.0 && pc > 0.0)
                for (int j = 0; j < dewT.Count; j++)
                    rel = Math.Min(rel, Math.Max(Math.Abs(dewT[j] - tc) / tc, Math.Abs(dewP[j] - pc) / pc));

            return (bubT.Count, dewT.Count, rel, tc, pc);
        }

        /// <summary>
        /// Methane/ethane across the composition range: the dew curve must reach the critical point.
        /// x(C1) = 0.05 is a normal envelope; x(C1) = 0.95 has its critical point below methane's own
        /// critical temperature; and the methane-rich band (x(C1) ~0.86-0.94) has a cricondentherm far
        /// above the critical temperature, so its dew line only reaches the critical point along its
        /// long retrograde branch. All must close on the critical point.
        /// </summary>
        [Test]
        public void MethaneEthaneDewCurveReachesCriticalPoint(
            [Values(0.05, 0.15, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.88, 0.90, 0.92, 0.94, 0.95)] double xC1)
        {
            var e = Envelope(PR78(new[] { "Methane", "Ethane" }, new[] { xC1, 1.0 - xC1 }));
            TestContext.WriteLine($"x(C1)={xC1:F2}  CP T={e.tc:F2} K P={e.pc / 1e5:F2} bar  bubble={e.nb} dew={e.nd}  closestDewRel={e.closestDewRel:F4}");

            Assert.That(e.nb, Is.GreaterThan(10), "bubble curve too short");
            Assert.That(e.nd, Is.GreaterThan(10), "dew curve too short");
            Assert.That(e.closestDewRel, Is.LessThan(0.05),
                        $"the dew curve stops short of the critical point (closest approach {e.closestDewRel:F4})");
        }

        /// <summary>
        /// Methane/ethane/propane at random compositions, sweeping methane from 0 to 100 percent with the
        /// balance split randomly between ethane and propane (fixed seed for reproducibility). Every
        /// composition must build an envelope without throwing; genuine ternary mixtures (no component
        /// vanishing) must produce a real envelope whose dew curve reaches the critical point. The
        /// near-pure endpoints are tolerated: pure methane is degenerate, and the ethane/propane binary
        /// at zero methane hits a separate critical-point-solver defect that is out of scope here.
        /// </summary>
        [Test]
        public void MethaneEthanePropaneEnvelopesBuildAcrossTheMethaneRange()
        {
            var rng = new Random(20260903);
            var problems = new List<string>();

            for (int step = 0; step <= 10; step++)
            {
                double xC1 = step / 10.0;
                double rest = 1.0 - xC1;
                double split = rng.NextDouble();
                double xC2 = rest * split, xC3 = rest * (1.0 - split);
                double sum = xC1 + xC2 + xC3;
                var fr = new[] { xC1 / sum, xC2 / sum, xC3 / sum };
                double minFrac = fr.Min();

                // A near-pure endpoint (a component vanishing) is a degenerate case - a single-component
                // vapour-pressure line, not a mixture envelope - so it is only required not to bring the
                // generator down. A genuine ternary mixture must build a real envelope.
                bool genuineMixture = minFrac > 0.02;

                try
                {
                    var e = Envelope(PR78(new[] { "Methane", "Ethane", "Propane" }, fr));
                    TestContext.WriteLine($"x=[{fr[0]:F3}, {fr[1]:F3}, {fr[2]:F3}]  CP T={e.tc:F1} K P={e.pc / 1e5:F1} bar  bubble={e.nb} dew={e.nd}  closestDewRel={e.closestDewRel:F4}");

                    if (genuineMixture)
                    {
                        if (e.nb < 10) problems.Add($"x(C1)={fr[0]:F3}: bubble curve too short ({e.nb})");
                        if (e.nd < 10) problems.Add($"x(C1)={fr[0]:F3}: dew curve too short ({e.nd})");
                        if (e.tc > 0.0 && e.closestDewRel >= 0.05)
                            problems.Add($"x(C1)={fr[0]:F3}: dew curve stops short of the critical point (closest {e.closestDewRel:F4})");
                    }
                }
                catch (Exception ex) when (genuineMixture)
                {
                    problems.Add($"x(C1)={fr[0]:F3}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"x=[{fr[0]:F3}, {fr[1]:F3}, {fr[2]:F3}] (near-pure, tolerated): {ex.Message}");
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        private static (double tc, double pc) Cp(string[] compounds, double[] fractions)
        {
            var cps = PR78(compounds, fractions).DW_CalculateCriticalPoints();
            return cps.Count > 0 ? (cps[0][0], cps[0][1]) : (0.0, 0.0);
        }

        /// <summary>
        /// A component present at exactly zero mole fraction must not corrupt the mixture critical
        /// point (its terms in the critical Hessian are otherwise infinite, and the solver returned a
        /// garbage point such as 695 K / -6393 bar). The critical point of a ternary with one
        /// component at zero must match that of the binary of the components actually present.
        /// </summary>
        [Test]
        public void CriticalPointIsRobustToAZeroFractionComponent()
        {
            var binaryC2C3 = Cp(new[] { "Ethane", "Propane" }, new[] { 0.342, 0.658 });
            var ternaryNoC1 = Cp(new[] { "Methane", "Ethane", "Propane" }, new[] { 0.0, 0.342, 0.658 });
            TestContext.WriteLine($"C2/C3 = {binaryC2C3.tc:F2} K / {binaryC2C3.pc / 1e5:F2} bar   ternary(0 C1) = {ternaryNoC1.tc:F2} K / {ternaryNoC1.pc / 1e5:F2} bar");
            Assert.That(ternaryNoC1.tc, Is.EqualTo(binaryC2C3.tc).Within(1.0), "zero-methane ternary Tc differs from the ethane/propane binary");
            Assert.That(ternaryNoC1.pc, Is.EqualTo(binaryC2C3.pc).Within(0.5e5), "zero-methane ternary Pc differs from the ethane/propane binary");

            var binaryC1C2 = Cp(new[] { "Methane", "Ethane" }, new[] { 0.5, 0.5 });
            var ternaryNoC3 = Cp(new[] { "Methane", "Ethane", "Propane" }, new[] { 0.5, 0.5, 0.0 });
            TestContext.WriteLine($"C1/C2 = {binaryC1C2.tc:F2} K / {binaryC1C2.pc / 1e5:F2} bar   ternary(0 C3) = {ternaryNoC3.tc:F2} K / {ternaryNoC3.pc / 1e5:F2} bar");
            Assert.That(ternaryNoC3.tc, Is.EqualTo(binaryC1C2.tc).Within(1.0), "zero-propane ternary Tc differs from the methane/ethane binary");
            Assert.That(ternaryNoC3.pc, Is.EqualTo(binaryC1C2.pc).Within(0.5e5), "zero-propane ternary Pc differs from the methane/ethane binary");
        }
    }
}
