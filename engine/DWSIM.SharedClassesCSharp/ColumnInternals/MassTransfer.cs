//    Column internals rating: mass transfer on trays and diffusivity estimates for the rate-based column.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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

namespace DWSIM.Automation.DynamicRunner.ColumnInternals
{
    /// <summary>Which correlation gives the gas-phase transfer units of a tray.</summary>
    public enum TrayMassTransferMethod
    {
        /// <summary>AIChE Bubble-Tray Design Manual (1958) for both phases.</summary>
        AIChE = 0,
        /// <summary>Chan and Fair (1984) for the gas phase (sieve trays), AIChE for the liquid.</summary>
        ChanFair = 1
    }

    /// <summary>
    /// Diffusion coefficient estimates for the rate-based column: Fuller, Schettler and Giddings for gases
    /// (with the Wilke and Fairbanks rule for a component in a mixture) and Wilke and Chang for liquids, the
    /// molar volumes at the normal boiling point from Tyn and Calus and the Fuller diffusion volumes scaled
    /// from them. They are order-of-magnitude estimates; a section of the internals case can override them.
    /// </summary>
    public static class Diffusivities
    {
        /// <summary>Molar volume at the normal boiling point, cm3/mol, from the critical volume (m3/kmol): Tyn and Calus, V_b = 0.285 V_c^1.048.</summary>
        public static double BoilingPointVolume(double criticalVolumeM3PerKmol)
        {
            var vc = criticalVolumeM3PerKmol * 1000.0;   // cm3/mol
            if (vc <= 0 || double.IsNaN(vc)) vc = 200.0;
            return 0.285 * Math.Pow(vc, 1.048);
        }

        /// <summary>Fuller diffusion volume, cm3/mol, taken as 0.8 of the boiling point volume (the ratio runs 0.5 to 0.95 over common molecules).</summary>
        public static double FullerVolume(double criticalVolumeM3PerKmol) { return 0.8 * BoilingPointVolume(criticalVolumeM3PerKmol); }

        /// <summary>Binary gas diffusivity, m2/s (Fuller): D = 1.013e-2 T^1.75 [1/M_A + 1/M_B]^0.5 / [P (v_A^(1/3) + v_B^(1/3))^2], P in Pa, M in g/mol, v in cm3/mol.</summary>
        public static double Fuller(double T, double P, double MA, double MB, double vA, double vB)
        {
            var mab = Math.Sqrt(1.0 / MA + 1.0 / MB);
            var den = P * Math.Pow(Math.Pow(vA, 1.0 / 3.0) + Math.Pow(vB, 1.0 / 3.0), 2);
            return 1.013e-2 * Math.Pow(T, 1.75) * mab / den;
        }

        /// <summary>Diffusivity of component j through the gas mixture, m2/s (Wilke and Fairbanks): 1/D_jm = sum_{k != j} (y_k / D_jk) / (1 - y_j).</summary>
        public static double GasInMixture(double T, double P, double[] M, double[] Vc, double[] y, int j)
        {
            int n = M.Length;
            if (n == 1) return Fuller(T, P, M[0], M[0], FullerVolume(Vc[0]), FullerVolume(Vc[0]));
            double sum = 0, ysum = 0;
            for (int k = 0; k < n; k++)
            {
                if (k == j) continue;
                var yk = Math.Max(y[k], 1e-12);
                sum += yk / Fuller(T, P, M[j], M[k], FullerVolume(Vc[j]), FullerVolume(Vc[k]));
                ysum += yk;
            }
            if (sum <= 0 || ysum <= 0) return Fuller(T, P, M[j], M[j], FullerVolume(Vc[j]), FullerVolume(Vc[j]));
            return ysum / sum;
        }

        /// <summary>Diffusivity of a dilute solute in a liquid, m2/s (Wilke and Chang): D = 7.4e-12 (phi M_B)^0.5 T / (mu_B V_A^0.6), mu_B in mPa s, V_A in cm3/mol.</summary>
        public static double WilkeChang(double T, double muLPaS, double solventM, double phi, double vbSolute)
        {
            var mu = Math.Max(muLPaS * 1000.0, 0.01);
            return 7.4e-12 * Math.Sqrt(phi * solventM) * T / (mu * Math.Pow(Math.Max(vbSolute, 10.0), 0.6));
        }

        /// <summary>Diffusivity of component j in the liquid mixture, m2/s: Wilke and Chang with the mixture as the solvent (its
        /// mean molar mass, association factor 2.6 when water carries more than half the moles).</summary>
        public static double LiquidInMixture(double T, double muLPaS, double[] M, double[] Vc, double[] x, int j, int waterIndex)
        {
            double msol = 0, xsum = 0;
            for (int k = 0; k < M.Length; k++) { if (k == j) continue; msol += x[k] * M[k]; xsum += x[k]; }
            msol = xsum > 1e-9 ? msol / xsum : M[j];
            var phi = waterIndex >= 0 && waterIndex != j && x[waterIndex] > 0.5 ? 2.6 : 1.0;
            return WilkeChang(T, muLPaS, msol, phi, BoilingPointVolume(Vc[j]));
        }
    }

    /// <summary>
    /// Tray point and Murphree efficiencies from mass transfer (Perry's Handbook 7th ed. eqs. 14-116 to 14-142,
    /// Seader and Henley eqs. 6-31 to 6-36): the AIChE Bubble-Tray Design Manual transfer units, or Chan and
    /// Fair for the gas phase of sieve trays, with the Bennett clear liquid height, the AIChE eddy diffusivity
    /// (Treybal's SI form) and the Gerster partial-mixing relation between point and tray efficiency. SI units.
    /// </summary>
    public static class TrayMassTransfer
    {
        /// <summary>A numerical ceiling on the Murphree efficiency. Partial mixing along a long flow path gives values above 1
        /// (Seader's example: 1.25) and the column applies them as computed; the ceiling only stops the plug-flow limit of the
        /// Gerster relation, (exp(lambda E_OG) - 1)/lambda, from running away at very large stripping factors.</summary>
        public const double MurphreeCap = 3.0;

        /// <summary>Effective froth density (Perry eq. 14-117): phi_e = exp(-12.55 K_s^0.91), K_s = u_a sqrt(rho_V/(rho_L - rho_V)) in m/s.</summary>
        public static double FrothDensity(double ua, double rhoV, double rhoL)
        {
            var ks = ua * Math.Sqrt(rhoV / Math.Max(rhoL - rhoV, 1e-6));
            return Math.Max(0.02, Math.Exp(-12.55 * Math.Pow(ks, 0.91)));
        }

        /// <summary>Bennett clear liquid height, m: phi_e [h_w + C (Q_L/(W_l phi_e))^0.67], C = 0.50 + 0.438 exp(-137.8 h_w).</summary>
        public static double ClearLiquidHeight(double hw, double weirLength, double liquidVolFlow, double phiE)
        {
            var C = 0.50 + 0.438 * Math.Exp(-137.8 * hw);
            return phiE * (hw + C * Math.Pow(liquidVolFlow / (Math.Max(weirLength, 1e-6) * phiE), 0.67));
        }

        /// <summary>AIChE eddy diffusivity of the liquid on the tray, m2/s (Treybal's SI form): D_E = (3.93e-3 + 0.0171 u_a + 3.67 Q_L/W_l + 0.18 h_w)^2.</summary>
        public static double EddyDiffusivity(double ua, double liquidVolFlow, double weirLength, double hw)
        {
            var r = 3.93e-3 + 0.0171 * ua + 3.67 * liquidVolFlow / Math.Max(weirLength, 1e-6) + 0.18 * hw;
            return r * r;
        }

        /// <summary>Murphree tray efficiency from the point efficiency with partial liquid mixing (Gerster et al., Seader eqs. 6-34 and 6-35):
        /// Pe = Z_L^2/(D_E t_L), eta = (Pe/2)[(1 + 4 lambda E_OG/Pe)^0.5 - 1].</summary>
        public static double MurphreeFromPoint(double eog, double lambda, double peclet)
        {
            if (eog <= 0) return 0;
            var le = lambda * eog;
            if (le < 1e-6 || peclet < 1e-6) return eog;
            if (peclet > 1e4) return (Math.Exp(le) - 1.0) / lambda;   // plug flow (Lewis case 1)
            var eta = 0.5 * peclet * (Math.Sqrt(1.0 + 4.0 * le / peclet) - 1.0);
            if (eta < 1e-9) return eog;
            var a = (1.0 - Math.Exp(-(eta + peclet))) / ((eta + peclet) * (1.0 + (eta + peclet) / eta));
            var b = (Math.Exp(eta) - 1.0) / (eta * (1.0 + eta / (eta + peclet)));
            return eog * (a + b);
        }

        /// <summary>Gas-phase transfer units. AIChE: N_G = (0.776 + 4.57 h_w - 0.238 F_va + 104.8 Q_L/W_l)/sqrt(Sc_G) (h_w in m, F_va = u_a sqrt(rho_V)
        /// in m/s (kg/m3)^0.5, Q_L/W_l in m3/(s m)). Chan and Fair: k_G a = 316 sqrt(D_G) (1030 f - 867 f^2)/sqrt(h_L[mm]) in 1/s, N_G = k_G a t_G
        /// with t_G = (1 - phi_e) h_L A_a/(phi_e Q_G).</summary>
        public static double GasTransferUnits(TrayMassTransferMethod method, double hw, double fva, double qlOverWl, double scG, double DG, double hL, double phiE, double tG, double floodFraction)
        {
            if (method == TrayMassTransferMethod.ChanFair)
            {
                var f = Math.Max(0.1, Math.Min(0.95, floodFraction));
                var kga = 316.0 * Math.Sqrt(DG) * (1030.0 * f - 867.0 * f * f) / Math.Sqrt(Math.Max(hL * 1000.0, 1.0));
                return Math.Max(0.05, kga * tG);
            }
            return Math.Max(0.05, (0.776 + 4.57 * hw - 0.238 * fva + 104.8 * qlOverWl) / Math.Sqrt(Math.Max(scG, 1e-3)));
        }

        /// <summary>Liquid-phase transfer units (AIChE): N_L = 19700 sqrt(D_L) (0.4 F_va + 0.17) t_L, t_L = h_L A_a/Q_L.</summary>
        public static double LiquidTransferUnits(double DL, double fva, double tL)
        {
            return Math.Max(0.05, 19700.0 * Math.Sqrt(DL) * (0.4 * fva + 0.17) * tL);
        }

        /// <summary>
        /// Point and Murphree efficiencies of every component on a tray. Inputs: weir height and length, flow path length, active
        /// area (m, m2), volumetric flows (m3/s), densities and viscosities, the gas and liquid diffusivities and the stripping
        /// factor lambda = K V/L of each component, and the fraction of flood (Chan and Fair). The Murphree values are clamped
        /// to 0.02 to 1.2.
        /// </summary>
        public static void Efficiencies(TrayMassTransferMethod method, double hw, double weirLength, double flowPath, double activeArea,
            double QL, double QG, double rhoV, double rhoL, double muV, double[] DG, double[] DL, double[] lambda, double floodFraction,
            out double[] pointEfficiency, out double[] murphreeEfficiency, out double clearLiquidHeight, out double peclet)
        {
            double[] ng, nl;
            Efficiencies(method, hw, weirLength, flowPath, activeArea, QL, QG, rhoV, rhoL, muV, DG, DL, lambda, floodFraction, out pointEfficiency, out murphreeEfficiency, out clearLiquidHeight, out peclet, out ng, out nl);
        }

        /// <summary>The same, returning the gas and liquid transfer units of every component as well.</summary>
        public static void Efficiencies(TrayMassTransferMethod method, double hw, double weirLength, double flowPath, double activeArea,
            double QL, double QG, double rhoV, double rhoL, double muV, double[] DG, double[] DL, double[] lambda, double floodFraction,
            out double[] pointEfficiency, out double[] murphreeEfficiency, out double clearLiquidHeight, out double peclet, out double[] gasUnits, out double[] liquidUnits)
        {
            int n = lambda.Length;
            pointEfficiency = new double[n]; murphreeEfficiency = new double[n]; gasUnits = new double[n]; liquidUnits = new double[n];
            var ua = QG / Math.Max(activeArea, 1e-6);
            var phiE = FrothDensity(ua, rhoV, rhoL);
            var hL = ClearLiquidHeight(hw, weirLength, QL, phiE);
            clearLiquidHeight = hL;
            var tG = (1.0 - phiE) * hL * activeArea / (phiE * Math.Max(QG, 1e-9));
            var tL = hL * activeArea / Math.Max(QL, 1e-9);
            var fva = ua * Math.Sqrt(rhoV);
            var qlw = QL / Math.Max(weirLength, 1e-6);
            var de = EddyDiffusivity(ua, QL, weirLength, hw);
            peclet = flowPath * flowPath / (de * tL);
            for (int j = 0; j < n; j++)
            {
                var scG = muV / (rhoV * Math.Max(DG[j], 1e-9));
                var ng = GasTransferUnits(method, hw, fva, qlw, scG, DG[j], hL, phiE, tG, floodFraction);
                var nl = LiquidTransferUnits(Math.Max(DL[j], 1e-12), fva, tL);
                var lam = Math.Max(lambda[j], 1e-6);
                var nog = 1.0 / (1.0 / ng + lam / nl);
                var eog = 1.0 - Math.Exp(-nog);
                pointEfficiency[j] = eog;
                gasUnits[j] = ng; liquidUnits[j] = nl;
                murphreeEfficiency[j] = Math.Max(0.02, Math.Min(MurphreeCap, MurphreeFromPoint(eog, lam, peclet)));
            }
        }

        /// <summary>Murphree efficiency equivalent to a slice of packing that holds n theoretical stages of component j at stripping
        /// factor lambda (Lewis: n = ln[1 + E (lambda - 1)]/ln lambda inverted), clamped to 0.02 to 1.</summary>
        public static double PackedStageEfficiency(double theoreticalStages, double lambda)
        {
            var n = Math.Max(0.0, theoreticalStages);
            double e;
            if (Math.Abs(lambda - 1.0) < 1e-4) e = n;
            else e = (Math.Pow(lambda, n) - 1.0) / (lambda - 1.0);
            return Math.Max(0.02, Math.Min(1.0, e));
        }
    }
}
