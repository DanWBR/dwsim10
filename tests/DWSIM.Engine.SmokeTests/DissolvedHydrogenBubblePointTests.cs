//    The bubble point of a CO2-rich liquid holding a little dissolved hydrogen.
//
//    Hydrogen is more soluble in such a liquid hot than cold, so at one pressure the bubble-point
//    equation has two roots: with Peng-Robinson, 0.93 CO2 / 0.02 hydrogen / 0.05 methane at 60 bar
//    boils at 271.17 K and again at 191.07 K, the second below CO2's triple point (216.58 K). The
//    Nested Loops temperature loop converges on the root next to the caller's estimate. Flash_PV then
//    read the loop's last temperature step as the result's validity: a loop that lands on the root
//    in one or two steps, as the Nested Loops v2 loop does, reports a step of a kelvin or more, and
//    the converged point went to the fallbacks, which start from the ideal-solution seed and from
//    1 atm and ended on the other root. A distillation column hands every stage its current
//    temperature as the estimate; each stage came back 80 K away or failed.

using System;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DissolvedHydrogenBubblePointTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.Thermodynamics.PropertyPackages.PropertyPackage CarbonDioxideHydrogenMethane(bool nestedLoopsV2)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Carbon dioxide");
            fs.AddCompound("Hydrogen");
            fs.AddCompound("Methane");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            pp.FlashSettings[DWSIM.Interfaces.Enums.FlashSetting.NestedLoops_v2] = nestedLoopsV2 ? "True" : "False";

            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "liquid");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;

            return pp;
        }

        private static double BubblePoint(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp,
                                          double[] x, double P, double Tstart)
        {
            var flash = (DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm)
                pp.FlashBase.Clone();

            var r = (object[])flash.Flash_PV(x, P, 0.0, Tstart, pp);

            return Convert.ToDouble(r[4]);
        }

        /// <summary>
        /// Nested Loops v2, the estimate within ten kelvin of the root: the bubble point is the root
        /// next to it, 271.17 K, from every start. Before, every start but the root itself gave 191.07 K.
        /// </summary>
        [TestCase(-10.0)]
        [TestCase(-5.0)]
        [TestCase(-1.0)]
        [TestCase(1.0)]
        [TestCase(5.0)]
        public void NestedLoopsV2KeepsTheBubblePointNextToTheEstimate(double offset)
        {
            var pp = CarbonDioxideHydrogenMethane(true);
            double[] x = { 0.93, 0.02, 0.05 };

            var T = BubblePoint(pp, x, 60e5, 271.17 + offset);

            TestContext.WriteLine("start {0:F2} K -> bubble point {1:F4} K", 271.17 + offset, T);
            Assert.That(T, Is.EqualTo(271.17).Within(0.05));
        }

        /// <summary>
        /// Nested Loops (v1), whose loop takes at least six steps, from forty kelvin under the root at
        /// 80 bar (0.90 CO2 / 0.03 hydrogen / 0.07 methane, roots 286.16 and 197.54 K): its sixth step is
        /// still 0.15 K, and the converged point was retried from the ideal-solution seed, which found the
        /// lower root.
        /// </summary>
        [Test]
        public void NestedLoopsKeepsTheBubblePointItConvergedOn()
        {
            var pp = CarbonDioxideHydrogenMethane(false);
            double[] x = { 0.90, 0.03, 0.07 };

            var T = BubblePoint(pp, x, 80e5, 246.16);

            TestContext.WriteLine("start 246.16 K -> bubble point {0:F4} K", T);
            Assert.That(T, Is.EqualTo(286.16).Within(0.05));
        }

        /// <summary>
        /// From 5 K over the 80 bar root both loops climb onto the trivial solution (K within 0.02 of 1 at
        /// 304.18 K), where the bubble-point equation holds at any temperature. That result is not a converged
        /// bubble point and still takes the fallbacks: whatever Flash_PV returns has to satisfy the equation on
        /// the package's own fugacity coefficients.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TheTrivialSolutionIsNotTakenForAConvergedBubblePoint(bool nestedLoopsV2)
        {
            var pp = CarbonDioxideHydrogenMethane(nestedLoopsV2);
            double[] x = { 0.90, 0.03, 0.07 };
            const double P = 80e5;

            var flash = (DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm)
                pp.FlashBase.Clone();
            var r = (object[])flash.Flash_PV(x, P, 0.0, 291.16, pp, false, null);

            var T = Convert.ToDouble(r[4]);
            var y = (double[])r[3];
            var phil = pp.DW_CalcFugCoeff(x, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
            var phiv = pp.DW_CalcFugCoeff(y, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Vapor);
            double s = 0.0;
            for (int i = 0; i < x.Length; i++) s += x[i] * phil[i] / phiv[i];

            TestContext.WriteLine("start 291.16 K -> {0:F4} K, sum K x = {1:F6}", T, s);
            Assert.That(s, Is.EqualTo(1.0).Within(1e-3));
        }

        /// <summary>
        /// From 298.16 K at 80 bar the first attempt ends on the trivial solution with a step under 0.01 K. The
        /// retry from T = 0 finds the lower root, 197.54 K, a bubble point of the model, but the trivial flag
        /// stayed set and the pressure stepping, which fails at 1 atm, replaced it: Nested Loops v2 answered
        /// with its Raoult's law fallback (104.76 K, sum K x = 8.5 on the package's fugacity coefficients), and
        /// Nested Loops (v1) failed, after which the package's fail-safe answered the same.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ARetryThatFindsABubblePointOfTheModelIsKept(bool nestedLoopsV2)
        {
            var pp = CarbonDioxideHydrogenMethane(nestedLoopsV2);
            double[] x = { 0.90, 0.03, 0.07 };
            const double P = 80e5;

            var flash = (DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm)
                pp.FlashBase.Clone();
            var r = (object[])flash.Flash_PV(x, P, 0.0, 298.16, pp, false, null);

            var T = Convert.ToDouble(r[4]);
            var y = (double[])r[3];
            var phil = pp.DW_CalcFugCoeff(x, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
            var phiv = pp.DW_CalcFugCoeff(y, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Vapor);
            double s = 0.0;
            for (int i = 0; i < x.Length; i++) s += x[i] * phil[i] / phiv[i];

            TestContext.WriteLine("start 298.16 K -> {0:F4} K, sum K x = {1:F6}", T, s);
            Assert.That(s, Is.EqualTo(1.0).Within(1e-3));
            Assert.That(T, Is.EqualTo(197.54).Within(0.05));
        }

        /// <summary>
        /// 0.96 CO2 / 0.04 methane at 70 bar, no estimate: the rigorous bubble point fails, and Raoult's law
        /// answers 199.75 K, where sum K x on the package's fugacity coefficients is 0.375. Nested Loops (v1)
        /// rejects that answer; Nested Loops v2 returned it from its own ideal fallback. With the package's
        /// fail-safe off, both now fail, so a caller's Catch can take over.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AnUnconfirmedIdealBubblePointIsRejected(bool nestedLoopsV2)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Carbon dioxide");
            fs.AddCompound("Methane");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            pp.FlashSettings[DWSIM.Interfaces.Enums.FlashSetting.NestedLoops_v2] = nestedLoopsV2 ? "True" : "False";
            pp.FlashSettings[DWSIM.Interfaces.Enums.FlashSetting.FailSafeCalculationMode] = "3";

            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "liquid");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;

            var flash = (DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm)
                pp.FlashBase.Clone();

            Assert.That(() => flash.Flash_PV(new[] { 0.96, 0.04 }, 70e5, 0.0, 0.0, pp, false, null), Throws.Exception);
        }
    }
}
