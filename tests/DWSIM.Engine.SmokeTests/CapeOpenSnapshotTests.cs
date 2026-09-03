//    CAPE-OPEN unit operations across a full snapshot restore (what the sensitivity analysis does).
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
using System.IO;
using System.Linq;
using System.Threading;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    // Needs the ChemSep CAPE-OPEN unit operation registered on the machine, so this is an
    // explicit test: dotnet test --filter FullyQualifiedName~CapeOpenSnapshotTests
    [TestFixture]
    [Explicit("requires the ChemSep CAPE-OPEN unit operation")]
    public class CapeOpenSnapshotTests
    {
        private const string ChemSepProgId = "ChemSepUO.ChemSep_UnitOperation.1";

        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.DynamicRunner.Flowsheet Load(string filename)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));

            return flowsheet;
        }

        // A CO2 purification column modelled with the ChemSep CAPE-OPEN unit operation. The
        // sensitivity analysis solves the flowsheet repeatedly and then restores a full snapshot;
        // rebuilding the CAPE-OPEN object from its persisted data on every restore, with the old
        // COM instance left to the finalizer, crashed the process on the next solve or garbage
        // collection. The restore now keeps the live instance.
        [Test]
        public void AChemSepColumnSurvivesRepeatedFullSnapshotRestores()
        {
            if (Type.GetTypeFromProgID(ChemSepProgId) == null)
            {
                Assert.Ignore("ChemSep is not registered on this machine");
            }

            // The scenario runs on its own STA thread that is never allowed to finish: ChemSep
            // brings the process down when the apartment that created it is uninitialized after
            // a calculation, and that would take the test result with it.
            Exception failure = null;
            var done = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                try
                {
                    Scenario();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    done.Set();
                }
                Thread.Sleep(Timeout.Infinite);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            Assert.That(done.Wait(TimeSpan.FromMinutes(5)), Is.True, "the scenario did not finish in time");
            if (failure != null) throw failure;
        }

        private static void Scenario()
        {
            var flowsheet = Load("ChemSepColumnSnapshot.dwxmz");
            var column = flowsheet.SimulationObjects.Values.OfType<CapeOpenUO>().Single();

            var errors = flowsheet.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));

            var state0 = flowsheet.GetSnapshot(SnapshotType.All);

            for (int k = 1; k <= 3; k++)
            {
                errors = flowsheet.SolveFlowsheet2();
                Assert.That(errors, Is.Empty, "run " + k + ": " + string.Join("; ", errors.Select(e => e.Message)));

                flowsheet.RestoreSnapshot(state0, SnapshotType.All);

                Assert.That(flowsheet.SimulationObjects.Values.OfType<CapeOpenUO>().Single(), Is.SameAs(column),
                            "the restore should keep the live CAPE-OPEN instance");

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            errors = flowsheet.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, "after the restores: " + string.Join("; ", errors.Select(e => e.Message)));
        }
    }
}
