using System;
using System.IO;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ExtractorInitializationTests
    {
        private static DWSIM.DynamicRunner.Flowsheet Load()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            DWSIM.FlowsheetBase.FlowsheetBase.AddPropPacks();
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null);
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", "LiquidLiquidExtraction.dwxmz"));
            return flowsheet;
        }

        // Disabling one saved estimate enters trial generation. The sample has valid trials,
        // but one later SimpleLLE flash fails; that numerical trial must not abort initialization.
        [TestCase("liquid")]
        [TestCase("second-liquid")]
        [TestCase("composition")]
        public void AFailedTrialDoesNotDiscardTheExtractorsInitialEstimates(string disabledEstimate)
        {
            var fs = Load();
            var column = fs.SimulationObjects.Values.OfType<AbsorptionColumn>().Single();
            if (disabledEstimate == "liquid") column.UseLiquidFlowEstimates = false;
            if (disabledEstimate == "second-liquid") column.UseVaporFlowEstimates = false;
            if (disabledEstimate == "composition") column.UseCompositionEstimates = false;
            var input = column.GetSolverInputData();
            Assert.That(input.L1trials, Is.Not.Empty);
            Assert.That(input.L2trials.Count, Is.EqualTo(input.L1trials.Count));
            Assert.That(input.x1trials.Count, Is.EqualTo(input.L1trials.Count));
            Assert.That(input.x2trials.Count, Is.EqualTo(input.L1trials.Count));
            for (int trial = 0; trial < input.L1trials.Count; ++trial)
                for (int stage = 0; stage < column.Stages.Count; ++stage)
                {
                    foreach (double flow in new[] { input.L1trials[trial][stage], input.L2trials[trial][stage] })
                        Assert.That(double.IsFinite(flow) && flow > 0, Is.True);
                    foreach (var comp in new[] { input.x1trials[trial][stage], input.x2trials[trial][stage] })
                    {
                        Assert.That(comp.All(x => double.IsFinite(x) && x >= 0), Is.True);
                        Assert.That(comp.Sum(), Is.EqualTo(1.0).Within(1e-6));
                    }
                }
        }

        [Test]
        public void CancellationOfATrialIsNotSwallowed()
        {
            var fs = Load();
            var column = fs.SimulationObjects.Values.OfType<AbsorptionColumn>().Single();
            var pp = new CancelTrialPackage();
            pp.LoadData(((PropertyPackage)column.PropertyPackage).SaveData());
            fs.PropertyPackages[pp.UniqueID] = pp;
            column.PropertyPackage = pp;
            column.UseLiquidFlowEstimates = false;
            var error = Assert.Throws<OperationCanceledException>(() => column.GetSolverInputData());
            Assert.That(error.Message, Is.EqualTo("cancel the first trial"));
            Assert.That(pp.Flash.Calls, Is.EqualTo(2), "feed flash succeeded, first trial was cancelled");
        }

        private class CancelTrialPackage : NRTLPropertyPackage
        {
            public readonly CancelTrialFlash Flash = new CancelTrialFlash();
            public override FlashAlgorithm FlashBase => Flash;
        }

        private class CancelTrialFlash : UniversalFlash
        {
            public int Calls;
            public override object Flash_PT(double[] z, double p, double t, PropertyPackage pp,
                bool reuseKI = false, double[] prevKi = null)
            {
                if (++Calls >= 2) throw new OperationCanceledException("cancel the first trial");
                return base.Flash_PT(z, p, t, pp, reuseKI, prevKi);
            }
        }
    }
}
