//    Property package comparator: the same binary, several thermodynamic models, one set of measurements.
//    Copyright 2026 Daniel Wagner O. de Medeiros
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
using System.Text;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Automation.DynamicRunner.PackageComparison
{
    /// <summary>Which binary diagram the comparison draws.</summary>
    public enum DiagramKind
    {
        /// <summary>T-x-y at a fixed pressure.</summary>
        Txy = 0,
        /// <summary>P-x-y at a fixed temperature.</summary>
        Pxy = 1
    }

    /// <summary>One measured equilibrium point, in SI (K, Pa). Y1 is NaN when the vapour composition was not measured.</summary>
    public sealed class ExperimentalPoint
    {
        public double X1;
        public double Y1 = double.NaN;
        public double T;
        public double P;
    }

    /// <summary>Inputs of the comparison; the simple fields go to the case file, the points as one text field.</summary>
    public sealed class PackageComparisonInput
    {
        public const string FileExtension = ".dwppc";

        public string Compound1 = "";
        public string Compound2 = "";
        public DiagramKind Kind = DiagramKind.Txy;
        /// <summary>Pa, for a T-x-y diagram.</summary>
        public double Pressure = 101325.0;
        /// <summary>K, for a P-x-y diagram.</summary>
        public double Temperature = 298.15;
        /// <summary>Tags of the packages to compare, separated by ';'. Empty means every package of the flowsheet.</summary>
        public string PackageTags = "";
        public int Points = 41;
        /// <summary>A label for the measured data (source, dataset id).</summary>
        public string DatasetLabel = "";
        /// <summary>The measured points as "x1,y1,T,P;..." in SI, invariant culture.</summary>
        public string DataText = "";

        public List<string> PackageList()
        {
            return PackageTags.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        }

        public void SetPackages(IEnumerable<string> tags) { PackageTags = string.Join(";", tags); }

        public List<ExperimentalPoint> Data()
        {
            var list = new List<ExperimentalPoint>();
            foreach (var item in DataText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(',');
                if (parts.Length < 4) continue;
                double x, y, t, p;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) y = double.NaN;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out t)) t = double.NaN;
                if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out p)) p = double.NaN;
                list.Add(new ExperimentalPoint { X1 = x, Y1 = y, T = t, P = p });
            }
            return list;
        }

        public void SetData(IEnumerable<ExperimentalPoint> points)
        {
            var ci = CultureInfo.InvariantCulture;
            DataText = string.Join(";", points.Select(p => p.X1.ToString("R", ci) + "," + (double.IsNaN(p.Y1) ? "" : p.Y1.ToString("R", ci)) + "," + p.T.ToString("R", ci) + "," + p.P.ToString("R", ci)));
        }

        public void CopyFrom(PackageComparisonInput other)
        {
            Compound1 = other.Compound1; Compound2 = other.Compound2; Kind = other.Kind; Pressure = other.Pressure; Temperature = other.Temperature;
            PackageTags = other.PackageTags; Points = other.Points; DatasetLabel = other.DatasetLabel; DataText = other.DataText;
        }

        public void SaveToFile(string path) { McCabeThiele.CaseFile.Save(this, path, "PackageComparisonCase"); }

        public static PackageComparisonInput LoadFromFile(string path)
        {
            var input = new PackageComparisonInput();
            McCabeThiele.CaseFile.Load(input, path, "PackageComparisonCase", "property package comparison case");
            return input;
        }
    }

    /// <summary>The curve one package draws, and how far it sits from the measurements.</summary>
    public sealed class PackageCurve
    {
        public string Tag = "";
        public string ModelName = "";
        public double[] X = new double[0];
        /// <summary>Bubble T (K) or bubble P (Pa) at each x.</summary>
        public double[] Bubble = new double[0];
        /// <summary>Dew T (K) or dew P (Pa) at each x (x here is the vapour composition y).</summary>
        public double[] Dew = new double[0];
        /// <summary>Vapour composition in equilibrium with the liquid X.</summary>
        public double[] Y = new double[0];
        public double Azeotrope = double.NaN;
        public int FailedPoints;
        /// <summary>Against the measurements: mean absolute deviation of the bubble T (K) or of the bubble P (% of the measured value).</summary>
        public double AadBubble = double.NaN;
        public double MaxBubble = double.NaN;
        /// <summary>Mean absolute deviation of y1, over the points that measured it.</summary>
        public double AadY = double.NaN;
        public int PointsCompared;
        public double[] DeviationX = new double[0];
        public double[] DeviationBubble = new double[0];
        public double[] DeviationY = new double[0];
        public List<string> Warnings = new List<string>();
        public bool Failed { get { return X.Length < 3; } }
    }

    public sealed class PackageComparisonResult
    {
        public DiagramKind Kind;
        public string Compound1 = "";
        public string Compound2 = "";
        public double Pressure;
        public double Temperature;
        public List<PackageCurve> Curves = new List<PackageCurve>();
        public List<ExperimentalPoint> Data = new List<ExperimentalPoint>();
        /// <summary>Tags ordered from the best fit to the worst; empty when there are no measurements.</summary>
        public List<string> Ranking = new List<string>();
        /// <summary>What the numbers say, one paragraph per entry.</summary>
        public List<string> Verdict = new List<string>();
        public List<string> Warnings = new List<string>();
        public string TextReport = "";
    }

    /// <summary>
    /// Draws the T-x-y or P-x-y diagram of one binary with every property package of the
    /// flowsheet (or the chosen ones), lays measured points over them and scores each package
    /// by its mean deviation from the measurements: the lesson being that the model is a choice,
    /// and that the data decide it.
    /// </summary>
    public static class PackageComparisonStudy
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public static PackageComparisonResult Run(IFlowsheet host, PackageComparisonInput input, Action<double> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            if (host == null) throw new ArgumentNullException("host");
            if (input == null) throw new ArgumentNullException("input");
            if (string.IsNullOrWhiteSpace(input.Compound1) || string.IsNullOrWhiteSpace(input.Compound2)) throw new ArgumentException("Choose the two compounds.");
            if (input.Compound1 == input.Compound2) throw new ArgumentException("The two compounds must differ.");
            if (input.Points < 5) throw new ArgumentException("Use at least 5 points.");
            ICompoundConstantProperties c1, c2;
            if (!host.SelectedCompounds.TryGetValue(input.Compound1, out c1)) throw new ArgumentException("'" + input.Compound1 + "' is not a compound of this flowsheet.");
            if (!host.SelectedCompounds.TryGetValue(input.Compound2, out c2)) throw new ArgumentException("'" + input.Compound2 + "' is not a compound of this flowsheet.");

            var wanted = input.PackageList();
            var packages = host.PropertyPackages.Values.Cast<IPropertyPackage>().Where(p => wanted.Count == 0 || wanted.Contains(p.Tag)).ToList();
            if (packages.Count == 0) throw new ArgumentException("No property package to compare; add packages to the simulation.");

            var r = new PackageComparisonResult { Kind = input.Kind, Compound1 = input.Compound1, Compound2 = input.Compound2, Pressure = input.Pressure, Temperature = input.Temperature, Data = input.Data() };
            int done = 0;
            foreach (var ipp in packages)
            {
                cancellation.ThrowIfCancellationRequested();
                var concrete = ipp as PropertyPackage;
                var curve = new PackageCurve { Tag = ipp.Tag, ModelName = concrete != null && !string.IsNullOrEmpty(concrete.ComponentName) ? concrete.ComponentName : ipp.GetType().Name.Replace("PropertyPackage", "") };
                try
                {
                    Trace(host, ipp, c1, c2, input, curve, cancellation);
                    Compare(input.Kind, curve, r.Data);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    curve.Warnings.Add("The package failed: " + ex.Message);
                }
                r.Curves.Add(curve);
                done++;
                if (progress != null) progress((double)done / packages.Count);
            }
            Judge(input, r);
            r.TextReport = Report(input, r);
            return r;
        }

        /// <summary>Bubble and dew curves of the pair with one package, on a scratch stream of the two compounds.</summary>
        private static void Trace(IFlowsheet host, IPropertyPackage ipp, ICompoundConstantProperties c1, ICompoundConstantProperties c2, PackageComparisonInput input, PackageCurve curve, CancellationToken cancellation)
        {
            var hostPp = ipp as PropertyPackage;
            if (hostPp == null) throw new InvalidOperationException("The package is not a DWSIM property package.");
            var pp = hostPp.Clone();
            pp.Flowsheet = host;

            var thermo = typeof(PropertyPackage).Assembly;
            var streamType = thermo.GetType("DWSIM.Thermodynamics.Streams.MaterialStream");
            var compoundType = thermo.GetType("DWSIM.Thermodynamics.BaseClasses.Compound");
            dynamic ms = Activator.CreateInstance(streamType, "", "");
            ms.SetFlowsheet(host);
            ms.PropertyPackage = pp;
            foreach (var pair in new[] { c1, c2 })
            {
                foreach (IPhase phase in ((IMaterialStream)ms).Phases.Values)
                {
                    dynamic compound = Activator.CreateInstance(compoundType, pair.Name, "");
                    compound.ConstantProperties = pair;
                    phase.Compounds.Add(pair.Name, (ICompound)compound);
                }
            }
            bool txy = input.Kind == DiagramKind.Txy;
            ((IMaterialStream)ms).Phases[0].Properties.pressure = txy ? input.Pressure : 101325.0;
            ((IMaterialStream)ms).Phases[0].Properties.temperature = txy ? 298.15 : input.Temperature;
            pp.CurrentMaterialStream = (IMaterialStream)ms;

            int n = input.Points;
            var xs = new List<double>(); var bub = new List<double>(); var dew = new List<double>(); var ys = new List<double>();
            double refB = 0, refD = 0;
            for (int i = 0; i < n; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double x = (double)i / (n - 1);
                double b = double.NaN, d = double.NaN, y = double.NaN;
                try
                {
                    var res = txy ? (object[])pp.DW_CalcBubT(new[] { x, 1 - x }, input.Pressure, refB) : (object[])pp.DW_CalcBubP(new[] { x, 1 - x }, input.Temperature, refB);
                    b = Convert.ToDouble(res[4]);
                    var vy = (double[])res[3];
                    y = i == 0 ? 0.0 : i == n - 1 ? 1.0 : vy[0];
                    if (b > 0 && !double.IsNaN(b) && !double.IsInfinity(b)) refB = b; else b = double.NaN;
                }
                catch (Exception) { }
                try
                {
                    var res = txy ? (object[])pp.DW_CalcDewT(new[] { x, 1 - x }, input.Pressure, refD) : (object[])pp.DW_CalcDewP(new[] { x, 1 - x }, input.Temperature, refD);
                    d = Convert.ToDouble(res[4]);
                    if (d > 0 && !double.IsNaN(d) && !double.IsInfinity(d)) refD = d; else d = double.NaN;
                }
                catch (Exception) { }
                if (double.IsNaN(b)) { curve.FailedPoints++; continue; }
                xs.Add(x); bub.Add(b); dew.Add(d); ys.Add(Math.Max(0, Math.Min(1, y)));
            }
            curve.X = xs.ToArray(); curve.Bubble = bub.ToArray(); curve.Dew = dew.ToArray(); curve.Y = ys.ToArray();
            if (curve.FailedPoints > 0) curve.Warnings.Add(curve.FailedPoints + " point(s) of the bubble curve did not converge and were left out.");
            if (curve.X.Length < 3) { curve.Warnings.Add("The package could not trace the diagram."); return; }

            // azeotrope: y - x changes sign strictly inside the range
            for (int i = 1; i < curve.X.Length - 2; i++)
            {
                double d0 = curve.Y[i] - curve.X[i], d1 = curve.Y[i + 1] - curve.X[i + 1];
                if ((d0 > 1e-9 && d1 <= 0) || (d0 < -1e-9 && d1 >= 0))
                {
                    curve.Azeotrope = curve.X[i] + (curve.X[i + 1] - curve.X[i]) * d0 / (d0 - d1);
                    break;
                }
            }
        }

        /// <summary>Deviation of the package's bubble curve and vapour composition at every measured liquid composition.</summary>
        private static void Compare(DiagramKind kind, PackageCurve curve, List<ExperimentalPoint> data)
        {
            if (curve.Failed || data.Count == 0) return;
            var dx = new List<double>(); var db = new List<double>(); var dy = new List<double>();
            double sumB = 0, maxB = 0, sumY = 0; int nB = 0, nY = 0;
            foreach (var pt in data)
            {
                if (pt.X1 < 0 || pt.X1 > 1) continue;
                double measured = kind == DiagramKind.Txy ? pt.T : pt.P;
                if (double.IsNaN(measured) || measured <= 0) continue;
                double model = Interp(curve.X, curve.Bubble, pt.X1);
                if (double.IsNaN(model)) continue;
                double dev = kind == DiagramKind.Txy ? model - measured : 100.0 * (model - measured) / measured;
                dx.Add(pt.X1); db.Add(dev);
                sumB += Math.Abs(dev); maxB = Math.Max(maxB, Math.Abs(dev)); nB++;
                if (!double.IsNaN(pt.Y1) && pt.Y1 >= 0 && pt.Y1 <= 1)
                {
                    double ym = Interp(curve.X, curve.Y, pt.X1);
                    double dyv = ym - pt.Y1;
                    dy.Add(dyv); sumY += Math.Abs(dyv); nY++;
                }
                else dy.Add(double.NaN);
            }
            curve.DeviationX = dx.ToArray(); curve.DeviationBubble = db.ToArray(); curve.DeviationY = dy.ToArray();
            curve.PointsCompared = nB;
            if (nB > 0) { curve.AadBubble = sumB / nB; curve.MaxBubble = maxB; }
            if (nY > 0) curve.AadY = sumY / nY;
        }

        private static double Interp(double[] xs, double[] ys, double x)
        {
            if (xs.Length == 0) return double.NaN;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            for (int i = 1; i < xs.Length; i++)
                if (x <= xs[i])
                {
                    if (double.IsNaN(ys[i]) || double.IsNaN(ys[i - 1])) return double.NaN;
                    double w = xs[i] - xs[i - 1];
                    return w <= 0 ? ys[i] : ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / w;
                }
            return ys[ys.Length - 1];
        }

        /// <summary>Ranks the packages and writes what the numbers mean.</summary>
        private static void Judge(PackageComparisonInput input, PackageComparisonResult r)
        {
            bool txy = input.Kind == DiagramKind.Txy;
            string unitB = txy ? "K" : "%";
            var ok = r.Curves.Where(c => !c.Failed).ToList();
            foreach (var c in r.Curves.Where(c => c.Failed)) r.Warnings.Add(c.Tag + ": " + string.Join(" ", c.Warnings));
            if (ok.Count == 0) { r.Warnings.Add("No package could trace the diagram."); return; }

            var scored = ok.Where(c => c.PointsCompared > 0).OrderBy(c => c.AadBubble).ToList();
            if (scored.Count > 0)
            {
                r.Ranking = scored.Select(c => c.Tag).ToList();
                var best = scored[0];
                r.Verdict.Add("Against " + best.PointsCompared + " measured point(s)" + (string.IsNullOrEmpty(input.DatasetLabel) ? "" : " (" + input.DatasetLabel + ")") + ", " + best.Tag + " (" + best.ModelName + ") fits best: mean deviation " + best.AadBubble.ToString("F2", Ci) + " " + unitB + " on the bubble " + (txy ? "temperature" : "pressure") + ", largest " + best.MaxBubble.ToString("F2", Ci) + " " + unitB + (double.IsNaN(best.AadY) ? "" : ", " + best.AadY.ToString("F4", Ci) + " on the vapour mole fraction") + ".");
                if (scored.Count > 1)
                {
                    var worst = scored[scored.Count - 1];
                    r.Verdict.Add(worst.Tag + " (" + worst.ModelName + ") fits worst: " + worst.AadBubble.ToString("F2", Ci) + " " + unitB + " mean, " + worst.MaxBubble.ToString("F2", Ci) + " " + unitB + " at most, " + (worst.AadBubble / Math.Max(1e-9, best.AadBubble)).ToString("F1", Ci) + " times the best.");
                    double good = txy ? 0.5 : 2.0;
                    var fine = scored.Where(c => c.AadBubble <= good).Select(c => c.Tag).ToList();
                    if (fine.Count == scored.Count) r.Verdict.Add("Every package sits within " + good.ToString("F1", Ci) + " " + unitB + " of the data: for this pair the choice of model changes little, and the cheapest one will do.");
                    else if (fine.Count > 0) r.Verdict.Add("Within " + good.ToString("F1", Ci) + " " + unitB + " of the data: " + string.Join(", ", fine) + ". The others would carry their error into every flash, column and heat balance of the flowsheet.");
                    else r.Verdict.Add("No package sits within " + good.ToString("F1", Ci) + " " + unitB + " of the data: either the binary interaction parameters are missing or estimated (regress them from these very points with the Data Regression tool) or the pair needs a different kind of model.");
                }
            }
            else
            {
                r.Verdict.Add("No measured data loaded: the diagrams are drawn for every package, and where they disagree the model matters. Load a dataset (or type points) to see which one is right.");
            }

            // azeotropes: who sees one, and where
            var azeo = ok.Where(c => !double.IsNaN(c.Azeotrope)).ToList();
            if (azeo.Count > 0 && azeo.Count < ok.Count)
                r.Verdict.Add("An azeotrope is predicted by " + string.Join(", ", azeo.Select(c => c.Tag + " (x1 = " + c.Azeotrope.ToString("F3", Ci) + ")")) + " and missed by " + string.Join(", ", ok.Where(c => double.IsNaN(c.Azeotrope)).Select(c => c.Tag)) + ". A model that misses an azeotrope will let a column separate what no column can; check the data near that composition.");
            else if (azeo.Count == ok.Count && azeo.Count > 0)
                r.Verdict.Add("Every package predicts an azeotrope near x1 = " + azeo.Average(c => c.Azeotrope).ToString("F3", Ci) + ": the pair is strongly non-ideal, and ordinary distillation cannot cross that composition.");

            // spread between the packages, with or without data
            if (ok.Count > 1)
            {
                double spread = 0, at = 0;
                var grid = Enumerable.Range(1, 19).Select(i => i / 20.0).ToList();
                foreach (var x in grid)
                {
                    var vals = ok.Select(c => Interp(c.X, c.Bubble, x)).Where(v => !double.IsNaN(v)).ToList();
                    if (vals.Count < 2) continue;
                    double s = txy ? vals.Max() - vals.Min() : 100.0 * (vals.Max() - vals.Min()) / vals.Min();
                    if (s > spread) { spread = s; at = x; }
                }
                r.Verdict.Add("The packages disagree by up to " + spread.ToString("F2", Ci) + " " + unitB + " on the bubble " + (txy ? "temperature" : "pressure") + ", at x1 = " + at.ToString("F2", Ci) + ". " + WhyTheyDiffer(ok));
            }
        }

        /// <summary>A sentence on the kind of models being compared.</summary>
        private static string WhyTheyDiffer(List<PackageCurve> curves)
        {
            bool anyEos = curves.Any(c => IsEos(c.ModelName)), anyAct = curves.Any(c => IsActivity(c.ModelName)), anyIdeal = curves.Any(c => c.ModelName.IndexOf("Raoult", StringComparison.OrdinalIgnoreCase) >= 0 || c.ModelName.IndexOf("Ideal", StringComparison.OrdinalIgnoreCase) >= 0);
            var sb = new StringBuilder();
            if (anyIdeal) sb.Append("Raoult's law takes the liquid as ideal (activity coefficients of 1), so it can only be right for molecules that look alike. ");
            if (anyEos) sb.Append("A cubic equation of state describes both phases with one equation and a mixing rule; without a fitted binary interaction parameter it treats the liquid as nearly ideal and misses hydrogen bonding and polarity. ");
            if (anyAct) sb.Append("An activity coefficient model (NRTL, UNIQUAC, Wilson, UNIFAC) puts the non-ideality of the liquid into fitted or group-contribution parameters, which is why it wins for polar and associating pairs near atmospheric pressure. ");
            if (sb.Length == 0) sb.Append("The models differ in how they describe the liquid phase; the data say which description holds for this pair.");
            return sb.ToString().TrimEnd();
        }

        private static bool IsEos(string name)
        {
            var n = name ?? "";
            return n.IndexOf("Peng", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Soave", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("SRK", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("PR", StringComparison.Ordinal) >= 0 || n.IndexOf("Lee-Kesler", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("SAFT", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("CoolProp", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("GERG", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsActivity(string name)
        {
            var n = name ?? "";
            return n.IndexOf("NRTL", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("UNIQUAC", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("UNIFAC", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Wilson", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Margules", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Laar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Report(PackageComparisonInput input, PackageComparisonResult r)
        {
            bool txy = input.Kind == DiagramKind.Txy;
            var sb = new StringBuilder();
            sb.AppendLine("Property package comparison: " + input.Compound1 + " (1) / " + input.Compound2 + " (2), " + (txy ? "T-x-y at " + input.Pressure.ToString("G6", Ci) + " Pa" : "P-x-y at " + input.Temperature.ToString("G6", Ci) + " K"));
            sb.AppendLine();
            sb.AppendLine("Package".PadRight(28) + "Model".PadRight(32) + (txy ? "AAD T (K)" : "AAD P (%)").PadRight(12) + "max".PadRight(10) + "AAD y1".PadRight(10) + "points".PadRight(8) + "azeotrope");
            foreach (var c in r.Curves)
                sb.AppendLine(c.Tag.PadRight(28) + c.ModelName.PadRight(32) + (c.Failed ? "failed" : (double.IsNaN(c.AadBubble) ? "-".PadRight(12) + "-".PadRight(10) + "-".PadRight(10) + "0".PadRight(8) : c.AadBubble.ToString("F3", Ci).PadRight(12) + c.MaxBubble.ToString("F3", Ci).PadRight(10) + (double.IsNaN(c.AadY) ? "-" : c.AadY.ToString("F4", Ci)).PadRight(10) + c.PointsCompared.ToString(Ci).PadRight(8)) + (double.IsNaN(c.Azeotrope) ? "none" : "x1 = " + c.Azeotrope.ToString("F3", Ci))));
            sb.AppendLine();
            foreach (var v in r.Verdict) sb.AppendLine(v);
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                foreach (var w in r.Warnings) sb.AppendLine("WARNING: " + w);
            }
            foreach (var c in r.Curves.Where(c => c.Warnings.Count > 0 && !c.Failed))
                foreach (var w in c.Warnings) sb.AppendLine("NOTE (" + c.Tag + "): " + w);
            return sb.ToString();
        }
    }
}
