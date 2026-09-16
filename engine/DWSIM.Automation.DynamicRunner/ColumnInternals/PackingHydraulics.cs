//    Column internals rating: packed bed hydraulics and mass transfer.
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
    /// Packed bed capacity, pressure drop and efficiency:
    /// Robbins (Chem. Eng. Progr. 87(1), 19, 1991; Perry 7th ed. eqs. 14-152 to 14-159) for the pressure
    /// drop, Kister and Gill (Chem. Eng. Progr. 87(2), 32, 1991; Seader eq. 6-104) for the pressure drop
    /// at flood, Billet and Schultes (Chem. Eng. Technol. 22, 1999; Seader 2nd ed. eqs. 6-97 to 6-115 and
    /// 6-132 to 6-140) for holdup, loading, flooding, pressure drop and HTUs, Onda, Takeuchi and Okumoto
    /// (J. Chem. Eng. Japan 1, 56, 1968) for the mass transfer coefficients of random packings, Rocha,
    /// Bravo and Fair (Ind. Eng. Chem. Res. 32, 641, 1993 and 35, 1660, 1996; the equations as Kooijman and
    /// Taylor, The ChemSep Book, 2nd ed., secs. 16.1.2 and 16.2.2 list them) for the holdup, pressure drop,
    /// flooding and mass transfer of structured packings, and the rules of thumb collected by Kister
    /// (Distillation Design, 1992, sec. 9.1.5; Seader eqs. 6-116 to 6-118). SI units throughout: velocities
    /// are superficial, m/s.
    /// </summary>
    public static class PackingHydraulics
    {
        public const double g = 9.80665;
        private const double InH2OPerFtToPaPerM = 249.089 / 0.3048;   // 817.2 Pa/m
        private const double KgM2SToLbHFt2 = 737.338;
        private const double KgM3ToLbFt3 = 1.0 / 16.01846;
        private const double PerMToPerFt = 0.3048;

        // ------------------------------------------------------------------ Robbins

        /// <summary>
        /// Robbins (1991) pressure drop of an irrigated random packing, Pa per metre of bed. G and L are the
        /// superficial mass velocities (kg/m2 s), Fpd the dry packing factor (1/m), mu_L in Pa s. The
        /// general forms of Perry eqs. 14-157 to 14-159 are used: G_f = 986 F_s (F_pd/20)^0.5 10^(0.3 rho_G),
        /// L_f = L (62.4/rho_L)(F_pd/20)^0.5 mu_L^0.2 (F_pd above 200 1/ft) or L (62.4/rho_L)(20/F_pd)^0.5 mu_L^0.1,
        /// delta P = C3 G_f^2 10^(C4 L_f) + 0.4 (L_f/20000)^0.1 [C3 G_f^2 10^(C4 L_f)]^4 in inches of water
        /// per foot, with C3 = 7.4e-8 and C4 = 2.7e-5 and everything else in US units.
        /// </summary>
        public static double RobbinsPressureDrop(double gasMassVelocity, double liquidMassVelocity, double rhoV, double rhoL, double muL, double fpdPerM)
        {
            var fpd = fpdPerM * PerMToPerFt;                         // 1/ft
            var rhoGlb = rhoV * KgM3ToLbFt3;
            var rhoLlb = rhoL * KgM3ToLbFt3;
            var muLcP = muL * 1000.0;
            var uG = gasMassVelocity / rhoV;                           // m/s
            var fs = uG / 0.3048 * Math.Sqrt(rhoGlb);                  // ft/s (lb/ft3)^0.5
            var gf = 986.0 * fs * Math.Sqrt(fpd / 20.0) * Math.Pow(10.0, 0.3 * rhoGlb);
            var L = liquidMassVelocity * KgM2SToLbHFt2;
            double lf;
            if (fpd > 200.0) lf = L * (62.4 / rhoLlb) * Math.Sqrt(fpd / 20.0) * Math.Pow(muLcP, 0.2);
            else lf = L * (62.4 / rhoLlb) * Math.Sqrt(20.0 / fpd) * Math.Pow(muLcP, 0.1);
            var dpd = 7.4e-8 * gf * gf * Math.Pow(10.0, 2.7e-5 * lf);
            var dp = dpd + 0.4 * Math.Pow(lf / 20000.0, 0.1) * Math.Pow(dpd, 4);
            return dp * InH2OPerFtToPaPerM;
        }

        /// <summary>Kister and Gill pressure drop at the flood point, Pa/m: delta P_flood = 0.115 F_p^0.7 (in H2O/ft, F_p in 1/ft).</summary>
        public static double KisterGillFloodPressureDrop(double fpPerM)
        {
            return 0.115 * Math.Pow(fpPerM * PerMToPerFt, 0.7) * InH2OPerFtToPaPerM;
        }

        /// <summary>Superficial gas velocity at which the Robbins pressure drop reaches the Kister-Gill flood
        /// pressure drop, at the given liquid mass velocity, m/s.</summary>
        public static double RobbinsFloodingVelocity(double liquidMassVelocity, double rhoV, double rhoL, double muL, double fpdPerM, double fpPerM)
        {
            var target = KisterGillFloodPressureDrop(fpPerM);
            double lo = 1e-4, hi = 50.0;
            for (int i = 0; i < 100; i++)
            {
                var mid = Math.Sqrt(lo * hi);
                var dp = RobbinsPressureDrop(mid * rhoV, liquidMassVelocity, rhoV, rhoL, muL, fpdPerM);
                if (dp < target) lo = mid; else hi = mid;
                if (hi / lo < 1.0005) break;
            }
            return Math.Sqrt(lo * hi);
        }

        // ------------------------------------------------------------------ Billet and Schultes

        /// <summary>Liquid Reynolds number u_L rho_L / (a mu_L) (Seader eq. 6-98).</summary>
        public static double LiquidReynolds(double uL, double rhoL, double muL, double a) { return uL * rhoL / (a * muL); }
        /// <summary>Liquid Froude number u_L^2 a / g (Seader eq. 6-99).</summary>
        public static double LiquidFroude(double uL, double a) { return uL * uL * a / g; }

        /// <summary>Hydraulic area ratio a_h/a (Seader eqs. 6-100 and 6-101).</summary>
        public static double BilletHydraulicAreaRatio(double ch, double reL, double frL)
        {
            if (reL < 5.0) return ch * Math.Pow(reL, 0.15) * Math.Pow(frL, 0.1);
            return 0.85 * ch * Math.Pow(reL, 0.25) * Math.Pow(frL, 0.1);
        }

        /// <summary>Specific liquid holdup below the loading point, m3/m3 (Seader eq. 6-97).</summary>
        public static double BilletHoldup(double uL, double rhoL, double muL, double a, double ch)
        {
            var reL = LiquidReynolds(uL, rhoL, muL, a);
            var frL = LiquidFroude(uL, a);
            var ahOverA = BilletHydraulicAreaRatio(ch, reL, frL);
            return Math.Pow(12.0 * frL / reL, 1.0 / 3.0) * Math.Pow(ahOverA, 2.0 / 3.0);
        }

        /// <summary>
        /// Superficial vapour velocity at the loading point, m/s (Seader eqs. 6-105 to 6-108). Solved for
        /// u_V,l with the liquid velocity tied to it by the L/V mass ratio: u_L = u_V (rho_V L_M)/(rho_L V_M).
        /// </summary>
        public static double BilletLoadingVelocity(double liquidToVaporMassRatio, double rhoV, double rhoL, double muV, double muL,
            double a, double eps, double cs)
        {
            var flv = liquidToVaporMassRatio * Math.Sqrt(rhoV / rhoL);
            double ns, C;
            if (flv <= 0.4) { ns = -0.326; C = cs; }
            else { ns = -0.723; C = 0.695 * Math.Pow(muL / muV, 0.1588) * cs; }
            var psi = g / (C * C) * Math.Pow(flv * Math.Pow(muL / muV, 0.4), -2.0 * ns);
            var ratio = rhoV * liquidToVaporMassRatio / rhoL;           // u_L / u_V
            // fixed point on u_V: u_V = sqrt(g/psi) [eps/a^(1/6) - a^(1/2) xi^(1/3)] xi^(1/6) (rho_L/rho_V)^(1/2), xi = 12 mu_L u_L /(g rho_L)
            double uV = 1.0;
            for (int i = 0; i < 200; i++)
            {
                var uL = ratio * uV;
                var xi = 12.0 * muL * uL / (g * rhoL);
                var f = Math.Sqrt(g / psi) * (eps / Math.Pow(a, 1.0 / 6.0) - Math.Sqrt(a) * Math.Pow(xi, 1.0 / 3.0)) * Math.Pow(xi, 1.0 / 6.0) * Math.Sqrt(rhoL / rhoV);
                if (f <= 0) { uV *= 0.5; continue; }
                var next = 0.5 * (uV + f);
                if (Math.Abs(next - uV) < 1e-7 * Math.Max(1.0, uV)) { uV = next; break; }
                uV = next;
            }
            return uV;
        }

        /// <summary>Flooding velocity as Billet suggests for design, u_V,f = u_V,l / 0.7 (Seader eq. 6-109).</summary>
        public static double BilletFloodingVelocity(double loadingVelocity) { return loadingVelocity / 0.7; }

        /// <summary>Wall factor K_W (Seader eq. 6-111) with the effective packing diameter D_p = 6 (1 - eps)/a.</summary>
        public static double BilletWallFactor(double a, double eps, double columnDiameter)
        {
            var dp = 6.0 * (1.0 - eps) / a;
            var inv = 1.0 + (2.0 / 3.0) * (1.0 / (1.0 - eps)) * dp / Math.Max(columnDiameter, 1e-6);
            return 1.0 / inv;
        }

        /// <summary>Dry bed pressure drop, Pa/m (Seader eqs. 6-110 to 6-114).</summary>
        public static double BilletDryPressureDrop(double uV, double rhoV, double muV, double a, double eps, double cp, double columnDiameter)
        {
            var kw = BilletWallFactor(a, eps, columnDiameter);
            var dp = 6.0 * (1.0 - eps) / a;
            var reV = uV * dp * rhoV / ((1.0 - eps) * muV) * kw;
            var psi0 = cp * (64.0 / reV + 1.8 / Math.Pow(reV, 0.08));
            return psi0 * a / Math.Pow(eps, 3) * uV * uV * rhoV / 2.0 / kw;
        }

        /// <summary>Irrigated bed pressure drop below the loading point, Pa/m (Seader eq. 6-115).</summary>
        public static double BilletPressureDrop(double uV, double uL, double rhoV, double rhoL, double muV, double muL,
            double a, double eps, double ch, double cp, double columnDiameter)
        {
            var dp0 = BilletDryPressureDrop(uV, rhoV, muV, a, eps, cp, columnDiameter);
            var hL = BilletHoldup(uL, rhoL, muL, a, ch);
            var frL = LiquidFroude(uL, a);
            return dp0 * Math.Pow(eps / (eps - hL), 1.5) * Math.Exp(13300.0 / Math.Pow(a, 1.5) * Math.Sqrt(frL));
        }

        /// <summary>Phase interface to packing area ratio a_Ph/a (Seader eqs. 6-136 to 6-140).</summary>
        public static double BilletInterfaceAreaRatio(double uL, double rhoL, double muL, double sigma, double a, double eps)
        {
            var dh = 4.0 * eps / a;
            var reLh = uL * dh * rhoL / muL;
            var weLh = uL * uL * rhoL * dh / sigma;
            var frLh = uL * uL / (g * dh);
            return 1.5 * Math.Pow(a * dh, -0.5) * Math.Pow(reLh, -0.2) * Math.Pow(weLh, 0.75) * Math.Pow(frLh, -0.45);
        }

        /// <summary>Height of a liquid transfer unit H_L, m (Seader eq. 6-132).</summary>
        public static double BilletHL(double uL, double rhoL, double muL, double sigma, double DL, double a, double eps, double ch, double cl)
        {
            var hL = BilletHoldup(uL, rhoL, muL, a, ch);
            var aph = BilletInterfaceAreaRatio(uL, rhoL, muL, sigma, a, eps);
            return BilletHL(uL, DL, a, eps, cl, hL, aph);
        }

        /// <summary>H_L from a given holdup and interface area ratio (Seader eq. 6-132).</summary>
        public static double BilletHL(double uL, double DL, double a, double eps, double cl, double hL, double aPhOverA)
        {
            return 1.0 / cl * Math.Pow(1.0 / 12.0, 1.0 / 6.0) * Math.Sqrt(4.0 * hL * eps / (DL * a * uL)) * uL / a / aPhOverA;
        }

        /// <summary>Height of a gas transfer unit H_G, m (Seader eq. 6-133).</summary>
        public static double BilletHG(double uV, double uL, double rhoV, double rhoL, double muV, double muL, double sigma, double DV,
            double a, double eps, double ch, double cv)
        {
            var hL = BilletHoldup(uL, rhoL, muL, a, ch);
            var aph = BilletInterfaceAreaRatio(uL, rhoL, muL, sigma, a, eps);
            return BilletHG(uV, rhoV, muV, DV, a, eps, cv, hL, aph);
        }

        /// <summary>H_G from a given holdup and interface area ratio (Seader eq. 6-133).</summary>
        public static double BilletHG(double uV, double rhoV, double muV, double DV, double a, double eps, double cv, double hL, double aPhOverA)
        {
            var reV = uV * rhoV / (a * muV);
            var scV = muV / (rhoV * DV);
            return 1.0 / cv * Math.Sqrt(eps - hL) * Math.Sqrt(4.0 * eps / Math.Pow(a, 4)) * Math.Pow(reV, -0.75) * Math.Pow(scV, -1.0 / 3.0) * uV / (DV * aPhOverA);
        }

        // ------------------------------------------------------------------ Onda

        /// <summary>Critical surface tension of the packing material, N/m (Onda: ceramic 61, steel 75, carbon 56, PVC 40, polyethylene 33 dyn/cm).</summary>
        public static double CriticalSurfaceTension(string material)
        {
            var m = (material ?? "").ToLowerInvariant();
            if (m.Contains("ceram")) return 0.061;
            if (m.Contains("carbon") || m.Contains("graph")) return 0.056;
            if (m.Contains("plastic") || m.Contains("pvc") || m.Contains("poly")) return 0.040;
            if (m.Contains("glass")) return 0.073;
            return 0.075; // metals
        }

        /// <summary>Onda wetted area fraction a_w/a_t.</summary>
        public static double OndaWettedAreaRatio(double liquidMassVelocity, double rhoL, double muL, double sigma, double sigmaC, double at)
        {
            var L = liquidMassVelocity;
            var x = -1.45 * Math.Pow(sigmaC / sigma, 0.75) * Math.Pow(L / (at * muL), 0.1)
                    * Math.Pow(L * L * at / (rhoL * rhoL * g), -0.05) * Math.Pow(L * L / (rhoL * sigma * at), 0.2);
            return 1.0 - Math.Exp(x);
        }

        /// <summary>Onda liquid-phase mass transfer coefficient k_L, m/s.</summary>
        public static double OndaKL(double liquidMassVelocity, double rhoL, double muL, double DL, double at, double aw, double dp)
        {
            var L = liquidMassVelocity;
            return 0.0051 * Math.Pow(L / (aw * muL), 2.0 / 3.0) * Math.Pow(muL / (rhoL * DL), -0.5) * Math.Pow(at * dp, 0.4) * Math.Pow(rhoL / (muL * g), -1.0 / 3.0);
        }

        /// <summary>Onda gas-phase mass transfer coefficient in velocity form k_G' = k_G R T, m/s (C = 5.23 for packings above 15 mm, 2.0 below).</summary>
        public static double OndaKG(double gasMassVelocity, double rhoV, double muV, double DV, double at, double dp)
        {
            var G = gasMassVelocity;
            var C = dp >= 0.015 ? 5.23 : 2.0;
            return C * at * DV * Math.Pow(G / (at * muV), 0.7) * Math.Pow(muV / (rhoV * DV), 1.0 / 3.0) * Math.Pow(at * dp, -2.0);
        }

        // ------------------------------------------------------------------ Rocha, Bravo and Fair (structured packings)

        /// <summary>Pressure drop at the flood point the authors recommend when nothing better is known, Pa/m.</summary>
        public const double RbfDefaultFloodPressureDrop = 1025.0;

        /// <summary>Contact angle term: cos(gamma) = 0.9 below 0.0453 N/m, 5.211 x 10^(-16.835 sigma) above (continuous at the switch).</summary>
        public static double RbfCosGamma(double sigma) { return sigma < 0.0453 ? 0.9 : 5.211 * Math.Pow(10.0, -16.835 * sigma); }

        /// <summary>Holdup correction factor F_t of Rocha, Bravo and Fair (1993), the ratio of wetted to total packing area
        /// that also scales the effective interfacial area: F_t = 29.12 (We_L Fr_L)^0.15 S^0.359 / [Re_L^0.2 eps^0.6 (sin theta)^0.3 (1 - 0.93 cos gamma)].</summary>
        public static double RbfHoldupFactor(double uL, double rhoL, double muL, double sigma, double S, double eps, double thetaDeg)
        {
            var we = uL * uL * rhoL * S / sigma;
            var fr = uL * uL / (S * g);
            var re = uL * S * rhoL / muL;
            var sin = Math.Sin(thetaDeg * Math.PI / 180.0);
            return 29.12 * Math.Pow(we * fr, 0.15) * Math.Pow(S, 0.359) / (Math.Pow(re, 0.2) * Math.Pow(eps, 0.6) * Math.Pow(sin, 0.3) * (1.0 - 0.93 * RbfCosGamma(sigma)));
        }

        /// <summary>Liquid holdup at an effective gravity, m3/m3: h_t = (4 F_t/S)^(2/3) [3 mu_L u_L/(rho_L sin theta eps g_eff)]^(1/3).</summary>
        public static double RbfHoldup(double ft, double uL, double rhoL, double muL, double S, double eps, double thetaDeg, double gEff)
        {
            var sin = Math.Sin(thetaDeg * Math.PI / 180.0);
            return Math.Pow(4.0 * ft / S, 2.0 / 3.0) * Math.Pow(3.0 * muL * uL / (rhoL * sin * eps * Math.Max(gEff, 1e-3)), 1.0 / 3.0);
        }

        /// <summary>Dry bed pressure drop, Pa/m: [0.177 rho_V/(S eps^2 sin^2 theta)] u_V^2 + [88.774 mu_V/(S^2 eps sin theta)] u_V.</summary>
        public static double RbfDryPressureDrop(double uV, double rhoV, double muV, double S, double eps, double thetaDeg)
        {
            var sin = Math.Sin(thetaDeg * Math.PI / 180.0);
            var A = 0.177 * rhoV / (S * eps * eps * sin * sin);
            var B = 88.774 * muV / (S * S * eps * sin);
            return A * uV * uV + B * uV;
        }

        /// <summary>
        /// Irrigated pressure drop of the Rocha-Bravo-Fair model, Pa/m, with the holdup that goes with it: dP/dz =
        /// dP_dry/(1 - K2 h_t)^5, K2 = 0.614 + 71.35 S, the holdup evaluated at g_eff = g (rho_L - rho_V)/rho_L
        /// (1 - dP/dP_flood). The two are solved together; NaN when no solution exists below the flood pressure drop
        /// (the bed floods at this vapour rate).
        /// </summary>
        public static double RbfPressureDrop(double uV, double uL, double rhoV, double rhoL, double muV, double muL, double sigma,
            double S, double eps, double thetaDeg, double dpFlood, out double holdup)
        {
            var dry = RbfDryPressureDrop(uV, rhoV, muV, S, eps, thetaDeg);
            var k2 = 0.614 + 71.35 * S;
            var ft = RbfHoldupFactor(uL, rhoL, muL, sigma, S, eps, thetaDeg);
            var g0 = g * (rhoL - rhoV) / rhoL;
            Func<double, double> h = dp => RbfHoldup(ft, uL, rhoL, muL, S, eps, thetaDeg, g0 * Math.Max(1e-3, 1.0 - dp / dpFlood));
            Func<double, double> f = dp => dry / Math.Pow(Math.Max(1e-6, 1.0 - k2 * h(dp)), 5) - dp;
            // f starts positive at the dry drop, dips as dP outruns the holdup term and climbs back to infinity as
            // g_eff vanishes at the flood pressure drop: the operating point is the first root, and the bed floods
            // when f never crosses zero
            if (dry >= dpFlood) { holdup = h(0.999 * dpFlood); return double.NaN; }
            const int steps = 400;
            double lo = dry, hi = double.NaN;
            for (int i = 1; i <= steps; i++)
            {
                var x = dry + (0.999 * dpFlood - dry) * i / steps;
                if (f(x) <= 0) { hi = x; break; }
                lo = x;
            }
            if (double.IsNaN(hi)) { holdup = h(0.999 * dpFlood); return double.NaN; }
            for (int i = 0; i < 60; i++)
            {
                var mid = 0.5 * (lo + hi);
                if (f(mid) > 0) lo = mid; else hi = mid;
            }
            var dpOut = 0.5 * (lo + hi);
            holdup = h(dpOut);
            return dpOut;
        }

        /// <summary>Superficial vapour velocity at which the Rocha-Bravo-Fair pressure drop reaches the flood pressure drop,
        /// the liquid tied to the vapour by the L/V mass ratio, m/s.</summary>
        public static double RbfFloodingVelocity(double liquidToVaporMassRatio, double rhoV, double rhoL, double muV, double muL, double sigma,
            double S, double eps, double thetaDeg, double dpFlood)
        {
            var ratio = rhoV * liquidToVaporMassRatio / rhoL;   // u_L / u_V
            double lo = 1e-3, hi = 30.0, hold;
            if (!double.IsNaN(RbfPressureDrop(hi, ratio * hi, rhoV, rhoL, muV, muL, sigma, S, eps, thetaDeg, dpFlood, out hold))) return hi;
            for (int i = 0; i < 60; i++)
            {
                var mid = Math.Sqrt(lo * hi);
                var dp = RbfPressureDrop(mid, ratio * mid, rhoV, rhoL, muV, muL, sigma, S, eps, thetaDeg, dpFlood, out hold);
                if (double.IsNaN(dp)) hi = mid; else lo = mid;
                if (hi / lo < 1.0005) break;
            }
            return Math.Sqrt(lo * hi);
        }

        /// <summary>
        /// Rocha, Bravo and Fair (1996) heights of a gas and a liquid transfer unit, m, from the effective velocities
        /// u_Le = u_L/(eps h_t sin theta) and u_Ge = u_V/(eps (1 - h_t) sin theta): k_G = 0.054 (D_G/S) Re_G^0.8 Sc_G^0.33
        /// with Re_G on u_Ge + u_Le, k_L = 2 sqrt(D_L C_E u_Le/(pi S)) with C_E = 0.9, and a_e = F_SE F_t a_p.
        /// </summary>
        public static void RbfMassTransfer(double uV, double uL, double holdup, double ft, double rhoV, double muV, double DV, double DL,
            double S, double eps, double thetaDeg, double aP, double fse, out double HG, out double HL, out double aeOverAp)
        {
            var sin = Math.Sin(thetaDeg * Math.PI / 180.0);
            var hL = Math.Max(1e-4, Math.Min(0.9 * eps, holdup));
            var uLe = uL / (eps * hL * sin);
            var uGe = uV / (eps * (1.0 - hL) * sin);
            var reG = (uGe + uLe) * rhoV * S / muV;
            var scG = muV / (rhoV * DV);
            var kG = 0.054 * (DV / S) * Math.Pow(reG, 0.8) * Math.Pow(scG, 0.33);
            var kL = 2.0 * Math.Sqrt(DL * 0.9 * uLe / (Math.PI * S));
            aeOverAp = fse * ft;
            var ae = aeOverAp * aP;
            HG = uV / (kG * ae);
            HL = uL / (kL * ae);
        }

        // ------------------------------------------------------------------ HETP

        /// <summary>HETP from H_OG and the stripping factor: HETP = H_OG ln(lambda)/(lambda - 1) (Seader eq. 6-94).</summary>
        public static double HetpFromHOG(double hog, double lambda)
        {
            if (lambda <= 0 || double.IsNaN(lambda) || Math.Abs(lambda - 1.0) < 1e-6) return hog;
            return hog * Math.Log(lambda) / (lambda - 1.0);
        }

        /// <summary>Rule-of-thumb HETP, m: Porter and Jenkins / Kister, HETP = 1.5 d_p for random packings (Seader
        /// eq. 6-116), HETP (ft) = 100/a (ft2/ft3) + 4/12 for structured packings (eq. 6-117), and at least the
        /// column diameter below 2 ft (eq. 6-118).</summary>
        public static double RuleOfThumbHETP(PackingData p, double columnDiameter)
        {
            double hetp;
            if (p.Structured) hetp = 0.3048 * (100.0 / (p.a * 0.3048) + 4.0 / 12.0);
            else hetp = 1.5 * Math.Max(p.NominalSize, 0.0);
            if (hetp <= 0) hetp = 0.5;
            if (columnDiameter < 0.6096) hetp = Math.Max(hetp, Math.Max(columnDiameter, 0.3048));
            return hetp;
        }

        /// <summary>Minimum superficial liquid velocity for wetting, m/s (Seader sec. 6.8: ceramic 0.00015, oxidized metal 0.0003, bright metal 0.0009, plastic 0.0012).</summary>
        public static double MinimumWettingVelocity(string material)
        {
            var m = (material ?? "").ToLowerInvariant();
            if (m.Contains("ceram")) return 0.00015;
            if (m.Contains("plastic") || m.Contains("pvc") || m.Contains("poly")) return 0.0012;
            if (m.Contains("carbon")) return 0.0003;
            return 0.0009;
        }

        // ------------------------------------------------------------------ full rating of one stage

        /// <summary>Rates one packed stage (one theoretical stage's worth of bed) at the stage conditions.</summary>
        public static StageRating RatePacking(InternalsSection s, PackingData p, StageProperties sp, double diameter)
        {
            var r = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = diameter };
            var rhoL = sp.LiquidDensity; var rhoV = sp.VaporDensity;
            var muL = sp.LiquidViscosity; var muV = sp.VaporViscosity;
            var Ac = Math.PI * diameter * diameter / 4.0;
            var G = sp.VaporMassFlow / Ac;
            var L = sp.LiquidMassFlow / Ac;
            var uV = G / rhoV;
            var uL = L / rhoL;

            r.VaporLoad = sp.VaporVolumetricFlow;
            r.LiquidLoad = sp.LiquidVolumetricFlow;
            r.FlowParameter = TrayHydraulics.FlowParameter(sp.LiquidMassFlow, sp.VaporMassFlow, rhoL, rhoV);
            r.NetVelocity = uV;
            r.FFactor = uV * Math.Sqrt(rhoV);
            r.CapacityFactor = uV * Math.Sqrt(rhoV / Math.Max(rhoL - rhoV, 1e-6));

            // the models that fit the packing: Rocha-Bravo-Fair is for corrugated sheets, Onda for dumped pieces
            var packModel = s.PackingModel;
            var hetpModel = s.HetpModel;
            var S = p.EffectiveCorrugationSide;
            bool corrugated = p.Structured && !double.IsNaN(S) && S > 0 && !double.IsNaN(p.Epsilon) && !double.IsNaN(p.a);
            if (packModel == PackingModel.RochaBravoFair && !corrugated)
            {
                r.Warnings.Add(p.Structured ? "The packing has no corrugation geometry; the Robbins / Kister-Gill route was used." : "Rocha-Bravo-Fair is a structured packing model; the Robbins / Kister-Gill route was used for this random packing.");
                packModel = PackingModel.RobbinsKisterGill;
            }
            if (hetpModel == HetpModel.RochaBravoFair && !corrugated)
            {
                r.Warnings.Add(p.Structured ? "The packing has no corrugation geometry for the Rocha-Bravo-Fair HETP." : "Rocha-Bravo-Fair is a structured packing model; Onda was used for this random packing.");
                hetpModel = p.Structured ? HetpModel.RuleOfThumb : HetpModel.Onda;
            }
            if (hetpModel == HetpModel.Onda && corrugated) hetpModel = HetpModel.RochaBravoFair;
            var dpFlood = !double.IsNaN(p.Fp) ? KisterGillFloodPressureDrop(p.Fp) : RbfDefaultFloodPressureDrop;
            double rbfHoldup = double.NaN, rbfFt = double.NaN;

            bool billet = packModel == PackingModel.BilletSchultes && p.HasBilletHydraulics && !double.IsNaN(p.a) && !double.IsNaN(p.Epsilon);
            if (packModel == PackingModel.BilletSchultes && !billet)
                r.Warnings.Add("No Billet-Schultes constants for this packing; the Robbins / Kister-Gill route was used.");

            if (packModel == PackingModel.RochaBravoFair)
            {
                var dp = RbfPressureDrop(uV, uL, rhoV, rhoL, muV, muL, sp.SurfaceTension, S, p.Epsilon, p.CorrugationAngle, dpFlood, out rbfHoldup);
                rbfFt = RbfHoldupFactor(uL, rhoL, muL, sp.SurfaceTension, S, p.Epsilon, p.CorrugationAngle);
                r.FloodingVelocity = RbfFloodingVelocity(sp.LiquidMassFlow / Math.Max(sp.VaporMassFlow, 1e-12), rhoV, rhoL, muV, muL, sp.SurfaceTension, S, p.Epsilon, p.CorrugationAngle, dpFlood);
                r.FloodFraction = uV / r.FloodingVelocity;
                r.LiquidHoldup = rbfHoldup;
                if (double.IsNaN(dp)) { r.PressureDrop = dpFlood; r.Warnings.Add("The bed floods at this vapour rate (no Rocha-Bravo-Fair solution below the flood pressure drop " + dpFlood.ToString("0") + " Pa/m)."); }
                else r.PressureDrop = dp;
            }
            else if (billet)
            {
                var uVl = BilletLoadingVelocity(sp.LiquidMassFlow / Math.Max(sp.VaporMassFlow, 1e-12), rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Cs);
                r.LoadingVelocity = uVl;
                r.FloodingVelocity = BilletFloodingVelocity(uVl);
                r.FloodFraction = uV / r.FloodingVelocity;
                r.LiquidHoldup = BilletHoldup(uL, rhoL, muL, p.a, p.Ch);
                r.PressureDrop = BilletPressureDrop(uV, uL, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Ch, p.Cp, diameter);
                if (uV > uVl) r.Warnings.Add("Above the loading point; the Billet-Schultes pressure drop is a preloading-region value.");
            }
            else
            {
                var fpd = !double.IsNaN(p.Fpd) ? p.Fpd : p.Fp;
                var fp = !double.IsNaN(p.Fp) ? p.Fp : p.Fpd;
                if (double.IsNaN(fpd) || double.IsNaN(fp))
                {
                    r.Warnings.Add("The packing has no packing factor; capacity and pressure drop could not be rated.");
                    r.FloodingVelocity = double.NaN; r.FloodFraction = double.NaN; r.PressureDrop = double.NaN;
                }
                else
                {
                    r.FloodingVelocity = RobbinsFloodingVelocity(L, rhoV, rhoL, muL, fpd, fp);
                    r.FloodFraction = uV / r.FloodingVelocity;
                    r.PressureDrop = RobbinsPressureDrop(G, L, rhoV, rhoL, muL, fpd);
                    if (!double.IsNaN(p.a) && !double.IsNaN(p.Ch) && !double.IsNaN(p.Epsilon))
                        r.LiquidHoldup = BilletHoldup(uL, rhoL, muL, p.a, p.Ch);
                }
            }

            // wetting
            var uLmin = MinimumWettingVelocity(p.Material);
            r.WettingRatio = uL / uLmin;
            if (r.WettingRatio < 1.0) r.Warnings.Add("Liquid rate below the minimum wetting rate of the packing material.");

            // efficiency
            r.HETPRuleOfThumb = RuleOfThumbHETP(p, diameter);
            var DL = s.LiquidDiffusivity > 0 ? s.LiquidDiffusivity : sp.LiquidDiffusivity;
            var DV = s.VapourDiffusivity > 0 ? s.VapourDiffusivity : sp.VaporDiffusivity;
            var lambda = sp.StrippingFactor;
            bool haveProps = DL > 0 && DV > 0 && !double.IsNaN(p.a) && sp.SurfaceTension > 0;
            if (hetpModel == HetpModel.RochaBravoFair && corrugated && haveProps)
            {
                if (double.IsNaN(rbfHoldup))
                {
                    RbfPressureDrop(uV, uL, rhoV, rhoL, muV, muL, sp.SurfaceTension, S, p.Epsilon, p.CorrugationAngle, dpFlood, out rbfHoldup);
                    rbfFt = RbfHoldupFactor(uL, rhoL, muL, sp.SurfaceTension, S, p.Epsilon, p.CorrugationAngle);
                }
                double hg, hl, ae;
                RbfMassTransfer(uV, uL, rbfHoldup, rbfFt, rhoV, muV, DV, DL, S, p.Epsilon, p.CorrugationAngle, p.a, p.SurfaceEnhancement, out hg, out hl, out ae);
                r.HG = hg; r.HL = hl;
                r.HOG = r.HG + lambda * r.HL;
                r.HETP = HetpFromHOG(r.HOG, lambda);
            }
            else if (hetpModel == HetpModel.BilletSchultes && p.HasBilletMassTransfer && p.HasBilletHydraulics && haveProps && !double.IsNaN(p.Epsilon))
            {
                r.HL = BilletHL(uL, rhoL, muL, sp.SurfaceTension, DL, p.a, p.Epsilon, p.Ch, p.CL);
                r.HG = BilletHG(uV, uL, rhoV, rhoL, muV, muL, sp.SurfaceTension, DV, p.a, p.Epsilon, p.Ch, p.CV);
                r.HOG = r.HG + lambda * r.HL;
                r.HETP = HetpFromHOG(r.HOG, lambda);
            }
            else if ((hetpModel == HetpModel.Onda || hetpModel == HetpModel.BilletSchultes) && haveProps && !p.Structured && p.NominalSize > 0)
            {
                if (hetpModel == HetpModel.BilletSchultes) r.Warnings.Add("No Billet-Schultes mass transfer constants; Onda was used for the HETP.");
                var aw = p.a * OndaWettedAreaRatio(L, rhoL, muL, sp.SurfaceTension, CriticalSurfaceTension(p.Material), p.a);
                var kL = OndaKL(L, rhoL, muL, DL, p.a, aw, p.NominalSize);
                var kG = OndaKG(G, rhoV, muV, DV, p.a, p.NominalSize);
                r.HL = uL / (kL * aw);
                r.HG = uV / (kG * aw);
                r.HOG = r.HG + lambda * r.HL;
                r.HETP = HetpFromHOG(r.HOG, lambda);
            }
            else
            {
                if (hetpModel != HetpModel.RuleOfThumb) r.Warnings.Add("HETP from the rule of thumb: the packing or the stage lacks the data the mass transfer model needs.");
                r.HETP = r.HETPRuleOfThumb;
            }
            if (r.HETP <= 0 || double.IsNaN(r.HETP)) r.HETP = r.HETPRuleOfThumb;
            r.PressureDropTotal = r.PressureDrop * r.HETP;

            if (r.FloodFraction > 1.0) r.Warnings.Add("Flooding: the vapour velocity exceeds the flooding velocity.");
            else if (r.FloodFraction > 0.8) r.Warnings.Add("Fraction of flood above 0.8.");
            return r;
        }

        /// <summary>Diameter that puts the packed stage at the target fraction of flood, m.</summary>
        public static double DiameterForFloodFraction(InternalsSection s, PackingData p, StageProperties sp, double target)
        {
            double lo = 0.05, hi = 30.0;
            for (int i = 0; i < 80; i++)
            {
                var mid = 0.5 * (lo + hi);
                var r = RatePacking(s, p, sp, mid);
                if (double.IsNaN(r.FloodFraction)) return double.NaN;
                if (r.FloodFraction > target) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }
    }
}
