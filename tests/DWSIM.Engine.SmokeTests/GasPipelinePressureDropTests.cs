//    The single-phase gas pipeline equations (Weymouth, Panhandle A/B) sanity-checked against the
//    engine's existing Darcy-Weisbach gas pressure drop for a natural-gas transmission line. They are
//    different correlations by design, so the check is order-of-magnitude agreement plus the known
//    conservatism ordering (Weymouth gives the largest drop), not equality.

using System;
using DWSIM.UnitOperations.FlowPackages;
using NUnit.Framework;
using FlowPackage = DWSIM.UnitOperations.UnitOperations.FlowPackage;
using Pipe = DWSIM.UnitOperations.UnitOperations.Pipe;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class GasPipelinePressureDropTests
    {
        // A 20-inch, 10 km line carrying ~2 MMSCMD of ~methane at 50 bar, 15 C.
        const double MW = 17.0;        // kg/kmol
        const double MWair = 28.9625;  // kg/kmol
        const double Pb = 101325.0;    // Pa (base)
        const double Tb = 288.15;      // K (base)
        const double R = 8314.462;     // J/kmol.K

        const double D = 0.5;          // m
        const double L = 10000.0;      // m
        const double k = 4.5e-5;       // m (commercial steel roughness)
        const double P1 = 5.0e6;       // Pa (absolute)
        const double T = 288.15;       // K
        const double Z = 0.90;
        const double Qstd = 2.0e6;     // m3/day (standard, 15 C / 101.325 kPa)
        const double E = 0.95;         // pipeline efficiency

        // Existing correlation: Darcy-Weisbach single-phase gas drop, frictional part (Pa).
        static double DarcyGasDrop()
        {
            double rhoStd = MW * Pb / (R * Tb);       // kg/m3 at base conditions (ideal)
            double rhoAct = MW * P1 / (Z * R * T);    // kg/m3 at pipe conditions
            double mass = Qstd * rhoStd / 86400.0;    // kg/s
            double qvActual_m3day = mass / rhoAct * 86400.0;
            var res = new BeggsBrill().CalculateDeltaPGas(D, L, 0.0, k, qvActual_m3day, 0.012, rhoAct);
            return Convert.ToDouble(res[2]);          // [2] = frictional dP (Pa)
        }

        static double GasEq(FlowPackage m)
            => Pipe.GasPipelineFrictionalDeltaP(m, D, L, Qstd, MW / MWair, T, Z, P1, E);

        [Test]
        public void GasEquationsAgreeInMagnitudeWithDarcyAndKeepTheConservatismOrdering()
        {
            double darcy = DarcyGasDrop();
            double wey = GasEq(FlowPackage.Weymouth);
            double pa = GasEq(FlowPackage.Panhandle_A);
            double pb = GasEq(FlowPackage.Panhandle_B);

            Console.WriteLine($"Darcy={darcy:F0} Pa  Weymouth={wey:F0}  Panhandle A={pa:F0}  Panhandle B={pb:F0}");

            foreach (var pair in new[] { ("Darcy", darcy), ("Weymouth", wey), ("Panhandle A", pa), ("Panhandle B", pb) })
            {
                Assert.That(double.IsFinite(pair.Item2), $"{pair.Item1} drop is not finite");
                Assert.That(pair.Item2, Is.GreaterThan(0.0), $"{pair.Item1} drop is not positive");
            }

            // Same order of magnitude as the rigorous Darcy drop: a wrong unit/constant would move a
            // gas-equation result by orders of magnitude and fall outside this band.
            foreach (var pair in new[] { ("Weymouth", wey), ("Panhandle A", pa), ("Panhandle B", pb) })
                Assert.That(pair.Item2, Is.InRange(0.25 * darcy, 4.0 * darcy),
                            $"{pair.Item1} drop {pair.Item2:F0} Pa is out of the Darcy magnitude band ({darcy:F0} Pa)");

            // Weymouth is the most conservative (largest drop) of the three.
            Assert.That(wey, Is.GreaterThanOrEqualTo(pa), "Weymouth should be >= Panhandle A");
            Assert.That(wey, Is.GreaterThanOrEqualTo(pb), "Weymouth should be >= Panhandle B");
        }
    }
}
