//    McCabe-Thiele diagram of a binary distillation column.
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Automation.DynamicRunner.McCabeThiele
{
    /// <summary>Everything the diagram needs. Compositions are mole fractions of the light key; pressure in Pa.</summary>
    public sealed class McCabeThieleInput
    {
        public const string FileExtension = ".dwmct";

        /// <summary>Tag of the property package of the host flowsheet that gives the equilibrium; empty picks the first.</summary>
        public string PropertyPackageTag = "";
        /// <summary>The more volatile compound; its mole fraction is the x and y of the diagram.</summary>
        public string LightKey = "";
        public string HeavyKey = "";
        public double Pressure = 101325.0;

        public double FeedComposition = 0.5;
        /// <summary>Feed quality q: 1 saturated liquid, 0 saturated vapour, above 1 subcooled, below 0 superheated.</summary>
        public double FeedQuality = 1.0;
        public double DistillateComposition = 0.95;
        public double BottomsComposition = 0.05;

        /// <summary>The reflux ratio used when <see cref="RefluxAsMultipleOfMinimum"/> is off.</summary>
        public double RefluxRatio = 2.0;
        public bool RefluxAsMultipleOfMinimum = false;
        /// <summary>R / Rmin used when <see cref="RefluxAsMultipleOfMinimum"/> is on. 1.2 to 1.5 is the usual design range.</summary>
        public double RefluxMultiplier = 1.5;

        /// <summary>Murphree vapour efficiency, 0 to 1. Below 1 the stages step between the operating line and a pseudo-equilibrium curve.</summary>
        public double MurphreeEfficiency = 1.0;

        /// <summary>Points of the equilibrium curve, x = 0 to 1.</summary>
        public int Points = 51;

        public void CopyFrom(McCabeThieleInput other)
        {
            foreach (var f in typeof(McCabeThieleInput).GetFields(BindingFlags.Public | BindingFlags.Instance))
                f.SetValue(this, f.GetValue(other));
        }

        public void SaveToFile(string path)
        {
            CaseFile.Save(this, path, "McCabeThieleCase");
        }

        public static McCabeThieleInput LoadFromFile(string path)
        {
            var input = new McCabeThieleInput();
            CaseFile.Load(input, path, "McCabeThieleCase", "McCabe-Thiele case file");
            return input;
        }
    }

    /// <summary>One theoretical stage stepped off on the diagram, numbered from the top.</summary>
    public sealed class McCabeThieleStage
    {
        public int Number;
        /// <summary>Liquid composition leaving the stage (on the equilibrium or pseudo-equilibrium curve).</summary>
        public double X;
        /// <summary>Vapour composition leaving the stage.</summary>
        public double Y;
        /// <summary>Stage temperature, interpolated on the bubble-point curve, K.</summary>
        public double Temperature;
        /// <summary>Rectifying, Feed, Stripping or Reboiler.</summary>
        public string Section = "";
    }

    public sealed class McCabeThieleResult
    {
        // equilibrium
        public double[] EquilibriumX = new double[0];
        public double[] EquilibriumY = new double[0];
        /// <summary>Bubble-point temperature at each x, K.</summary>
        public double[] EquilibriumT = new double[0];
        /// <summary>Dew-point temperature at each x (as y), K, for the T-x-y plot.</summary>
        public double[] DewT = new double[0];

        // lines, as the two end points of each segment
        public double[] RectifyingX = new double[0], RectifyingY = new double[0];
        public double[] StrippingX = new double[0], StrippingY = new double[0];
        public double[] QLineX = new double[0], QLineY = new double[0];
        /// <summary>The staircase, as a polyline.</summary>
        public double[] StairX = new double[0], StairY = new double[0];
        /// <summary>The staircase at total reflux, for Nmin.</summary>
        public double[] TotalRefluxStairX = new double[0], TotalRefluxStairY = new double[0];

        /// <summary>Where the operating lines meet the q-line.</summary>
        public double IntersectionX, IntersectionY;
        /// <summary>Where the q-line meets the equilibrium curve: the pinch at minimum reflux.</summary>
        public double PinchX, PinchY;

        public double MinimumReflux;
        public double RefluxRatio;
        /// <summary>Stages at total reflux, including the reboiler.</summary>
        public int MinimumStages;
        /// <summary>Theoretical stages stepped off, including the reboiler.</summary>
        public int TheoreticalStages;
        /// <summary>Stage on which the feed enters, counted from the top.</summary>
        public int FeedStage;
        /// <summary>Azeotrope composition when the equilibrium curve crosses the diagonal, else NaN.</summary>
        public double Azeotrope = double.NaN;
        public bool Feasible = true;

        public List<McCabeThieleStage> Stages = new List<McCabeThieleStage>();
        public List<string> Warnings = new List<string>();
        public string TextReport = "";
        public double ElapsedSeconds;
    }

    /// <summary>
    /// The graphical construction of Lewis and McCabe-Thiele: an equilibrium curve from the
    /// property package, the operating lines from the material balances at constant molar
    /// overflow, the q-line from the feed condition, and stages stepped off between them.
    /// </summary>
    /// <remarks>
    /// The equilibrium curve is a series of bubble-point flashes at the column pressure, so it
    /// carries whatever the property package knows, azeotropes included. Everything else is
    /// straight lines, which is the point: a student sees where the stages go and why a column
    /// pinches at minimum reflux or needs infinitely many stages at an azeotrope.
    /// </remarks>
    public static class McCabeThieleStudy
    {
        public static McCabeThieleResult Run(IFlowsheet host, McCabeThieleInput input,
            Action<double> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.LightKey) || string.IsNullOrEmpty(input.HeavyKey) || input.LightKey == input.HeavyKey)
                throw new ArgumentException("Pick two different compounds: the light key and the heavy key.");
            if (input.Pressure <= 0) throw new ArgumentException("The pressure must be positive.");
            foreach (var pair in new[] { Tuple.Create("feed", input.FeedComposition), Tuple.Create("distillate", input.DistillateComposition), Tuple.Create("bottoms", input.BottomsComposition) })
                if (pair.Item2 <= 0 || pair.Item2 >= 1) throw new ArgumentException("The " + pair.Item1 + " composition must be between 0 and 1 (exclusive).");
            if (!(input.BottomsComposition < input.FeedComposition && input.FeedComposition < input.DistillateComposition))
                throw new ArgumentException("The light key fraction must increase from the bottoms to the feed to the distillate: xB < xF < xD.");
            if (input.MurphreeEfficiency <= 0 || input.MurphreeEfficiency > 1) throw new ArgumentException("The Murphree efficiency must be above 0 and at most 1.");
            if (input.Points < 11) input.Points = 11;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = new McCabeThieleResult();

            // ---- equilibrium curve from the property package
            var pp = ResolvePackage(host, input.PropertyPackageTag);
            Equilibrium(host, pp, input, r, progress, cancellation);

            var xe = r.EquilibriumX; var ye = r.EquilibriumY;
            Func<double, double> yEq = x => Interpolate(xe, ye, x);

            // the diagonal crossing, if any, is an azeotrope
            for (int i = 1; i < xe.Length; i++)
            {
                if (xe[i] <= 0 || xe[i] >= 1) continue;
                double d0 = ye[i - 1] - xe[i - 1], d1 = ye[i] - xe[i];
                // a flash that lands exactly on the diagonal (the trivial solution) counts as the crossing
                if (d0 > 1e-9 && d1 <= 0 || d0 < -1e-9 && d1 >= 0)
                {
                    r.Azeotrope = d0 == d1 ? xe[i] : xe[i - 1] + (xe[i] - xe[i - 1]) * d0 / (d0 - d1);
                    break;
                }
            }
            if (!double.IsNaN(r.Azeotrope))
            {
                r.Warnings.Add("The equilibrium curve crosses the diagonal at x = " + r.Azeotrope.ToString("F4", CultureInfo.InvariantCulture) +
                               ": an azeotrope. No number of stages takes the distillate past it.");
                if (input.DistillateComposition >= r.Azeotrope && yEq(input.FeedComposition) > input.FeedComposition)
                {
                    r.Warnings.Add("The distillate composition is beyond the azeotrope; the construction cannot reach it.");
                    r.Feasible = false;
                }
            }

            double xF = input.FeedComposition, xD = input.DistillateComposition, xB = input.BottomsComposition, q = input.FeedQuality;

            // ---- pinch and minimum reflux
            Pinch(xe, ye, yEq, xF, q, r);
            double rmin = double.NaN;
            if (r.Feasible)
            {
                // the rectifying line from (xD, xD) has to stay above the curve all the way to the pinch: a
                // tangent pinch on a curved equilibrium sets Rmin before the q-line pinch does
                rmin = 0;
                int n = 200;
                for (int i = 0; i <= n; i++)
                {
                    double x = r.PinchX + (xD - r.PinchX) * i / n;
                    double y = i == 0 ? r.PinchY : yEq(x);
                    if (y - x <= 1e-12 || x >= xD - 1e-12) continue;
                    double rr = (xD - y) / (y - x);
                    if (rr > rmin) rmin = rr;
                }
                if (rmin <= 0) { r.Warnings.Add("No pinch found between the feed and the distillate; check the compositions."); r.Feasible = false; }
            }
            r.MinimumReflux = rmin;

            double R = input.RefluxAsMultipleOfMinimum ? input.RefluxMultiplier * rmin : input.RefluxRatio;
            r.RefluxRatio = R;
            if (r.Feasible && R <= rmin)
            {
                r.Warnings.Add("The reflux ratio " + R.ToString("F3", CultureInfo.InvariantCulture) + " is at or below the minimum " +
                               rmin.ToString("F3", CultureInfo.InvariantCulture) + ": the stages pinch and never reach the feed.");
            }

            // ---- operating lines
            double mR = R / (R + 1), bR = xD / (R + 1);
            double xi, yi;
            if (Math.Abs(q - 1.0) < 1e-9) { xi = xF; yi = mR * xF + bR; }
            else
            {
                double mq = q / (q - 1), bq = -xF / (q - 1);
                xi = (bq - bR) / (mR - mq);
                yi = mR * xi + bR;
            }
            r.IntersectionX = xi; r.IntersectionY = yi;
            double mS = (yi - xB) / (xi - xB), bS = xB - mS * xB;
            r.RectifyingX = new[] { xi, xD }; r.RectifyingY = new[] { yi, xD };
            r.StrippingX = new[] { xB, xi }; r.StrippingY = new[] { xB, yi };
            r.QLineX = new[] { xF, xi }; r.QLineY = new[] { xF, yi };
            if (yi > yEq(xi) && r.Feasible)
            {
                r.Warnings.Add("The operating lines meet above the equilibrium curve: the reflux is below the minimum for this feed condition.");
            }

            // ---- stages
            var stairX = new List<double>(); var stairY = new List<double>();
            if (r.Feasible)
            {
                Step(input, yEq, x => x < xi ? mS * x + bS : mR * x + bR, xi, xD, xB, r, stairX, stairY, false);
                r.StairX = stairX.ToArray(); r.StairY = stairY.ToArray();

                var nminX = new List<double>(); var nminY = new List<double>();
                var total = new McCabeThieleResult();
                Step(input, yEq, x => x, double.NaN, xD, xB, total, nminX, nminY, true);
                r.MinimumStages = total.TheoreticalStages;
                r.TotalRefluxStairX = nminX.ToArray(); r.TotalRefluxStairY = nminY.ToArray();
                if (!total.Feasible) r.Warnings.Add("Even at total reflux the construction does not reach the bottoms composition.");
            }

            // stage temperatures from the bubble-point curve
            foreach (var s in r.Stages) s.Temperature = Interpolate(xe, r.EquilibriumT, s.X);

            r.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            r.TextReport = Report(input, r, pp);
            return r;
        }

        /// <summary>
        /// Reads the inputs off a shortcut column of the flowsheet: the keys, the condenser pressure,
        /// the reflux ratio, the key purities, and the feed composition as light key over both keys
        /// in the stream attached to its inlet, with q from that stream's vapour fraction.
        /// </summary>
        public static void FillFromShortcutColumn(IFlowsheet fs, ISimulationObject column, McCabeThieleInput input)
        {
            if (fs == null || column == null || input == null) return;
            var t = column.GetType();
            Func<string, object> field = n =>
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(column);
                var pr = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                return pr == null ? null : pr.GetValue(column, null);
            };
            var lk = field("m_lightkey") as string;
            var hk = field("m_heavykey") as string;
            if (!string.IsNullOrEmpty(lk)) input.LightKey = lk;
            if (!string.IsNullOrEmpty(hk)) input.HeavyKey = hk;
            var pc = field("m_condenserpressure");
            if (pc is double && (double)pc > 0) input.Pressure = (double)pc;
            var rr = field("m_refluxratio");
            if (rr is double && (double)rr > 0) { input.RefluxRatio = (double)rr; input.RefluxAsMultipleOfMinimum = false; }
            var xb = field("m_lightkeymolarfrac");
            if (xb is double && (double)xb > 0 && (double)xb < 1) input.BottomsComposition = (double)xb;
            var hd = field("m_heavykeymolarfrac");
            if (hd is double && (double)hd > 0 && (double)hd < 1) input.DistillateComposition = 1 - (double)hd;
            if (column.PropertyPackage != null && !string.IsNullOrEmpty(column.PropertyPackage.Tag)) input.PropertyPackageTag = column.PropertyPackage.Tag;

            var port = column.GraphicObject == null ? null : column.GraphicObject.InputConnectors
                .FirstOrDefault(c => c.IsAttached && c.Type == DWSIM.Interfaces.Enums.GraphicObjects.ConType.ConIn);
            var from = port == null || port.AttachedConnector == null ? null : port.AttachedConnector.AttachedFrom;
            ISimulationObject feedObj;
            if (from != null && fs.SimulationObjects.TryGetValue(from.Name, out feedObj) && feedObj is IMaterialStream)
            {
                var feed = (IMaterialStream)feedObj;
                try
                {
                    var comps = feed.Phases[0].Compounds;
                    double xl = comps.ContainsKey(input.LightKey) ? comps[input.LightKey].MoleFraction.GetValueOrDefault() : 0;
                    double xh = comps.ContainsKey(input.HeavyKey) ? comps[input.HeavyKey].MoleFraction.GetValueOrDefault() : 0;
                    if (xl + xh > 0) input.FeedComposition = xl / (xl + xh);
                    var vf = feed.Phases[2].Properties.molarfraction.GetValueOrDefault();
                    input.FeedQuality = 1 - Math.Max(0, Math.Min(1, vf));
                }
                catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ equilibrium

        private static PropertyPackage ResolvePackage(IFlowsheet host, string tag)
        {
            IPropertyPackage found = null;
            if (!string.IsNullOrEmpty(tag)) found = host.PropertyPackages.Values.FirstOrDefault(p => p.Tag == tag);
            if (found == null) found = host.PropertyPackages.Values.FirstOrDefault();
            var pp = found as PropertyPackage;
            if (pp == null) throw new ArgumentException("The flowsheet has no property package to compute the equilibrium with.");
            return pp;
        }

        /// <summary>Bubble and dew points at the column pressure across x, on a scratch stream of the two keys.</summary>
        private static void Equilibrium(IFlowsheet host, PropertyPackage hostPp, McCabeThieleInput input, McCabeThieleResult r,
            Action<double> progress, CancellationToken cancellation)
        {
            ICompoundConstantProperties lk, hk;
            if (!host.SelectedCompounds.TryGetValue(input.LightKey, out lk)) throw new ArgumentException("'" + input.LightKey + "' is not a compound of this flowsheet.");
            if (!host.SelectedCompounds.TryGetValue(input.HeavyKey, out hk)) throw new ArgumentException("'" + input.HeavyKey + "' is not a compound of this flowsheet.");

            // a clone, so the flowsheet's own package keeps its current stream untouched
            var pp = hostPp.Clone();
            pp.Flowsheet = host;

            // The scratch stream is built through reflection: the concrete MaterialStream carries the
            // CAPE-OPEN interfaces, which this assembly does not reference.
            var thermo = typeof(PropertyPackage).Assembly;
            var streamType = thermo.GetType("DWSIM.Thermodynamics.Streams.MaterialStream");
            var compoundType = thermo.GetType("DWSIM.Thermodynamics.BaseClasses.Compound");
            dynamic ms = Activator.CreateInstance(streamType, "", "");
            ms.SetFlowsheet(host);
            ms.PropertyPackage = pp;
            foreach (var pair in new[] { lk, hk })
            {
                foreach (IPhase phase in ((IMaterialStream)ms).Phases.Values)
                {
                    dynamic compound = Activator.CreateInstance(compoundType, pair.Name, "");
                    compound.ConstantProperties = pair;
                    phase.Compounds.Add(pair.Name, (ICompound)compound);
                }
            }
            ((IMaterialStream)ms).Phases[0].Properties.pressure = input.Pressure;
            pp.CurrentMaterialStream = (IMaterialStream)ms;

            int n = input.Points;
            var xs = new double[n]; var ys = new double[n]; var ts = new double[n]; var td = new double[n];
            double tRef = 0, tDew = 0;
            for (int i = 0; i < n; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double x = (double)i / (n - 1);
                xs[i] = x;
                try
                {
                    var res = (object[])pp.DW_CalcBubT(new[] { x, 1 - x }, input.Pressure, tRef);
                    var vy = (double[])res[3];
                    ts[i] = Convert.ToDouble(res[4]);
                    tRef = ts[i];
                    // the pure ends are their own equilibrium; a flash there can return a stale y
                    ys[i] = i == 0 ? 0.0 : i == n - 1 ? 1.0 : vy[0];
                }
                catch (Exception)
                {
                    ys[i] = double.NaN; ts[i] = double.NaN;
                }
                try
                {
                    var res = (object[])pp.DW_CalcDewT(new[] { x, 1 - x }, input.Pressure, tDew);
                    td[i] = Convert.ToDouble(res[4]);
                    tDew = td[i];
                }
                catch (Exception) { td[i] = double.NaN; }
                if (progress != null) progress((i + 1.0) / n);
            }

            // drop the points the flash could not give
            var keep = Enumerable.Range(0, n).Where(i => !double.IsNaN(ys[i])).ToList();
            if (keep.Count < 5) throw new InvalidOperationException("The property package could not compute the bubble points of this pair at " + input.Pressure.ToString("G5", CultureInfo.InvariantCulture) + " Pa.");
            r.EquilibriumX = keep.Select(i => xs[i]).ToArray();
            r.EquilibriumY = keep.Select(i => Math.Max(0, Math.Min(1, ys[i]))).ToArray();
            r.EquilibriumT = keep.Select(i => ts[i]).ToArray();
            r.DewT = keep.Select(i => td[i]).ToArray();
            if (keep.Count < n) r.Warnings.Add((n - keep.Count) + " point(s) of the equilibrium curve failed to flash and were skipped.");
        }

        // ------------------------------------------------------------------ construction

        private static void Pinch(double[] xe, double[] ye, Func<double, double> yEq, double xF, double q, McCabeThieleResult r)
        {
            if (Math.Abs(q - 1.0) < 1e-9)
            {
                r.PinchX = xF; r.PinchY = yEq(xF);
                return;
            }
            double mq = q / (q - 1), bq = -xF / (q - 1);
            // f(x) = yEq(x) - qline(x) changes sign at the pinch; scan then bisect
            Func<double, double> f = x => yEq(x) - (mq * x + bq);
            double a = 0, b = 1;
            bool found = false;
            int n = 400;
            double prevX = 0, prevF = f(0);
            for (int i = 1; i <= n; i++)
            {
                double x = (double)i / n, fx = f(x);
                if (prevF == 0 || prevF * fx < 0)
                {
                    a = prevX; b = x; found = true;
                    // prefer the crossing nearest the feed composition
                    if (Math.Abs(x - xF) < 0.5) break;
                }
                prevX = x; prevF = fx;
            }
            if (!found)
            {
                r.PinchX = xF; r.PinchY = yEq(xF);
                r.Warnings.Add("The q-line does not meet the equilibrium curve; the pinch was taken at the feed composition.");
                return;
            }
            for (int i = 0; i < 60; i++)
            {
                double m = 0.5 * (a + b);
                if (f(a) * f(m) <= 0) b = m; else a = m;
            }
            r.PinchX = 0.5 * (a + b); r.PinchY = yEq(r.PinchX);
        }

        /// <summary>Steps stages from the distillate down to the bottoms between the operating line and the (pseudo-)equilibrium curve.</summary>
        private static void Step(McCabeThieleInput input, Func<double, double> yEq, Func<double, double> yOp, double xi,
            double xD, double xB, McCabeThieleResult r, List<double> stairX, List<double> stairY, bool totalReflux)
        {
            double E = totalReflux ? 1.0 : input.MurphreeEfficiency;
            Func<double, double> yPseudo = xl => yOp(xl) + E * (yEq(xl) - yOp(xl));

            double x = xD, y = xD;
            stairX.Add(x); stairY.Add(y);
            bool rectifying = !totalReflux;
            int n = 0;
            r.Feasible = true;
            r.Stages.Clear();
            r.FeedStage = 0;
            for (int guard = 0; guard < 300; guard++)
            {
                // horizontal step: the liquid in equilibrium with the vapour y, x in [0, x)
                double xNew;
                if (!SolveX(yPseudo, y, x, out xNew) || xNew >= x - 1e-10)
                {
                    r.Warnings.Add("The stages pinched at x = " + x.ToString("F4", CultureInfo.InvariantCulture) + " after " + n + " stage(s): the operating line touches the equilibrium curve there.");
                    r.Feasible = false;
                    break;
                }
                n++;
                stairX.Add(xNew); stairY.Add(y);
                var stage = new McCabeThieleStage { Number = n, X = xNew, Y = y };
                r.Stages.Add(stage);

                if (xNew <= xB)
                {
                    stage.Section = totalReflux ? "Total reflux" : "Reboiler";
                    break;
                }

                if (rectifying && xNew <= xi)
                {
                    rectifying = false;
                    r.FeedStage = n;
                    stage.Section = "Feed";
                }
                else if (!totalReflux)
                {
                    stage.Section = rectifying ? "Rectifying" : "Stripping";
                }
                else stage.Section = "Total reflux";

                // vertical step: down to the operating line
                x = xNew;
                y = yOp(x);
                stairX.Add(x); stairY.Add(y);
            }
            r.TheoreticalStages = n;
            if (r.Stages.Count > 0 && r.Stages.Last().X > xB) r.Feasible = false;
        }

        /// <summary>Finds x in [0, xMax] with curve(x) = y, the curve rising; false when y is off the curve.</summary>
        private static bool SolveX(Func<double, double> curve, double y, double xMax, out double x)
        {
            x = double.NaN;
            double a = 0.0, b = xMax;
            double fa = curve(a) - y, fb = curve(b) - y;
            if (double.IsNaN(fa) || double.IsNaN(fb)) return false;
            if (fa > 0) { x = 0; return true; }          // y below the curve's start: the bottom of the diagram
            if (fb < 0)
            {
                // the curve at xMax is below y: no equilibrium liquid leaner than xMax gives this vapour
                return false;
            }
            for (int i = 0; i < 80; i++)
            {
                double m = 0.5 * (a + b), fm = curve(m) - y;
                if (fm > 0) b = m; else a = m;
            }
            x = 0.5 * (a + b);
            return true;
        }

        private static double Interpolate(double[] xs, double[] ys, double x)
        {
            if (xs.Length == 0) return double.NaN;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (xs[mid] <= x) lo = mid; else hi = mid;
            }
            double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + t * (ys[hi] - ys[lo]);
        }

        // ------------------------------------------------------------------ report

        private static string Report(McCabeThieleInput input, McCabeThieleResult r, PropertyPackage pp)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("MCCABE-THIELE DIAGRAM");
            sb.AppendLine("System: " + input.LightKey + " (light key) / " + input.HeavyKey + " (heavy key), " + pp.ComponentName);
            sb.AppendLine("Pressure: " + input.Pressure.ToString("G6", ci) + " Pa");
            sb.AppendLine("Feed xF = " + input.FeedComposition.ToString("F4", ci) + ", q = " + input.FeedQuality.ToString("F3", ci));
            sb.AppendLine("Distillate xD = " + input.DistillateComposition.ToString("F4", ci) + ", bottoms xB = " + input.BottomsComposition.ToString("F4", ci));
            sb.AppendLine();
            sb.AppendLine("Minimum reflux ratio Rmin = " + r.MinimumReflux.ToString("F4", ci) + " (pinch at x = " + r.PinchX.ToString("F4", ci) + ", y = " + r.PinchY.ToString("F4", ci) + ")");
            sb.AppendLine("Reflux ratio R = " + r.RefluxRatio.ToString("F4", ci) + (r.MinimumReflux > 0 ? " (R/Rmin = " + (r.RefluxRatio / r.MinimumReflux).ToString("F3", ci) + ")" : ""));
            sb.AppendLine("Minimum stages at total reflux Nmin = " + r.MinimumStages + " (including the reboiler)");
            sb.AppendLine("Theoretical stages N = " + r.TheoreticalStages + " (including the reboiler), feed on stage " + r.FeedStage + " from the top");
            sb.AppendLine("Operating lines meet at x = " + r.IntersectionX.ToString("F4", ci) + ", y = " + r.IntersectionY.ToString("F4", ci));
            if (input.MurphreeEfficiency < 1) sb.AppendLine("Murphree vapour efficiency " + input.MurphreeEfficiency.ToString("F3", ci) + ": stages stepped on the pseudo-equilibrium curve");
            if (!double.IsNaN(r.Azeotrope)) sb.AppendLine("Azeotrope at x = " + r.Azeotrope.ToString("F4", ci));
            sb.AppendLine();
            sb.AppendLine("Stage   x        y        T (K)    section");
            foreach (var s in r.Stages)
                sb.AppendLine(s.Number.ToString().PadLeft(5) + "   " + s.X.ToString("F4", ci) + "   " + s.Y.ToString("F4", ci) + "   " + s.Temperature.ToString("F2", ci).PadLeft(7) + "  " + s.Section);
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                foreach (var w in r.Warnings) sb.AppendLine("WARNING: " + w);
            }
            return sb.ToString();
        }
    }

    /// <summary>Small XML case files, one element per public field, SI units, invariant culture; shared by the teaching tools.</summary>
    internal static class CaseFile
    {
        public static void Save(object input, string path, string rootName)
        {
            var root = new XElement(rootName, new XAttribute("version", 1), new XAttribute("units", "SI"));
            foreach (var f in input.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(input);
                string text = v is double ? ((double)v).ToString("R", CultureInfo.InvariantCulture)
                            : v is bool ? ((bool)v ? "true" : "false")
                            : v == null ? "" : v.ToString();
                root.Add(new XElement(f.Name, text));
            }
            new XDocument(root).Save(path);
        }

        public static void Load(object input, string path, string rootName, string what)
        {
            var doc = XDocument.Load(path);
            if (doc.Root == null || doc.Root.Name != rootName)
                throw new InvalidOperationException("'" + Path.GetFileName(path) + "' is not a " + what + ".");
            foreach (var f in input.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var e = doc.Root.Element(f.Name);
                if (e == null) continue;
                string text = e.Value.Trim();
                if (f.FieldType == typeof(double))
                {
                    double d;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) f.SetValue(input, d);
                }
                else if (f.FieldType == typeof(int))
                {
                    int n;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) f.SetValue(input, n);
                }
                else if (f.FieldType == typeof(bool))
                {
                    bool b;
                    if (bool.TryParse(text, out b)) f.SetValue(input, b);
                }
                else if (f.FieldType.IsEnum)
                {
                    try { f.SetValue(input, Enum.Parse(f.FieldType, text, true)); } catch { }
                }
                else if (f.FieldType == typeof(string))
                {
                    f.SetValue(input, text);
                }
            }
        }
    }
}
