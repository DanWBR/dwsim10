//    Column internals rating: stage properties from a converged column and the rating of its sections.
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;

namespace DWSIM.Automation.DynamicRunner.ColumnInternals
{
    /// <summary>
    /// Rates the internals of a converged rigorous column: reads the stage flows and properties the
    /// column solved, then rates every stage of every section with the tray or packing models.
    /// </summary>
    public static class ColumnInternalsStudy
    {
        /// <summary>Runs the rating on the column named in the input. The column must be solved.</summary>
        public static ColumnInternalsResult Run(IFlowsheet host, ColumnInternalsInput input)
        {
            var column = FindColumn(host, input.ColumnName);
            if (column == null) throw new ArgumentException("Column '" + input.ColumnName + "' was not found on the flowsheet.");
            var props = ExtractStageProperties(host, column);
            var result = Rate(props, input);
            result.ColumnName = input.ColumnName;
            return result;
        }

        /// <summary>The rigorous columns of the flowsheet (distillation and absorption), by tag.</summary>
        public static List<string> ColumnNames(IFlowsheet host)
        {
            var names = new List<string>();
            foreach (var o in host.SimulationObjects.Values)
            {
                var t = o.GraphicObject != null ? o.GraphicObject.ObjectType : ObjectType.Nenhum;
                if (t == ObjectType.DistillationColumn || t == ObjectType.AbsorptionColumn || t == ObjectType.ReboiledAbsorber || t == ObjectType.RefluxedAbsorber)
                    names.Add(o.GraphicObject.Tag);
            }
            names.Sort();
            return names;
        }

        public static ISimulationObject FindColumn(IFlowsheet host, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            foreach (var o in host.SimulationObjects.Values)
                if (o.GraphicObject != null && string.Equals(o.GraphicObject.Tag, tag, StringComparison.OrdinalIgnoreCase)) return o;
            return null;
        }

        // ------------------------------------------------------------------ the case kept in the column

        /// <summary>The internals case saved in the column (its InternalsCase property), or null when there is none.</summary>
        public static ColumnInternalsInput LoadCaseFromColumn(ISimulationObject column)
        {
            var c = column as Column;
            if (c == null || string.IsNullOrWhiteSpace(c.InternalsCase)) return null;
            try
            {
                var input = ColumnInternalsInput.FromXml(XElement.Parse(c.InternalsCase));
                input.ColumnName = column.GraphicObject != null ? column.GraphicObject.Tag : input.ColumnName;
                return input;
            }
            catch { return null; }
        }

        /// <summary>Keeps the case in the column, so it travels with the simulation file.</summary>
        public static void StoreCaseInColumn(ISimulationObject column, ColumnInternalsInput input)
        {
            var c = column as Column;
            if (c == null) return;
            c.InternalsCase = input == null ? "" : input.ToXml().ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Number of stages of the column (condenser and reboiler included when the column has them).</summary>
        public static int StageCount(ISimulationObject column)
        {
            dynamic c = column;
            try { return (int)c.NumberOfStages; } catch { return 0; }
        }

        /// <summary>
        /// Stage flows and properties from the column's last solution: the vapour rising into the stage
        /// (from the stage below) and the liquid leaving it, with the phase properties of the property
        /// package at the stage temperature and pressure and compositions. Stage 1 is the top stage.
        /// </summary>
        public static List<StageProperties> ExtractStageProperties(IFlowsheet host, ISimulationObject column)
        {
            dynamic c = column;
            if (!column.Calculated) throw new InvalidOperationException("The column has not been solved; solve the flowsheet first.");
            double[] Tf = c.Tf, Vf = c.Vf, Lf = c.Lf;
            IList xf = c.xf, yf = c.yf, Kf = c.Kf;
            var stages = (IList)c.Stages;
            int n = Tf.Length;
            if (n == 0 || Vf.Length < n || Lf.Length < n || xf.Count < n || yf.Count < n)
                throw new InvalidOperationException("The column has no stored stage profiles; solve it again.");

            dynamic pp = c.PropertyPackage;
            var ms = (IMaterialStream)host.AddObject(ObjectType.MaterialStream, 0, 0, "internals_probe_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            ((ISimulationObject)ms).GraphicObject.Active = false;
            var list = new List<StageProperties>();
            try
            {
                ((dynamic)ms).SetPropertyPackage(pp);
                pp.CurrentMaterialStream = ms;

                for (int i = 0; i < n; i++)
                {
                    var sp = new StageProperties { Stage = i + 1 };
                    sp.T = Tf[i];
                    double p = 0;
                    try { p = (double)((dynamic)stages[i]).P; } catch { }
                    if (p <= 0) { try { p = ((double[])c.P0)[i]; } catch { } }
                    sp.P = p;

                    var x = (double[])xf[i];
                    var y = (double[])yf[i];
                    // vapour through the stage: what comes up from the stage below
                    int iv = i + 1 < n ? i + 1 : i;
                    sp.VaporMolarFlow = Vf[iv];
                    sp.LiquidMolarFlow = Lf[i];
                    var yv = (double[])yf[iv];

                    sp.VaporMW = (double)pp.AUX_MMM(yv);
                    sp.LiquidMW = (double)pp.AUX_MMM(x);
                    sp.VaporMassFlow = sp.VaporMolarFlow / 1000.0 * sp.VaporMW;
                    sp.LiquidMassFlow = sp.LiquidMolarFlow / 1000.0 * sp.LiquidMW;

                    ms.SetOverallComposition(yv);
                    ms.SetPhaseComposition(yv, 5);     // Phase.Vapor (the integer is the Phase enum value)
                    sp.VaporDensity = (double)pp.AUX_VAPDENS(sp.T, sp.P);
                    double etaV = double.NaN;
                    try { etaV = (double)pp.AUX_VAPVISCm(sp.T, sp.VaporDensity, sp.VaporMW); } catch { }
                    // the package's mixture vapour viscosity; kept within the range of real gases so a
                    // failed correlation does not distort the Reynolds numbers of the mass transfer models
                    sp.VaporViscosity = etaV > 0 && !double.IsNaN(etaV) ? Math.Max(2.0e-6, Math.Min(5.0e-4, etaV)) : 1.0e-5;

                    ms.SetOverallComposition(x);
                    ms.SetPhaseComposition(x, 0);      // Phase.Liquid
                    ms.SetPhaseComposition(x, 1);      // Phase.Liquid1
                    sp.LiquidDensity = (double)pp.AUX_LIQDENS(sp.T, x, sp.P);
                    double etaL = double.NaN;
                    try { etaL = (double)pp.AUX_LIQVISCm(sp.T, sp.P); } catch { }
                    sp.LiquidViscosity = etaL > 0 && !double.IsNaN(etaL) ? etaL : 1.0e-3;
                    double sigma = double.NaN;
                    try { sigma = (double)pp.AUX_SURFTM(sp.T); } catch { }
                    sp.SurfaceTension = sigma > 0 && !double.IsNaN(sigma) ? sigma : 0.02;

                    // diffusivities: order-of-magnitude estimates unless the section overrides them
                    sp.VaporDiffusivity = 1.0e-5 * Math.Pow(sp.T / 298.15, 1.75) * (101325.0 / Math.Max(sp.P, 1000.0));
                    sp.LiquidDiffusivity = 1.0e-9 * (sp.T / 298.15) * (1.0e-3 / sp.LiquidViscosity);

                    // stripping factor of the component transferring to the vapour (largest y - x)
                    try
                    {
                        var K = (double[])Kf[i];
                        int key = 0, heavy = 0; double best = double.NegativeInfinity, worst = double.PositiveInfinity;
                        for (int j = 0; j < x.Length; j++)
                        {
                            var d = y[j] - x[j];
                            if (d > best) { best = d; key = j; }
                            if (d < worst) { worst = d; heavy = j; }
                        }
                        if (sp.LiquidMolarFlow > 0 && K[key] > 0) sp.StrippingFactor = K[key] * sp.VaporMolarFlow / sp.LiquidMolarFlow;
                        if (K[key] > 0 && K[heavy] > 0 && heavy != key) sp.RelativeVolatility = K[key] / K[heavy];
                    }
                    catch { }
                    list.Add(sp);
                }
            }
            finally
            {
                try { host.DeleteSelectedObject(null, null, ((ISimulationObject)ms).GraphicObject, false, false); } catch { }
            }
            return list;
        }

        /// <summary>
        /// Writes the rated pressure profile and/or the O'Connell efficiencies into the column's stages.
        /// Pressures: the top stage keeps its pressure and each rated tray or packed stage adds its own
        /// pressure drop to the stage below (stages outside the sections carry the running value); the
        /// column's linear pressure drop is switched off so the solver uses the stage pressures.
        /// Efficiencies: the O'Connell value of every rated tray goes into the stage efficiency. The
        /// column is left to be solved again; returns what was written.
        /// </summary>
        public static string ApplyToColumn(ISimulationObject column, ColumnInternalsResult result, bool pressures, bool efficiencies)
        {
            dynamic c = column;
            var stages = (IList)c.Stages;
            int n = stages.Count;
            var dp = new double[n + 1];
            var eff = new double[n + 1];
            for (int i = 0; i <= n; i++) eff[i] = double.NaN;
            foreach (var sr in result.Sections)
                foreach (var r in sr.Stages)
                {
                    if (r.Stage < 1 || r.Stage > n) continue;
                    if (!double.IsNaN(r.PressureDropTotal) && r.PressureDropTotal > 0) dp[r.Stage] = r.PressureDropTotal;
                    if (!double.IsNaN(r.OConnellEfficiency)) eff[r.Stage] = r.OConnellEfficiency;
                }
            int np = 0, ne = 0;
            if (pressures)
            {
                double p = (double)((dynamic)stages[0]).P;
                for (int i = 1; i < n; i++)
                {
                    p += dp[i];
                    ((dynamic)stages[i]).P = p;
                    if (dp[i] > 0) np++;
                }
                try { c.ColumnPressureDrop = double.NaN; } catch { }
            }
            if (efficiencies)
            {
                for (int i = 0; i < n; i++)
                    if (!double.IsNaN(eff[i + 1])) { ((dynamic)stages[i]).Efficiency = eff[i + 1]; ne++; }
                // a packed stage is a theoretical stage by definition: the HETP already carries the efficiency
                foreach (var sr in result.Sections)
                    if (!sr.Section.IsTray)
                        foreach (var r in sr.Stages)
                            if (r.Stage >= 1 && r.Stage <= n) ((dynamic)stages[r.Stage - 1]).Efficiency = 1.0;
            }
            WriteGeometry(column, result);
            try { column.Calculated = false; } catch { }
            var parts = new List<string>();
            if (pressures) parts.Add(np + " stage pressure drops written (top stage pressure kept, linear profile off)");
            if (efficiencies) parts.Add(ne + " stage efficiencies written");
            return string.Join("; ", parts) + ". Solve the flowsheet and rate again.";
        }

        /// <summary>Writes the sized diameter, the internals height and the height of every rated stage (tray spacing or
        /// HETP) into the column, where the costing and the dynamic holdups read them.</summary>
        public static void WriteGeometry(ISimulationObject column, ColumnInternalsResult result)
        {
            var c = column as Column;
            if (c == null) return;
            double d = 0;
            foreach (var sr in result.Sections) if (sr.Diameter > d) d = sr.Diameter;
            if (d > 0) c.EstimatedDiameter = d;
            if (result.TotalHeight > 0) c.EstimatedHeight = result.TotalHeight;
            foreach (var sr in result.Sections)
            {
                var packing = sr.Section.IsTray ? null : sr.Section.ResolvePacking();
                foreach (var r in sr.Stages)
                {
                    if (r.Stage < 1 || r.Stage > c.Stages.Count) continue;
                    var st = c.Stages[r.Stage - 1];
                    var h = sr.Section.IsTray ? sr.Section.TraySpacing : (!double.IsNaN(r.HETP) && r.HETP > 0 ? r.HETP : sr.AverageHETP);
                    if (h > 0) st.StageHeight = h;
                    // the dynamic model reads the packing from the stage
                    st.IsPacked = !sr.Section.IsTray && packing != null;
                    if (st.IsPacked)
                    {
                        st.PackingName = sr.Section.CustomPacking == null ? sr.Section.PackingName : "";
                        st.PackingStructured = packing.Structured;
                        st.PackingFp = packing.Fp; st.PackingFpd = packing.Fpd; st.PackingArea = packing.a; st.PackingVoid = packing.Epsilon;
                        st.PackingCh = packing.Ch; st.PackingCp = packing.Cp; st.PackingCs = packing.Cs;
                        st.PackingCorrugationSide = packing.CorrugationSide; st.PackingCorrugationAngle = packing.CorrugationAngle;
                        st.PackingModel = (int)sr.Section.PackingModel;
                    }
                }
            }
        }

        /// <summary>True when a section is packed and its bed height is given, so its number of stages follows from the HETP.</summary>
        public static bool HasRestageableSection(ColumnInternalsInput input)
        {
            return input.Sections.Any(s => !s.IsTray && s.BedHeight > 0);
        }

        /// <summary>
        /// Sets the number of stages of every packed section with a given bed height to bed height / average HETP,
        /// inserting theoretical stages evenly into the section or removing stages that carry no feed, draw or duty.
        /// The other sections keep their stages; the stage ranges of the input are moved to follow. The column's
        /// initial estimates are rebuilt and it is left to be solved again. Returns what was done.
        /// </summary>
        public static string ApplyStagesToColumn(ISimulationObject column, ColumnInternalsResult result, ColumnInternalsInput input)
        {
            var c = column as Column;
            if (c == null) throw new ArgumentException("Only a rigorous column can be re-staged.");
            var log = new List<string>();
            var changes = new List<Tuple<int, int, int>>();   // old from, old to, delta
            // bottom sections first so the indices of the sections above stay valid while stages move
            foreach (var s in input.Sections.Where(x => !x.IsTray && x.BedHeight > 0).OrderByDescending(x => x.FromStage).ToList())
            {
                var sr = result.Sections.FirstOrDefault(x => x.Section.Name == s.Name && x.Section.FromStage == s.FromStage && x.Section.ToStage == s.ToStage);
                if (sr == null || double.IsNaN(sr.AverageHETP) || sr.AverageHETP <= 0) { log.Add(s.Name + ": no HETP to re-stage with."); continue; }
                int nOld = s.ToStage - s.FromStage + 1;
                int nNew = Math.Max(1, (int)Math.Round(s.BedHeight / sr.AverageHETP));
                if (nNew == nOld) { log.Add(s.Name + ": " + nOld + " stages already match the bed (" + s.BedHeight.ToString("0.00") + " m / HETP " + sr.AverageHETP.ToString("0.000") + " m)."); continue; }
                int from = s.FromStage - 1, to = s.ToStage - 1;   // 0-based
                if (from < 0 || to >= c.Stages.Count || from > to) { log.Add(s.Name + ": stage range outside the column."); continue; }
                int delta;
                if (nNew > nOld)
                {
                    // interleave new stages evenly among the old ones, keeping their order
                    var old = c.Stages.GetRange(from, nOld);
                    var merged = new List<Stage>();
                    int used = 0;
                    for (int j = 0; j < nNew; j++)
                    {
                        int mapped = (int)Math.Floor((double)j * nOld / nNew);
                        if (mapped >= used && used < nOld) merged.Add(old[used++]);
                        else merged.Add(new Stage(Guid.NewGuid().ToString()));
                    }
                    while (used < nOld) merged.Add(old[used++]);
                    c.Stages.RemoveRange(from, nOld);
                    c.Stages.InsertRange(from, merged);
                    delta = merged.Count - nOld;
                }
                else
                {
                    var referenced = new HashSet<string>();
                    foreach (var si in c.MaterialStreams.Values) referenced.Add(si.AssociatedStage);
                    foreach (var si in c.EnergyStreams.Values) referenced.Add(si.AssociatedStage);
                    var removable = new List<int>();
                    for (int i = from; i <= to; i++)
                        if (!referenced.Contains(c.Stages[i].ID) && !referenced.Contains(c.Stages[i].Name)) removable.Add(i);
                    int toRemove = Math.Min(nOld - nNew, removable.Count);
                    if (toRemove < nOld - nNew) log.Add(s.Name + ": only " + toRemove + " of " + (nOld - nNew) + " stages could be removed; the others carry feeds, draws or duties.");
                    var picked = new List<int>();
                    for (int k = 0; k < toRemove; k++) picked.Add(removable[(int)Math.Floor((k + 0.5) * removable.Count / toRemove)]);
                    foreach (var i in picked.Distinct().OrderByDescending(x => x)) c.Stages.RemoveAt(i);
                    delta = -picked.Distinct().Count();
                }
                for (int i = 0; i < c.Stages.Count; i++)
                    if (i > 0 && i < c.Stages.Count - 1 && (string.IsNullOrEmpty(c.Stages[i].Name) || c.Stages[i].Name.StartsWith("Stage"))) c.Stages[i].Name = "Stage" + i;
                changes.Add(Tuple.Create(s.FromStage, s.ToStage, delta));
                log.Add(s.Name + ": " + nOld + " stages to " + (nOld + delta) + " (bed " + s.BedHeight.ToString("0.00") + " m / HETP " + sr.AverageHETP.ToString("0.000") + " m).");
            }
            if (changes.Count == 0) return string.Join(" ", log);

            // move the stage ranges of every section to follow the stages that were inserted or removed above it
            foreach (var s in input.Sections)
            {
                int shift = 0, own = 0;
                foreach (var ch in changes)
                {
                    if (ch.Item1 == s.FromStage && ch.Item2 == s.ToStage) own = ch.Item3;
                    else if (ch.Item2 < s.FromStage) shift += ch.Item3;
                }
                s.FromStage += shift;
                s.ToStage += shift + own;
            }
            c.NumberOfStages = c.Stages.Count;
            c.InitialEstimates = c.RebuildEstimates();
            WriteGeometry(column, result);
            try { column.Calculated = false; } catch { }
            return string.Join(" ", log) + " The column has " + c.Stages.Count + " stages now; solve the flowsheet and rate again.";
        }

        /// <summary>
        /// Rates the column, writes the rated pressure profile and O'Connell efficiencies into it, solves the
        /// flowsheet and rates again, until the column pressure drop settles within the tolerance or the passes
        /// run out. The host must solve synchronously (RequestCalculationAndWait). Returns the last rating with
        /// the passes in its log; Converged says whether the tolerance was met.
        /// </summary>
        public static ColumnInternalsResult RunIterating(IFlowsheet host, ColumnInternalsInput input, Action<string> progress = null)
        {
            var ci = CultureInfo.InvariantCulture;
            var column = FindColumn(host, input.ColumnName);
            if (column == null) throw new ArgumentException("Column '" + input.ColumnName + "' was not found on the flowsheet.");
            var result = Run(host, input);
            result.Input = input;
            var log = new List<string> { string.Format(ci, "pass 0: column pressure drop {0:F0} Pa, {1} stages", result.TotalPressureDrop, StageCount(column)) };
            if (progress != null) progress(log[0]);
            bool restage = input.IterateStages && HasRestageableSection(input);
            if (!input.IteratePressures && !input.IterateEfficiencies && !restage) { result.Log.AddRange(log); return result; }
            int passes = 0; bool converged = false;
            for (int it = 1; it <= Math.Max(1, input.MaxIterations); it++)
            {
                int stagesBefore = StageCount(column);
                if (restage) log.Add("pass " + it + ": " + ApplyStagesToColumn(column, result, input));
                ApplyToColumn(column, result, input.IteratePressures, input.IterateEfficiencies);
                bool stagesChanged = StageCount(column) != stagesBefore;
                List<Exception> errors = null;
                try { errors = host.RequestCalculationAndWait(); }
                catch (Exception ex) { errors = new List<Exception> { ex }; }
                if (errors != null && errors.Count > 0)
                {
                    log.Add("pass " + it + ": the flowsheet did not solve with the rated profile: " + errors[0].Message);
                    result.Log.AddRange(log); result.Iterations = passes; result.Converged = false;
                    return result;
                }
                if (!column.Calculated)
                {
                    log.Add("pass " + it + ": the column did not solve with the rated profile.");
                    result.Log.AddRange(log); result.Iterations = passes; result.Converged = false;
                    return result;
                }
                var next = Run(host, input);
                next.Input = input;
                passes = it;
                var change = Math.Abs(next.TotalPressureDrop - result.TotalPressureDrop) / Math.Max(Math.Abs(result.TotalPressureDrop), 1.0);
                double effChange = 0.0;
                for (int k = 0; k < Math.Min(next.Sections.Count, result.Sections.Count); k++)
                    for (int j = 0; j < Math.Min(next.Sections[k].Stages.Count, result.Sections[k].Stages.Count); j++)
                    {
                        var a = next.Sections[k].Stages[j].OConnellEfficiency; var b = result.Sections[k].Stages[j].OConnellEfficiency;
                        if (!double.IsNaN(a) && !double.IsNaN(b)) effChange = Math.Max(effChange, Math.Abs(a - b));
                    }
                log.Add(string.Format(ci, "pass {0}: column pressure drop {1:F0} Pa (changed {2:F1} %), efficiencies moved by up to {3:F3}, {4} stages", it, next.TotalPressureDrop, change * 100.0, effChange, StageCount(column)));
                if (progress != null) progress(log[log.Count - 1]);
                result = next;
                if (change <= input.IterationTolerance && effChange <= 0.01 && !stagesChanged) { converged = true; break; }
            }
            if (!converged) log.Add("The passes ran out before the pressure drop settled within " + (input.IterationTolerance * 100.0).ToString("0.#", ci) + " %; the last rating is shown.");
            else log.Add("Settled after " + passes + " pass(es); the column now carries the rated pressures and efficiencies" + (restage ? " and the stages of its packed beds" : "") + ".");
            result.Log.AddRange(log);
            result.Iterations = passes;
            result.Converged = converged;
            return result;
        }

        /// <summary>Rates the sections against the stage properties; pure, no flowsheet needed.</summary>
        public static ColumnInternalsResult Rate(List<StageProperties> props, ColumnInternalsInput input)
        {
            var result = new ColumnInternalsResult { StageProperties = props };
            foreach (var s in input.Sections)
            {
                var sr = new SectionRating { Section = s };
                var stages = props.Where(p => p.Stage >= s.FromStage && p.Stage <= s.ToStage).ToList();
                if (stages.Count == 0) { sr.Warnings.Add("The section has no stages in the column."); result.Sections.Add(sr); continue; }
                var usable = stages.Where(p => p.VaporMassFlow > 0 && p.LiquidMassFlow > 0 && p.VaporDensity > 0 && p.LiquidDensity > 0).ToList();
                if (usable.Count == 0) { sr.Warnings.Add("No stage of the section carries both vapour and liquid."); result.Sections.Add(sr); continue; }

                PackingData packing = null;
                if (!s.IsTray)
                {
                    packing = s.ResolvePacking();
                    if (packing == null) { sr.Warnings.Add("No packing selected for the section."); result.Sections.Add(sr); continue; }
                }
                if (s.Type == InternalType.BubbleCapTray && s.FloodModel == TrayFloodModel.KisterHaas) sr.Warnings.Add("Kister and Haas covers sieve and valve trays; the bubble-cap section was rated with Fair's flooding correlation.");

                // required diameter for the target fraction of flood: the largest over the stages
                var target = s.IsTray ? input.TargetFloodFractionTrays : input.TargetFloodFractionPackings;
                double req = 0;
                foreach (var sp in usable)
                {
                    var d = s.IsTray ? TrayHydraulics.DiameterForFloodFraction(s, sp, target) : PackingHydraulics.DiameterForFloodFraction(s, packing, sp, target);
                    if (!double.IsNaN(d) && d > req) req = d;
                }
                sr.RequiredDiameter = req;
                sr.Diameter = s.Diameter > 0 ? s.Diameter : req;
                if (sr.Diameter <= 0) { sr.Warnings.Add("The section could not be sized."); result.Sections.Add(sr); continue; }

                double totalDp = 0, height = 0, hetpSum = 0; int hetpCount = 0;
                foreach (var sp in stages)
                {
                    if (!usable.Contains(sp))
                    {
                        var empty = new StageRating { Stage = sp.Stage, SectionName = s.Name, Type = s.Type, Diameter = sr.Diameter };
                        empty.Warnings.Add("Stage without both phases (condenser, reboiler or dry stage); not rated.");
                        sr.Stages.Add(empty);
                        continue;
                    }
                    var r = s.IsTray
                        ? TrayHydraulics.RateTray(s, sp, sr.Diameter, input.Turndown, input.MinDowncomerResidenceTime, 1.0)
                        : PackingHydraulics.RatePacking(s, packing, sp, sr.Diameter);
                    sr.Stages.Add(r);
                    if (!double.IsNaN(r.FloodFraction) && r.FloodFraction > sr.MaxFloodFraction) { sr.MaxFloodFraction = r.FloodFraction; sr.LimitingStage = r.Stage; }
                    if (s.IsTray)
                    {
                        if (!double.IsNaN(r.PressureDrop)) totalDp += r.PressureDrop;
                        height += s.TraySpacing;
                    }
                    else
                    {
                        if (!double.IsNaN(r.HETP)) { hetpSum += r.HETP; hetpCount++; }
                    }
                }
                if (!s.IsTray)
                {
                    sr.AverageHETP = hetpCount > 0 ? hetpSum / hetpCount : double.NaN;
                    sr.BedHeight = s.BedHeight > 0 ? s.BedHeight : hetpSum;
                    // pressure drop over the bed: each rated stage owns a slice of the bed
                    double slice = sr.BedHeight / Math.Max(1, sr.Stages.Count(x => !double.IsNaN(x.PressureDrop)));
                    foreach (var r in sr.Stages) if (!double.IsNaN(r.PressureDrop)) { r.PressureDropTotal = r.PressureDrop * slice; totalDp += r.PressureDropTotal; }
                    height = sr.BedHeight;
                    if (s.BedHeight > 0 && hetpSum > 0 && s.BedHeight < hetpSum)
                        sr.Warnings.Add("The bed height given is shorter than the stages times the HETP (" + hetpSum.ToString("0.00") + " m).");
                }
                sr.TotalPressureDrop = totalDp;
                result.TotalPressureDrop += totalDp;
                result.TotalHeight += height;
                result.Sections.Add(sr);
            }
            return result;
        }
    }
}
