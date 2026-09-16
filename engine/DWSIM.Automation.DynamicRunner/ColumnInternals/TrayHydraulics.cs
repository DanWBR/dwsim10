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
    /// Sieve tray hydraulics after the design procedure of Towler and Sinnott, Chemical Engineering
    /// Design (2008), section 11.13 (Coulson and Richardson vol. 6, ch. 11), with Kister, Distillation
    /// Design (1992), chapter 6, for the Kister and Haas flooding correlation. All inputs in SI unless
    /// the name says otherwise; heads are returned in mm of clear liquid as the books tabulate them.
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

        // ------------------------------------------------------------------ helpers

        internal static double Interp(double[] xs, double[] ys, double x)
        {
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int i = 0; while (i < xs.Length - 2 && xs[i + 1] < x) i++;
            var f = (x - xs[i]) / (xs[i + 1] - xs[i]);
            return ys[i] + f * (ys[i + 1] - ys[i]);
        }

        // ------------------------------------------------------------------ full rating of one tray

        /// <summary>Rates one sieve tray of the section at the stage conditions; diameter must be set.</summary>
        public static StageRating RateSieveTray(InternalsSection s, StageProperties sp, double diameter,
            double turndown, double minResidenceTime, double murphreeEfficiency)
        {
            var r = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = diameter };
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            var Lw = sp.LiquidMassFlow; var Vw = sp.VaporMassFlow;

            var Ac = Math.PI * diameter * diameter / 4.0;
            var fd = Math.Max(0.02, Math.Min(0.45, s.DowncomerAreaFraction));
            var Ad = fd * Ac;
            var An = Ac - Ad;
            var Aa = Ac - 2.0 * Ad;
            var Ah = s.HoleAreaFraction * Aa;
            var lw = WeirLengthRatio(fd) * diameter;
            var hw = s.WeirHeight;

            r.FlowParameter = FlowParameter(Lw, Vw, rhoL, rhoV);
            r.VaporLoad = sp.VaporVolumetricFlow;
            r.LiquidLoad = sp.LiquidVolumetricFlow;
            r.NetVelocity = r.VaporLoad / An;
            r.CapacityFactor = r.NetVelocity * Math.Sqrt(rhoV / Math.Max(rhoL - rhoV, 1e-6));

            double uf;
            if (s.FloodModel == TrayFloodModel.KisterHaas)
            {
                var csb = KisterHaasCapacityFactor(s.HoleDiameter, sp.SurfaceTension, rhoL, rhoV, s.TraySpacing,
                    s.Type == InternalType.ValveTray ? s.ValveOpenAreaFraction : s.HoleAreaFraction, r.LiquidLoad / Math.Max(lw, 1e-6), hw);
                uf = csb * Math.Sqrt((rhoL - rhoV) / rhoV);
            }
            else
            {
                uf = FairFloodingVelocity(r.FlowParameter, s.TraySpacing, rhoL, rhoV, sp.SurfaceTension);
            }
            uf *= Math.Max(0.1, Math.Min(1.0, s.SystemFactor));
            r.FloodingVelocity = uf;
            r.FloodFraction = r.NetVelocity / uf;

            // weir crest and weeping
            r.WeirCrest = WeirCrest(Lw, rhoL, lw);
            var howTurndown = WeirCrest(Lw * turndown, rhoL, lw);
            r.HoleVelocity = r.VaporLoad / Math.Max(Ah, 1e-9);
            r.WeepPointVelocity = WeepPointVelocity(hw, howTurndown, s.HoleDiameter, rhoV);
            r.WeepRatio = r.HoleVelocity / r.WeepPointVelocity;
            r.WeepRatioTurndown = r.HoleVelocity * turndown / r.WeepPointVelocity;

            // pressure drop (perforated area taken as the active area, as Towler does in the example)
            var c0 = OrificeCoefficient(s.PlateThickness / s.HoleDiameter, s.HoleAreaFraction);
            r.DryPressureDrop = DryPlateDrop(r.HoleVelocity, c0, rhoV, rhoL);
            var hr = ResidualHead(rhoL);
            r.TotalHead = r.DryPressureDrop + hw * 1000.0 + r.WeirCrest + hr;
            r.PressureDrop = HeadToPressure(r.TotalHead, rhoL);
            r.PressureDropTotal = r.PressureDrop;

            // downcomer backup
            var hap = s.DowncomerClearance > 0 ? s.DowncomerClearance : Math.Max(0.005, hw - 0.010);
            var Aap = lw * hap;
            var hdc = DowncomerHeadLoss(Lw, rhoL, Ad, Aap);
            r.DowncomerBackup = hw * 1000.0 + r.WeirCrest + r.TotalHead + hdc;
            r.DowncomerBackupLimit = 0.5 * (s.TraySpacing + hw) * 1000.0;
            r.DowncomerResidenceTime = Ad * (r.DowncomerBackup / 1000.0) * rhoL / Math.Max(Lw, 1e-9);
            r.DowncomerVelocity = r.LiquidLoad / Ad;

            // entrainment
            r.Entrainment = FairEntrainment(r.FlowParameter, r.FloodFraction);
            r.EntrainmentEfficiencyFactor = EntrainmentEfficiencyFactor(r.Entrainment, murphreeEfficiency);

            if (r.FloodFraction > 1.0) r.Warnings.Add("Entrainment flooding: the vapour velocity exceeds the flooding velocity.");
            else if (r.FloodFraction > 0.85) r.Warnings.Add("Fraction of flood above 0.85.");
            if (r.DowncomerBackup > r.DowncomerBackupLimit) r.Warnings.Add("Downcomer backup exceeds half the tray spacing plus the weir height (downcomer flooding).");
            if (r.DowncomerResidenceTime < minResidenceTime) r.Warnings.Add("Downcomer residence time below " + minResidenceTime.ToString("0.#") + " s.");
            if (r.WeepRatioTurndown < 1.0) r.Warnings.Add("Weeping at turndown: the hole velocity falls below the weep point.");
            else if (r.WeepRatio < 1.0) r.Warnings.Add("Weeping at the design rate: the hole velocity is below the weep point.");
            if (r.Entrainment > 0.1) r.Warnings.Add("Fractional entrainment above 0.1; the efficiency penalty is large.");
            if (r.WeirCrest < 10.0) r.Warnings.Add("Weir crest below 10 mm; the liquid may not flow evenly along the weir.");
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
                var r = RateSieveTray(s, sp, mid, 1.0, 0.0, 1.0);
                if (r.FloodFraction > target) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }
    }
}
