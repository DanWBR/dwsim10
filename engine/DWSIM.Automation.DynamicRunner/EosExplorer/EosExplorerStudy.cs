//    Equation of state explorer: P-V isotherms, Maxwell construction and Z of a pure compound.
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Automation.DynamicRunner.EosExplorer
{
    /// <summary>The cubic equations of state the explorer draws, in the generic form P = RT/(V - b) - a(T)/((V + eps b)(V + sigma b)).</summary>
    public enum CubicEos
    {
        VanDerWaals = 0,
        RedlichKwong = 1,
        SoaveRedlichKwong = 2,
        PengRobinson = 3
    }

    public sealed class EosExplorerInput
    {
        public const string FileExtension = ".dweos";

        /// <summary>Name of the compound in the host flowsheet.</summary>
        public string CompoundName = "";
        public CubicEos Equation = CubicEos.PengRobinson;
        /// <summary>Isotherm temperatures in K, separated by ";". Empty draws 0.8, 0.9, 1.0 and 1.1 times Tc.</summary>
        public string Temperatures = "";
        /// <summary>The isotherms run from just above b up to this many critical volumes.</summary>
        public double VolumeRangeInCriticalVolumes = 8.0;
        /// <summary>Points per isotherm.</summary>
        public int Points = 300;

        public void CopyFrom(EosExplorerInput other)
        {
            foreach (var f in typeof(EosExplorerInput).GetFields(BindingFlags.Public | BindingFlags.Instance))
                f.SetValue(this, f.GetValue(other));
        }

        public void SaveToFile(string path) { McCabeThiele.CaseFile.Save(this, path, "EosExplorerCase"); }

        public static EosExplorerInput LoadFromFile(string path)
        {
            var input = new EosExplorerInput();
            McCabeThiele.CaseFile.Load(input, path, "EosExplorerCase", "equation of state explorer case file");
            return input;
        }

        /// <summary>The isotherm temperatures, parsed; the defaults around Tc when the text is empty.</summary>
        public List<double> TemperatureList(double tc)
        {
            var list = new List<double>();
            foreach (var part in (Temperatures ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double t;
                if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out t) && t > 0) list.Add(t);
            }
            if (list.Count == 0) list.AddRange(new[] { 0.8 * tc, 0.9 * tc, tc, 1.1 * tc });
            return list.Distinct().OrderBy(t => t).ToList();
        }
    }

    /// <summary>One isotherm: the P(V) curve, and where the equation says it condenses.</summary>
    public sealed class EosIsotherm
    {
        public double Temperature;
        public double ReducedTemperature;
        /// <summary>Molar volume, m3/mol.</summary>
        public double[] Volume = new double[0];
        /// <summary>Pressure along the isotherm, Pa. Below the critical temperature it carries the van der Waals loop.</summary>
        public double[] Pressure = new double[0];
        /// <summary>Compressibility factor against pressure, the stable root at each pressure.</summary>
        public double[] ZPressure = new double[0];
        public double[] Z = new double[0];

        /// <summary>Saturation pressure by equal fugacity (the Maxwell construction), Pa; NaN above Tc.</summary>
        public double SaturationPressure = double.NaN;
        /// <summary>Saturation pressure from the compound database correlation, Pa; NaN above Tc.</summary>
        public double DatabaseSaturationPressure = double.NaN;
        public double LiquidVolume = double.NaN, VaporVolume = double.NaN;
        public double LiquidZ = double.NaN, VaporZ = double.NaN;
        public bool Subcritical;
    }

    public sealed class EosExplorerResult
    {
        public string CompoundName = "";
        public CubicEos Equation;
        public double Tc, Pc, Omega;
        /// <summary>Critical volume, m3/mol, and Zc from the database.</summary>
        public double DatabaseVc = double.NaN, DatabaseZc = double.NaN;
        /// <summary>What the equation gives at the critical point: Zc is a constant of the equation, Vc = Zc R Tc / Pc.</summary>
        public double EquationZc, EquationVc;
        /// <summary>a and b of the equation at Tc, in Pa m6/mol2 and m3/mol.</summary>
        public double A, B;
        public List<EosIsotherm> Isotherms = new List<EosIsotherm>();
        /// <summary>The saturation curve Psat(T) from the triple region up to Tc, equation against database.</summary>
        public double[] SaturationT = new double[0], SaturationP = new double[0], SaturationPDatabase = new double[0];
        public List<string> Warnings = new List<string>();
        public string TextReport = "";
    }

    /// <summary>
    /// Draws what a cubic equation of state says about one compound: the P-V isotherms with their
    /// loops below Tc, the flat line the Maxwell construction puts across the loop, the
    /// compressibility factor against pressure, and how the saturation pressure the equation
    /// predicts compares with the compound's own vapour pressure correlation.
    /// </summary>
    /// <remarks>
    /// The equation is evaluated here, in its textbook form, from the critical constants and the
    /// acentric factor of the compound. Nothing goes through the property package except the
    /// database vapour pressure used for the comparison, so a student sees the equation itself and
    /// not the machinery around it.
    /// </remarks>
    public static class EosExplorerStudy
    {
        public const double R = 8.314462618;

        public static EosExplorerResult Run(IFlowsheet host, EosExplorerInput input,
            Action<double> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (input == null) throw new ArgumentNullException(nameof(input));
            ICompoundConstantProperties cp;
            if (string.IsNullOrEmpty(input.CompoundName) || !host.SelectedCompounds.TryGetValue(input.CompoundName, out cp))
                throw new ArgumentException("Pick a compound of the flowsheet.");
            if (cp.Critical_Temperature <= 0 || cp.Critical_Pressure <= 0)
                throw new ArgumentException("'" + input.CompoundName + "' has no critical constants; a cubic equation needs Tc and Pc.");
            if (input.Points < 50) input.Points = 50;
            if (input.VolumeRangeInCriticalVolumes < 2) input.VolumeRangeInCriticalVolumes = 2;

            var r = new EosExplorerResult
            {
                CompoundName = cp.Name,
                Equation = input.Equation,
                Tc = cp.Critical_Temperature,
                Pc = cp.Critical_Pressure,
                Omega = cp.Acentric_Factor
            };
            if (cp.Critical_Volume > 0) r.DatabaseVc = cp.Critical_Volume / 1000.0;   // the database keeps it in m3/kmol
            if (cp.Critical_Compressibility > 0) r.DatabaseZc = cp.Critical_Compressibility;

            var eos = new Cubic(input.Equation, r.Tc, r.Pc, r.Omega);
            r.EquationZc = eos.Zc;
            r.EquationVc = eos.Zc * R * r.Tc / r.Pc;
            r.A = eos.AOf(r.Tc);
            r.B = eos.B;

            // the database correlation, through any property package: it only needs the compound's constants
            PropertyPackage pp = host.PropertyPackages.Values.FirstOrDefault() as PropertyPackage;
            Func<double, double> psatDb = t =>
            {
                if (pp == null) return double.NaN;
                try { var p = pp.AUX_PVAPi(cp, t); return p > 0 ? p : double.NaN; } catch (Exception) { return double.NaN; }
            };

            var temps = input.TemperatureList(r.Tc);
            double vMin = 1.02 * eos.B, vMax = input.VolumeRangeInCriticalVolumes * r.EquationVc;
            int n = input.Points;
            int done = 0;
            foreach (var T in temps)
            {
                cancellation.ThrowIfCancellationRequested();
                var iso = new EosIsotherm { Temperature = T, ReducedTemperature = T / r.Tc, Subcritical = T < r.Tc };
                var vs = new double[n]; var ps = new double[n];
                double logMin = Math.Log(vMin), logMax = Math.Log(vMax);
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Exp(logMin + (logMax - logMin) * i / (n - 1));
                    vs[i] = v;
                    ps[i] = eos.P(T, v);
                }
                iso.Volume = vs; iso.Pressure = ps;

                if (iso.Subcritical)
                {
                    double psat, zl, zv;
                    if (eos.Saturation(T, out psat, out zl, out zv))
                    {
                        iso.SaturationPressure = psat;
                        iso.LiquidZ = zl; iso.VaporZ = zv;
                        iso.LiquidVolume = zl * R * T / psat;
                        iso.VaporVolume = zv * R * T / psat;
                    }
                    else r.Warnings.Add("No saturation pressure could be found at " + T.ToString("F2", CultureInfo.InvariantCulture) + " K.");
                    iso.DatabaseSaturationPressure = psatDb(T);
                }

                // Z against P: the stable root, liquid above Psat and vapour below it
                int m = 120;
                var zp = new double[m]; var zz = new double[m];
                double pMax = Math.Max(2.0 * r.Pc, iso.Subcritical && !double.IsNaN(iso.SaturationPressure) ? 3.0 * iso.SaturationPressure : 0);
                for (int i = 0; i < m; i++)
                {
                    double p = pMax * (i + 1) / m;
                    zp[i] = p;
                    zz[i] = eos.StableZ(T, p, iso.SaturationPressure);
                }
                iso.ZPressure = zp; iso.Z = zz;

                r.Isotherms.Add(iso);
                done++;
                if (progress != null) progress(0.7 * done / temps.Count);
            }

            // saturation curve, equation against database, from 0.45 Tc to Tc
            var st = new List<double>(); var sp = new List<double>(); var sd = new List<double>();
            int k = 40;
            for (int i = 0; i <= k; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double T = r.Tc * (0.45 + 0.55 * i / k);
                double psat, zl, zv;
                if (T >= r.Tc) { st.Add(T); sp.Add(r.Pc); sd.Add(psatDb(T)); continue; }
                if (eos.Saturation(T, out psat, out zl, out zv)) { st.Add(T); sp.Add(psat); sd.Add(psatDb(T)); }
                if (progress != null) progress(0.7 + 0.3 * i / k);
            }
            r.SaturationT = st.ToArray(); r.SaturationP = sp.ToArray(); r.SaturationPDatabase = sd.ToArray();

            r.TextReport = Report(r);
            return r;
        }

        // ------------------------------------------------------------------ the equation

        /// <summary>A two-parameter cubic in its generic form, from Smith, Van Ness and Abbott's table.</summary>
        public sealed class Cubic
        {
            private readonly CubicEos _kind;
            private readonly double _tc, _pc, _omega;
            public readonly double Sigma, Epsilon, OmegaB, Psi, Zc, B;

            public Cubic(CubicEos kind, double tc, double pc, double omega)
            {
                _kind = kind; _tc = tc; _pc = pc; _omega = omega;
                switch (kind)
                {
                    case CubicEos.VanDerWaals: Sigma = 0; Epsilon = 0; OmegaB = 1.0 / 8.0; Psi = 27.0 / 64.0; Zc = 3.0 / 8.0; break;
                    case CubicEos.RedlichKwong: Sigma = 1; Epsilon = 0; OmegaB = 0.08664; Psi = 0.42748; Zc = 1.0 / 3.0; break;
                    case CubicEos.SoaveRedlichKwong: Sigma = 1; Epsilon = 0; OmegaB = 0.08664; Psi = 0.42748; Zc = 1.0 / 3.0; break;
                    default: Sigma = 1 + Math.Sqrt(2); Epsilon = 1 - Math.Sqrt(2); OmegaB = 0.07780; Psi = 0.45724; Zc = 0.30740; break;
                }
                B = OmegaB * R * tc / pc;
            }

            /// <summary>The temperature function alpha(Tr).</summary>
            public double Alpha(double T)
            {
                double tr = T / _tc;
                switch (_kind)
                {
                    case CubicEos.VanDerWaals: return 1.0;
                    case CubicEos.RedlichKwong: return 1.0 / Math.Sqrt(tr);
                    case CubicEos.SoaveRedlichKwong:
                        {
                            double m = 0.480 + 1.574 * _omega - 0.176 * _omega * _omega;
                            double s = 1 + m * (1 - Math.Sqrt(tr));
                            return s * s;
                        }
                    default:
                        {
                            double m = 0.37464 + 1.54226 * _omega - 0.26992 * _omega * _omega;
                            double s = 1 + m * (1 - Math.Sqrt(tr));
                            return s * s;
                        }
                }
            }

            public double AOf(double T) { return Psi * Alpha(T) * R * R * _tc * _tc / _pc; }

            public double P(double T, double v)
            {
                double a = AOf(T);
                return R * T / (v - B) - a / ((v + Epsilon * B) * (v + Sigma * B));
            }

            /// <summary>The real roots of the cubic in Z at T and P, ascending.</summary>
            public double[] ZRoots(double T, double P)
            {
                double A = AOf(T) * P / (R * T * R * T);
                double Bd = B * P / (R * T);
                double es = Epsilon + Sigma, ep = Epsilon * Sigma;
                double c2 = -(1 + Bd - es * Bd);
                double c1 = A + ep * Bd * Bd - es * Bd * (1 + Bd);
                double c0 = -(A * Bd + ep * Bd * Bd * (1 + Bd));
                return RealRoots(c2, c1, c0).Where(z => z > Bd).OrderBy(z => z).ToArray();
            }

            /// <summary>ln of the fugacity coefficient of the pure compound at a root Z.</summary>
            public double LnPhi(double T, double P, double Z)
            {
                double A = AOf(T) * P / (R * T * R * T);
                double Bd = B * P / (R * T);
                if (Math.Abs(Sigma - Epsilon) < 1e-12)
                    return Z - 1 - Math.Log(Z - Bd) - A / Z;
                return Z - 1 - Math.Log(Z - Bd) - A / (Bd * (Sigma - Epsilon)) * Math.Log((Z + Sigma * Bd) / (Z + Epsilon * Bd));
            }

            /// <summary>
            /// The saturation pressure at T by equal fugacity of the liquid and vapour roots, by
            /// successive substitution from Wilson's estimate; false when T is at or above Tc or no
            /// two-root pressure exists.
            /// </summary>
            public bool Saturation(double T, out double psat, out double zl, out double zv)
            {
                psat = double.NaN; zl = zv = double.NaN;
                if (T >= _tc) return false;
                double p = _pc * Math.Exp(5.373 * (1 + _omega) * (1 - _tc / T));
                p = Math.Max(1.0, Math.Min(p, 0.999 * _pc));
                for (int it = 0; it < 200; it++)
                {
                    var roots = ZRoots(T, p);
                    if (roots.Length < 2)
                    {
                        // one root: a vapour-like root means P is too low, a liquid-like one too high
                        double z = roots.Length == 1 ? roots[0] : double.NaN;
                        if (double.IsNaN(z)) return false;
                        double vlike = z * R * T / p;
                        if (vlike > 3 * B) p *= 1.5; else p /= 1.5;
                        if (p >= _pc) p = 0.999 * _pc;
                        continue;
                    }
                    double zLiq = roots[0], zVap = roots[roots.Length - 1];
                    double ratio = Math.Exp(LnPhi(T, p, zLiq) - LnPhi(T, p, zVap));
                    double pNew = p * ratio;
                    if (double.IsNaN(pNew) || pNew <= 0) return false;
                    if (Math.Abs(pNew - p) / p < 1e-9)
                    {
                        psat = pNew; zl = zLiq; zv = zVap;
                        return true;
                    }
                    p = pNew;
                }
                return false;
            }

            /// <summary>The root a stable phase takes at T and P: liquid above the saturation pressure, vapour below, the single root when there is one.</summary>
            public double StableZ(double T, double P, double psat)
            {
                var roots = ZRoots(T, P);
                if (roots.Length == 0) return double.NaN;
                if (roots.Length == 1) return roots[0];
                if (double.IsNaN(psat)) return roots[roots.Length - 1];
                return P > psat ? roots[0] : roots[roots.Length - 1];
            }

            /// <summary>Real roots of z^3 + c2 z^2 + c1 z + c0 = 0.</summary>
            private static double[] RealRoots(double c2, double c1, double c0)
            {
                double q = (3 * c1 - c2 * c2) / 9.0;
                double rr = (9 * c2 * c1 - 27 * c0 - 2 * c2 * c2 * c2) / 54.0;
                double disc = q * q * q + rr * rr;
                double shift = -c2 / 3.0;
                if (disc > 0)
                {
                    double s = Cbrt(rr + Math.Sqrt(disc)), t = Cbrt(rr - Math.Sqrt(disc));
                    return new[] { shift + s + t };
                }
                if (Math.Abs(disc) < 1e-30 && Math.Abs(q) < 1e-30) return new[] { shift };
                double theta = Math.Acos(Math.Max(-1, Math.Min(1, rr / Math.Sqrt(-q * q * q))));
                double m = 2 * Math.Sqrt(-q);
                return new[]
                {
                    shift + m * Math.Cos(theta / 3),
                    shift + m * Math.Cos((theta + 2 * Math.PI) / 3),
                    shift + m * Math.Cos((theta + 4 * Math.PI) / 3)
                };
            }

            private static double Cbrt(double x) { return x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0); }
        }

        // ------------------------------------------------------------------ report

        public static string EquationName(CubicEos eos)
        {
            switch (eos)
            {
                case CubicEos.VanDerWaals: return "van der Waals";
                case CubicEos.RedlichKwong: return "Redlich-Kwong";
                case CubicEos.SoaveRedlichKwong: return "Soave-Redlich-Kwong";
                default: return "Peng-Robinson";
            }
        }

        private static string Report(EosExplorerResult r)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("EQUATION OF STATE EXPLORER: " + r.CompoundName + ", " + EquationName(r.Equation));
            sb.AppendLine("Tc = " + r.Tc.ToString("F2", ci) + " K, Pc = " + (r.Pc / 1e5).ToString("F3", ci) + " bar, omega = " + r.Omega.ToString("F4", ci));
            sb.AppendLine("b = " + r.B.ToString("E4", ci) + " m3/mol, a(Tc) = " + r.A.ToString("E4", ci) + " Pa m6/mol2");
            sb.AppendLine("Zc of the equation = " + r.EquationZc.ToString("F4", ci) + (double.IsNaN(r.DatabaseZc) ? "" : ", database Zc = " + r.DatabaseZc.ToString("F4", ci)));
            sb.AppendLine("Vc of the equation = " + (r.EquationVc * 1e6).ToString("F1", ci) + " cm3/mol" + (double.IsNaN(r.DatabaseVc) ? "" : ", database Vc = " + (r.DatabaseVc * 1e6).ToString("F1", ci) + " cm3/mol"));
            sb.AppendLine();
            sb.AppendLine("T (K)     Tr      Psat EOS (bar)  Psat DB (bar)  dev %    VL (cm3/mol)  VV (cm3/mol)  ZL       ZV");
            foreach (var iso in r.Isotherms)
            {
                if (!iso.Subcritical)
                {
                    sb.AppendLine(iso.Temperature.ToString("F2", ci).PadRight(10) + iso.ReducedTemperature.ToString("F3", ci).PadRight(8) + "supercritical: no condensation, one root at every pressure");
                    continue;
                }
                double dev = double.IsNaN(iso.DatabaseSaturationPressure) ? double.NaN : 100 * (iso.SaturationPressure / iso.DatabaseSaturationPressure - 1);
                sb.AppendLine(iso.Temperature.ToString("F2", ci).PadRight(10) + iso.ReducedTemperature.ToString("F3", ci).PadRight(8) +
                    (iso.SaturationPressure / 1e5).ToString("F4", ci).PadRight(16) + (iso.DatabaseSaturationPressure / 1e5).ToString("F4", ci).PadRight(15) +
                    (double.IsNaN(dev) ? "n/a" : dev.ToString("F2", ci)).PadRight(9) + (iso.LiquidVolume * 1e6).ToString("F1", ci).PadRight(14) +
                    (iso.VaporVolume * 1e6).ToString("F1", ci).PadRight(14) + iso.LiquidZ.ToString("F4", ci).PadRight(9) + iso.VaporZ.ToString("F4", ci));
            }
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                foreach (var w in r.Warnings) sb.AppendLine("WARNING: " + w);
            }
            return sb.ToString();
        }
    }
}
