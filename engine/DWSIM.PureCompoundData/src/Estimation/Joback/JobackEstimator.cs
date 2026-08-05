using System;
using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Estimation.Joback
{
    /// Classic Joback group-contribution method. Requires <see cref="CompoundInputs.JobackGroups"/>
    /// populated (group symbol → count). Produces Tc, Pc, Vc, Tb, Tm, Hform, Gform, and the
    /// ideal-gas heat-capacity polynomial Cp = A + B*T + C*T^2 + D*T^3 (J/mol/K).
    ///
    /// Formulas (Reid-Prausnitz-Poling):
    ///   Tb = 198.2 + Σ n_i * tbk_i                                               [K]
    ///   Tc = Tb / (0.584 + 0.965*Σn*tck - (Σn*tck)^2)                             [K]
    ///   Pc = (0.113 + 0.0032*N_atoms - Σn*pck)^-2                                 [bar]
    ///   Vc = 17.5 + Σ n_i * vck_i                                                 [cm3/mol]
    ///   Tm = 122.5 + Σ n_i * tmk_i                                                [K]
    ///   Hform = 68.29 + Σ n_i * hfk_i                                             [kJ/mol]
    ///   Gform = 53.88 + Σ n_i * gfk_i                                             [kJ/mol]
    ///   Cp    : A = ΣnCpA - 37.93 ; B = ΣnCpB + 0.210 ; C = ΣnCpC - 3.91e-4 ;
    ///           D = ΣnCpD + 2.06e-7                                               [J/mol/K]
    public sealed class JobackEstimator : IPropertyEstimator
    {
        public string Name => "Joback";

        public IReadOnlyList<PropertyCategory> Provides => new[]
        {
            PropertyCategory.Critical,
            PropertyCategory.NormalBoilingPoint,
            PropertyCategory.MeltingPoint,
            PropertyCategory.FormationEnergetics,
            PropertyCategory.IdealGasCp,
        };

        public IReadOnlyList<PropertyCategory> Requires => Array.Empty<PropertyCategory>();

        public EstimationResult Estimate(CompoundInputs inputs)
        {
            var r = new EstimationResult();
            if (inputs.JobackGroups.Count == 0) return r;

            double sumTc = 0, sumPc = 0, sumVc = 0, sumTb = 0, sumTm = 0, sumHf = 0, sumGf = 0, sumHfus = 0;
            double sumCpA = 0, sumCpB = 0, sumCpC = 0, sumCpD = 0;
            int totalAtoms = 0;
            bool anyKnown = false;

            foreach (var kv in inputs.JobackGroups)
            {
                if (!JobackGroupTable.Groups.TryGetValue(kv.Key, out var g)) continue;
                anyKnown = true;
                int n = kv.Value;
                totalAtoms += n * g.AtomCount;
                sumTc += n * g.Tc;
                sumPc += n * g.Pc;
                sumVc += n * g.Vc;
                sumTb += n * g.Tb;
                sumTm += n * g.Tm;
                sumHf += n * g.Hform;
                sumGf += n * g.Gform;
                sumHfus += n * g.Hfus;
                sumCpA += n * g.CpA;
                sumCpB += n * g.CpB;
                sumCpC += n * g.CpC;
                sumCpD += n * g.CpD;
            }
            if (!anyKnown) return r;

            double tb = 198.2 + sumTb;
            double denom = 0.584 + 0.965 * sumTc - sumTc * sumTc;
            double tc = denom > 0 ? tb / denom : double.NaN;
            double pcBar = Math.Pow(0.113 + 0.0032 * totalAtoms - sumPc, -2);
            double pcPa = pcBar * 1e5;
            double vc = (17.5 + sumVc) * 1e-6; // cm3/mol -> m3/mol
            double tm = 122.5 + sumTm;
            double hfJ = (68.29 + sumHf) * 1000.0;
            double gfJ = (53.88 + sumGf) * 1000.0;
            double hfusJ = (-0.88 + sumHfus) * 1000.0;

            r.Values["Tb"] = tb; r.Units["Tb"] = "K";
            if (!double.IsNaN(tc)) { r.Values["Tc"] = tc; r.Units["Tc"] = "K"; }
            r.Values["Pc"] = pcPa; r.Units["Pc"] = "Pa";
            r.Values["Vc"] = vc; r.Units["Vc"] = "m3/mol";
            r.Values["Tm"] = tm; r.Units["Tm"] = "K";
            r.Values["HformIG"] = hfJ; r.Units["HformIG"] = "J/mol";
            r.Values["GformIG"] = gfJ; r.Units["GformIG"] = "J/mol";
            r.Values["Hfus"] = hfusJ; r.Units["Hfus"] = "J/mol";

            double cpA = sumCpA - 37.93;
            double cpB = sumCpB + 0.210;
            double cpC = sumCpC - 3.91e-4;
            double cpD = sumCpD + 2.06e-7;
            r.Fits["IdealGasCpPoly"] = new[] { cpA, cpB, cpC, cpD };

            return r;
        }
    }
}
