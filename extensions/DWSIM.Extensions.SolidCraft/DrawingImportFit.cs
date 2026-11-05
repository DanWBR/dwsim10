// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The fit step of the import, where the Patreon edition's data reconciliation is installed: the
    /// equipment parameters the drawing does not give (the efficiencies of compressors, expanders
    /// and pumps, the reflux ratio of rigorous columns) are moved, within their bounds, to bring the
    /// built flowsheet to the stream table, the flowsheet solved by the simultaneous solver at each
    /// point. The reconciliation is found by reflection, so the open edition builds and runs without it.
    /// </summary>
    public static class DrawingImportFit
    {
        const string Namespace = "DWSIM.FlowsheetSolver.Simultaneous";

        /// <summary>An equipment parameter the fit may move, in the units the object's property takes.</summary>
        public class Parameter
        {
            public string Tag = "", Property = "", Name = "";
            public double Lower, Upper, Initial = double.NaN, Value = double.NaN, Sigma = double.NaN;
            public bool Use = true;
            public override string ToString() => Tag + ": " + Name;
        }

        public class Outcome
        {
            public bool Converged;
            public string Message = "", Report = "";
            public double Objective = double.NaN;
            public int Iterations, Solves;
            public List<Parameter> Parameters = new List<Parameter>();
        }

        static Type Find(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(Namespace + "." + name, false); } catch (Exception) { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Whether the reconciliation is installed (the Patreon edition's simultaneous solver).</summary>
        public static bool Available => Find("Reconciliation.DataReconciliation") != null;

        /// <summary>What keeps the fit from running: the membership the Patreon features check; empty when it can.</summary>
        public static string Denied()
        {
            var gate = Find("PatreonGate");
            if (gate == null) return Available ? "" : "the fit needs the data reconciliation of DWSIM's Patreon edition";
            try
            {
                var allowed = (bool)gate.GetMethod("IsAllowed", Type.EmptyTypes).Invoke(null, null);
                return allowed ? "" : (gate.GetProperty("DeniedMessage")?.GetValue(null) ?? gate.GetField("DeniedMessage")?.GetValue(null))?.ToString() ?? "a Patreon membership is needed";
            }
            catch (Exception ex) { return ex.GetBaseException().Message; }
        }

        /// <summary>
        /// The parameters the drawing leaves open and the table can tell: the efficiency of each
        /// compressor, expander and pump whose outlet temperature is on the table, and the reflux
        /// ratio of each rigorous column that works to one.
        /// </summary>
        public static List<Parameter> Parameters(DrawingReading r, DrawingImportBuilder.Result res)
        {
            var list = new List<Parameter>();
            bool OutletT(string tag) => r.Lines.Any(l => r.Takes(l) && string.Equals(l.From, tag, StringComparison.OrdinalIgnoreCase) && l.T.HasValue);
            foreach (var kv in res.Items)
            {
                var o = kv.Value;
                var tag = o.GraphicObject.Tag;
                if ((o is Compressor || o is Expander) && OutletT(kv.Key))
                    list.Add(new Parameter { Tag = tag, Property = "PROP_CO_1", Name = "adiabatic efficiency (%)", Lower = 30, Upper = 100 });
                else if (o is Pump && OutletT(kv.Key))
                    // a pump barely warms what it moves: its efficiency is offered, not fitted unless ticked
                    list.Add(new Parameter { Tag = tag, Property = "PROP_PU_1", Name = "efficiency (%)", Lower = 20, Upper = 100, Use = false });
                else if (o is DistillationColumn dc && dc.Specs.ContainsKey("C") &&
                         dc.Specs["C"].SType == DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSpec.SpecType.Stream_Ratio)
                    list.Add(new Parameter { Tag = tag, Property = "Condenser_Specification_Value", Name = "reflux ratio", Lower = 0.05, Upper = 50 });
            }
            return list;
        }

        /// <summary>
        /// The table's values of the built streams as measurements, in SI: the temperature (1 K),
        /// the pressure and the mass flow (1 %), and the mole fractions of what leaves a column
        /// whose reflux is fitted (1 %, at least 0.002).
        /// </summary>
        static List<(string tag, string prop, double value, double sigma)> Measurements(IFlowsheet fs, DrawingReading r, DrawingImportBuilder.Result res, List<Parameter> parameters)
        {
            var list = new List<(string, string, double, double)>();
            var u = r.Units;
            double? SI(string unit, double? v)
            {
                if (!v.HasValue || double.IsNaN(v.Value)) return null;
                try { return Converter.ConvertToSI(unit, v.Value); } catch (Exception) { return null; }
            }
            var columns = new HashSet<string>(parameters.Where(p => p.Use && p.Property == "Condenser_Specification_Value").Select(p => p.Tag), StringComparer.OrdinalIgnoreCase);
            foreach (var kv in res.Streams)
            {
                var l = kv.Key;
                if (l.Feed || !l.HasData) continue;
                var tag = kv.Value.GraphicObject.Tag;
                var t = SI(u.Temperature, l.T);
                var p = SI(u.Pressure, l.P);
                var w = SI(u.MassFlow, l.W);
                if (t.HasValue) list.Add((tag, "PROP_MS_0", t.Value, 1.0));
                if (p.HasValue && p.Value > 0) list.Add((tag, "PROP_MS_1", p.Value, Math.Max(0.01 * p.Value, 1000)));
                if (w.HasValue && w.Value > 0) list.Add((tag, "PROP_MS_2", w.Value, 0.01 * w.Value));
                var from = res.Items.TryGetValue(l.From ?? "", out var src) ? src.GraphicObject.Tag : "";
                if (!columns.Contains(from) || l.Compounds.Count == 0) continue;
                // mole fractions from the table's mass flows
                var moles = new Dictionary<string, double>();
                foreach (var c in l.Compounds)
                {
                    var name = fs.SelectedCompounds.Keys.FirstOrDefault(k => string.Equals(k, c.Key, StringComparison.OrdinalIgnoreCase)) ?? DrawingImportBuilder.CompoundName(fs, c.Key);
                    if (name == null || !fs.SelectedCompounds.ContainsKey(name) || c.Value < 0) continue;
                    moles[name] = (moles.TryGetValue(name, out var m) ? m : 0) + c.Value / fs.SelectedCompounds[name].Molar_Weight;
                }
                var total = moles.Values.Sum();
                if (total <= 0) continue;
                foreach (var m in moles.Where(x => x.Value / total > 1e-3))
                    list.Add((tag, "PROP_MS_102/" + m.Key, m.Value / total, Math.Max(0.01 * m.Value / total, 0.002)));
            }
            return list;
        }

        /// <summary>
        /// Moves the chosen parameters to bring the solved flowsheet to the table. The objects end
        /// with the fitted values, to be solved once more by the caller; the parameters carry their
        /// initial and fitted values and the standard deviation of each estimate. It may run off
        /// the interface's thread.
        /// </summary>
        public static Outcome Fit(IFlowsheet fs, DrawingReading r, DrawingImportBuilder.Result res, List<Parameter> parameters, CancellationToken ct, Action<string> log)
        {
            var outcome = new Outcome { Parameters = parameters };
            var denied = Denied();
            if (denied.Length > 0) { outcome.Message = denied; return outcome; }
            var use = parameters.Where(p => p.Use).ToList();
            if (use.Count == 0) { outcome.Message = "no parameters to fit"; return outcome; }
            var data = Measurements(fs, r, res, use);
            if (data.Count <= use.Count) { outcome.Message = "the table gives " + data.Count + " values for " + use.Count + " parameters; the fit needs more values than parameters"; return outcome; }

            var tMeasurement = Find("Reconciliation.Measurement");
            var tInput = Find("Reconciliation.AdjustableInput");
            var tOptions = Find("Reconciliation.ReconciliationOptions");
            var tRecon = Find("Reconciliation.DataReconciliation");
            var measurements = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tMeasurement));
            foreach (var d in data) measurements.Add(Activator.CreateInstance(tMeasurement, d.tag, d.prop, d.value, d.sigma));
            var inputs = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tInput));
            foreach (var p in use) inputs.Add(Activator.CreateInstance(tInput, p.Tag, p.Property, p.Lower, p.Upper));
            var options = Activator.CreateInstance(tOptions);
            tOptions.GetProperty("Log").SetValue(options, log ?? (Action<string>)(_ => { }));
            tOptions.GetProperty("Cancellation").SetValue(options, ct);
            tOptions.GetProperty("MaxIterations").SetValue(options, 30);
            log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Fitting {0} parameters to {1} values of the table", use.Count, data.Count));

            object result;
            try
            {
                var reconcile = tRecon.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "Reconcile" && m.GetParameters().Length == 4);
                result = reconcile.Invoke(null, new object[] { fs, measurements, inputs, options });
            }
            catch (Exception ex) { outcome.Message = ex.GetBaseException().Message; return outcome; }
            finally { GlobalSettings.Settings.CalculatorBusy = false; }

            object Get(object o, string name) => o.GetType().GetProperty(name)?.GetValue(o);
            // the flowsheet takes the fitted values; the caller solves it (on the interface's thread)
            var k = 0;
            foreach (var input in (IEnumerable)Get(result, "Inputs"))
            {
                var p = use[k++];
                p.Initial = (double)Get(input, "Initial");
                p.Value = (double)Get(input, "Value");
                p.Sigma = (double)Get(input, "Sigma");
                if (!double.IsNaN(p.Value)) try { fs.GetFlowsheetSimulationObject(p.Tag)?.SetPropertyValue(p.Property, p.Value); } catch (Exception) { }
            }
            outcome.Converged = (bool)Get(result, "Converged");
            outcome.Message = (string)Get(result, "Message") ?? "";
            outcome.Objective = (double)Get(result, "Objective");
            outcome.Iterations = (int)Get(result, "Iterations");
            outcome.Solves = (int)Get(result, "FlowsheetSolves");
            try { outcome.Report = (string)result.GetType().GetMethod("Report").Invoke(result, null); } catch (Exception) { }
            return outcome;
        }

        /// <summary>What the fit did, a line per parameter.</summary>
        public static string Describe(Outcome o)
        {
            var ci = CultureInfo.InvariantCulture;
            var lines = new List<string>
            {
                (o.Converged ? "Fitted" : "The fit did not converge") + string.Format(ci, " in {0} iterations, {1} flowsheet solves", o.Iterations, o.Solves) +
                (o.Message.Length > 0 ? ": " + o.Message : "")
            };
            foreach (var p in o.Parameters.Where(p => p.Use && !double.IsNaN(p.Value)))
                lines.Add(string.Format(ci, "{0} {1}: {2:G4} (was {3:G4}{4})", p.Tag, p.Name, p.Value, p.Initial,
                    double.IsNaN(p.Sigma) ? "" : string.Format(ci, ", ± {0:G2}", p.Sigma)));
            return string.Join(Environment.NewLine, lines);
        }
    }
}
