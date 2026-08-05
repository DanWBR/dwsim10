using System;
using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Estimation.Correlations
{
    /// Rackett equation for saturated-liquid molar volume:
    ///   V_s = (R * Tc / Pc) * Zc^(1 + (1 - T/Tc)^(2/7))
    /// If Zc isn't known, falls back to the Spencer-Danner Zra estimate
    /// Zra = 0.29056 - 0.08775 * omega when omega is available.
    /// Output "rhoL(298.15K)" in kg/m3 when MW is available; fit stored under
    /// "RackettParams" as [Tc, Pc, Zc_or_Zra, MW].
    public sealed class RackettLiquidDensity : IPropertyEstimator
    {
        private const double R = 8.314462618; // J/mol/K

        public string Name => "Rackett";
        public IReadOnlyList<PropertyCategory> Provides => new[] { PropertyCategory.LiquidDensity };
        public IReadOnlyList<PropertyCategory> Requires => new[] { PropertyCategory.Critical };

        public EstimationResult Estimate(CompoundInputs inputs)
        {
            var r = new EstimationResult();
            if (!(inputs.Tc.HasValue && inputs.Pc.HasValue)) return r;

            double zc = inputs.Zc ??
                        (inputs.Acentric.HasValue ? 0.29056 - 0.08775 * inputs.Acentric.Value : double.NaN);
            if (double.IsNaN(zc) || zc <= 0) return r;

            double tc = inputs.Tc.Value;
            double pc = inputs.Pc.Value;
            double mw = inputs.MolecularWeight ?? double.NaN;

            r.Fits["RackettParams"] = new[] { tc, pc, zc, mw };

            double t = 298.15;
            if (t < tc && !double.IsNaN(mw))
            {
                double vm = (R * tc / pc) * Math.Pow(zc, 1.0 + Math.Pow(1.0 - t / tc, 2.0 / 7.0));
                double rho = (mw * 1e-3) / vm; // kg/m3
                r.Values["rhoL(298.15K)"] = rho;
                r.Units["rhoL(298.15K)"] = "kg/m3";
            }
            return r;
        }
    }
}
