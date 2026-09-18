//    Unit operation insight: the equations a solved object satisfied, with its numbers in them.
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
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;

namespace DWSIM.Automation.DynamicRunner.Insight
{
    /// <summary>One curve of an insight chart.</summary>
    public sealed class InsightSeries
    {
        public string Title = "";
        public double[] X = new double[0];
        public double[] Y = new double[0];
        /// <summary>Points only (a marker), no line.</summary>
        public bool Scatter;
        public bool Dashed;
    }

    /// <summary>A chart of the explanation: the axes carry the flowsheet's units.</summary>
    public sealed class InsightChart
    {
        public string Title = "";
        public string XTitle = "";
        public string YTitle = "";
        public List<InsightSeries> Series = new List<InsightSeries>();
    }

    /// <summary>A table of the explanation, already formatted in the flowsheet's units.</summary>
    public sealed class InsightTable
    {
        public string Title = "";
        public List<string> Columns = new List<string>();
        public List<string[]> Rows = new List<string[]>();
    }

    /// <summary>The explanation of one solved object.</summary>
    public sealed class InsightResult
    {
        public string ObjectName = "";
        public string ObjectTag = "";
        public string ObjectType = "";
        public string Title = "";
        /// <summary>The written explanation, one paragraph per entry; headings start with "## ".</summary>
        public List<string> Lines = new List<string>();
        public List<InsightChart> Charts = new List<InsightChart>();
        public List<InsightTable> Tables = new List<InsightTable>();
        public List<string> Warnings = new List<string>();
        /// <summary>Lines and tables as one text, for the clipboard and the assistant.</summary>
        public string TextReport = "";
    }

    /// <summary>
    /// "Why this result?": for a solved unit operation or stream, writes the balances and the
    /// equilibrium relations it satisfied with the flowsheet's numbers substituted in, and draws
    /// the diagram a textbook would draw for it: the Rachford-Rice function of a flash, the
    /// heating curve of a heater, the T-Q diagram of an exchanger, the isenthalpic path of a
    /// valve, the isentropic path of a compressor and the Levenspiel plot of a kinetic reactor.
    /// Values come out in the flowsheet's unit system.
    /// </summary>
    public static class UnitInsightStudy
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Object types the study can explain.</summary>
        public static bool Supports(ISimulationObject obj)
        {
            if (obj == null || obj.GraphicObject == null) return false;
            switch (obj.GraphicObject.ObjectType)
            {
                case ObjectType.MaterialStream:
                case ObjectType.Vessel:
                case ObjectType.Heater:
                case ObjectType.Cooler:
                case ObjectType.HeatExchanger:
                case ObjectType.Pump:
                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.Valve:
                case ObjectType.NodeIn:
                case ObjectType.NodeOut:
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_PFR:
                case ObjectType.ShortcutColumn:
                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.ReboiledAbsorber:
                case ObjectType.RefluxedAbsorber:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Explains one object of the flowsheet. The object must be solved.</summary>
        public static InsightResult Explain(IFlowsheet fs, ISimulationObject obj)
        {
            if (fs == null) throw new ArgumentNullException("fs");
            if (obj == null) throw new ArgumentNullException("obj");
            var ctx = new Ctx(fs, obj);
            var r = ctx.R;
            if (!Supports(obj))
            {
                r.Title = r.ObjectTag;
                r.Lines.Add("There is no written explanation for a " + r.ObjectType + " yet.");
                r.TextReport = Render(r);
                return r;
            }
            if (!obj.Calculated)
            {
                r.Title = r.ObjectTag;
                r.Lines.Add(r.ObjectTag + " is not solved. Solve the flowsheet and ask again: the explanation reads the numbers the solver produced.");
                r.TextReport = Render(r);
                return r;
            }
            try
            {
                switch (obj.GraphicObject.ObjectType)
                {
                    case ObjectType.MaterialStream: ExplainStream(ctx); break;
                    case ObjectType.Vessel: ExplainVessel(ctx); break;
                    case ObjectType.Heater:
                    case ObjectType.Cooler: ExplainHeaterCooler(ctx); break;
                    case ObjectType.HeatExchanger: ExplainHeatExchanger(ctx); break;
                    case ObjectType.Pump: ExplainPump(ctx); break;
                    case ObjectType.Compressor:
                    case ObjectType.Expander: ExplainCompressorExpander(ctx); break;
                    case ObjectType.Valve: ExplainValve(ctx); break;
                    case ObjectType.NodeIn: ExplainMixer(ctx); break;
                    case ObjectType.NodeOut: ExplainSplitter(ctx); break;
                    case ObjectType.RCT_Conversion:
                    case ObjectType.RCT_Equilibrium:
                    case ObjectType.RCT_Gibbs:
                    case ObjectType.RCT_CSTR:
                    case ObjectType.RCT_PFR: ExplainReactor(ctx); break;
                    case ObjectType.ShortcutColumn: ExplainShortcutColumn(ctx); break;
                    case ObjectType.DistillationColumn:
                    case ObjectType.AbsorptionColumn:
                    case ObjectType.ReboiledAbsorber:
                    case ObjectType.RefluxedAbsorber: ExplainRigorousColumn(ctx); break;
                }
            }
            catch (Exception ex)
            {
                r.Warnings.Add("The explanation stopped early: " + ex.Message);
            }
            r.TextReport = Render(r);
            return r;
        }

        /// <summary>Lines and tables as plain text.</summary>
        public static string Render(InsightResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine(r.Title);
            sb.AppendLine(new string('=', Math.Max(4, r.Title.Length)));
            foreach (var l in r.Lines)
            {
                if (l.StartsWith("## ")) { sb.AppendLine(); sb.AppendLine(l.Substring(3)); sb.AppendLine(new string('-', l.Length - 3)); }
                else sb.AppendLine(l);
            }
            foreach (var t in r.Tables)
            {
                sb.AppendLine();
                sb.AppendLine(t.Title);
                var widths = t.Columns.Select((c, i) => Math.Max(c.Length, t.Rows.Count == 0 ? 0 : t.Rows.Max(row => i < row.Length ? row[i].Length : 0))).ToArray();
                sb.AppendLine(string.Join("  ", t.Columns.Select((c, i) => c.PadRight(widths[i]))));
                foreach (var row in t.Rows)
                    sb.AppendLine(string.Join("  ", row.Select((c, i) => i < widths.Length ? c.PadRight(widths[i]) : c)));
            }
            if (r.Warnings.Count > 0)
            {
                sb.AppendLine();
                foreach (var w in r.Warnings) sb.AppendLine("WARNING: " + w);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ context and helpers

        /// <summary>What every explanation needs: the flowsheet, its units and number format, and the result.</summary>
        private sealed class Ctx
        {
            public readonly IFlowsheet Fs;
            public readonly ISimulationObject Obj;
            public readonly IUnitsOfMeasure Su;
            public readonly string Nf;
            public readonly InsightResult R = new InsightResult();

            public Ctx(IFlowsheet fs, ISimulationObject obj)
            {
                Fs = fs; Obj = obj;
                Su = fs.FlowsheetOptions.SelectedUnitSystem;
                Nf = string.IsNullOrEmpty(fs.FlowsheetOptions.NumberFormat) ? "G6" : fs.FlowsheetOptions.NumberFormat;
                R.ObjectName = obj.Name;
                R.ObjectTag = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name;
                R.ObjectType = obj.GraphicObject != null ? obj.GraphicObject.ObjectType.ToString() : "";
            }

            public string N(double v) { return double.IsNaN(v) ? "NaN" : v.ToString(Nf, Ci); }
            public string N(double v, string format) { return double.IsNaN(v) ? "NaN" : v.ToString(format, Ci); }
            /// <summary>An SI value in the flowsheet's unit, with the unit.</summary>
            public string U(double si, string unit) { return N(Converter.ConvertFromSI(unit, si)) + " " + unit; }
            public double C(double si, string unit) { return Converter.ConvertFromSI(unit, si); }
            public string T(double k) { return U(k, Su.temperature); }
            public string P(double pa) { return U(pa, Su.pressure); }
            public string Q(double kw) { return U(kw, Su.heatflow); }
            public string H(double kjkg) { return U(kjkg, Su.enthalpy); }
            public string S(double kjkgk) { return U(kjkgk, Su.entropy); }
            public string M(double kgs) { return U(kgs, Su.massflow); }
            public string Mol(double mols) { return U(mols, Su.molarflow); }
            /// <summary>SI values for the arithmetic of a balance: kg/s, kJ/kg and kW multiply through; negatives in parentheses.</summary>
            public string Msi(double kgs) { return Sg(kgs, "F4") + " kg/s"; }
            public string Hsi(double kjkg) { return Sg(kjkg, "F2") + " kJ/kg"; }
            public string Ssi(double kjkgk) { return Sg(kjkgk, "F4") + " kJ/(kg K)"; }
            public string Qsi(double kw) { return Sg(kw, "F2") + " kW" + (Su.heatflow == "kW" ? "" : " = " + Q(kw)); }
            private string Sg(double v, string format) { return v < 0 ? "(" + N(v, format) + ")" : N(v, format); }
            public string Tag(object o) { var so = o as ISimulationObject; return so == null ? "?" : so.GraphicObject != null ? so.GraphicObject.Tag : so.Name; }

            public void Heading(string text) { R.Lines.Add("## " + text); }
            public void Line(string text) { R.Lines.Add(text); }
            public void Warn(string text) { R.Warnings.Add(text); }

            public IMaterialStream Inlet(int index) { return Attached(Obj.GraphicObject.InputConnectors, index, ConType.ConIn); }
            public IMaterialStream Outlet(int index) { return Attached(Obj.GraphicObject.OutputConnectors, index, ConType.ConOut); }
            public List<IMaterialStream> Inlets() { return AllAttached(Obj.GraphicObject.InputConnectors, ConType.ConIn); }
            public List<IMaterialStream> Outlets() { return AllAttached(Obj.GraphicObject.OutputConnectors, ConType.ConOut); }

            private IMaterialStream Attached(IList<IConnectionPoint> ports, int index, ConType kind)
            {
                if (index < 0 || index >= ports.Count) return null;
                var port = ports[index];
                if (!port.IsAttached || port.Type != kind || port.AttachedConnector == null) return null;
                var other = kind == ConType.ConIn ? port.AttachedConnector.AttachedFrom : port.AttachedConnector.AttachedTo;
                if (other == null) return null;
                ISimulationObject o;
                return Fs.SimulationObjects.TryGetValue(other.Name, out o) ? o as IMaterialStream : null;
            }

            private List<IMaterialStream> AllAttached(IList<IConnectionPoint> ports, ConType kind)
            {
                var list = new List<IMaterialStream>();
                for (int i = 0; i < ports.Count; i++)
                {
                    var s = Attached(ports, i, kind);
                    if (s != null) list.Add(s);
                }
                return list;
            }

            /// <summary>The energy stream on the object's energy port, if any, and its flow in kW.</summary>
            public double EnergyFlow()
            {
                foreach (var port in Obj.GraphicObject.InputConnectors.Concat(Obj.GraphicObject.OutputConnectors))
                {
                    if (!port.IsAttached || port.Type != ConType.ConEn || port.AttachedConnector == null) continue;
                    var other = port.AttachedConnector.AttachedFrom != null && port.AttachedConnector.AttachedFrom.Name != Obj.Name
                        ? port.AttachedConnector.AttachedFrom : port.AttachedConnector.AttachedTo;
                    if (other == null) continue;
                    ISimulationObject o;
                    if (Fs.SimulationObjects.TryGetValue(other.Name, out o) && o is IEnergyStream) return ((IEnergyStream)o).GetEnergyFlow();
                }
                return double.NaN;
            }
        }

        private static double Temp(IMaterialStream s) { return s.Phases[0].Properties.temperature.GetValueOrDefault(); }
        private static double Pres(IMaterialStream s) { return s.Phases[0].Properties.pressure.GetValueOrDefault(); }
        private static double MassFlow(IMaterialStream s) { return s.Phases[0].Properties.massflow.GetValueOrDefault(); }
        private static double MolarFlow(IMaterialStream s) { return s.Phases[0].Properties.molarflow.GetValueOrDefault(); }
        private static double Enth(IMaterialStream s) { return s.Phases[0].Properties.enthalpy.GetValueOrDefault(); }
        private static double Entr(IMaterialStream s) { return s.Phases[0].Properties.entropy.GetValueOrDefault(); }
        private static double VapFrac(IMaterialStream s) { return s.Phases[2].Properties.molarfraction.GetValueOrDefault(); }
        private static double Dens(IMaterialStream s) { return s.Phases[0].Properties.density.GetValueOrDefault(); }
        private static double VolFlow(IMaterialStream s) { return s.Phases[0].Properties.volumetric_flow.GetValueOrDefault(); }
        /// <summary>Energy carried by the stream, kW.</summary>
        private static double EnergyFlow(IMaterialStream s) { return MassFlow(s) * Enth(s); }

        /// <summary>A private copy of the stream on a private copy of its property package, flashed at the given spec.</summary>
        private static IMaterialStream Flash(IMaterialStream source, StreamSpec spec, double pressure, double second)
        {
            var clone = source.Clone();
            clone.SetPropertyPackageObject(source.GetPropertyPackageObjectCopy());
            clone.SpecType = spec;
            clone.Phases[0].Properties.pressure = pressure;
            switch (spec)
            {
                case StreamSpec.Temperature_and_Pressure: clone.Phases[0].Properties.temperature = second; break;
                case StreamSpec.Pressure_and_Enthalpy: clone.Phases[0].Properties.enthalpy = second; break;
                case StreamSpec.Pressure_and_Entropy: clone.Phases[0].Properties.entropy = second; break;
                case StreamSpec.Pressure_and_VaporFraction: clone.Phases[2].Properties.molarfraction = second; break;
            }
            clone.AtEquilibrium = false;
            ((ISimulationObject)(object)clone).Calculate(null);
            return clone;
        }

        /// <summary>Bubble and dew temperatures of the stream's overall composition at its pressure (NaN when the package cannot give them).</summary>
        private static void BubbleDew(IMaterialStream s, out double tb, out double td)
        {
            tb = double.NaN; td = double.NaN;
            var pp = s.GetPropertyPackageObjectCopy() as PropertyPackage;
            if (pp == null) return;
            var clone = s.Clone();
            clone.SetPropertyPackageObject(pp);
            pp.CurrentMaterialStream = clone;
            var z = s.GetOverallComposition();
            double p = Pres(s), t = Temp(s);
            try { tb = Convert.ToDouble(((object[])pp.DW_CalcBubT(z, p, t))[4]); } catch (Exception) { }
            try { td = Convert.ToDouble(((object[])pp.DW_CalcDewT(z, p, t))[4]); } catch (Exception) { }
        }

        private static InsightChart Chart(Ctx c, string title, string xTitle, string yTitle)
        {
            var ch = new InsightChart { Title = title, XTitle = xTitle, YTitle = yTitle };
            c.R.Charts.Add(ch);
            return ch;
        }

        private static InsightSeries Series(InsightChart ch, string title, IEnumerable<double> x, IEnumerable<double> y, bool scatter = false, bool dashed = false)
        {
            var s = new InsightSeries { Title = title, X = x.ToArray(), Y = y.ToArray(), Scatter = scatter, Dashed = dashed };
            ch.Series.Add(s);
            return s;
        }

        private static InsightTable Table(Ctx c, string title, params string[] columns)
        {
            var t = new InsightTable { Title = title };
            t.Columns.AddRange(columns);
            c.R.Tables.Add(t);
            return t;
        }

        // ------------------------------------------------------------------ material stream

        private static void ExplainStream(Ctx c)
        {
            var s = (IMaterialStream)c.Obj;
            c.R.Title = "Material stream " + c.R.ObjectTag + ": how its state was found";
            c.Heading("State");
            c.Line("The stream sits at T = " + c.T(Temp(s)) + " and P = " + c.P(Pres(s)) + ", with a mass flow of " + c.M(MassFlow(s)) + " (" + c.Mol(MolarFlow(s)) + ").");
            c.Line("You specified " + SpecName(s.SpecType) + ". From those two values and the composition, the property package (" + PackageName(s) + ") worked out everything else: which phases exist, how much of each, what they are made of, and every property listed on the stream.");
            FlashSection(c, s, s.Phases[3].Compounds.Values.Any(x => x.MoleFraction.GetValueOrDefault() > 0) ? 3 : 1, 2, VapFrac(s), true);
        }

        private static string SpecName(StreamSpec spec)
        {
            switch (spec)
            {
                case StreamSpec.Temperature_and_Pressure: return "temperature and pressure (PT flash)";
                case StreamSpec.Pressure_and_Enthalpy: return "pressure and enthalpy (PH flash)";
                case StreamSpec.Pressure_and_Entropy: return "pressure and entropy (PS flash)";
                case StreamSpec.Pressure_and_VaporFraction: return "pressure and vapour fraction (PVF flash)";
                case StreamSpec.Temperature_and_VaporFraction: return "temperature and vapour fraction (TVF flash)";
                default: return spec.ToString();
            }
        }

        private static string PackageName(IMaterialStream s)
        {
            var pp = s.GetPropertyPackageObject() as PropertyPackage;
            if (pp == null) return "?";
            return !string.IsNullOrEmpty(pp.ComponentName) ? pp.ComponentName : !string.IsNullOrEmpty(pp.Tag) ? pp.Tag : pp.GetType().Name;
        }

        /// <summary>
        /// The phase split of a stream as the Rachford-Rice problem: the K values, the function,
        /// the root, and where T sits against the bubble and dew points.
        /// </summary>
        private static void FlashSection(Ctx c, IMaterialStream s, int liquidPhase, int vaporPhase, double beta, bool withBubbleDew)
        {
            var names = s.Phases[0].Compounds.Keys.ToList();
            var z = names.Select(nm => s.Phases[0].Compounds[nm].MoleFraction.GetValueOrDefault()).ToArray();
            var x = names.Select(nm => s.Phases[liquidPhase].Compounds[nm].MoleFraction.GetValueOrDefault()).ToArray();
            var y = names.Select(nm => s.Phases[vaporPhase].Compounds[nm].MoleFraction.GetValueOrDefault()).ToArray();
            bool twoPhase = beta > 1e-8 && beta < 1 - 1e-8;

            c.Heading("Phase split");
            double solid = s.Phases[7].Properties.molarfraction.GetValueOrDefault();
            c.Line("On a molar basis, " + c.N(beta, "F4") + " of the stream is vapour and " + c.N(1 - beta - solid, "F4") + " is liquid" + (solid > 1e-8 ? ", with " + c.N(solid, "F4") + " as solid" : "") + ".");

            var t = Table(c, "Composition and K values", "Compound", "z (feed)", "x (liquid)", "y (vapour)", "K = y/x");
            for (int i = 0; i < names.Count; i++)
            {
                double k = twoPhase && x[i] > 0 ? y[i] / x[i] : double.NaN;
                t.Rows.Add(new[] { names[i], c.N(z[i], "F5"), twoPhase ? c.N(x[i], "F5") : "", twoPhase ? c.N(y[i], "F5") : "", twoPhase ? c.N(k, "F4") : "" });
            }

            if (withBubbleDew)
            {
                double tb, td;
                BubbleDew(s, out tb, out td);
                double T = Temp(s);
                if (!double.IsNaN(tb) || !double.IsNaN(td))
                {
                    c.Line("At this pressure, " + c.P(Pres(s)) + ", the mixture starts to boil at " + (double.IsNaN(tb) ? "a bubble point that could not be found" : c.T(tb) + " (its bubble point)") + " and finishes vaporising at " + (double.IsNaN(td) ? "a dew point that could not be found" : c.T(td) + " (its dew point)") + ". Between those two temperatures liquid and vapour exist side by side.");
                    if (!double.IsNaN(tb) && T < tb - 1e-6) c.Line("The stream temperature, " + c.T(T) + ", is below the bubble point, so the stream is entirely liquid. If it were heated past the bubble point, the first bubble of vapour would appear, richer in the lighter compounds than the liquid it came from.");
                    else if (!double.IsNaN(td) && T > td + 1e-6) c.Line("The stream temperature, " + c.T(T) + ", is above the dew point, so the stream is entirely vapour. If it were cooled below the dew point, the first drop of liquid would appear, richer in the heavier compounds than the vapour it came from.");
                    else c.Line("The stream temperature, " + c.T(T) + ", falls between the bubble and dew points, so the stream splits into a liquid and a vapour in equilibrium with each other. How much of each there is, and which compounds go where, is what the flash calculation decides.");
                }
            }

            if (!twoPhase)
            {
                c.Line("Since only one phase exists there is nothing to split. The composition of that phase is the composition of the stream itself, and all the property package had to do was evaluate its properties at this temperature and pressure.");
                return;
            }

            c.Heading("Rachford-Rice");
            c.Line("Each compound distributes itself between the two phases according to its K value, K_i = y_i / x_i, the ratio of its mole fraction in the vapour to its mole fraction in the liquid. A compound with K above 1 prefers the vapour; one with K below 1 prefers the liquid. Writing the mole balance of every compound in terms of its K value and adding them all up gives a single equation in a single unknown, the vapour fraction beta. That equation is the Rachford-Rice equation:");
            c.Line("    f(beta) = sum_i z_i (K_i - 1) / (1 + beta (K_i - 1)) = 0,    x_i = z_i / (1 + beta (K_i - 1)),    y_i = K_i x_i");
            var K = new double[names.Count];
            for (int i = 0; i < K.Length; i++) K[i] = x[i] > 0 ? y[i] / x[i] : 1.0;
            Func<double, double> f = b =>
            {
                double sum = 0;
                for (int i = 0; i < K.Length; i++) sum += z[i] * (K[i] - 1) / (1 + b * (K[i] - 1));
                return sum;
            };
            c.Line("The property package solved it and found beta = " + c.N(beta, "F4") + ". Putting that value back into the function gives f(beta) = " + c.N(f(beta), "E3") + ", which is zero within the tolerance of the flash. For comparison, f(0) = " + c.N(f(0), "F4") + " and f(1) = " + c.N(f(1), "F4") + ".");
            c.Line("The signs at the two ends tell the story. f(0) is positive when the mixture is above its bubble point (it wants to make some vapour) and f(1) is negative when it is below its dew point (it wants to keep some liquid), so the root has to lie between them, and it is found there. The K values themselves come from the property package at this temperature and pressure. For an ideal solution they reduce to K_i = Psat_i / P, the vapour pressure of each compound divided by the total pressure, which is a useful way to check the numbers in the table.");

            var ch = Chart(c, "Rachford-Rice function", "beta (vapour fraction)", "f(beta)");
            int n = 101;
            var bx = Enumerable.Range(0, n).Select(i => (double)i / (n - 1)).ToArray();
            Series(ch, "f(beta)", bx, bx.Select(b => f(b)));
            Series(ch, "zero", new[] { 0.0, 1.0 }, new[] { 0.0, 0.0 }, false, true);
            Series(ch, "solution", new[] { beta }, new[] { f(beta) }, true);
        }

        // ------------------------------------------------------------------ vessel

        private static void ExplainVessel(Ctx c)
        {
            var v = (Vessel)c.Obj;
            var feeds = c.Inlets();
            var vap = c.Outlet(0);
            var liq = c.Outlet(1);
            var liq2 = c.Outlet(2);
            c.R.Title = "Separator " + c.R.ObjectTag + ": one flash, two products";
            if (feeds.Count == 0 || vap == null || liq == null) { c.Warn("The vessel needs its feed and both product streams connected."); return; }

            c.Heading("What the vessel does");
            double T = Temp(vap), P = Pres(vap);
            c.Line("The separator does one thing. It takes everything that enters (" + feeds.Count + " feed stream" + (feeds.Count == 1 ? "" : "s") + "), brings the mixture to T = " + c.T(T) + " and P = " + c.P(P) + ", lets it settle into a vapour and a liquid in equilibrium with each other, and sends the vapour out through " + c.Tag(vap) + " and the liquid through " + c.Tag(liq) + (liq2 != null ? " and " + c.Tag(liq2) : "") + ".");
            string mode = v.OverrideT || v.OverrideP ? "the temperature and pressure typed on the vessel" : "the feed conditions (" + (v.PressureCalculation == Vessel.PressureBehavior.Minimum ? "lowest" : v.PressureCalculation == Vessel.PressureBehavior.Maximum ? "highest" : "average") + " feed pressure)";
            c.Line("The temperature and pressure of that flash come from " + mode + ".");

            c.Heading("Mass balance");
            var names = feeds[0].Phases[0].Compounds.Keys.ToList();
            var t = Table(c, "Molar flows (" + c.Su.molarflow + ")", new[] { "Compound" }.Concat(feeds.Select(s => "in: " + c.Tag(s))).Concat(new[] { "out: " + c.Tag(vap), "out: " + c.Tag(liq), "in - out" }).ToArray());
            double worst = 0;
            foreach (var n in names)
            {
                double fin = feeds.Sum(s => s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault());
                double fout = vap.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault() + liq.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault() + (liq2 != null ? liq2.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault() : 0);
                worst = Math.Max(worst, Math.Abs(fin - fout));
                var row = new List<string> { n };
                row.AddRange(feeds.Select(s => c.N(c.C(s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault(), c.Su.molarflow))));
                row.Add(c.N(c.C(vap.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault(), c.Su.molarflow)));
                row.Add(c.N(c.C(liq.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault(), c.Su.molarflow)));
                row.Add(c.N(c.C(fin - fout, c.Su.molarflow), "E2"));
                t.Rows.Add(row.ToArray());
            }
            c.Line("Nothing is created or consumed in a separator, so every mole of each compound that comes in has to leave in one of the products. In symbols, F z_i = V y_i + L x_i for each compound i. The last column of the table shows how well that closes; the largest gap is " + c.Mol(worst) + ", which is round-off from the solver.");

            c.Heading("Energy balance");
            double hin = feeds.Sum(s => EnergyFlow(s));
            double hout = EnergyFlow(vap) + EnergyFlow(liq) + (liq2 != null ? EnergyFlow(liq2) : 0);
            double q = v.DeltaQ.GetValueOrDefault();
            c.Line("The energy balance says that the enthalpy carried in by the feeds, plus any duty on the vessel, equals the enthalpy carried out by the products. In numbers, sum(m H)_in + Q = sum(m H)_out:  " + c.Qsi(hin) + " + " + c.Qsi(q) + " = " + c.Qsi(hout) + (Math.Abs(hin + q - hout) < 1e-3 * Math.Max(1, Math.Abs(hout)) ? ", which closes." : ", leaving a residual of " + c.Q(hin + q - hout) + "."));
            if (Math.Abs(q) < 1e-9) c.Line("There is no duty on this vessel, so the flash is adiabatic. The outlet temperature was not typed anywhere: it is whatever temperature makes the products carry exactly the enthalpy the feeds brought in. That is a pressure-enthalpy (PH) flash, and it is why the outlet temperature can differ from the feed temperature when part of the feed vaporises or condenses on the way in.");

            double V = MolarFlow(vap), L = MolarFlow(liq) + (liq2 != null ? MolarFlow(liq2) : 0);
            double beta = V + L > 0 ? V / (V + L) : 0;
            var mixed = Flash(feeds[0], StreamSpec.Temperature_and_Pressure, P, T);
            if (feeds.Count > 1)
            {
                var z = names.Select(n => feeds.Sum(s => s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault())).ToArray();
                double tot = z.Sum();
                if (tot > 0) for (int i = 0; i < z.Length; i++) z[i] /= tot;
                mixed.SetOverallComposition(z);
                mixed.AtEquilibrium = false;
                ((ISimulationObject)(object)mixed).Calculate(null);
            }
            FlashSection(c, mixed, 1, 2, beta, true);
        }

        // ------------------------------------------------------------------ heater and cooler

        private static void ExplainHeaterCooler(Ctx c)
        {
            bool heater = c.Obj.GraphicObject.ObjectType == ObjectType.Heater;
            var sin = c.Inlet(0);
            var sout = c.Outlet(0);
            c.R.Title = (heater ? "Heater " : "Cooler ") + c.R.ObjectTag + ": the energy balance";
            if (sin == null || sout == null) { c.Warn("Connect the inlet and the outlet streams."); return; }
            double q, eff, dp;
            string modeText;
            if (heater)
            {
                var h = (Heater)c.Obj;
                q = h.DeltaQ.GetValueOrDefault(); eff = h.Eficiencia.GetValueOrDefault(); dp = h.DeltaP.GetValueOrDefault();
                modeText = h.CalcMode.ToString();
            }
            else
            {
                var h = (Cooler)c.Obj;
                q = h.DeltaQ.GetValueOrDefault(); eff = h.Eficiencia.GetValueOrDefault(); dp = h.DeltaP.GetValueOrDefault();
                modeText = h.CalcMode.ToString();
            }
            double m = MassFlow(sin), h1 = Enth(sin), h2 = Enth(sout);
            double t1 = Temp(sin), t2 = Temp(sout), p1 = Pres(sin), p2 = Pres(sout);

            c.Heading("Energy balance");
            c.Line("The " + (heater ? "heater" : "cooler") + " is calculated in the mode \"" + modeText + "\", with an efficiency of " + c.N(eff) + " %.");
            c.Line("At steady state, with one stream in, one stream out and no moving parts, the first law reduces to a single statement: the duty is the change in enthalpy of the stream between its inlet and its outlet.");
            c.Line("    Q = m (H_out - H_in)" + (heater ? " / (eff / 100)" : " * (eff / 100)"));
            c.Line("    " + c.Msi(m) + " * (" + c.Hsi(h2) + " - " + c.Hsi(h1) + ")" + (Math.Abs(eff - 100) > 1e-9 ? (heater ? " / " : " * ") + c.N(eff / 100) : "") + " = " + c.Qsi(q));
            double qStream = m * (h2 - h1);
            c.Line("So the stream " + (qStream >= 0 ? "gains " : "loses ") + c.Q(Math.Abs(qStream)) + " between inlet and outlet. The " + (heater ? "heater itself draws " : "cooler itself rejects ") + c.Q(Math.Abs(q)) + " through its energy stream" + (Math.Abs(eff - 100) > 1e-9 ? "; the difference between the two numbers is the part of the duty that never reaches the stream, the efficiency loss" : "") + ".");
            c.Line("A note on the enthalpy values themselves: they are the property package's, with the reference state chosen so that H = 0 for the ideal gas formed from the elements at 25 C. Their absolute values (often negative) mean nothing on their own; only differences between two states do, and a difference is all the balance uses.");
            c.Line("The pressure falls by the drop you set: P_out = P_in - dP = " + c.P(p1) + " - " + c.U(dp, c.Su.deltaP) + " = " + c.P(p2) + ".");
            c.Line("The temperature goes from " + c.T(t1) + " to " + c.T(t2) + ", and the vapour fraction from " + c.N(VapFrac(sin), "F4") + " to " + c.N(VapFrac(sout), "F4") + ".");
            if (Math.Abs(t2 - t1) < 1e-6 && Math.Abs(q) > 1e-9) c.Line("Notice that the temperature did not change even though heat was exchanged. The stream is boiling or condensing, and all of the heat went into changing phase (latent heat) rather than into moving the temperature.");

            HeatingCurve(c, sin, p1, p2, t1, t2, m, h1, "Heating curve of " + c.Tag(sin), "The chart below is the heating curve: the temperature of the stream as heat is added to it, from the inlet state to the outlet state. Where the line is flat the stream is changing phase and the heat goes into latent heat. Where it slopes, the slope is 1 / (m Cp): a stream with a large flow or a large heat capacity needs more heat for the same rise in temperature.");
        }

        /// <summary>T and vapour fraction against the heat added from T1 to T2 (flashes on a copy of the stream).</summary>
        private static void HeatingCurve(Ctx c, IMaterialStream s, double p1, double p2, double t1, double t2, double m, double h1, string title, string caption)
        {
            if (Math.Abs(t2 - t1) < 1e-9) return;
            int n = 25;
            var qs = new List<double>(); var ts = new List<double>(); var vfs = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double frac = (double)i / (n - 1);
                double T = t1 + frac * (t2 - t1), P = p1 + frac * (p2 - p1);
                try
                {
                    var f = Flash(s, StreamSpec.Temperature_and_Pressure, P, T);
                    qs.Add(c.C(m * (Enth(f) - h1), c.Su.heatflow)); ts.Add(c.C(T, c.Su.temperature)); vfs.Add(VapFrac(f));
                }
                catch (Exception) { }
            }
            if (qs.Count < 3) return;
            c.Line(caption);
            var ch = Chart(c, title, "Heat added (" + c.Su.heatflow + ")", "T (" + c.Su.temperature + ")");
            Series(ch, "T", qs, ts);
            var ch2 = Chart(c, "Vapour fraction along the heating curve", "Heat added (" + c.Su.heatflow + ")", "Vapour fraction");
            Series(ch2, "vapour fraction", qs, vfs);
        }

        // ------------------------------------------------------------------ heat exchanger

        private static void ExplainHeatExchanger(Ctx c)
        {
            var hx = (HeatExchanger)c.Obj;
            var in1 = c.Inlet(0); var in2 = c.Inlet(1);
            var out1 = c.Outlet(0); var out2 = c.Outlet(1);
            c.R.Title = "Heat exchanger " + c.R.ObjectTag + ": the T-Q diagram";
            if (in1 == null || in2 == null || out1 == null || out2 == null) { c.Warn("Connect both inlets and both outlets."); return; }
            bool firstHot = Temp(in1) >= Temp(in2);
            var hin = firstHot ? in1 : in2; var hout = firstHot ? out1 : out2;
            var cin = firstHot ? in2 : in1; var cout = firstHot ? out2 : out1;
            double mh = MassFlow(hin), mc = MassFlow(cin);
            double th1 = Temp(hin), th2 = Temp(hout), tc1 = Temp(cin), tc2 = Temp(cout);
            double qh = mh * (Enth(hin) - Enth(hout)), qc = mc * (Enth(cout) - Enth(cin));
            double q = hx.Q.GetValueOrDefault();
            bool counter = hx.FlowDir == FlowDirection.CounterCurrent;

            c.Heading("Energy balance");
            c.Line("The hot side is " + c.Tag(hin) + ", which enters at " + c.T(th1) + " and leaves as " + c.Tag(hout) + " at " + c.T(th2) + ". The cold side is " + c.Tag(cin) + ", which enters at " + c.T(tc1) + " and leaves as " + c.Tag(cout) + " at " + c.T(tc2) + ".");
            c.Line("Heat released by the hot stream: Q_h = m_h (H_h,in - H_h,out) = " + c.Msi(mh) + " * (" + c.Hsi(Enth(hin)) + " - " + c.Hsi(Enth(hout)) + ") = " + c.Qsi(qh));
            c.Line("Heat taken by the cold stream: Q_c = m_c (H_c,out - H_c,in) = " + c.Msi(mc) + " * (" + c.Hsi(Enth(cout)) + " - " + c.Hsi(Enth(cin)) + ") = " + c.Qsi(qc));
            c.Line("The exchanger duty is Q = " + c.Q(q) + (hx.HeatLoss > 0 ? ", with a heat loss to the surroundings of " + c.Q(hx.HeatLoss) : "") + ". Apart from any loss, the heat the hot fluid gives up is exactly the heat the cold fluid receives, so the two values above have to agree; a small difference between them is round-off.");
            c.Line("The exchanger was calculated in the mode \"" + hx.CalculationMode + "\", with the streams flowing " + (counter ? "counter-current (in opposite directions)" : "co-current (in the same direction)") + ".");

            c.Heading("Driving force");
            double dt1 = counter ? th1 - tc2 : th1 - tc1;
            double dt2 = counter ? th2 - tc1 : th2 - tc2;
            double lmtd = Math.Abs(dt1 - dt2) < 1e-9 ? dt1 : (dt1 - dt2) / Math.Log(dt1 / dt2);
            c.Line("Heat only flows from the hotter fluid to the colder one, and how fast it flows depends on how far apart the two temperatures are at each point along the exchanger. At the two ends the differences are dT1 = " + c.U(dt1, c.Su.deltaT) + " and dT2 = " + c.U(dt2, c.Su.deltaT) + ".");
            if (dt1 <= 0 || dt2 <= 0)
                c.Line("One of those differences is zero or negative. That is a temperature cross: at that end the cold fluid would have to be hotter than the hot fluid, which no exchanger with this flow arrangement can achieve. The duty or the outlet temperatures you specified are not consistent with each other.");
            else
                c.Line("Because the difference changes along the exchanger, the effective driving force is the logarithmic mean of the two end values: LMTD = (dT1 - dT2) / ln(dT1 / dT2) = " + c.U(lmtd, c.Su.deltaT) + ". The exchanger reports " + c.U(hx.LMTD, c.Su.deltaT) + ".");
            double F = hx.LMTD_F > 0 ? hx.LMTD_F : 1.0;
            if (Math.Abs(F - 1) > 1e-6) c.Line("A multi-pass geometry is less effective than pure counter-current flow, and the correction factor F = " + c.N(F, "F4") + " accounts for that: the effective driving force becomes F * LMTD = " + c.U(F * lmtd, c.Su.deltaT) + ".");
            if (lmtd > 0 && !double.IsNaN(lmtd))
            {
                double ua = q * 1000 / (F * lmtd);
                c.Line("The heat transfer equation is Q = U A F LMTD. Turned around, U A = Q / (F LMTD) = " + c.N(ua) + " W/K: this product of the transfer coefficient and the area is what the exchanger needs in order to move this duty with this driving force" + (hx.Area.GetValueOrDefault() > 0 ? ". With A = " + c.U(hx.Area.GetValueOrDefault(), c.Su.area) + ", the coefficient works out to U = " + c.U(ua / hx.Area.GetValueOrDefault(), c.Su.heat_transf_coeff) : "") + ".");
            }
            if (hx.MaxHeatExchange > 0) c.Line("The effectiveness is Q / Q_max = " + c.Q(q) + " / " + c.Q(hx.MaxHeatExchange) + " = " + c.N(q / hx.MaxHeatExchange, "F3") + ". Q_max is the most heat that could possibly be transferred between these two streams: it would take an infinitely large exchanger, in which the stream with the smaller heat capacity leaves at the inlet temperature of the other one.");

            // the two curves against the heat exchanged, from flashes on copies of the streams
            int n = 25;
            var hq = new List<double>(); var ht = new List<double>();
            var cq = new List<double>(); var ct = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double frac = (double)i / (n - 1);
                try
                {
                    double T = th1 + frac * (th2 - th1);
                    var f = Flash(hin, StreamSpec.Temperature_and_Pressure, Pres(hin) + frac * (Pres(hout) - Pres(hin)), T);
                    double released = mh * (Enth(hin) - Enth(f));
                    hq.Add(counter ? qh - released : released); ht.Add(T);
                }
                catch (Exception) { }
                try
                {
                    double T = tc1 + frac * (tc2 - tc1);
                    var f = Flash(cin, StreamSpec.Temperature_and_Pressure, Pres(cin) + frac * (Pres(cout) - Pres(cin)), T);
                    cq.Add(mc * (Enth(f) - Enth(cin))); ct.Add(T);
                }
                catch (Exception) { }
            }
            if (hq.Count > 2 && cq.Count > 2)
            {
                // pinch: the smallest gap between the curves on a common heat axis
                double qmax = Math.Min(hq.Max(), cq.Max()), qmin = Math.Max(hq.Min(), cq.Min());
                double pinch = double.PositiveInfinity, pinchQ = 0, pinchTh = 0, pinchTc = 0;
                var hOrder = hq.Select((v, i) => i).OrderBy(i => hq[i]).ToList();
                var cOrder = cq.Select((v, i) => i).OrderBy(i => cq[i]).ToList();
                for (int k = 0; k <= 200; k++)
                {
                    double qq = qmin + (qmax - qmin) * k / 200.0;
                    double Th = Interp(hOrder.Select(i => hq[i]).ToArray(), hOrder.Select(i => ht[i]).ToArray(), qq);
                    double Tc = Interp(cOrder.Select(i => cq[i]).ToArray(), cOrder.Select(i => ct[i]).ToArray(), qq);
                    if (Th - Tc < pinch) { pinch = Th - Tc; pinchQ = qq; pinchTh = Th; pinchTc = Tc; }
                }
                c.Heading("T-Q diagram");
                c.Line("The T-Q diagram below draws the temperature of each stream against the heat it has exchanged so far" + (counter ? " (counter-current: the hot stream enters at the right and moves left, the cold one enters at the left and moves right)" : " (co-current: both streams enter at the left)") + ". At any point, the vertical distance between the two lines is the local driving force. A bend or a flat stretch in a line means that stream is changing phase there.");
                c.Line("The two lines come closest at Q = " + c.Q(pinchQ) + ", where the hot stream is at " + c.T(pinchTh) + " and the cold one at " + c.T(pinchTc) + ", a gap of " + c.U(pinch, c.Su.deltaT) + ". That closest point is the pinch" + (hx.MITA > 0 ? "; the minimum approach you specified is " + c.U(hx.MITA, c.Su.deltaT) : "") + ".");
                if (pinch < -1e-6) c.Line("The two lines cross inside the exchanger even though the end temperatures look fine. A phase change bends one of the curves into the other, and at the crossing the cold fluid would be hotter than the hot fluid. This duty cannot be achieved in a real exchanger; reduce it or change the outlet temperatures.");
                else if (pinch < 1.0) c.Line("The closest approach is under 1 K. An exchanger can only do that with a very large area, because U A = Q / LMTD grows without limit as the gap between the curves closes.");
                var ch = Chart(c, "T-Q diagram", "Heat exchanged (" + c.Su.heatflow + ")", "T (" + c.Su.temperature + ")");
                Series(ch, "hot: " + c.Tag(hin), hq.Select(v => c.C(v, c.Su.heatflow)), ht.Select(v => c.C(v, c.Su.temperature)));
                Series(ch, "cold: " + c.Tag(cin), cq.Select(v => c.C(v, c.Su.heatflow)), ct.Select(v => c.C(v, c.Su.temperature)));
                Series(ch, "pinch", new[] { c.C(pinchQ, c.Su.heatflow), c.C(pinchQ, c.Su.heatflow) }, new[] { c.C(pinchTc, c.Su.temperature), c.C(pinchTh, c.Su.temperature) }, false, true);
            }
        }

        private static double Interp(double[] xs, double[] ys, double x)
        {
            if (xs.Length == 0) return double.NaN;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
            for (int i = 1; i < xs.Length; i++)
                if (x <= xs[i])
                {
                    double w = xs[i] - xs[i - 1];
                    return w <= 0 ? ys[i] : ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / w;
                }
            return ys[ys.Length - 1];
        }

        // ------------------------------------------------------------------ pump

        private static void ExplainPump(Ctx c)
        {
            var p = (Pump)c.Obj;
            var sin = c.Inlet(0); var sout = c.Outlet(0);
            c.R.Title = "Pump " + c.R.ObjectTag + ": work and head";
            if (sin == null || sout == null) { c.Warn("Connect the inlet and the outlet streams."); return; }
            double m = MassFlow(sin), rho = Dens(sin), qv = VolFlow(sin);
            double dp = Pres(sout) - Pres(sin), w = p.DeltaQ.GetValueOrDefault(), eff = p.Eficiencia.GetValueOrDefault();
            double hyd = dp * qv / 1000.0;
            c.Heading("Energy balance");
            c.Line("The pump is calculated in the mode \"" + p.CalcMode + "\", with an efficiency of " + c.N(eff) + " %.");
            c.Line("All of the shaft power goes into the liquid, so it shows up as the rise of its enthalpy: W = m (H_out - H_in) = " + c.Msi(m) + " * (" + c.Hsi(Enth(sout)) + " - " + c.Hsi(Enth(sin)) + ") = " + c.Qsi(m * (Enth(sout) - Enth(sin))) + ". The pump reports " + c.Q(w) + ".");
            c.Line("The useful part of that work is what actually raises the pressure, the hydraulic power: W_hyd = dP * Q_vol = " + c.U(dp, c.Su.deltaP) + " * " + c.U(qv, c.Su.volumetricFlow) + " = " + c.Q(hyd) + ".");
            if (w > 0) c.Line("The ratio of the two is the efficiency: W_hyd / W = " + c.Q(hyd) + " / " + c.Q(w) + " = " + c.N(100 * hyd / w, "F1") + " %. The rest of the shaft work is dissipated by friction inside the pump and ends up as heat in the liquid, which is why the outlet is warmer by " + c.U(Temp(sout) - Temp(sin), c.Su.deltaT) + ".");
            c.Line("Pump makers quote the pressure rise as a head, the height of a column of this liquid that the pump could hold up: h = dP / (rho g) = " + c.U(dp, c.Su.deltaP) + " / (" + c.U(rho, c.Su.density) + " * 9.81 m/s2) = " + c.U(dp / (rho * 9.80665), c.Su.distance) + ".");
            c.Line("Because a liquid hardly compresses, the ideal (isentropic) work per kilogram is simply dP / rho = " + c.N(dp / rho / 1000, "F4") + " kJ/kg. The real work per kilogram is " + c.N(Enth(sout) - Enth(sin), "F4") + " kJ/kg; the difference is the friction loss.");
            if (p.NPSH.HasValue && p.NPSH.Value != 0) c.Line("The available NPSH is " + c.U(p.NPSH.Value, c.Su.distance) + ": the margin by which the inlet pressure exceeds the vapour pressure of the liquid, expressed as a head. If it falls below what the pump needs, the liquid boils inside the pump and the pump cavitates.");
        }

        // ------------------------------------------------------------------ compressor and expander

        private static void ExplainCompressorExpander(Ctx c)
        {
            bool compressor = c.Obj.GraphicObject.ObjectType == ObjectType.Compressor;
            var sin = c.Inlet(0); var sout = c.Outlet(0);
            c.R.Title = (compressor ? "Compressor " : "Expander ") + c.R.ObjectTag + ": the isentropic reference";
            if (sin == null || sout == null) { c.Warn("Connect the inlet and the outlet streams."); return; }
            double effA, effP, w, k, headA, headP;
            string mode, path;
            if (compressor)
            {
                var o = (Compressor)c.Obj;
                effA = o.AdiabaticEfficiency; effP = o.PolytropicEfficiency; w = o.DeltaQ; k = o.AdiabaticCoefficient; headA = o.AdiabaticHead; headP = o.PolytropicHead; mode = o.CalcMode.ToString(); path = o.ProcessPath.ToString();
            }
            else
            {
                var o = (Expander)c.Obj;
                effA = o.AdiabaticEfficiency; effP = o.PolytropicEfficiency; w = o.DeltaQ; k = o.AdiabaticCoefficient; headA = o.AdiabaticHead; headP = o.PolytropicHead; mode = o.CalcMode.ToString(); path = o.ProcessPath.ToString();
            }
            double m = MassFlow(sin), h1 = Enth(sin), h2 = Enth(sout), s1 = Entr(sin), s2 = Entr(sout);
            double p1 = Pres(sin), p2 = Pres(sout), t1 = Temp(sin), t2 = Temp(sout);

            c.Heading("Energy balance");
            c.Line("The machine is calculated in the mode \"" + mode + "\" along the " + path + " path, with an adiabatic efficiency of " + c.N(effA) + " % and a polytropic efficiency of " + c.N(effP) + " %.");
            c.Line("The machine exchanges no heat with its surroundings, so the shaft work is exactly the change of enthalpy of the gas: W = m (H_out - H_in) = " + c.Msi(m) + " * (" + c.Hsi(h2) + " - " + c.Hsi(h1) + ") = " + c.Qsi(m * (h2 - h1)) + ". The machine reports " + c.Q(w) + ".");
            c.Line("The pressure ratio is P_out / P_in = " + c.P(p2) + " / " + c.P(p1) + " = " + c.N(p2 / p1, "F3") + ", and the temperature goes from " + c.T(t1) + " to " + c.T(t2) + ".");

            c.Heading("Isentropic reference");
            double h2s = double.NaN, t2s = double.NaN;
            try
            {
                var iso = Flash(sin, StreamSpec.Pressure_and_Entropy, p2, s1);
                h2s = Enth(iso); t2s = Temp(iso);
            }
            catch (Exception ex) { c.Warn("The isentropic flash failed: " + ex.Message); }
            if (!double.IsNaN(h2s))
            {
                c.Line("To judge the machine we compare it with the ideal one, which is reversible as well as adiabatic and therefore changes the gas at constant entropy: S_out = S_in = " + c.S(s1) + ". A flash at the outlet pressure and that entropy gives the ideal outlet state, T_out,s = " + c.T(t2s) + " and H_out,s = " + c.H(h2s) + ".");
                if (compressor)
                {
                    double eta = (h2s - h1) / (h2 - h1);
                    c.Line("Isentropic efficiency = (H_out,s - H_in) / (H_out - H_in) = (" + c.Hsi(h2s) + " - " + c.Hsi(h1) + ") / (" + c.Hsi(h2) + " - " + c.Hsi(h1) + ") = " + c.N(100 * eta, "F1") + " %.");
                    c.Line("The real compressor needs more work than the ideal one to reach the same pressure. That extra work does not disappear: it shows up as a hotter outlet (" + c.T(t2) + " instead of " + c.T(t2s) + ") and as entropy generated by friction and turbulence inside the machine, S_out - S_in = " + c.S(s2 - s1) + ".");
                }
                else
                {
                    double eta = (h1 - h2) / (h1 - h2s);
                    c.Line("Isentropic efficiency = (H_in - H_out) / (H_in - H_out,s) = (" + c.Hsi(h1) + " - " + c.Hsi(h2) + ") / (" + c.Hsi(h1) + " - " + c.Hsi(h2s) + ") = " + c.N(100 * eta, "F1") + " %.");
                    c.Line("The real expander delivers less work than the ideal one from the same pressure drop. The work it fails to extract stays in the gas, which leaves warmer than the ideal outlet (" + c.T(t2) + " instead of " + c.T(t2s) + "), and entropy is generated on the way: S_out - S_in = " + c.S(s2 - s1) + ".");
                }
            }
            if (k > 0) c.Line("The heat capacity ratio is k = Cp/Cv = " + c.N(k, "F4") + ". If the gas were ideal, the isentropic outlet temperature would follow the textbook formula T2s = T1 (P2/P1)^((k-1)/k) = " + c.T(t1 * Math.Pow(p2 / p1, (k - 1) / k)) + "; compare it with the value the property package found above to see how far this gas is from ideal.");
            if (headA > 0) c.Line("Expressed as heads, the work per unit weight of gas: adiabatic head " + c.U(headA, c.Su.distance) + ", polytropic head " + c.U(headP, c.Su.distance) + ".");

            // the isentropic path and the real outlet on a T-P chart
            var ps = new List<double>(); var ts = new List<double>();
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                double P = p1 + (p2 - p1) * i / (n - 1);
                try { var f = Flash(sin, StreamSpec.Pressure_and_Entropy, P, s1); ps.Add(c.C(P, c.Su.pressure)); ts.Add(c.C(Temp(f), c.Su.temperature)); } catch (Exception) { }
            }
            if (ps.Count > 2)
            {
                var ch = Chart(c, "Isentropic path and real outlet", "P (" + c.Su.pressure + ")", "T (" + c.Su.temperature + ")");
                Series(ch, "isentropic (S = S_in)", ps, ts);
                Series(ch, "real outlet", new[] { c.C(p2, c.Su.pressure) }, new[] { c.C(t2, c.Su.temperature) }, true);
                Series(ch, "inlet", new[] { c.C(p1, c.Su.pressure) }, new[] { c.C(t1, c.Su.temperature) }, true);
            }
        }

        // ------------------------------------------------------------------ valve

        private static void ExplainValve(Ctx c)
        {
            var v = (Valve)c.Obj;
            var sin = c.Inlet(0); var sout = c.Outlet(0);
            c.R.Title = "Valve " + c.R.ObjectTag + ": the isenthalpic expansion";
            if (sin == null || sout == null) { c.Warn("Connect the inlet and the outlet streams."); return; }
            double h1 = Enth(sin), h2 = Enth(sout), p1 = Pres(sin), p2 = Pres(sout), t1 = Temp(sin), t2 = Temp(sout);
            c.Heading("Energy balance");
            c.Line("The valve is calculated in the mode \"" + v.CalcMode + "\".");
            c.Line("A valve exchanges no heat, does no work and involves no change of elevation, so the enthalpy of the stream is the same before and after it: H_out = H_in, " + c.H(h2) + " = " + c.H(h1) + ". The pressure drops, and everything else follows from that one fact.");
            c.Line("The outlet state is therefore found by a pressure-enthalpy flash at P_out = " + c.P(p2) + " and H = " + c.H(h1) + ", which gives T_out = " + c.T(t2) + ", a change of " + c.U(t2 - t1, c.Su.deltaT) + ".");
            double vf1 = VapFrac(sin), vf2 = VapFrac(sout);
            if (vf2 > vf1 + 1e-6) c.Line("The vapour fraction rises from " + c.N(vf1, "F4") + " to " + c.N(vf2, "F4") + ". At the lower pressure part of the liquid boils off, and the latent heat it needs is taken from the stream itself, which is why the stream cools.");
            else if (Math.Abs(t2 - t1) > 1e-6) c.Line("The temperature changed even though no phase change took place. This is the Joule-Thomson effect. For an ideal gas the enthalpy depends on temperature alone, so an expansion at constant enthalpy would leave the temperature untouched; a real gas " + (t2 < t1 ? "cools" : "warms") + " on expansion because its enthalpy also depends on pressure, through the forces between its molecules.");
            c.Line("The entropy rises across the valve, S_out - S_in = " + c.S(Entr(sout) - Entr(sin)) + ". An expansion through a valve is irreversible: the pressure thrown away could have driven a turbine and produced work, and the entropy generated is the measure of that lost opportunity.");

            var ps = new List<double>(); var ts = new List<double>(); var vfs = new List<double>();
            int n = 15;
            for (int i = 0; i < n; i++)
            {
                double P = p1 + (p2 - p1) * i / (n - 1);
                try { var f = Flash(sin, StreamSpec.Pressure_and_Enthalpy, P, h1); ps.Add(c.C(P, c.Su.pressure)); ts.Add(c.C(Temp(f), c.Su.temperature)); vfs.Add(VapFrac(f)); } catch (Exception) { }
            }
            if (ps.Count > 2)
            {
                var ch = Chart(c, "Isenthalpic path", "P (" + c.Su.pressure + ")", "T (" + c.Su.temperature + ")");
                Series(ch, "T at H = H_in", ps, ts);
                var ch2 = Chart(c, "Vapour fraction along the expansion", "P (" + c.Su.pressure + ")", "Vapour fraction");
                Series(ch2, "vapour fraction", ps, vfs);
            }
        }

        // ------------------------------------------------------------------ mixer and splitter

        private static void ExplainMixer(Ctx c)
        {
            var mx = (Mixer)c.Obj;
            var ins = c.Inlets(); var sout = c.Outlet(0);
            c.R.Title = "Mixer " + c.R.ObjectTag + ": the balances";
            if (ins.Count == 0 || sout == null) { c.Warn("Connect the inlets and the outlet."); return; }
            c.Heading("Mass balance");
            var names = sout.Phases[0].Compounds.Keys.ToList();
            var t = Table(c, "Molar flows (" + c.Su.molarflow + ")", new[] { "Compound" }.Concat(ins.Select(s => c.Tag(s))).Concat(new[] { "sum in", c.Tag(sout) }).ToArray());
            foreach (var n in names)
            {
                var row = new List<string> { n };
                double sum = 0;
                foreach (var s in ins) { double f = s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault(); sum += f; row.Add(c.N(c.C(f, c.Su.molarflow))); }
                row.Add(c.N(c.C(sum, c.Su.molarflow)));
                row.Add(c.N(c.C(sout.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault(), c.Su.molarflow)));
                t.Rows.Add(row.ToArray());
            }
            c.Line("For each compound, the flows arriving through the inlets add up to the flow leaving; the table shows the sum next to the outlet. In total mass: " + string.Join(" + ", ins.Select(s => c.M(MassFlow(s)))) + " = " + c.M(MassFlow(sout)) + ".");
            c.Heading("Energy balance");
            c.Line("The enthalpy carried by the inlets adds up to the enthalpy carried by the outlet: sum(m_i H_i) = m_out H_out, that is " + string.Join(" + ", ins.Select(s => c.Qsi(EnergyFlow(s)))) + " = " + c.Qsi(EnergyFlow(sout)) + ".");
            c.Line("The outlet temperature is not typed anywhere. It is the temperature at which the mixed stream carries exactly that enthalpy, found by a pressure-enthalpy flash: T_out = " + c.T(Temp(sout)) + ", with the inlets at " + string.Join(", ", ins.Select(s => c.T(Temp(s)))) + ". Only when nothing boils, condenses or gives off heat on mixing does this reduce to the familiar average weighted by flow and heat capacity.");
            c.Heading("Pressure");
            c.Line("The outlet pressure follows the rule \"" + mx.PressureCalculation + "\" applied to the inlet pressures (" + string.Join(", ", ins.Select(s => c.P(Pres(s)))) + "), which gives " + c.P(Pres(sout)) + ". In a real plant two streams at different pressures cannot meet in a pipe, because the one at the higher pressure would push the other back. The lowest inlet pressure is the physically sensible choice, and the difference to the other inlets is a pressure drop that happens somewhere upstream of the junction.");
        }

        private static void ExplainSplitter(Ctx c)
        {
            var ins = c.Inlets(); var outs = c.Outlets();
            c.R.Title = "Splitter " + c.R.ObjectTag + ": one stream, several flows";
            if (ins.Count == 0 || outs.Count == 0) { c.Warn("Connect the inlet and the outlets."); return; }
            var sin = ins[0];
            c.Heading("Mass balance");
            c.Line("A splitter only divides the flow. Nothing is separated, heated or throttled, so every outlet carries exactly the composition, temperature and pressure of the inlet; only the amounts differ.");
            double total = MassFlow(sin);
            foreach (var o in outs)
                c.Line("    " + c.Tag(o) + ": " + c.M(MassFlow(o)) + " = " + c.N(total > 0 ? MassFlow(o) / total : 0, "F4") + " of " + c.M(total) + ", T = " + c.T(Temp(o)) + ", P = " + c.P(Pres(o)));
            c.Line("The outlets add up to " + c.M(outs.Sum(o => MassFlow(o))) + ", against " + c.M(total) + " entering.");
        }

        // ------------------------------------------------------------------ reactors

        private static void ExplainReactor(Ctx c)
        {
            var rc = (Reactor)c.Obj;
            var ins = c.Inlets(); var outs = c.Outlets();
            var type = c.Obj.GraphicObject.ObjectType;
            string kind = type == ObjectType.RCT_Conversion ? "Conversion reactor" : type == ObjectType.RCT_Equilibrium ? "Equilibrium reactor" : type == ObjectType.RCT_Gibbs ? "Gibbs reactor" : type == ObjectType.RCT_CSTR ? "CSTR" : "PFR";
            c.R.Title = kind + " " + c.R.ObjectTag + ": the reactions";
            if (ins.Count == 0 || outs.Count == 0) { c.Warn("Connect the inlet and the outlet streams."); return; }

            c.Heading("What fixes the extent");
            switch (type)
            {
                case ObjectType.RCT_Conversion: c.Line("A conversion reactor does not work out how far the reactions go. You tell it, through the conversion of the base compound of each reaction (a number, or an expression in temperature), and it applies the stoichiometry to find how much of everything else is consumed or formed. No kinetics and no equilibrium are involved, so the result is only as good as the conversions you supplied."); break;
                case ObjectType.RCT_Equilibrium: c.Line("An equilibrium reactor assumes the reactions have all the time they need. It finds the extent of each reaction at which the equilibrium constant K(T) equals the product of the activities (or fugacities) raised to their stoichiometric coefficients. At that point the forward and reverse rates balance and the composition stops changing; the outlet is that composition."); break;
                case ObjectType.RCT_Gibbs: c.Line("A Gibbs reactor takes a different route to equilibrium: it looks for the outlet composition with the lowest total Gibbs energy while keeping the amount of each element fixed. No reactions have to be written, because at that minimum every conceivable reaction among the compounds present is already at equilibrium; anything the elements can form is allowed to form."); break;
                case ObjectType.RCT_CSTR: c.Line("A CSTR (continuous stirred-tank reactor) is assumed to be perfectly mixed, so the composition and temperature are the same everywhere inside the vessel and equal to those of the outlet. The reaction rate is therefore evaluated once, at the outlet conditions, and the balance of each compound reads F_i,in - F_i,out + r_i V = 0: what comes in, minus what goes out, plus what the reactions make in the volume V, is zero."); break;
                case ObjectType.RCT_PFR: c.Line("A PFR (plug-flow reactor) has no mixing along its length: each slice of fluid moves through the tube as a plug and reacts as it goes. The composition changes from inlet to outlet and the rate changes with it, so the balance is written per unit of volume, dF_i/dV = r_i, and integrated along the reactor from the inlet to the outlet."); break;
            }
            c.Line("The reactor operates in the mode \"" + rc.ReactorOperationMode + "\". The outlet temperature is " + c.T(rc.OutletTemperature) + " and the duty is " + c.Q(rc.DeltaQ.GetValueOrDefault()) + ".");

            var names = ins[0].Phases[0].Compounds.Keys.ToList();
            var t = Table(c, "Molar flows (" + c.Su.molarflow + ")", "Compound", "in", "out", "change", "conversion (%)");
            foreach (var n in names)
            {
                double fin = ins.Sum(s => s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault());
                double fout = outs.Sum(s => s.Phases[0].Compounds[n].MolarFlow.GetValueOrDefault());
                double conv;
                if (!rc.ComponentConversions.TryGetValue(n, out conv)) conv = fin > 0 ? (fin - fout) / fin : 0;
                t.Rows.Add(new[] { n, c.N(c.C(fin, c.Su.molarflow)), c.N(c.C(fout, c.Su.molarflow)), c.N(c.C(fout - fin, c.Su.molarflow)), fin > 0 && fout < fin ? c.N(100 * conv, "F2") : "" });
            }

            IReactionSet set;
            if (!string.IsNullOrEmpty(rc.ReactionSetID) && c.Fs.ReactionSets.TryGetValue(rc.ReactionSetID, out set))
            {
                var rt = Table(c, "Reactions of set " + set.Name, "Reaction", "Equation", "Type", "Base compound", "Conversion (%)", "Heat of reaction (kJ/mol)");
                foreach (var rsb in set.Reactions.Values)
                {
                    if (!rsb.IsActive) continue;
                    IReaction rxn;
                    if (!c.Fs.Reactions.TryGetValue(rsb.ReactionID, out rxn)) continue;
                    double conv;
                    if (!rc.Conversions.TryGetValue(rxn.ID, out conv)) rc.ComponentConversions.TryGetValue(rxn.BaseReactant, out conv);
                    rt.Rows.Add(new[] { rxn.Name, rxn.Equation, rxn.ReactionType.ToString(), rxn.BaseReactant, c.N(100 * conv, "F2"), c.N(rxn.ReactionHeat / 1000.0, "F2") });
                }
                c.Line("Multiplying the heat of reaction by the extent of each reaction gives the heat released (or absorbed) inside the reactor. In adiabatic operation that heat has nowhere to go but the stream, so it sets the outlet temperature; in isothermal operation it is removed (or supplied) through the duty, which is what the duty above represents.");
                if (type == ObjectType.RCT_CSTR || type == ObjectType.RCT_PFR) Levenspiel(c, rc, set, ins[0], outs);
            }
            else c.Warn("The reactor has no reaction set.");
        }

        /// <summary>
        /// 1/(-rA) against conversion for the first kinetic reaction, at the outlet temperature,
        /// with the CSTR rectangle and the PFR area compared to the reactor's own volume.
        /// </summary>
        private static void Levenspiel(Ctx c, Reactor rc, IReactionSet set, IMaterialStream feed, List<IMaterialStream> outs)
        {
            IReaction rxn = null;
            foreach (var rsb in set.Reactions.Values)
            {
                IReaction r;
                if (rsb.IsActive && c.Fs.Reactions.TryGetValue(rsb.ReactionID, out r) && r.ReactionType == ReactionType.Kinetic) { rxn = r; break; }
            }
            if (rxn == null) { c.Line("None of the reactions in this set has a kinetic rate expression, so there is no rate to plot against conversion and no Levenspiel plot."); return; }
            if (rxn.ReactionKinetics != ReactionKinetics.Expression || rxn.ReactionKinFwdType != ReactionKineticType.Arrhenius || (rxn.A_Reverse != 0 && rxn.ReactionKinRevType != ReactionKineticType.Arrhenius))
            {
                c.Line("The rate of " + rxn.Name + " is given by a user expression or a script. The Levenspiel plot is drawn only for Arrhenius power-law kinetics, whose rate this tool can evaluate at any conversion.");
                return;
            }
            var reaction = rxn as DWSIM.Thermodynamics.BaseClasses.Reaction;
            if (reaction == null) return;
            string A = rxn.BaseReactant;
            IReactionStoichBase baseSb;
            if (!rxn.Components.TryGetValue(A, out baseSb)) return;
            double nuA = Math.Abs(baseSb.StoichCoeff);

            // concentrations of the reaction phase at the inlet, mol/m3, and the conversion factors to the reaction's units
            var conc0 = new Dictionary<string, double>();
            double Qv;
            int phase;
            switch (rxn.ReactionPhase)
            {
                case ReactionPhase.Liquid: phase = 1; break;
                case ReactionPhase.Vapor: phase = 2; break;
                default: phase = 0; break;
            }
            Qv = feed.Phases[phase].Properties.volumetric_flow.GetValueOrDefault();
            if (Qv <= 0) { c.Line("The feed carries no " + rxn.ReactionPhase + " phase, and the Levenspiel plot needs the reaction phase to be present at the inlet to define the starting concentrations."); return; }
            foreach (var sb in rxn.Components.Values)
                conc0[sb.CompName] = feed.Phases[phase].Compounds[sb.CompName].MolarFlow.GetValueOrDefault() / Qv;
            var convf = rc.GetConvFactors(reaction, feed);
            double T = rc.OutletTemperature;
            double kf = rxn.A_Forward * Math.Exp(-Converter.Convert(rxn.E_Forward_Unit, "J/mol", rxn.E_Forward) / (8.314 * T));
            double kr = rxn.A_Reverse * Math.Exp(-Converter.Convert(rxn.E_Reverse_Unit, "J/mol", rxn.E_Reverse) / (8.314 * T));
            if (T < rxn.Tmin || T > rxn.Tmax) { kf = 0; kr = 0; }
            double cA0 = conc0[A];
            if (cA0 <= 0) { c.Line("The base compound " + A + " is absent from the feed, so there is no conversion to follow."); return; }

            // -rA(X) in mol/(m3 s), constant volumetric flow (liquid, or a gas with no mole change)
            Func<double, double> rateA = X =>
            {
                double rf = 1, rr = 1;
                foreach (var sb in rxn.Components.Values)
                {
                    double ci = conc0[sb.CompName] + sb.StoichCoeff / nuA * cA0 * X;
                    if (ci < 0) ci = 0;
                    double cu = ci * convf[sb.CompName];
                    rf *= Math.Pow(cu, sb.DirectOrder);
                    rr *= Math.Pow(cu, sb.ReverseOrder);
                }
                double r = kf * rf - kr * rr;                       // rate in the reaction's units
                return Converter.ConvertToSI(rxn.VelUnit, r);       // -r_A, mol/(m3 s): the consumption of the base compound
            };
            double FA0 = feed.Phases[phase].Compounds[A].MolarFlow.GetValueOrDefault();
            double FAout = outs.Sum(s => s.Phases[0].Compounds[A].MolarFlow.GetValueOrDefault());
            double Xout = FA0 > 0 ? Math.Max(0, Math.Min(1, (FA0 - FAout) / FA0)) : 0;
            double vol = rc is Reactor_CSTR ? ((Reactor_CSTR)rc).Volume : rc is Reactor_PFR ? ((Reactor_PFR)rc).Volume : 0;

            c.Heading("Levenspiel plot");
            c.Line("Take the reaction " + rxn.Name + " (" + rxn.Equation + ") at the outlet temperature, " + c.T(T) + ". The Arrhenius expression gives its rate constant: k_f = A exp(-E/RT) = " + c.N(rxn.A_Forward, "G4") + " * exp(-" + c.N(Converter.Convert(rxn.E_Forward_Unit, "J/mol", rxn.E_Forward), "G4") + " / (8.314 * " + c.N(T, "F2") + ")) = " + c.N(kf, "G4") + (kr > 0 ? ", and for the reverse reaction k_r = " + c.N(kr, "G4") : "") + ". The rate is expressed in " + rxn.VelUnit + " and the concentrations in " + rxn.ConcUnit + ".");
            c.Line("As the conversion X of " + A + " increases, the concentration of every compound follows the stoichiometry: C_i = C_i0 + (nu_i / |nu_A|) C_A0 X, starting from C_A0 = " + c.N(cA0, "G4") + " mol/m3. The volumetric flow is held at its inlet value of " + c.U(Qv, c.Su.volumetricFlow) + ", which is exact for a liquid and a fair approximation for a gas whose number of moles does not change.");
            int n = 60;
            var xs = new List<double>(); var inv = new List<double>();
            double xmax = Math.Min(0.995, Math.Max(Xout * 1.25, 0.9));
            for (int i = 0; i <= n; i++)
            {
                double X = xmax * i / n;
                double r = rateA(X);
                if (r <= 0 || double.IsNaN(r) || double.IsInfinity(r)) break;
                xs.Add(X); inv.Add(1.0 / r);
            }
            if (xs.Count < 3) { c.Line("The rate comes out zero or negative over the whole conversion range, either because the reaction is at equilibrium or because the temperature is outside the range the kinetics are valid for. There is nothing to plot."); return; }
            double rOut = rateA(Xout);
            double vCstr = rOut > 0 ? FA0 * Xout / rOut : double.NaN;
            double vPfr = 0;
            for (int i = 1; i < xs.Count && xs[i] <= Xout + 1e-12; i++) vPfr += 0.5 * (inv[i] + inv[i - 1]) * (xs[i] - xs[i - 1]);
            if (xs.Count > 1 && xs[xs.Count - 1] < Xout) vPfr += inv[inv.Count - 1] * (Xout - xs[xs.Count - 1]);
            vPfr *= FA0;
            c.Line("The Levenspiel plot draws F_A0 / (-r_A) against the conversion X. The higher the curve, the slower the reaction and the more volume each increment of conversion costs, so the volume a reactor needs can be read straight off it. A CSTR runs entirely at its outlet conditions, so its volume is the rectangle V = F_A0 X / (-r_A) evaluated at the outlet. A PFR passes through every conversion from 0 to X on its way, so its volume is the area under the curve, V = F_A0 int dX / (-r_A).");
            c.Line("At this reactor's conversion, X = " + c.N(Xout, "F4") + " of " + A + ", the rate is -r_A = " + c.N(rOut, "G4") + " mol/(m3 s). Read from the plot, a CSTR would need V_CSTR = " + c.U(vCstr, c.Su.volume) + " and a PFR would need V_PFR = " + c.U(vPfr, c.Su.volume) + ". This reactor's volume is " + c.U(vol, c.Su.volume) + ".");
            if (!double.IsNaN(vCstr) && vPfr > 0) c.Line(rc is Reactor_CSTR ? "Because the CSTR works at the outlet composition throughout, where the reactant is depleted and the rate is at its lowest, it needs " + c.N(vCstr / vPfr, "F2") + " times the volume a PFR would need for the same conversion. This holds whenever the rate rises with concentration, which is the usual case." : "Because the PFR starts at the inlet composition, where the reactant is concentrated and the rate is high, and only reaches the slow region near the outlet at the very end, it gets to this conversion with " + c.N(vPfr / vCstr, "F2") + " of the volume a CSTR would need.");
            if (Math.Abs(vol - (rc is Reactor_CSTR ? vCstr : vPfr)) > 0.2 * Math.Max(vol, 1e-9)) c.Line("The volume read from the plot differs from the reactor's own. The plot assumes a constant volumetric flow and the outlet temperature everywhere; inside the reactor the flow, the temperature or the density may change along the way, and other reactions may consume " + A + " as well. Each of these moves the curve.");
            var ch = Chart(c, "Levenspiel plot: " + rxn.Name, "Conversion of " + A, "F_A0 / (-r_A) (" + c.Su.volume + ")");
            Series(ch, "F_A0 / (-r_A)", xs, inv.Select(v => c.C(FA0 * v, c.Su.volume)));
            if (rOut > 0)
            {
                double h = c.C(FA0 / rOut, c.Su.volume);
                Series(ch, "CSTR rectangle", new[] { 0.0, Xout, Xout }, new[] { h, h, 0.0 }, false, true);
                Series(ch, "reactor outlet", new[] { Xout }, new[] { h }, true);
            }
        }

        // ------------------------------------------------------------------ rigorous column

        private static void ExplainRigorousColumn(Ctx c)
        {
            var col = (Column)c.Obj;
            bool distillation = col.ColumnType == Column.ColType.DistillationColumn;
            string kind = distillation ? "Distillation column" : col.ColumnType == Column.ColType.AbsorptionColumn ? "Absorber" : col.ColumnType == Column.ColType.ReboiledAbsorber ? "Reboiled absorber" : "Refluxed absorber";
            c.R.Title = kind + " " + c.R.ObjectTag + ": stage by stage";
            int n = col.NumberOfStages;
            var Tf = col.Tf; var Lf = col.Lf; var Vf = col.Vf;
            if (Tf == null || Tf.Length < n || Lf.Length < n || Vf.Length < n || col.xf.Count < n) { c.Warn("The column has no stored stage profiles; solve it again."); return; }
            var names = new List<string>();
            foreach (var id in col.compids) names.Add(id.ToString());
            int nc = names.Count;

            // streams by role
            var feeds = new List<Tuple<IMaterialStream, int>>();
            var products = new List<Tuple<IMaterialStream, string>>();
            foreach (var si in col.MaterialStreams.Values)
            {
                ISimulationObject o;
                if (string.IsNullOrEmpty(si.StreamID) || !c.Fs.SimulationObjects.TryGetValue(si.StreamID, out o)) continue;
                var ms = o as IMaterialStream;
                if (ms == null) continue;
                int stage = col.Stages.FindIndex(st => st.ID == si.AssociatedStage) + 1;
                switch (si.StreamBehavior)
                {
                    case StreamInformation.Behavior.Feed: feeds.Add(Tuple.Create(ms, stage)); break;
                    case StreamInformation.Behavior.Distillate: products.Add(Tuple.Create(ms, "distillate")); break;
                    case StreamInformation.Behavior.OverheadVapor: products.Add(Tuple.Create(ms, "overhead vapour")); break;
                    case StreamInformation.Behavior.BottomsLiquid: products.Add(Tuple.Create(ms, "bottoms")); break;
                    case StreamInformation.Behavior.Sidedraw: products.Add(Tuple.Create(ms, "side draw, stage " + stage)); break;
                }
            }
            if (feeds.Count == 0 || products.Count == 0) { c.Warn("The column needs at least one feed and one product connected."); return; }

            c.Heading("What fixes the result");
            c.Line("The column is modelled as " + n + " equilibrium stages, numbered from the top" + (distillation ? ": stage 1 is the condenser (" + col.CondenserType.ToString().Replace('_', ' ').ToLowerInvariant() + ") and stage " + n + " is the reboiler" : "") + ". On each stage the vapour that leaves is taken to be in equilibrium with the liquid that leaves (y_i = K_i x_i, corrected by the stage efficiency where one is set), and each stage closes its own mass and energy balances. Written for every stage and every compound, these are the MESH equations (Material balances, Equilibrium relations, Summation of mole fractions, Heat balances), a large system that the " + col.SolvingMethodName + " method solves all at once.");
            foreach (var f in feeds) c.Line("The feed " + c.Tag(f.Item1) + " enters on stage " + f.Item2 + " at " + c.T(Temp(f.Item1)) + ", with a vapour fraction of " + c.N(VapFrac(f.Item1), "F3") + " and a flow of " + c.Mol(MolarFlow(f.Item1)) + ".");
            foreach (var kv in col.Specs)
            {
                var sp = kv.Value;
                string where = kv.Key == "C" ? "Condenser" : "Reboiler";
                c.Line("The " + where.ToLowerInvariant() + " is specified by its " + sp.SType.ToString().Replace('_', ' ').ToLowerInvariant() + " = " + c.N(sp.SpecValue) + " " + sp.SpecUnit + (string.IsNullOrEmpty(sp.ComponentID) ? "" : " of " + sp.ComponentID) + ".");
            }
            c.Line("Once the number of stages, the feeds and the pressure profile are fixed, a column has two degrees of freedom left, and these two specifications use them up. Everything else you see here (temperatures, flows, compositions, duties) is a consequence of those two numbers; change either of them and the whole profile moves.");

            // mass balance
            c.Heading("Mass balance");
            var cols = new List<string> { "Compound" };
            cols.AddRange(feeds.Select(f => "in: " + c.Tag(f.Item1)));
            cols.AddRange(products.Select(pr => "out: " + c.Tag(pr.Item1)));
            cols.Add("recovery to " + c.Tag(products[0].Item1) + " (%)");
            var t = Table(c, "Molar flows (" + c.Su.molarflow + ")", cols.ToArray());
            var recovery = new Dictionary<string, double>();
            foreach (var nm in names)
            {
                double fin = feeds.Sum(f => f.Item1.Phases[0].Compounds[nm].MolarFlow.GetValueOrDefault());
                double top = products[0].Item1.Phases[0].Compounds[nm].MolarFlow.GetValueOrDefault();
                recovery[nm] = fin > 0 ? top / fin : 0;
                var row = new List<string> { nm };
                row.AddRange(feeds.Select(f => c.N(c.C(f.Item1.Phases[0].Compounds[nm].MolarFlow.GetValueOrDefault(), c.Su.molarflow))));
                row.AddRange(products.Select(pr => c.N(c.C(pr.Item1.Phases[0].Compounds[nm].MolarFlow.GetValueOrDefault(), c.Su.molarflow))));
                row.Add(fin > 0 ? c.N(100 * recovery[nm], "F2") : "");
                t.Rows.Add(row.ToArray());
            }
            c.Line("Every compound that enters leaves in one of the products: a column separates, it does not consume. How each compound splits between the top and the bottom is what the stages and the reflux buy. A compound recovered almost entirely at the top is lighter than the key pair, one that goes almost entirely to the bottom is heavier, and the keys are the two that actually split.");

            c.Heading("Energy balance");
            double hin = feeds.Sum(f => EnergyFlow(f.Item1)), hout = products.Sum(pr => EnergyFlow(pr.Item1));
            // the reboiler adds heat and the condenser removes it; the stored signs follow the solver's convention, so magnitudes are used
            double qc = Math.Abs(col.CondenserDuty), qb = Math.Abs(col.ReboilerDuty);
            double residual = hin + qb - hout - qc;
            c.Line("For the column as a whole, what the feeds bring in plus the reboiler duty equals what the products carry out plus the condenser duty. In numbers, sum(m H)_feeds + Q_reboiler = sum(m H)_products + Q_condenser:  " + c.Qsi(hin) + " + " + c.Qsi(qb) + " = " + c.Qsi(hout) + " + " + c.Qsi(qc) + (Math.Abs(residual) < 1e-2 * Math.Max(1, Math.Max(Math.Abs(hin), qb)) ? ", which closes." : ", leaving a residual of " + c.Q(residual) + "."));
            if (distillation && qb > 0 && qc > 0) c.Line("The reboiler supplies " + c.Q(qb) + " and the condenser removes " + c.Q(qc) + ", nearly the same amount. That is the price of the separation. The reboiler boils up a vapour that carries the lighter compounds towards the top, and the condenser turns that same vapour back into liquid so that part of it can flow down again as reflux. The heat goes in at the bottom and comes out at the top, and the separation happens in between.");

            // reflux and the keys
            if (distillation && products.Count >= 2 && products.Any(pr => pr.Item2 == "bottoms"))
            {
                var dist = products[0].Item1;
                var bott = products.First(pr => pr.Item2 == "bottoms").Item1;
                double D = MolarFlow(dist), L = Lf[0], V = Vf.Length > 1 ? Vf[1] : double.NaN;
                c.Heading("Reflux");
                c.Line("The reflux ratio is the liquid sent back down the column divided by the distillate taken out: R = L / D = " + c.Mol(L) + " / " + c.Mol(D) + " = " + c.N(D > 0 ? L / D : double.NaN, "F3") + ". The column reports " + c.N(col.RefluxRatio, "F3") + ". The vapour arriving at the condenser has to supply both the reflux and the distillate, so V = (R + 1) D = " + c.Mol(V) + ".");
                c.Line("More reflux means more liquid flowing down over the stages, washing the heavier compounds back towards the bottom, so the same stages give a sharper split. The cost is energy: the vapour V grows with R, and every extra mole of vapour has to be boiled in the reboiler and condensed again at the top.");

                // keys: the least volatile compound mostly recovered at the top, and the most volatile one mostly sent to the bottom
                int feedStage = Math.Max(0, Math.Min(n - 1, (feeds[0].Item2 > 0 ? feeds[0].Item2 : n / 2) - 1));
                var Kfeed = (double[])col.Kf[feedStage];
                var order = Enumerable.Range(0, nc).OrderByDescending(i => Kfeed[i]).ToList();   // most volatile first
                // the keys are the adjacent pair, in volatility order, across which the recovery to the top drops the most
                int lk = -1, hk = -1; double drop = 0.05;
                for (int k = 0; k + 1 < order.Count; k++)
                {
                    double d = recovery[names[order[k]]] - recovery[names[order[k + 1]]];
                    if (d > drop) { drop = d; lk = order[k]; hk = order[k + 1]; }
                }
                if (lk >= 0 && hk >= 0)
                {
                    string LK = names[lk], HK = names[hk];
                    double xDl = dist.Phases[0].Compounds[LK].MoleFraction.GetValueOrDefault(), xDh = dist.Phases[0].Compounds[HK].MoleFraction.GetValueOrDefault();
                    double xBl = bott.Phases[0].Compounds[LK].MoleFraction.GetValueOrDefault(), xBh = bott.Phases[0].Compounds[HK].MoleFraction.GetValueOrDefault();
                    var Ktop = (double[])col.Kf[0]; var Kbot = (double[])col.Kf[n - 1];
                    double aTop = Ktop[lk] / Ktop[hk], aBot = Kbot[lk] / Kbot[hk], alpha = Math.Sqrt(aTop * aBot);
                    c.Heading("The key pair against the shortcut methods");
                    c.Line("The separation is really being made between two compounds, the keys: the light key " + LK + ", of which " + c.N(100 * recovery[LK], "F1") + " % reaches the distillate, and the heavy key " + HK + ", of which only " + c.N(100 * recovery[HK], "F1") + " % does. How easy they are to separate is measured by their relative volatility, alpha = K_LK / K_HK, which is " + c.N(aTop, "F3") + " at the top of the column and " + c.N(aBot, "F3") + " at the bottom; the geometric mean of the two, " + c.N(alpha, "F3") + ", is used below. The further alpha is above 1, the easier the separation.");
                    if (xDh > 0 && xBl > 0 && alpha > 1)
                    {
                        double nmin = Math.Log((xDl / xDh) * (xBh / xBl)) / Math.Log(alpha);
                        c.Line("Fenske: N_min = ln[(x_D,LK / x_D,HK)(x_B,HK / x_B,LK)] / ln(alpha) = ln[(" + c.N(xDl, "F4") + " / " + c.N(xDh, "F4") + ")(" + c.N(xBh, "F4") + " / " + c.N(xBl, "F4") + ")] / ln(" + c.N(alpha, "F3") + ") = " + c.N(nmin, "F1") + ". That is the smallest number of stages that could make this split, and only at total reflux, with all the condensate returned and no product drawn. This column has " + n + " stages.");
                        if (xBl < 1e-4 || xDh < 1e-4) c.Line("One of the keys is below 0.0001 in a product. With a product that pure, Fenske's count hinges on a trace composition and on an average volatility, and it can come out higher than the stages the column actually has. The rigorous solution is the one to trust here: the column reaches the purity with fewer stages because the relative volatility is larger in the section where the separation is made.");
                        // Underwood with the feed-stage volatilities, q from the feed's vapour fraction
                        var z = names.Select(nm => feeds.Sum(f => f.Item1.Phases[0].Compounds[nm].MolarFlow.GetValueOrDefault())).ToArray();
                        double ztot = z.Sum(); if (ztot > 0) for (int i = 0; i < nc; i++) z[i] /= ztot;
                        double q = 1 - feeds.Sum(f => VapFrac(f.Item1) * MolarFlow(f.Item1)) / Math.Max(1e-12, feeds.Sum(f => MolarFlow(f.Item1)));
                        var a = Enumerable.Range(0, nc).Select(i => Kfeed[i] / Kfeed[hk]).ToArray();
                        Func<double, double> fu = th => { double sum = 0; for (int i = 0; i < nc; i++) sum += a[i] * z[i] / (a[i] - th); return sum - (1 - q); };
                        double lo = 1.0 + 1e-6, hi = a[lk] - 1e-6, theta = double.NaN;
                        if (hi > lo && fu(lo) * fu(hi) < 0)
                        {
                            for (int it = 0; it < 100; it++) { double m = 0.5 * (lo + hi); if (fu(lo) * fu(m) <= 0) hi = m; else lo = m; }
                            theta = 0.5 * (lo + hi);
                        }
                        if (!double.IsNaN(theta))
                        {
                            double rmin = -1; for (int i = 0; i < nc; i++) rmin += a[i] * dist.Phases[0].Compounds[names[i]].MoleFraction.GetValueOrDefault() / (a[i] - theta);
                            double ratio = rmin > 0 ? col.RefluxRatio / rmin : double.NaN;
                            c.Line("Underwood's method gives the other limit, the smallest reflux that could make the split if the column had infinitely many stages. With the volatilities of the feed stage and the feed quality q = " + c.N(q, "F3") + " (1 for a liquid at its bubble point, 0 for a saturated vapour), the Underwood root is theta = " + c.N(theta, "F4") + ", between alpha_HK = 1 and alpha_LK = " + c.N(a[lk], "F3") + ", and the minimum reflux comes out as R_min = sum_i alpha_i x_D,i / (alpha_i - theta) - 1 = " + c.N(rmin, "F3") + ". This column runs at R / R_min = " + c.N(ratio, "F2") + (ratio < 1.05 ? ", so close to the minimum that the stages around the feed are doing almost nothing: the profile is pinched there, and a small upset would lose the specification." : ratio > 3 ? ", well above the 1.2 to 1.5 that designs usually settle on. The reboiler is working harder than the separation needs; a lower reflux would save energy at the expense of a few more stages." : ", inside the range that designs usually settle on, between about 1.2 and 1.5 times the minimum."));
                        }
                        c.Line("To see these stages drawn one by one, open the McCabe-Thiele tool on the Utilities menu for the pair " + LK + " / " + HK + " at this reflux ratio.");
                    }
                }
            }

            // profiles
            var stages = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
            var chT = Chart(c, "Temperature profile", "Stage (1 = top)", "T (" + c.Su.temperature + ")");
            Series(chT, "T", stages, Tf.Take(n).Select(v => c.C(v, c.Su.temperature)));
            var chF = Chart(c, "Internal flows", "Stage (1 = top)", "Molar flow (" + c.Su.molarflow + ")");
            Series(chF, "liquid leaving the stage", stages, Lf.Take(n).Select(v => c.C(v, c.Su.molarflow)));
            Series(chF, "vapour leaving the stage", stages, Vf.Take(n).Select(v => c.C(v, c.Su.molarflow)));
            var chX = Chart(c, "Liquid composition profile", "Stage (1 = top)", "x (mole fraction)");
            for (int j = 0; j < nc; j++)
            {
                int jj = j;
                Series(chX, names[j], stages, Enumerable.Range(0, n).Select(i => ((double[])col.xf[i])[jj]));
            }
            c.Heading("Profiles");
            c.Line("The charts show what happens stage by stage. The temperature rises from the top to the bottom, because the top is rich in the lighter compounds, which boil at lower temperatures, and the bottom in the heavier ones. In the composition profile, a stretch of stages where nothing changes is a warning sign: those stages are doing little, either because there are more stages than this reflux can use or because the profile is pinched near the feed. The liquid and vapour flows jump at the feed stage, by the amount of liquid and vapour the feed brings in.");
        }

        // ------------------------------------------------------------------ shortcut column

        private static void ExplainShortcutColumn(Ctx c)
        {
            var col = (ShortcutColumn)c.Obj;
            var feed = c.Inlet(0);
            var dist = c.Outlet(0); var bott = c.Outlet(1);
            c.R.Title = "Shortcut column " + c.R.ObjectTag + ": Fenske, Underwood and Gilliland";
            if (feed == null || dist == null || bott == null) { c.Warn("Connect the feed, the distillate and the bottoms."); return; }
            string lk = col.m_lightkey, hk = col.m_heavykey;
            double xDl = dist.Phases[0].Compounds[lk].MoleFraction.GetValueOrDefault(), xDh = dist.Phases[0].Compounds[hk].MoleFraction.GetValueOrDefault();
            double xBl = bott.Phases[0].Compounds[lk].MoleFraction.GetValueOrDefault(), xBh = bott.Phases[0].Compounds[hk].MoleFraction.GetValueOrDefault();
            double Nmin = col.m_Nmin, Rmin = col.m_Rmin, N = col.m_N, R = col.m_refluxratio;

            c.Heading("Specifications");
            c.Line("The light key is " + lk + ", with a mole fraction of " + c.N(col.m_lightkeymolarfrac, "F4") + " allowed in the bottoms, and the heavy key is " + hk + ", with " + c.N(col.m_heavykeymolarfrac, "F4") + " allowed in the distillate. The reflux ratio is " + c.N(R, "F3") + ", the condenser pressure " + c.P(col.m_condenserpressure) + " and the reboiler pressure " + c.P(col.m_boilerpressure) + ".");
            c.Line("The shortcut method, known as FUG after Fenske, Underwood and Gilliland, sizes the column from the split of the two keys alone. It assumes that the relative volatility is the same on every stage and that the molar flows of liquid and vapour are constant within each section (constant molar overflow). Those assumptions make it fast and approximate: it is a design estimate, and the rigorous column is the check.");

            c.Heading("Fenske: minimum stages at total reflux");
            double sep = (xDl / xDh) * (xBh / xBl);
            double alpha = Nmin > 0 ? Math.Exp(Math.Log(sep) / Nmin) : double.NaN;
            c.Line("N_min = ln[(x_D,LK / x_D,HK)(x_B,HK / x_B,LK)] / ln(alpha_LK,HK) = ln[(" + c.N(xDl, "F4") + " / " + c.N(xDh, "F4") + ")(" + c.N(xBh, "F4") + " / " + c.N(xBl, "F4") + ")] / ln(" + c.N(alpha, "F4") + ") = " + c.N(Nmin, "F2"));
            c.Line("At total reflux everything that condenses at the top flows back down and no product is drawn, so each stage works as a full equilibrium step and the separation per stage is as large as it can be. Under those conditions " + c.N(Nmin, "F1") + " stages are needed, and no reflux ratio, however large, can do it with fewer. The alpha used here, " + c.N(alpha, "F3") + ", is the geometric mean of the relative volatility at the top and at the bottom.");

            c.Heading("Underwood: minimum reflux with infinite stages");
            c.Line("The other limit is the minimum reflux, the smallest reflux that could still make the split if the column had infinitely many stages. Underwood's method finds it in two steps: first solve sum_i alpha_i z_i / (alpha_i - theta) = 1 - q for the root theta that lies between the volatilities of the two keys, then evaluate R_min + 1 = sum_i alpha_i x_D,i / (alpha_i - theta).");
            c.Line("That gives R_min = " + c.N(Rmin, "F3") + ". Below this reflux the operating line touches the equilibrium curve (a pinch) and no number of stages would make the split. The column runs at R / R_min = " + c.N(R / Rmin, "F2") + ".");

            c.Heading("Gilliland: stages at the operating reflux");
            double X = (R - Rmin) / (R + 1), Y = (N - Nmin) / (N + 1);
            c.Line("X = (R - R_min) / (R + 1) = (" + c.N(R, "F3") + " - " + c.N(Rmin, "F3") + ") / (" + c.N(R, "F3") + " + 1) = " + c.N(X, "F4"));
            c.Line("The Gilliland correlation (here in the Molokanov form) relates X to Y = (N - N_min) / (N + 1) = " + c.N(Y, "F4") + ", which unwinds to N = " + c.N(N, "F2") + " theoretical stages at the operating reflux. This is the trade-off the correlation captures: more reflux, fewer stages, and the other way round.");
            c.Line("Kirkbride's equation then places the feed on stage " + c.N(col.ofs, "F1") + " from the top, using the ratio of the key compositions in the feed and in the two products to decide how many stages go above the feed and how many below.");

            c.Heading("Duties");
            c.Line("The condenser removes " + c.Q(col.m_Qc) + " at " + c.T(col.m_Tc) + " and the reboiler supplies " + c.Q(col.m_Qb) + " at " + c.T(col.m_Tb) + ". In the rectifying section the vapour rising is V = (R + 1) D = " + c.Mol(col.V) + " and the liquid flowing down is L = R D = " + c.Mol(col.L) + ".");
            c.Line("To see these stages drawn one by one, open the McCabe-Thiele tool on the Utilities menu for the two keys at this reflux ratio.");
        }
    }
}
