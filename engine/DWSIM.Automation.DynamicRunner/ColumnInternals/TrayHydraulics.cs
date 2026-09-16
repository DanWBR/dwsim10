//    Column internals rating: sieve tray hydraulics.
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
    /// <summary>
    /// Tray hydraulics. Sieve trays after the design procedure of Towler and Sinnott, Chemical Engineering
    /// Design (2008), section 11.13 (Coulson and Richardson vol. 6, ch. 11), with Kister, Distillation
    /// Design (1992), chapter 6, for the Kister and Haas flooding correlation; valve trays with the dry
    /// pressure drop of Klein (Chem. Eng., May 3, 1982, p. 81; Kister sec. 6.3.2, Table 6.9), which
    /// fine-tunes Bolles (1976); bubble-cap trays with the Bolles (1956) method as Ludwig, Applied Process
    /// Design vol. 2, ch. 8 presents it (Figs. 8-106, 8-108, 8-113 and 8-114, eqs. 8-225 to 8-245). All
    /// inputs in SI unless the name says otherwise; heads are returned in mm of clear liquid as the books
    /// tabulate them.
    /// </summary>
    public static class TrayHydraulics
    {
        public const double g = 9.80665;

        // ------------------------------------------------------------------ geometry

        /// <summary>Downcomer (segment) area fraction of the circle for a chord of length lw on a circle of
        /// diameter Dc: (theta - sin theta) / 2 pi with theta the angle subtended by the chord.</summary>
        public static double SegmentAreaFraction(double weirLengthOverDiameter)
        {
            var r = Math.Max(0.0, Math.Min(1.0, weirLengthOverDiameter));
            var theta = 2.0 * Math.Asin(r);
            return (theta - Math.Sin(theta)) / (2.0 * Math.PI);
        }

        /// <summary>Weir length over the column diameter that gives the downcomer area fraction (Towler Fig. 11.33 is this geometry).</summary>
        public static double WeirLengthRatio(double downcomerAreaFraction)
        {
            double lo = 0.0, hi = 1.0;
            for (int i = 0; i < 60; i++)
            {
                var mid = 0.5 * (lo + hi);
                if (SegmentAreaFraction(mid) < downcomerAreaFraction) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>Chord height (downcomer width at the wall) for a chord of length lw on a circle of diameter Dc.</summary>
        public static double ChordHeight(double diameter, double weirLength)
        {
            var r = Math.Max(0.0, Math.Min(1.0, weirLength / diameter));
            return 0.5 * diameter * (1.0 - Math.Cos(Math.Asin(r)));
        }

        // ------------------------------------------------------------------ flooding

        /// <summary>Flow parameter F_LV = (L_w / V_w) sqrt(rho_V / rho_L) (Towler eq. 11.82), mass flows.</summary>
        public static double FlowParameter(double liquidMassFlow, double vaporMassFlow, double rhoL, double rhoV)
        {
            if (vaporMassFlow <= 0) return double.PositiveInfinity;
            return liquidMassFlow / vaporMassFlow * Math.Sqrt(rhoV / rhoL);
        }

        /// <summary>
        /// Fair (1961) flooding capacity factor C_SB (Towler K1, Fig. 11.29) on the net area, m/s, at
        /// sigma = 20 dyn/cm, as the fit of Lygeros and Magoulas (Hydrocarbon Processing 65(12), 43, 1986):
        /// C_SB = 0.0105 + 8.127e-4 TS^0.755 exp(-1.463 F_LV^0.842), TS the tray spacing in mm.
        /// The chart covers 0.01 &lt;= F_LV &lt;= 1 and 0.15 to 0.9 m spacing; F_LV is clamped to the chart.
        /// </summary>
        public static double FairCapacityFactor(double flowParameter, double traySpacing)
        {
            var flv = Math.Max(0.01, Math.Min(1.0, flowParameter));
            var ts = Math.Max(150.0, Math.Min(900.0, traySpacing * 1000.0));
            return 0.0105 + 8.127e-4 * Math.Pow(ts, 0.755) * Math.Exp(-1.463 * Math.Pow(flv, 0.842));
        }

        /// <summary>Fair flooding velocity on the net area (Towler eq. 11.81 with the surface tension
        /// correction (sigma/0.02)^0.2 of section 11.13.3), m/s.</summary>
        public static double FairFloodingVelocity(double flowParameter, double traySpacing, double rhoL, double rhoV, double sigma)
        {
            var k1 = FairCapacityFactor(flowParameter, traySpacing) * Math.Pow(Math.Max(sigma, 1e-4) / 0.02, 0.2);
            return k1 * Math.Sqrt((rhoL - rhoV) / rhoV);
        }

        /// <summary>
        /// Kister and Haas (1990) entrainment flood capacity factor on the net area, m/s (Kister eq. 6.12):
        /// C_SB = 0.144 (d_h^2 sigma / rho_L)^0.125 (rho_G / rho_L)^0.1 (S / h_ct)^0.5, in ft/s with d_h in
        /// inches, sigma in dyn/cm, rho in lb/ft3, S the tray spacing and h_ct the clear liquid height at the
        /// froth-to-spray transition in inches (Kister eqs. 6.68 to 6.70, Jeronimo and Sawistowski form).
        /// Surface tension is capped at 25 dyn/cm as the authors recommend. Valid for hole area fractions
        /// 0.06 to 0.20, spacing over 14 in, non-foaming systems.
        /// </summary>
        public static double KisterHaasCapacityFactor(double holeDiameter, double sigma, double rhoL, double rhoV,
            double traySpacing, double holeAreaFraction, double liquidLoadPerWeirLength, double weirHeight)
        {
            var dhIn = holeDiameter / 0.0254;
            var sigmaDyn = Math.Min(25.0, sigma * 1000.0);
            var rhoLlb = rhoL / 16.01846;
            var rhoVlb = rhoV / 16.01846;
            var sIn = traySpacing / 0.0254;
            var hct = ClearLiquidHeightAtTransition(holeDiameter, holeAreaFraction, rhoL, rhoV, liquidLoadPerWeirLength, weirHeight) / 0.0254;
            var csbFt = 0.144 * Math.Pow(dhIn * dhIn * sigmaDyn / rhoLlb, 0.125) * Math.Pow(rhoVlb / rhoLlb, 0.1) * Math.Sqrt(sIn / Math.Max(hct, 0.05));
            return csbFt * 0.3048;
        }

        /// <summary>
        /// Clear liquid height at the froth-to-spray transition (Kister eqs. 6.68 to 6.70, the Jeronimo and
        /// Sawistowski correlation with the Kister and Haas physical property correction), m. In the book's
        /// units: h_ct = h_ct,water (62.2 / rho_L)^(0.5 (1 - n)), h_ct,water = 0.294 A_f^-0.791 d_h^0.833 /
        /// (1 + 0.0036 Q_L^-0.59 A_f^-1.79), n = 0.0231 d_h / A_f, with d_h and heights in inches, rho_L in
        /// lb/ft3 and Q_L in gpm per inch of weir (Kister's sizing example: d_h 0.5 in, A_f 0.1, Q_L 6.45
        /// gpm/in give h_ct,water 0.937 in and n 0.115).
        /// </summary>
        public static double ClearLiquidHeightAtTransition(double holeDiameter, double holeAreaFraction, double rhoL, double rhoV,
            double liquidLoadPerWeirLength, double weirHeight)
        {
            var dhIn = holeDiameter / 0.0254;
            var af = Math.Max(0.02, holeAreaFraction);
            var qlGpmIn = Math.Max(0.05, liquidLoadPerWeirLength * 402.6); // m3/(s m) -> gpm/in
            var hctw = 0.294 * Math.Pow(af, -0.791) * Math.Pow(dhIn, 0.833) / (1.0 + 0.0036 * Math.Pow(qlGpmIn, -0.59) * Math.Pow(af, -1.79));
            var n = 0.0231 * dhIn / af;
            var hct = hctw * Math.Pow(62.2 / (rhoL / 16.01846), 0.5 * (1.0 - n));
            return hct * 0.0254;
        }

        // ------------------------------------------------------------------ entrainment

        // Fair (1961) fractional entrainment psi(F_LV, fraction of flood), digitised from Towler and Sinnott
        // Fig. 11.31 (Perry 7th Fig. 14-28). Rows: percent flood; columns: F_LV grid. NaN = outside the chart.
        private static readonly double[] EntFlv = { 0.01, 0.015, 0.02, 0.03, 0.05, 0.07, 0.1, 0.15, 0.2, 0.3, 0.5 };
        private static readonly double[] EntPct = { 30, 35, 40, 45, 50, 60, 70, 80, 90, 95 };
        private static readonly double[,] EntPsi =
        {
            // 0.01     0.015   0.02    0.03    0.05    0.07    0.1     0.15    0.2     0.3     0.5
            { 0.0030, 0.0028, 0.0026, 0.0023, 0.0019, 0.0017, 0.0015, 0.0012, 0.0010, 0.0008, 0.0005 }, // 30
            { 0.0079, 0.0067, 0.0059, 0.0048, 0.0037, 0.0030, 0.0024, 0.0018, 0.0015, 0.0010, 0.0006 }, // 35
            { 0.0155, 0.0137, 0.0112, 0.0086, 0.0060, 0.0047, 0.0035, 0.0025, 0.0020, 0.0013, 0.0008 }, // 40
            { 0.0367, 0.0264, 0.0211, 0.0150, 0.0095, 0.0070, 0.0050, 0.0033, 0.0024, 0.0015, 0.0009 }, // 45
            { 0.0629, 0.0455, 0.0350, 0.0239, 0.0144, 0.0103, 0.0070, 0.0044, 0.0032, 0.0019, 0.0010 }, // 50
            { 0.0950, 0.0788, 0.0608, 0.0416, 0.0249, 0.0173, 0.0116, 0.0070, 0.0047, 0.0026, 0.0011 }, // 60
            { 0.1765, 0.1377, 0.1087, 0.0725, 0.0404, 0.0279, 0.0189, 0.0113, 0.0074, 0.0039, 0.0014 }, // 70
            { 0.2500, 0.2179, 0.1788, 0.1257, 0.0716, 0.0467, 0.0303, 0.0177, 0.0112, 0.0056, 0.0018 }, // 80
            { 0.3800, 0.3329, 0.2792, 0.2007, 0.1200, 0.0786, 0.0480, 0.0271, 0.0169, 0.0079, 0.0025 }, // 90
            { 0.5300, 0.5100, 0.4987, 0.3883, 0.2426, 0.1625, 0.1000, 0.0530, 0.0290, 0.0130, 0.0040 }  // 95
        };

        /// <summary>Fair fractional entrainment psi (kg entrained per kg of gross liquid flow) from the chart,
        /// interpolated log-log in F_LV and log-linear between the percent-flood curves. Outside the chart the
        /// nearest curve is used and the caller should treat the value as indicative.</summary>
        public static double FairEntrainment(double flowParameter, double floodFraction)
        {
            var flv = Math.Max(EntFlv[0], Math.Min(EntFlv[EntFlv.Length - 1], flowParameter));
            var pct = Math.Max(EntPct[0], Math.Min(EntPct[EntPct.Length - 1], floodFraction * 100.0));
            int j = 0; while (j < EntFlv.Length - 2 && EntFlv[j + 1] < flv) j++;
            int i = 0; while (i < EntPct.Length - 2 && EntPct[i + 1] < pct) i++;
            var fx = (Math.Log(flv) - Math.Log(EntFlv[j])) / (Math.Log(EntFlv[j + 1]) - Math.Log(EntFlv[j]));
            var fy = (pct - EntPct[i]) / (EntPct[i + 1] - EntPct[i]);
            var a = Math.Log(EntPsi[i, j]) + fx * (Math.Log(EntPsi[i, j + 1]) - Math.Log(EntPsi[i, j]));
            var b = Math.Log(EntPsi[i + 1, j]) + fx * (Math.Log(EntPsi[i + 1, j + 1]) - Math.Log(EntPsi[i + 1, j]));
            return Math.Exp(a + fy * (b - a));
        }

        /// <summary>Colburn correction of the Murphree efficiency for entrainment: E_a = E_mv / (1 + E_mv psi/(1-psi)) (Towler eq. 11.79); returns E_a/E_mv.</summary>
        public static double EntrainmentEfficiencyFactor(double psi, double murphreeEfficiency)
        {
            if (psi >= 1) return 0;
            return 1.0 / (1.0 + murphreeEfficiency * psi / (1.0 - psi));
        }

        // ------------------------------------------------------------------ weir, weeping

        /// <summary>Weir crest by the Francis formula for a segmental downcomer, mm of liquid (Towler eq. 11.85).</summary>
        public static double WeirCrest(double liquidMassFlow, double rhoL, double weirLength)
        {
            return 750.0 * Math.Pow(liquidMassFlow / (rhoL * Math.Max(weirLength, 1e-6)), 2.0 / 3.0);
        }

        // Eduljee (1959) weep-point constant K2 vs (h_w + h_ow) in mm, Towler Fig. 11.32
        private static readonly double[] K2h = { 15, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
        private static readonly double[] K2v = { 27.4, 28.3, 29.1, 29.65, 30.05, 30.35, 30.6, 30.75, 30.9, 31.05, 31.15, 31.25 };

        public static double EduljeeK2(double clearLiquidHeightMm)
        {
            return Interp(K2h, K2v, clearLiquidHeightMm);
        }

        /// <summary>Weep-point hole velocity (Towler eq. 11.84): u_h,min = [K2 - 0.90 (25.4 - d_h)] / sqrt(rho_V), d_h in mm.</summary>
        public static double WeepPointVelocity(double weirHeight, double weirCrestMm, double holeDiameter, double rhoV)
        {
            var k2 = EduljeeK2(weirHeight * 1000.0 + weirCrestMm);
            return (k2 - 0.90 * (25.4 - holeDiameter * 1000.0)) / Math.Sqrt(rhoV);
        }

        // ------------------------------------------------------------------ pressure drop

        // Liebson et al. (1957) orifice coefficient, Towler Fig. 11.36: C0 vs percent perforated area for
        // plate thickness / hole diameter of 0.2, 0.6, 0.8, 1.0, 1.2; each line read at 3 % and its slope.
        private static readonly double[] C0t = { 0.2, 0.6, 0.8, 1.0, 1.2 };
        private static readonly double[] C0at3 = { 0.660, 0.685, 0.725, 0.782, 0.830 };
        private static readonly double[] C0slope = { 0.00793, 0.00793, 0.00759, 0.00814, 0.00960 }; // per percent

        /// <summary>Orifice coefficient C0 for the dry plate pressure drop.</summary>
        public static double OrificeCoefficient(double plateThicknessOverHoleDiameter, double holeToPerforatedAreaRatio)
        {
            var t = Math.Max(C0t[0], Math.Min(C0t[C0t.Length - 1], plateThicknessOverHoleDiameter));
            var pct = Math.Max(3.0, Math.Min(20.0, holeToPerforatedAreaRatio * 100.0));
            int i = 0; while (i < C0t.Length - 2 && C0t[i + 1] < t) i++;
            var f = (t - C0t[i]) / (C0t[i + 1] - C0t[i]);
            var a = C0at3[i] + C0slope[i] * (pct - 3.0);
            var b = C0at3[i + 1] + C0slope[i + 1] * (pct - 3.0);
            return a + f * (b - a);
        }

        /// <summary>Dry plate drop h_d = 51 (u_h / C0)^2 rho_V / rho_L, mm liquid (Towler eq. 11.88).</summary>
        public static double DryPlateDrop(double holeVelocity, double c0, double rhoV, double rhoL)
        {
            return 51.0 * Math.Pow(holeVelocity / c0, 2) * rhoV / rhoL;
        }

        /// <summary>Residual head h_r = 12.5e3 / rho_L, mm liquid (Towler eq. 11.89).</summary>
        public static double ResidualHead(double rhoL) { return 12500.0 / rhoL; }

        /// <summary>Head loss in the downcomer h_dc = 166 [L_wd / (rho_L A_m)]^2, mm liquid (Towler eq. 11.92), A_m the smaller of the downcomer area and the area under the apron.</summary>
        public static double DowncomerHeadLoss(double liquidMassFlow, double rhoL, double downcomerArea, double apronArea)
        {
            var am = Math.Max(1e-9, Math.Min(downcomerArea, apronArea));
            return 166.0 * Math.Pow(liquidMassFlow / (rhoL * am), 2);
        }

        /// <summary>Millimetres of liquid to pascals: 9.81e-3 h rho_L (Towler eq. 11.87).</summary>
        public static double HeadToPressure(double headMm, double rhoL) { return 9.81e-3 * headMm * rhoL; }

        // ------------------------------------------------------------------ efficiency

        /// <summary>O'Connell (1946) overall efficiency in the Eduljee form used by Towler and Sinnott (eq. 11.67):
        /// E_o = 0.51 - 0.325 log10(mu_L alpha), with mu_L the liquid viscosity in mPa s and alpha the relative
        /// volatility of the key components; clamped to 0.1 to 1.</summary>
        public static double OConnellEfficiency(double liquidViscosity, double relativeVolatility)
        {
            if (double.IsNaN(relativeVolatility) || relativeVolatility <= 0 || liquidViscosity <= 0) return double.NaN;
            var e = 0.51 - 0.325 * Math.Log10(liquidViscosity * 1000.0 * relativeVolatility);
            return Math.Max(0.1, Math.Min(1.0, e));
        }

        // ------------------------------------------------------------------ helpers

        internal static double Interp(double[] xs, double[] ys, double x)
        {
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int i = 0; while (i < xs.Length - 2 && xs[i + 1] < x) i++;
            var f = (x - xs[i]) / (xs[i + 1] - xs[i]);
            return ys[i] + f * (ys[i + 1] - ys[i]);
        }

        // ------------------------------------------------------------------ aeration, liquid gradient

        /// <summary>Tray aeration factor beta against the active-area F-factor F_va = u_a sqrt(rho_V) in ft/s (lb/ft3)^0.5:
        /// a fit of the Fair and Bolles curve (Kister Fig. 6.22a, Perry 7th Fig. 14-32) through the valve tray point of
        /// Klein's example (beta = 0.61 at F_va = 1.04): beta = 0.5 + 0.48 exp(-1.4 F_va).</summary>
        public static double AerationFactor(double fvaUS) { return 0.5 + 0.48 * Math.Exp(-1.4 * Math.Max(0.0, fvaUS)); }

        // Bolles' modified Davies liquid gradient factor chart (Ludwig Fig. 8-108): (Q/L_w)/C_d against Q/L_w in gpm per ft of mean tray width
        private static readonly double[] GradQ = { 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 40, 60 };
        private static readonly double[] GradF = { 11.5, 13.2, 14.6, 15.8, 16.8, 18.5, 20.0, 23.5, 27.0, 32.5, 38.0, 48.0 };

        /// <summary>Uncorrected liquid gradient per row of caps, in, from the Davies equation as Bolles fitted it (Ludwig eq. 8-228):
        /// (Q/L_w)/C_d = 25.8 [gamma/(1 + gamma)] D'^0.5 [1.6 D' + 3 (h_l + 0.3 s/gamma)], with Q/L_w in gpm per ft of mean
        /// tray width (the left-hand side read from Fig. 8-108), gamma the gap between caps over the cap diameter, h_l the
        /// clear liquid depth and s the skirt clearance in inches.</summary>
        public static double BollesGradientPerRow(double gpmPerFt, double gamma, double clearLiquidIn, double skirtClearanceIn)
        {
            var q = Math.Max(0.1, gpmPerFt);
            double lhs;
            int last = GradQ.Length - 1;
            if (q <= GradQ[0]) lhs = GradF[0] * Math.Pow(q / GradQ[0], 0.35);
            else if (q >= GradQ[last]) lhs = GradF[last] * Math.Pow(q / GradQ[last], 0.58);
            else
            {
                int i = 0; while (i < last - 1 && GradQ[i + 1] < q) i++;
                var f = Math.Log(q / GradQ[i]) / Math.Log(GradQ[i + 1] / GradQ[i]);
                lhs = Math.Exp(Math.Log(GradF[i]) + f * (Math.Log(GradF[i + 1]) - Math.Log(GradF[i])));
            }
            var g = Math.Max(0.05, gamma);
            var k = 25.8 * g / (1.0 + g);
            var c = 3.0 * (Math.Max(0.1, clearLiquidIn) + 0.3 * Math.Max(0.0, skirtClearanceIn) / g);
            double lo = 0.0, hi = 5.0;
            for (int it = 0; it < 80; it++)
            {
                var mid = 0.5 * (lo + hi);
                var v = k * Math.Sqrt(mid) * (1.6 * mid + c);
                if (v < lhs) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        // Davies (1947) vapour load correction of the liquid gradient (Ludwig Fig. 8-113): C_v at 5 gpm/ft for each
        // V_s sqrt(rho_V) in ft/s (lb/ft3)^0.5, every line reaching 1.0 at 70 gpm/ft
        private static readonly double[] CvF = { 0.5, 0.8, 1.0, 1.1, 1.2, 1.4 };
        private static readonly double[] CvAt5 = { 0.545, 0.70, 0.88, 1.0, 1.13, 1.245 };

        public static double DaviesVaporCorrection(double superficialFFactorUS, double gpmPerFt)
        {
            var c5 = Interp(CvF, CvAt5, superficialFFactorUS);
            if (gpmPerFt >= 70.0) return 1.0;
            return 1.0 + (c5 - 1.0) * (70.0 - gpmPerFt) / 65.0;
        }

        // ------------------------------------------------------------------ bubble caps (Bolles)

        // Bolles' cap pressure drop constant against the annular over riser area ratio (Ludwig Fig. 8-114)
        private static readonly double[] KcRatio = { 1.0, 1.1, 1.2, 1.3, 1.4, 1.5 };
        private static readonly double[] KcVal = { 0.650, 0.575, 0.515, 0.470, 0.440, 0.420 };

        /// <summary>K_c of h_pc = K_c [rho_V/(rho_L - rho_V)] (V/A_r)^2 (Ludwig eq. 8-231, inches with V in ft3/s and A_r in ft2).</summary>
        public static double BollesCapConstant(double annularOverRiserArea) { return Interp(KcRatio, KcVal, annularOverRiserArea); }

        /// <summary>Riser, reversal and annulus drop of the caps, in (Ludwig eq. 8-231).</summary>
        public static double BollesCapDropIn(double kc, double rhoV, double rhoL, double vaporLoadFt3s, double riserAreaFt2)
        {
            return kc * rhoV / (rhoL - rhoV) * Math.Pow(vaporLoadFt3s / riserAreaFt2, 2);
        }

        /// <summary>Slot opening of rectangular slots, in (Ludwig eq. 8-225): h_s = 32 [rho_V/(rho_L - rho_V)]^(1/3) [V/(N_c N_s w_s)]^(2/3), w_s in inches.</summary>
        public static double BollesSlotOpeningIn(double rhoV, double rhoL, double vaporLoadFt3s, int caps, int slotsPerCap, double slotWidthIn)
        {
            return 32.0 * Math.Pow(rhoV / (rhoL - rhoV), 1.0 / 3.0) * Math.Pow(vaporLoadFt3s / (caps * slotsPerCap * slotWidthIn), 2.0 / 3.0);
        }

        /// <summary>Maximum slot capacity of rectangular slots, ft3/s (Ludwig eq. 8-226): V_m = 0.79 A_s [H_s (rho_L - rho_V)/rho_V]^0.5, A_s in ft2 and H_s in inches.</summary>
        public static double BollesSlotCapacityFt3s(double slotAreaFt2, double slotHeightIn, double rhoV, double rhoL)
        {
            return 0.79 * slotAreaFt2 * Math.Sqrt(slotHeightIn * (rhoL - rhoV) / rhoV);
        }

        /// <summary>Maximum slot capacity of trapezoidal slots, ft3/s (Ludwig eq. 8-227): V_m = 2.36 A_s [(2/3) R/(1+R) + (4/15)(1-R)/(1+R)]
        /// [H_s (rho_L - rho_V)/rho_V]^0.5 with R the top over bottom width (0.629, 0.734 and 0.787 A_s [...]^0.5 for R = 0, 0.5 and 1).</summary>
        public static double BollesSlotCapacityFt3s(double slotAreaFt2, double slotHeightIn, double rhoV, double rhoL, double topWidthRatio)
        {
            var R = Math.Max(0.0, Math.Min(1.0, topWidthRatio));
            if (R >= 1.0) return BollesSlotCapacityFt3s(slotAreaFt2, slotHeightIn, rhoV, rhoL);
            return 2.36 * slotAreaFt2 * ((2.0 / 3.0) * R / (1.0 + R) + (4.0 / 15.0) * (1.0 - R) / (1.0 + R)) * Math.Sqrt(slotHeightIn * (rhoL - rhoV) / rhoV);
        }

        /// <summary>Vapour load over the slot capacity that a slot opening fraction x = h_s/H_s carries, for a slot whose width
        /// grows linearly from R w_b at the top to w_b at the bottom (the head on the open strip being the liquid depth below
        /// the top of the slot): V/V_m = [(2/3) R x^1.5 + (4/15)(1-R) x^2.5] / [(2/3) R + (4/15)(1-R)]. This is the generalised
        /// correlation of Bolles' Figure 8-107; for R = 1 it is the rectangular slot, h_s/H_s = (V/V_m)^(2/3).</summary>
        public static double SlotLoadForOpening(double openingFraction, double topWidthRatio)
        {
            var R = Math.Max(0.0, Math.Min(1.0, topWidthRatio));
            var x = Math.Max(0.0, openingFraction);
            return ((2.0 / 3.0) * R * Math.Pow(x, 1.5) + (4.0 / 15.0) * (1.0 - R) * Math.Pow(x, 2.5)) / ((2.0 / 3.0) * R + (4.0 / 15.0) * (1.0 - R));
        }

        /// <summary>Slot opening as a fraction of the slot height for a vapour load over the slot capacity (inverse of SlotLoadForOpening; above 1 the slots are fully open).</summary>
        public static double SlotOpeningFraction(double slotLoad, double topWidthRatio)
        {
            if (slotLoad <= 0) return 0.0;
            double lo = 0.0, hi = 4.0;
            for (int i = 0; i < 80; i++)
            {
                var mid = 0.5 * (lo + hi);
                if (SlotLoadForOpening(mid, topWidthRatio) < slotLoad) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        // ------------------------------------------------------------------ bubble caps (modified Dauphine)

        /// <summary>Riser drop, in (Ludwig eqs. 8-232 and 8-233): 0.111 (d_r/rho_L) [rho_V^0.5 V/A_r]^2.09 when the reversal area exceeds the
        /// riser area, 0.099 (d_r/rho_L) (a_r/a_x)^0.5 [rho_V^0.5 V/A_r]^2.1 otherwise; d_r the riser inside diameter in inches, densities in lb/ft3.</summary>
        public static double DauphineRiserDropIn(double riserDiameterIn, double rhoV, double rhoL, double vaporLoadFt3s, double riserAreaFt2, double riserAreaPerCap, double reversalAreaPerCap)
        {
            var ur = Math.Sqrt(rhoV) * vaporLoadFt3s / riserAreaFt2;
            if (reversalAreaPerCap >= riserAreaPerCap) return 0.111 * riserDiameterIn / rhoL * Math.Pow(ur, 2.09);
            return 0.099 * riserDiameterIn / rhoL * Math.Sqrt(riserAreaPerCap / Math.Max(reversalAreaPerCap, 1e-9)) * Math.Pow(ur, 2.1);
        }

        /// <summary>Reversal and annulus drop, in (Ludwig eq. 8-234, riser height above 2.5 in): 0.68/rho_L [(2 a_r^2/(a_x a_c)) rho_V^0.5 V/A_r]^1.71,
        /// with a_r, a_x and a_c the riser, reversal and cap inside areas per cap.</summary>
        public static double DauphineReversalDropIn(double rhoV, double rhoL, double vaporLoadFt3s, double riserAreaFt2, double riserAreaPerCap, double reversalAreaPerCap, double capAreaPerCap)
        {
            var term = 2.0 * riserAreaPerCap * riserAreaPerCap / (Math.Max(reversalAreaPerCap, 1e-9) * capAreaPerCap) * Math.Sqrt(rhoV) * vaporLoadFt3s / riserAreaFt2;
            return 0.68 / rhoL * Math.Pow(term, 1.71);
        }

        /// <summary>Dry slot drop of rectangular slots, in (Ludwig eq. 8-235 as the worked example applies it): 0.163/rho_L [(d_c rho_V)^0.5 V/A_s]^1.73,
        /// d_c the cap inside diameter in inches and A_s the total slot area in ft2.</summary>
        public static double DauphineDrySlotDropIn(double capDiameterIn, double rhoV, double rhoL, double vaporLoadFt3s, double slotAreaFt2)
        {
            return 0.163 / rhoL * Math.Pow(Math.Sqrt(capDiameterIn * rhoV) * vaporLoadFt3s / slotAreaFt2, 1.73);
        }

        // Dauphine wet cap correction C_w (Ludwig Fig. 8-115) against (V/A_s) sqrt((rho_V/rho_L)(a_s/a_an)), V in ft3/s, A_s in ft2
        private static readonly double[] CwX = { 0.15, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4, 1.55 };
        private static readonly double[] CwY = { 0.0, 0.05, 0.15, 0.25, 0.34, 0.44, 0.53, 0.60, 0.67, 0.73, 0.85, 0.95, 1.0 };

        /// <summary>Wet cap correction: the dry cap drop over C_w is the wet cap drop (Ludwig eq. 8-237). Slots of 1 to 2 in; about 25 % more for 0.5 in slots.</summary>
        public static double DauphineWetCapCorrection(double x) { return Math.Max(0.02, Interp(CwX, CwY, x)); }

        // ------------------------------------------------------------------ valves (Klein)

        /// <summary>Ratio of the valve weight with legs to the weight without (Klein, Kister Table 6.9).</summary>
        public static double ValveLegRatio(ValveLegs legs, bool venturi)
        {
            switch (legs)
            {
                case ValveLegs.ThreeLegs: return venturi ? 1.29 : 1.23;
                case ValveLegs.FourLegs: return venturi ? 1.45 : 1.34;
                default: return 1.0;
            }
        }

        /// <summary>Loss coefficient with the valves closed, s2 in/ft2 (Klein): 6.154 flat, 3.077 venturi.</summary>
        public static double ValveClosedCoefficient(bool venturi) { return venturi ? 3.077 : 6.154; }

        /// <summary>Loss coefficient with the valves open (Klein): 0.448 for venturi valves; for flat valves 0.821, 0.931 and 1.104
        /// at 0.134, 0.104 and 0.074 in deck thickness, scaled with 1/sqrt(t) (Kister eq. 6.45) at other thicknesses.</summary>
        public static double ValveOpenCoefficient(bool venturi, double deckThicknessIn)
        {
            if (venturi) return 0.448;
            return 0.931 * Math.Sqrt(0.104 / Math.Max(0.03, deckThicknessIn));
        }

        /// <summary>Hole velocity at the closed balance point, ft/s (Klein; Kister eq. 6.46a): u = sqrt(t_v R_vw (C_vw/K_c) rho_vm/rho_V),
        /// t_v the valve thickness in inches, C_vw = 1.3 the eddy loss coefficient, the density ratio dimensionless.</summary>
        public static double ValveClosedBalanceVelocityFt(double valveThicknessIn, double legRatio, double kClosed, double valveDensity, double rhoV)
        {
            return Math.Sqrt(valveThicknessIn * legRatio * (1.3 / kClosed) * valveDensity / rhoV);
        }

        // ------------------------------------------------------------------ valves (Glitsch Bulletin 4900)

        // Figure 5b of the Ballast Tray Design Manual: CAF_0 against the vapour density (lb/ft3) for tray spacings of 12, 18, 24, 36 and 48 in,
        // read from the 1993 chart; below the plateau start of each spacing the manual's low-density equation applies
        private static readonly double[] CafTs = { 12, 18, 24, 36, 48 };
        private static readonly double[] CafDv = { 0.2, 0.4, 0.6, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0 };
        private static readonly double[,] Caf0 =
        {
            { 0.300, 0.298, 0.295, 0.290, 0.285, 0.279, 0.267, 0.256, 0.248, 0.240, 0.229, 0.222 }, // 12 in
            { 0.400, 0.398, 0.396, 0.394, 0.389, 0.383, 0.367, 0.351, 0.334, 0.318, 0.300, 0.285 }, // 18 in
            { 0.445, 0.443, 0.441, 0.438, 0.433, 0.427, 0.411, 0.388, 0.364, 0.350, 0.330, 0.310 }, // 24 in
            { 0.500, 0.497, 0.494, 0.490, 0.483, 0.475, 0.460, 0.435, 0.405, 0.385, 0.360, 0.335 }, // 36 in
            { 0.530, 0.525, 0.521, 0.514, 0.507, 0.498, 0.479, 0.454, 0.425, 0.400, 0.370, 0.345 }  // 48 in
        };
        private static readonly double[] CafPlateauStart = { 0.17, 0.17, 0.10, 0.045, 0.02 };
        // the limit line at high vapour density
        private static readonly double[] LimitDv = { 4.0, 4.5, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
        private static readonly double[] LimitCaf = { 0.470, 0.440, 0.406, 0.355, 0.313, 0.281, 0.247, 0.222 };

        /// <summary>Glitsch flood capacity factor at zero liquid load CAF_0, ft/s (Bulletin 4900 Figure 5): the chart value for the
        /// tray spacing (interpolated between the 12 to 48 in curves, flat below each plateau start), the low-density equation
        /// CAF_0 = TS^0.63 D_v^(1/6)/12 below 0.17 lb/ft3, and the limit line above 4 lb/ft3, the smallest of the three.</summary>
        public static double GlitschCaf0(double rhoVlb, double traySpacingIn)
        {
            var ts = Math.Max(CafTs[0], Math.Min(CafTs[CafTs.Length - 1], traySpacingIn));
            int i = 0; while (i < CafTs.Length - 2 && CafTs[i + 1] < ts) i++;
            var f = (ts - CafTs[i]) / (CafTs[i + 1] - CafTs[i]);
            Func<int, double> curve = row =>
            {
                var dv = Math.Max(CafPlateauStart[row], Math.Min(CafDv[CafDv.Length - 1], rhoVlb));
                var xs = new double[CafDv.Length]; var ys = new double[CafDv.Length];
                for (int k = 0; k < CafDv.Length; k++) { xs[k] = Math.Log(CafDv[k]); ys[k] = Caf0[row, k]; }
                return Interp(xs, ys, Math.Log(Math.Max(dv, CafDv[0])));
            };
            var caf = curve(i) + f * (curve(i + 1) - curve(i));
            if (rhoVlb < 0.17) caf = Math.Min(caf, Math.Pow(ts, 0.63) * Math.Pow(Math.Max(rhoVlb, 1e-6), 1.0 / 6.0) / 12.0);
            if (rhoVlb >= LimitDv[0]) caf = Math.Min(caf, Interp(LimitDv, LimitCaf, Math.Min(rhoVlb, LimitDv[LimitDv.Length - 1])));
            return caf;
        }

        /// <summary>Downcomer design velocity, gpm/ft2 (Bulletin 4900 eq. 1): the smallest of 250, 41 sqrt(D_L - D_V) and 7.5 sqrt(TS) sqrt(D_L - D_V), times the system factor.</summary>
        public static double GlitschDowncomerDesignVelocity(double rhoLlb, double rhoVlb, double traySpacingIn, double systemFactor)
        {
            var d = Math.Sqrt(Math.Max(rhoLlb - rhoVlb, 0.1));
            return Math.Min(250.0, Math.Min(41.0 * d, 7.5 * Math.Sqrt(traySpacingIn) * d)) * systemFactor;
        }

        /// <summary>Per cent of flood at constant V/L as a fraction (eq. 13): (Vload + GPM FPL/13000)/(AA CAF), Vload = CFS sqrt(D_V/(D_L - D_V)) in ft3/s, FPL in inches, AA in ft2.</summary>
        public static double GlitschFloodFraction(double vload, double gpm, double flowPathIn, double activeAreaFt2, double caf)
        {
            return (vload + gpm * flowPathIn / 13000.0) / (activeAreaFt2 * caf);
        }

        /// <summary>K1 and K2 of the dry pressure drop (Bulletin 4900 p. 27): V-1 flat orifice K1 = 0.20, K2 = 1.18, 0.95, 0.86, 0.67, 0.61 for decks of
        /// 0.074, 0.104, 0.134, 0.187 and 0.250 in; V-4 venturi orifice K1 = 0.10, K2 = 0.68.</summary>
        public static void GlitschCoefficients(bool venturi, double deckThicknessIn, out double k1, out double k2)
        {
            if (venturi) { k1 = 0.10; k2 = 0.68; return; }
            k1 = 0.20;
            k2 = Interp(new[] { 0.074, 0.104, 0.134, 0.187, 0.250 }, new[] { 1.18, 0.95, 0.86, 0.67, 0.61 }, deckThicknessIn);
        }

        /// <summary>Dry tray pressure drop, in of liquid (eq. 18): the larger of 1.35 t_m D_m/D_L + K1 V_H^2 D_V/D_L (units part open) and K2 V_H^2 D_V/D_L (fully open), t_m in inches, V_H in ft/s.</summary>
        public static double GlitschDryDropIn(double valveThicknessIn, double valveDensityLb, double rhoLlb, double rhoVlb, double holeVelocityFt, double k1, double k2)
        {
            var x = holeVelocityFt * holeVelocityFt * rhoVlb / rhoLlb;
            return Math.Max(1.35 * valveThicknessIn * valveDensityLb / rhoLlb + k1 * x, k2 * x);
        }

        // leakage point (Bulletin 4900 p. 28): V_H sqrt(D_V/D_L) at which no leakage occurs against the liquid level on the tray, inches
        private static readonly double[] LeakLevel = { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0 };
        private static readonly double[] LeakV1 = { 0.35, 0.45, 0.53, 0.59, 0.69, 0.75, 0.82 };
        private static readonly double[] LeakV4 = { 0.63, 0.81, 0.97, 1.11, 1.24, 1.36, 1.48 };

        /// <summary>Hole velocity (as V_H sqrt(D_V/D_L), ft/s) above which a single-pass Ballast tray does not leak, for the clear liquid level h_w + h_ow in inches.</summary>
        public static double GlitschLeakagePoint(bool venturi, double liquidLevelIn)
        {
            return Interp(LeakLevel, venturi ? LeakV4 : LeakV1, liquidLevelIn);
        }

        // ------------------------------------------------------------------ full rating of one tray

        /// <summary>Rates one tray of the section at the stage conditions with the model of its type; diameter must be set.</summary>
        public static StageRating RateTray(InternalsSection s, StageProperties sp, double diameter,
            double turndown, double minResidenceTime, double murphreeEfficiency)
        {
            switch (s.Type)
            {
                case InternalType.ValveTray: return RateValveTray(s, sp, diameter, turndown, minResidenceTime, murphreeEfficiency);
                case InternalType.BubbleCapTray: return RateBubbleCapTray(s, sp, diameter, turndown, minResidenceTime, murphreeEfficiency);
                default: return RateSieveTray(s, sp, diameter, turndown, minResidenceTime, murphreeEfficiency);
            }
        }

        /// <summary>The areas, weir and flooding figures every tray type shares; fills the rating and returns the areas.</summary>
        private static void TrayCommon(InternalsSection s, StageProperties sp, double diameter, StageRating r, double holeDiameterForKisterHaas, double areaFractionForKisterHaas,
            out double Ad, out double An, out double Aa, out double lw)
        {
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            var Ac = Math.PI * diameter * diameter / 4.0;
            var fd = Math.Max(0.02, Math.Min(0.45, s.DowncomerAreaFraction));
            Ad = fd * Ac;
            An = Ac - Ad;
            Aa = Ac - 2.0 * Ad;
            lw = WeirLengthRatio(fd) * diameter;

            r.FlowParameter = FlowParameter(sp.LiquidMassFlow, sp.VaporMassFlow, rhoL, rhoV);
            r.VaporLoad = sp.VaporVolumetricFlow;
            r.LiquidLoad = sp.LiquidVolumetricFlow;
            r.NetVelocity = r.VaporLoad / An;
            r.CapacityFactor = r.NetVelocity * Math.Sqrt(rhoV / Math.Max(rhoL - rhoV, 1e-6));

            double uf;
            if (s.FloodModel == TrayFloodModel.KisterHaas && s.Type != InternalType.BubbleCapTray)
            {
                var csb = KisterHaasCapacityFactor(holeDiameterForKisterHaas, sp.SurfaceTension, rhoL, rhoV, s.TraySpacing,
                    areaFractionForKisterHaas, r.LiquidLoad / Math.Max(lw, 1e-6), s.WeirHeight);
                uf = csb * Math.Sqrt((rhoL - rhoV) / rhoV);
            }
            else
            {
                uf = FairFloodingVelocity(r.FlowParameter, s.TraySpacing, rhoL, rhoV, sp.SurfaceTension);
            }
            uf *= Math.Max(0.1, Math.Min(1.0, s.SystemFactor));
            r.FloodingVelocity = uf;
            r.FloodFraction = r.NetVelocity / uf;
            r.WeirCrest = WeirCrest(sp.LiquidMassFlow, rhoL, lw);
        }

        /// <summary>Downcomer, entrainment, efficiency and the warnings every tray type shares. extraBackup is added to the
        /// backup on top of h_w + h_ow + h_t + h_dc (the liquid gradient of bubble-cap trays).</summary>
        private static void TrayFinish(InternalsSection s, StageProperties sp, StageRating r, double Ad, double lw, double minResidenceTime, double murphreeEfficiency, double extraBackupMm)
        {
            var rhoL = sp.LiquidDensity; var hw = s.WeirHeight; var Lw = sp.LiquidMassFlow;
            var hap = s.DowncomerClearance > 0 ? s.DowncomerClearance : Math.Max(0.005, hw - 0.010);
            var Aap = lw * hap;
            var hdc = DowncomerHeadLoss(Lw, rhoL, Ad, Aap);
            r.DowncomerBackup = hw * 1000.0 + r.WeirCrest + r.TotalHead + hdc + extraBackupMm;
            r.DowncomerBackupLimit = 0.5 * (s.TraySpacing + hw) * 1000.0;
            r.DowncomerResidenceTime = Ad * (r.DowncomerBackup / 1000.0) * rhoL / Math.Max(Lw, 1e-9);
            r.DowncomerVelocity = r.LiquidLoad / Ad;
            r.OConnellEfficiency = OConnellEfficiency(sp.LiquidViscosity, sp.RelativeVolatility);
            r.Entrainment = FairEntrainment(r.FlowParameter, r.FloodFraction);
            r.EntrainmentEfficiencyFactor = EntrainmentEfficiencyFactor(r.Entrainment, murphreeEfficiency);

            if (r.FloodFraction > 1.0) r.Warnings.Add("Entrainment flooding: the vapour velocity exceeds the flooding velocity.");
            else if (r.FloodFraction > 0.85) r.Warnings.Add("Fraction of flood above 0.85.");
            if (r.DowncomerBackup > r.DowncomerBackupLimit) r.Warnings.Add("Downcomer backup exceeds half the tray spacing plus the weir height (downcomer flooding).");
            if (r.DowncomerResidenceTime < minResidenceTime) r.Warnings.Add("Downcomer residence time below " + minResidenceTime.ToString("0.#") + " s.");
            if (r.Entrainment > 0.1) r.Warnings.Add("Fractional entrainment above 0.1; the efficiency penalty is large.");
            if (r.WeirCrest < 10.0) r.Warnings.Add("Weir crest below 10 mm; the liquid may not flow evenly along the weir.");
        }

        /// <summary>Rates one sieve tray of the section at the stage conditions; diameter must be set.</summary>
        public static StageRating RateSieveTray(InternalsSection s, StageProperties sp, double diameter,
            double turndown, double minResidenceTime, double murphreeEfficiency)
        {
            var r = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = diameter };
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            var Lw = sp.LiquidMassFlow;
            double Ad, An, Aa, lw;
            TrayCommon(s, sp, diameter, r, s.HoleDiameter, s.HoleAreaFraction, out Ad, out An, out Aa, out lw);
            var Ah = s.HoleAreaFraction * Aa;
            var hw = s.WeirHeight;

            // weeping
            var howTurndown = WeirCrest(Lw * turndown, rhoL, lw);
            r.HoleVelocity = r.VaporLoad / Math.Max(Ah, 1e-9);
            r.WeepPointVelocity = WeepPointVelocity(hw, howTurndown, s.HoleDiameter, rhoV);
            r.WeepRatio = r.HoleVelocity / r.WeepPointVelocity;
            r.WeepRatioTurndown = r.HoleVelocity * turndown / r.WeepPointVelocity;

            // pressure drop (perforated area taken as the active area, as Towler does in the example)
            var c0 = OrificeCoefficient(s.PlateThickness / s.HoleDiameter, s.HoleAreaFraction);
            r.DryPressureDrop = DryPlateDrop(r.HoleVelocity, c0, rhoV, rhoL);
            var hr = ResidualHead(rhoL);
            r.AeratedLiquidHead = hw * 1000.0 + r.WeirCrest + hr;
            r.TotalHead = r.DryPressureDrop + r.AeratedLiquidHead;
            r.PressureDrop = HeadToPressure(r.TotalHead, rhoL);
            r.PressureDropTotal = r.PressureDrop;

            TrayFinish(s, sp, r, Ad, lw, minResidenceTime, murphreeEfficiency, 0.0);
            if (r.WeepRatioTurndown < 1.0) r.Warnings.Add("Weeping at turndown: the hole velocity falls below the weep point.");
            else if (r.WeepRatio < 1.0) r.Warnings.Add("Weeping at the design rate: the hole velocity is below the weep point.");
            return r;
        }

        /// <summary>Rates one valve tray: flooding by Fair or Kister-Haas on the open valve area, dry pressure drop by Klein's
        /// closed and open balance points, aerated liquid by the aeration factor, weeping read against the closed balance point.</summary>
        public static StageRating RateValveTray(InternalsSection s, StageProperties sp, double diameter,
            double turndown, double minResidenceTime, double murphreeEfficiency)
        {
            var r = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = diameter };
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            double Ad, An, Aa, lw;
            TrayCommon(s, sp, diameter, r, s.ValveHoleDiameter, s.ValveOpenAreaFraction, out Ad, out An, out Aa, out lw);
            var hw = s.WeirHeight;

            var valves = Math.Max(1.0, s.ValvesPerArea * Aa);
            var Ah = valves * Math.PI / 4.0 * s.ValveHoleDiameter * s.ValveHoleDiameter;
            if (s.ValveModel == ValveTrayModel.Glitsch) return RateGlitschValveTray(s, sp, diameter, turndown, minResidenceTime, murphreeEfficiency, r, Ad, Aa, lw, valves);
            r.HoleVelocity = r.VaporLoad / Ah;

            // Klein's balance points, in his units
            var tvIn = s.ValveThickness / 0.0254;
            var rvw = ValveLegRatio(s.ValveLegs, s.ValveVenturi);
            var kc = ValveClosedCoefficient(s.ValveVenturi);
            var ko = ValveOpenCoefficient(s.ValveVenturi, s.PlateThickness / 0.0254);
            var uCbp = ValveClosedBalanceVelocityFt(tvIn, rvw, kc, s.ValveDensity, rhoV);
            var uObp = uCbp * Math.Sqrt(kc / ko);
            var uhFt = r.HoleVelocity / 0.3048;
            double hdIn; string regime;
            if (uhFt <= uCbp) { hdIn = kc * (rhoV / rhoL) * uhFt * uhFt; regime = "valves closed"; }
            else if (uhFt < uObp) { hdIn = kc * (rhoV / rhoL) * uCbp * uCbp; regime = "valves opening"; }
            else { hdIn = ko * (rhoV / rhoL) * uhFt * uhFt; regime = "valves open"; }
            r.ClosedBalanceVelocity = uCbp * 0.3048;
            r.OpenBalanceVelocity = uObp * 0.3048;
            r.UnitReference = uhFt / uObp;
            r.Regime = regime + ", " + (r.UnitReference * 100.0).ToString("0") + " % of the open balance point";
            r.DryPressureDrop = hdIn * 25.4;

            var fva = (r.VaporLoad / Aa) / 0.3048 * Math.Sqrt(rhoV / 16.01846);
            var beta = AerationFactor(fva);
            r.AeratedLiquidHead = beta * (hw * 1000.0 + r.WeirCrest);
            r.TotalHead = r.DryPressureDrop + r.AeratedLiquidHead;
            r.PressureDrop = HeadToPressure(r.TotalHead, rhoL);
            r.PressureDropTotal = r.PressureDrop;

            // weeping: below the closed balance point the valves sit on the deck and the liquid finds the crevices
            r.WeepPointVelocity = r.ClosedBalanceVelocity;
            r.WeepRatio = r.HoleVelocity / r.ClosedBalanceVelocity;
            r.WeepRatioTurndown = r.WeepRatio * turndown;

            TrayFinish(s, sp, r, Ad, lw, minResidenceTime, murphreeEfficiency, 0.0);
            if (r.WeepRatioTurndown < 1.0) r.Warnings.Add("At turndown the valves stay closed: expect weeping through the valve crevices.");
            else if (r.UnitReference * turndown < 0.4) r.Warnings.Add("Unit reference below 40 % at turndown: the vapour may channel through part of the valves (Kister asks 40, 60 and 80 % for one, two and four passes).");
            return r;
        }

        /// <summary>Glitsch Ballast Tray Design Manual (Bulletin 4900, 6th ed.) procedure for a single-pass V-1 or V-4 tray: per cent of
        /// flood at constant V/L from the CAF chart with the system factor (eqs. 2 and 13), the downcomer against its design velocity
        /// (eqs. 1 and 5), the dry and total pressure drop (eqs. 18 to 20), the downcomer backup (eqs. 22 and 23), the leakage point
        /// table and the dry pressure drop capacity limit (eq. 17). Units of the manual inside; the rating in SI.</summary>
        private static StageRating RateGlitschValveTray(InternalsSection s, StageProperties sp, double diameter, double turndown, double minResidenceTime,
            double murphreeEfficiency, StageRating r, double Ad, double Aa, double lw, double valves)
        {
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity; var hw = s.WeirHeight;
            var rhoLlb = rhoL / 16.01846; var rhoVlb = rhoV / 16.01846;
            var cfs = r.VaporLoad / 0.0283168;
            var gpm = r.LiquidLoad * 15850.32;
            var tsIn = s.TraySpacing / 0.0254;
            var hwIn = hw / 0.0254;
            var tsEff = hwIn > 0.15 * tsIn ? tsIn - (hwIn - 0.15 * tsIn) : tsIn;   // a tall weir counts against the spacing (p. 35)
            var sf = Math.Max(0.1, Math.Min(1.0, s.SystemFactor));

            // capacity: jet flood at constant V/L
            var caf = GlitschCaf0(rhoVlb, tsEff) * sf;
            var vload = cfs * Math.Sqrt(rhoVlb / Math.Max(rhoLlb - rhoVlb, 1e-6));
            var fplIn = (diameter - 2.0 * ChordHeight(diameter, lw)) / 0.0254;
            var aaFt = Aa / 0.09290304;
            r.CapacityFactor = vload / aaFt * 0.3048;      // Vload/AA, the capacity factor on the active area, m/s
            r.FloodingVelocity = caf * 0.3048 * Math.Sqrt((rhoL - rhoV) / rhoV) * Aa / (Math.PI * diameter * diameter / 4.0 - Ad);
            r.FloodFraction = GlitschFloodFraction(vload, gpm, fplIn, aaFt, caf);

            // downcomer against its design velocity
            var vdDsg = GlitschDowncomerDesignVelocity(rhoLlb, rhoVlb, tsIn, sf);
            r.DowncomerFloodFraction = gpm / (Ad / 0.09290304 * vdDsg);

            // pressure drop
            var ahFt = valves / 78.5;
            r.HoleVelocity = cfs / ahFt * 0.3048;
            var vhFt = cfs / ahFt;
            double k1, k2;
            GlitschCoefficients(s.ValveVenturi, s.PlateThickness / 0.0254, out k1, out k2);
            var dpDryIn = GlitschDryDropIn(s.ValveThickness / 0.0254, s.ValveDensity / 16.01846, rhoLlb, rhoVlb, vhFt, k1, k2);
            var lwiIn = lw / 0.0254;
            var howIn = 0.4 * Math.Pow(gpm / lwiIn, 2.0 / 3.0);
            r.WeirCrest = howIn * 25.4;
            r.DryPressureDrop = dpDryIn * 25.4;
            r.AeratedLiquidHead = (howIn + 0.4 * hwIn) * 25.4;
            r.TotalHead = r.DryPressureDrop + r.AeratedLiquidHead;
            r.PressureDrop = HeadToPressure(r.TotalHead, rhoL);
            r.PressureDropTotal = r.PressureDrop;
            r.DryDropLimitRatio = dpDryIn / (0.2 * tsIn);

            // the valves: fully open once K2 V_H^2 D_V/D_L exceeds the part-open drop
            var x = vhFt * vhFt * rhoVlb / rhoLlb;
            var partOpen = 1.35 * (s.ValveThickness / 0.0254) * (s.ValveDensity / 16.01846) / rhoLlb + k1 * x;
            var uObpFt = Math.Sqrt(1.35 * (s.ValveThickness / 0.0254) * (s.ValveDensity / 16.01846) / rhoLlb / ((k2 - k1) * rhoVlb / rhoLlb));
            r.OpenBalanceVelocity = uObpFt * 0.3048;
            r.UnitReference = vhFt / uObpFt;

            // leakage point
            var leak = GlitschLeakagePoint(s.ValveVenturi, hwIn + howIn);
            var vhCorr = vhFt * Math.Sqrt(rhoVlb / rhoLlb);
            r.WeepPointVelocity = leak / Math.Sqrt(rhoVlb / rhoLlb) * 0.3048;
            r.ClosedBalanceVelocity = r.WeepPointVelocity;
            r.WeepRatio = vhCorr / leak;
            r.WeepRatioTurndown = r.WeepRatio * turndown;

            // downcomer backup (eq. 22), the clearance area under the downcomer
            var hap = s.DowncomerClearance > 0 ? s.DowncomerClearance : Math.Max(0.005, hw - 0.010);
            var audFt = lw * hap / 0.09290304;
            var vud = gpm / (448.83 * audFt);
            var hudIn = 0.65 * vud * vud;
            var hdcIn = hwIn + howIn + (r.TotalHead / 25.4 + hudIn) * rhoLlb / (rhoLlb - rhoVlb);
            r.DowncomerBackup = hdcIn * 25.4;
            var limitFrac = rhoVlb < 1.0 ? 0.6 : rhoVlb < 3.0 ? 0.5 : 0.4;
            r.DowncomerBackupLimit = limitFrac * s.TraySpacing * 1000.0;
            r.DowncomerResidenceTime = Ad * (r.DowncomerBackup / 1000.0) * rhoL / Math.Max(sp.LiquidMassFlow, 1e-9);
            r.DowncomerVelocity = r.LiquidLoad / Ad;
            r.OConnellEfficiency = OConnellEfficiency(sp.LiquidViscosity, sp.RelativeVolatility);
            r.Entrainment = FairEntrainment(r.FlowParameter, r.FloodFraction);
            r.EntrainmentEfficiencyFactor = EntrainmentEfficiencyFactor(r.Entrainment, murphreeEfficiency);
            r.Regime = "Glitsch " + (s.ValveVenturi ? "V-4" : "V-1") + ": jet flood " + (r.FloodFraction * 100).ToString("0") + " %, downcomer " + (r.DowncomerFloodFraction * 100).ToString("0")
                + " %, dry drop " + (r.DryDropLimitRatio * 100).ToString("0") + " % of its limit, " + (partOpen > k2 * x ? "units part open" : "units fully open");

            if (r.FloodFraction > 1.0) r.Warnings.Add("Jet flooding: the vapour load exceeds the Glitsch flood capacity at this liquid rate.");
            else if (r.FloodFraction > 0.82) r.Warnings.Add("Per cent of flood above 82, the most the manual recommends for a new design (77 in vacuum).");
            if (r.DowncomerFloodFraction > 1.0) r.Warnings.Add("The liquid load exceeds the downcomer design velocity times the downcomer area.");
            if (r.DryDropLimitRatio > 1.0) r.Warnings.Add("Dry pressure drop above 0.2 times the tray spacing, the Glitsch capacity limit by pressure drop (too few valves).");
            if (r.DowncomerBackup > r.DowncomerBackupLimit) r.Warnings.Add("Downcomer backup above " + (limitFrac * 100).ToString("0") + " % of the tray spacing, the manual's limit at this vapour density.");
            if (r.DowncomerResidenceTime < minResidenceTime) r.Warnings.Add("Downcomer residence time below " + minResidenceTime.ToString("0.#") + " s.");
            if (r.WeepRatioTurndown < 1.0) r.Warnings.Add("Below the Glitsch leakage point at turndown: about 25 % of the liquid leaks and the efficiency drops by about 10 %.");
            if (r.Entrainment > 0.1) r.Warnings.Add("Fractional entrainment above 0.1 (Fair's chart); the efficiency penalty is large.");
            return r;
        }

        private const double CapWall = 0.002;     // cap wall thickness, m
        private const double RiserWall = 0.001;   // riser wall thickness, m

        /// <summary>Rates one bubble-cap tray by the Bolles method: riser, reversal and annulus drop, slot opening, static seal,
        /// weir crest and half the liquid gradient; flooding by Fair; downcomer backup as Ludwig eq. 8-245.</summary>
        public static StageRating RateBubbleCapTray(InternalsSection s, StageProperties sp, double diameter,
            double turndown, double minResidenceTime, double murphreeEfficiency)
        {
            var r = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = diameter };
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            double Ad, An, Aa, lw;
            TrayCommon(s, sp, diameter, r, s.HoleDiameter, s.HoleAreaFraction, out Ad, out An, out Aa, out lw);
            var hw = s.WeirHeight;
            var Ac = Math.PI * diameter * diameter / 4.0;

            // cap layout
            var capOD = s.CapDiameter + 2.0 * CapWall;
            var pitch = Math.Max(1.05, s.CapPitchRatio) * capOD;
            int caps = Math.Max(1, (int)Math.Floor(Aa / (0.866 * pitch * pitch)));
            var riserArea = Math.PI / 4.0 * s.RiserDiameter * s.RiserDiameter;
            var riserOD = s.RiserDiameter + 2.0 * RiserWall;
            var annulus = Math.PI / 4.0 * (s.CapDiameter * s.CapDiameter - riserOD * riserOD);
            var ratio = annulus / riserArea;
            if (ratio < 1.0) r.Warnings.Add("The annular area between riser and cap is smaller than the riser area; Bolles' constant is read at 1.0.");

            // Bolles, in his units
            var Vft = r.VaporLoad / 0.0283168;
            var ArFt = caps * riserArea / 0.09290304;
            var rhoVlb = rhoV / 16.01846; var rhoLlb = rhoL / 16.01846;
            var kc = BollesCapConstant(Math.Max(1.0, Math.Min(1.5, ratio)));
            var hpcIn = BollesCapDropIn(kc, rhoVlb, rhoLlb, Vft, ArFt);
            var wsIn = s.SlotWidth / 0.0254; var HsIn = s.SlotHeight / 0.0254;
            var Rs = Math.Max(0.0, Math.Min(1.0, s.SlotTopWidthRatio));
            var slotAreaPerCapIn2 = s.SlotsPerCap * wsIn * HsIn * 0.5 * (1.0 + Rs);
            var AsFt = caps * slotAreaPerCapIn2 / 144.0;
            var VmFt = BollesSlotCapacityFt3s(AsFt, HsIn, rhoVlb, rhoLlb, Rs);
            r.HoleVelocity = r.VaporLoad / (AsFt * 0.09290304);   // slot velocity
            r.SlotLoad = Vft / VmFt;
            r.SlotOpeningFraction = SlotOpeningFraction(r.SlotLoad, Rs);
            var hsIn = r.SlotOpeningFraction * HsIn;
            r.SlotOpening = hsIn * 25.4;
            double hcIn;
            if (s.CapMethod == BubbleCapMethod.Dauphine)
            {
                // per-cap areas in in2: riser inside, reversal (cylinder between the top of the riser and the cap ceiling) and cap inside
                var arIn2 = riserArea / 6.4516e-4;
                var acIn2 = Math.PI / 4.0 * s.CapDiameter * s.CapDiameter / 6.4516e-4;
                var dMeanIn = (s.RiserDiameter + riserOD) / 2.0 / 0.0254;
                var gapIn = Math.Max(0.05, (s.CapInsideHeight - s.RiserHeight) / 0.0254);
                var axIn2 = Math.PI * dMeanIn * gapIn;
                var hrIn = DauphineRiserDropIn(s.RiserDiameter / 0.0254, rhoVlb, rhoLlb, Vft, ArFt, arIn2, axIn2);
                var hraIn = DauphineReversalDropIn(rhoVlb, rhoLlb, Vft, ArFt, arIn2, axIn2, acIn2);
                var hsDryIn = DauphineDrySlotDropIn(s.CapDiameter / 0.0254, rhoVlb, rhoLlb, Vft, AsFt);
                var anIn2 = annulus / 6.4516e-4;
                var cw = DauphineWetCapCorrection(Vft / AsFt * Math.Sqrt(rhoVlb / rhoLlb * slotAreaPerCapIn2 / anIn2));
                hcIn = (hrIn + hraIn + hsDryIn) / cw;
                r.CapPressureDrop = (hrIn + hraIn) * 25.4;
                r.CapDropLimit = (hrIn + hraIn + HsIn + 0.25) * 25.4;   // cap blows under the shroud ring (taken 0.25 in) above this
                if (s.RiserHeight / 0.0254 < 2.5) r.Warnings.Add("Riser lower than 2.5 in: the Dauphine reversal drop is read for taller risers.");
            }
            else
            {
                hcIn = hpcIn + hsIn;
                r.CapPressureDrop = hpcIn * 25.4;
            }
            r.DryPressureDrop = hcIn * 25.4;
            var howIn = r.WeirCrest / 25.4;
            var hssIn = s.StaticSeal / 0.0254;

            // liquid gradient: given, or Davies as Bolles charts it, corrected for the vapour load
            double deltaIn;
            if (s.LiquidGradient > 0) deltaIn = s.LiquidGradient / 0.0254;
            else
            {
                var meanWidthFt = 0.5 * (lw + diameter) / 0.3048;
                var gpmPerFt = r.LiquidLoad * 15850.32 / meanWidthFt;
                var gamma = (pitch - capOD) / capOD;
                var skirtIn = s.SkirtClearance / 0.0254;
                var flowPath = diameter - 2.0 * ChordHeight(diameter, lw);
                int rows = Math.Max(1, (int)Math.Round(flowPath / (0.866 * pitch)));
                var cv = DaviesVaporCorrection(r.VaporLoad / Ac / 0.3048 * Math.Sqrt(rhoVlb), gpmPerFt);
                deltaIn = 0.0;
                for (int it = 0; it < 4; it++)
                {
                    var hl = hw / 0.0254 + howIn + 0.5 * deltaIn;
                    deltaIn = BollesGradientPerRow(gpmPerFt, gamma, hl, skirtIn) * rows * cv;
                }
            }
            r.LiquidGradient = deltaIn * 25.4;
            r.DynamicSeal = (hssIn + howIn + 0.5 * deltaIn) * 25.4;
            r.AeratedLiquidHead = r.DynamicSeal;
            r.TotalHead = r.DryPressureDrop + r.DynamicSeal;                 // Ludwig eq. 8-239
            r.PressureDrop = HeadToPressure(r.TotalHead, rhoL);
            r.PressureDropTotal = r.PressureDrop;
            r.VaporDistributionRatio = deltaIn / Math.Max(hcIn, 1e-6);
            r.Regime = "slots " + (r.SlotOpeningFraction * 100.0).ToString("0") + " % open, " + (r.SlotLoad * 100.0).ToString("0") + " % of the slot capacity";

            TrayFinish(s, sp, r, Ad, lw, minResidenceTime, murphreeEfficiency, r.LiquidGradient);   // Ludwig eq. 8-245
            if (r.SlotLoad > 1.0) r.Warnings.Add("Vapour load above the slot capacity: the slots are fully open and the vapour blows under the cap skirt.");
            if (!double.IsNaN(r.CapDropLimit) && r.DryPressureDrop > r.CapDropLimit) r.Warnings.Add("Wet cap drop above the riser, reversal and slot height sum: the vapour blows under the shroud ring.");
            if (r.SlotOpening * Math.Pow(turndown, 2.0 / 3.0) < 12.7) r.Warnings.Add("Slot opening below 0.5 in at turndown: the tray may pulse.");
            if (r.VaporDistributionRatio > 0.5) r.Warnings.Add("Liquid gradient above half the cap drop: the inlet caps may stop bubbling (Bolles keeps delta/h_c below 0.5).");
            if (r.DynamicSeal < 12.7) r.Warnings.Add("Dynamic slot seal below 0.5 in; the caps may cone (Bolles' Table 8-18 asks 0.5 to 1.5 in in vacuum, more at pressure).");
            return r;
        }

        /// <summary>Diameter that puts the tray at the target fraction of flood (Fair or Kister-Haas), m.</summary>
        public static double DiameterForFloodFraction(InternalsSection s, StageProperties sp, double target)
        {
            // net area basis: u_n = V / A_n = target * u_f ; A_c = A_n / (1 - f_d)
            double lo = 0.1, hi = 30.0;
            for (int i = 0; i < 80; i++)
            {
                var mid = 0.5 * (lo + hi);
                var r = RateTray(s, sp, mid, 1.0, 0.0, 1.0);
                if (r.FloodFraction > target) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }
    }
}
