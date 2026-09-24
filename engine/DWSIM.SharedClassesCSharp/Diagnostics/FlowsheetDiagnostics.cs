using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>
    /// Checks a flowsheet before it is solved, and explains what went wrong after it is.
    /// </summary>
    /// <remarks>
    /// The rules encode the mistakes that keep a freshly assembled flowsheet from solving: a feed
    /// with no composition, a port left dangling, a recycle with nothing to start from, a loop with
    /// nothing to tear it. After a solve they also look at the result the way an instructor would:
    /// a heater that cooled its stream, a pump that lowered the pressure, an exchanger whose outlets
    /// crossed. Each finding carries a fix, so a caller can act on it without having read the model,
    /// and <see cref="FindingExplanations"/> carries the longer story for whoever wants it.
    ///
    /// Checking is cheap and solving is not, so <see cref="Check"/> is worth running first every
    /// time. What it cannot know it does not guess: a rule fires only when it is certain, because a
    /// false blocker sends a caller chasing a problem that is not there.
    /// </remarks>
    public static class FlowsheetDiagnostics
    {
        /// <summary>Objects that legitimately have no material ports.</summary>
        private static readonly HashSet<ObjectType> PortlessTypes = new HashSet<ObjectType>
        {
            ObjectType.OT_Adjust, ObjectType.OT_Spec, ObjectType.Controller_PID,
            ObjectType.AnalogGauge, ObjectType.DigitalGauge, ObjectType.LevelGauge,
            ObjectType.Controller_Python, ObjectType.Controller_MPC,
            ObjectType.Input, ObjectType.Switch, ObjectType.OT_InformationCarrier
        };

        /// <summary>
        /// Everything wrong with the flowsheet as it stands, worst first.
        /// </summary>
        /// <param name="flowsheet">The flowsheet to check.</param>
        /// <returns>
        /// Findings ordered blockers first. An empty list means nothing known to be wrong; it is
        /// not a promise that the solve will converge.
        /// </returns>
        public static IReadOnlyList<Finding> Check(IFlowsheet flowsheet)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            var findings = new List<Finding>();

            CheckSetup(flowsheet, findings);
            if (findings.Any(f => f.Code == FlowsheetCodes.EmptyFlowsheet)) return Order(findings);

            CheckConnectivity(flowsheet, findings);
            CheckFeeds(flowsheet, findings);
            CheckSpecifications(flowsheet, findings);
            CheckLogicalObjects(flowsheet, findings);
            CheckStaleResults(flowsheet, findings);

            return Order(findings);
        }

        /// <summary>
        /// Explains a solve that failed, or that finished with objects left unconverged, and
        /// points out results that a physical process could not produce.
        /// </summary>
        /// <param name="flowsheet">The flowsheet that was solved.</param>
        /// <param name="errors">The exceptions the solver returned; may be null or empty.</param>
        public static IReadOnlyList<Finding> Diagnose(IFlowsheet flowsheet, IEnumerable<Exception> errors)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            var findings = new List<Finding>();
            var raised = (errors ?? Enumerable.Empty<Exception>()).Where(e => e != null).ToList();

            DiagnoseExceptions(flowsheet, raised, findings);
            DiagnoseUnconverged(flowsheet, findings);
            DiagnoseResults(flowsheet, findings);
            DiagnoseIneffectiveUnits(flowsheet, findings);
            DiagnosePlausibility(flowsheet, findings);

            // Whatever stopped the solve, the flowsheet's own faults belong in the report with it:
            // a solver exception is usually the symptom, and a setup fault the cause.
            findings.AddRange(Check(flowsheet).Where(f => f.Severity == DiagnosticSeverity.Blocker));

            return Order(findings);
        }

        // ── Before solving ────────────────────────────────────────────────────

        private static void CheckSetup(IFlowsheet flowsheet, List<Finding> findings)
        {
            if (flowsheet.SimulationObjects.Count == 0)
            {
                findings.Add(new Finding(FlowsheetCodes.EmptyFlowsheet, DiagnosticSeverity.Blocker, "",
                    "The flowsheet has no objects.",
                    "Add streams and unit operations before solving."));
                return;
            }

            if (flowsheet.SelectedCompounds.Count == 0)
            {
                findings.Add(new Finding(FlowsheetCodes.NoCompounds, DiagnosticSeverity.Blocker, "",
                    "The flowsheet has no compounds, so no stream can carry anything.",
                    "Add compounds before setting any composition."));
            }

            if (flowsheet.PropertyPackages.Count == 0)
            {
                findings.Add(new Finding(FlowsheetCodes.NoPropertyPackage, DiagnosticSeverity.Blocker, "",
                    "The flowsheet has no property package, so nothing can be flashed.",
                    "Add one, chosen for the compounds and the pressure range in play."));
            }

            var tags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var tag = TagOf(obj);
                if (string.IsNullOrEmpty(tag)) continue;
                int seen;
                tags[tag] = tags.TryGetValue(tag, out seen) ? seen + 1 : 1;
            }

            foreach (var duplicate in tags.Where(t => t.Value > 1))
            {
                findings.Add(new Finding(FlowsheetCodes.DuplicateTag, DiagnosticSeverity.Warning, duplicate.Key,
                    duplicate.Value + " objects share this tag, so addressing one of them by tag is ambiguous.",
                    "Rename all but one."));
            }
        }

        /// <summary>
        /// Results that belong to a property package the object no longer uses.
        /// </summary>
        /// <remarks>
        /// Replacing a package does not clear anything: every object keeps Calculated and its
        /// numbers until the next solve, and a student comparing packages reads the old ones.
        /// Objects saved before this check exist carry no record and are left alone.
        /// </remarks>
        private static void CheckStaleResults(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active || !obj.Calculated) continue;

                // A material stream resolves its package by ID behind a property that shadows the
                // base one, so the base property may be empty; ask the stream for its own.
                var stream = obj as IMaterialStream;
                var package = (stream != null ? stream.GetPropertyPackageObject() as IPropertyPackage : null) ?? obj.PropertyPackage;
                var solvedWith = obj.LastSolvedPropertyPackageID;
                if (package == null || string.IsNullOrEmpty(solvedWith) || solvedWith == package.UniqueID) continue;

                findings.Add(new Finding(FlowsheetCodes.PropertyPackageChanged, DiagnosticSeverity.Warning, TagOf(obj),
                    "Its results were computed with a property package that has since been replaced by " + package.Tag + ".",
                    "Solve the flowsheet again before reading them."));
            }
        }

        private static void CheckConnectivity(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                var type = graphic.ObjectType;
                var tag = TagOf(obj);

                if (type == ObjectType.MaterialStream || type == ObjectType.EnergyStream)
                {
                    var attachedIn = graphic.InputConnectors.Any(c => c.IsAttached);
                    var attachedOut = graphic.OutputConnectors.Any(c => c.IsAttached);

                    // A stream attached at neither end is in the flowsheet but not in the process. An
                    // energy stream attached at one end is the normal case: the duty of a heater, the
                    // power of a pump or a compressor, the heat a cooler removes.
                    if (!attachedIn && !attachedOut)
                    {
                        findings.Add(new Finding(FlowsheetCodes.StreamDangling, DiagnosticSeverity.Blocker, tag,
                            "This stream is connected to nothing at either end.",
                            "Connect it to a unit operation, or remove it."));
                    }
                    continue;
                }

                if (PortlessTypes.Contains(type)) continue;

                var materialIn = graphic.InputConnectors.Count(c => c.IsAttached && c.Type == ConType.ConIn);
                var materialOut = graphic.OutputConnectors.Count(c => c.IsAttached && c.Type == ConType.ConOut);

                if (materialIn == 0 && materialOut == 0)
                {
                    findings.Add(new Finding(FlowsheetCodes.UnitUnconnected, DiagnosticSeverity.Blocker, tag,
                        "This unit operation has nothing connected to it.",
                        "Connect a feed and a product, or remove it."));
                }
                else if (materialIn == 0)
                {
                    findings.Add(new Finding(FlowsheetCodes.UnitNoFeed, DiagnosticSeverity.Blocker, tag,
                        "This unit operation has no feed, so it has nothing to process.",
                        "Connect a material stream to one of its inlet ports."));
                }
                else if (materialOut == 0)
                {
                    findings.Add(new Finding(FlowsheetCodes.UnitNoProduct, DiagnosticSeverity.Blocker, tag,
                        "This unit operation has no product, so its result has nowhere to go.",
                        "Connect a material stream to one of its outlet ports."));
                }
            }
        }

        private static void CheckFeeds(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;
                if (graphic.ObjectType != ObjectType.MaterialStream) continue;

                // Only a boundary feed is specified by hand. Everything downstream is computed, and
                // reporting a computed stream as empty before the solve would be noise.
                if (graphic.InputConnectors.Any(c => c.IsAttached)) continue;

                var stream = obj as IMaterialStream;
                if (stream == null) continue;
                var tag = TagOf(obj);

                CheckFeedConditions(stream, tag, findings);
                CheckFeedComposition(stream, tag, findings);
            }
        }

        private static void CheckFeedConditions(IMaterialStream stream, string tag, List<Finding> findings)
        {
            double temperature, pressure, massFlow, molarFlow;

            try
            {
                temperature = stream.GetTemperature();
                pressure = stream.GetPressure();
                massFlow = stream.GetMassFlow();
                molarFlow = stream.GetMolarFlow();
            }
            catch (Exception)
            {
                // A stream too incomplete to answer is covered by the composition rules below.
                return;
            }

            var spec = stream.SpecType;
            var needsPressure = spec != StreamSpec.Temperature_and_VaporFraction
                && spec != StreamSpec.Volume_and_Temperature
                && spec != StreamSpec.Volume_and_Enthalpy
                && spec != StreamSpec.Volume_and_Entropy;
            var needsTemperature = spec == StreamSpec.Temperature_and_Pressure
                || spec == StreamSpec.Temperature_and_VaporFraction
                || spec == StreamSpec.Volume_and_Temperature;

            if (needsPressure && (pressure <= 0.0 || double.IsNaN(pressure)))
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoPressure, DiagnosticSeverity.Blocker, tag,
                    "This feed has no pressure, so it cannot be flashed.",
                    "Set its pressure."));
            }

            if (needsTemperature && (temperature <= 0.0 || double.IsNaN(temperature)))
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoTemperature, DiagnosticSeverity.Blocker, tag,
                    "This feed has no temperature, so it cannot be flashed.",
                    "Set its temperature, or specify a vapour fraction instead."));
            }

            if (spec == StreamSpec.Pressure_and_VaporFraction || spec == StreamSpec.Temperature_and_VaporFraction)
            {
                double? vf = null;
                try { vf = stream.Phases[2].Properties.molarfraction; } catch (Exception) { }
                if (vf.HasValue && (vf.Value < 0.0 || vf.Value > 1.0))
                {
                    findings.Add(new Finding(FlowsheetCodes.VaporFractionOutOfRange, DiagnosticSeverity.Blocker, tag,
                        "This feed is specified by a vapour fraction of " +
                        vf.Value.ToString("G4", CultureInfo.InvariantCulture) + ", which is outside 0 to 1.",
                        "A vapour fraction is a mole fraction: 0 for a saturated liquid, 1 for a saturated vapour."));
                }
            }

            if (massFlow <= 0.0 && molarFlow <= 0.0)
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoFlow, DiagnosticSeverity.Warning, tag,
                    "This feed carries no flow, so everything downstream of it will be empty.",
                    "Set a mass or a molar flow."));
            }
        }

        private static void CheckFeedComposition(IMaterialStream stream, string tag, List<Finding> findings)
        {
            double total;
            try
            {
                total = stream.Phases[0].Compounds.Values.Sum(c => c.MoleFraction.GetValueOrDefault());
            }
            catch (Exception)
            {
                return;
            }

            if (total <= 0.0)
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoComposition, DiagnosticSeverity.Blocker, tag,
                    "Every compound in this feed is at zero, so it carries nothing.",
                    "Set its composition."));
            }
            else if (Math.Abs(total - 1.0) > 1e-4)
            {
                findings.Add(new Finding(FlowsheetCodes.FeedCompositionNotNormalised, DiagnosticSeverity.Warning, tag,
                    "The mole fractions of this feed sum to " +
                    total.ToString("G6", CultureInfo.InvariantCulture) + ", not 1.",
                    "Set the composition again; the fractions are normalised on the way in."));
            }
        }

        /// <summary>
        /// The specifications each unit's calculation mode reads, and whether they have a value.
        /// </summary>
        /// <remarks>
        /// Feeds are left to <see cref="CheckFeeds"/>, which names each missing value with its own
        /// code. Here a unit with a hole in its specification gets one finding listing the holes,
        /// which is what a student needs to see: "this heater is in outlet-temperature mode and has
        /// no outlet temperature".
        /// </remarks>
        private static void CheckSpecifications(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                var type = graphic.ObjectType;
                if (type == ObjectType.MaterialStream || type == ObjectType.EnergyStream) continue;
                if (type == ObjectType.OT_Adjust || type == ObjectType.OT_Spec || type == ObjectType.OT_Recycle) continue;

                var tag = TagOf(obj);

                CheckEfficiencies(obj, tag, type, findings);
                CheckSplitter(obj, tag, type, findings);
                CheckReactor(flowsheet, obj, tag, type, findings);

                var dof = DegreesOfFreedomAnalysis.Analyze(flowsheet, obj);
                if (dof == null || !dof.Supported || dof.Remaining == 0) continue;

                // The reactor rule above already covers an empty reaction set with its own code.
                var missing = dof.Missing.Where(s => s.Property != "ReactionSetID").Select(s => s.Name).ToList();
                if (missing.Count == 0) continue;

                var mode = string.IsNullOrEmpty(dof.Mode) ? "" : " in mode " + dof.Mode;
                findings.Add(new Finding(FlowsheetCodes.SpecMissing, DiagnosticSeverity.Warning, tag,
                    "This " + dof.ObjectType + mode + " has no value for: " + string.Join(", ", missing) + ".",
                    "Open its editor and fill those in, or switch to a calculation mode whose specifications you have."));
            }
        }

        private static void CheckEfficiencies(ISimulationObject obj, string tag, ObjectType type, List<Finding> findings)
        {
            string[] properties;
            switch (type)
            {
                case ObjectType.Pump:
                case ObjectType.Heater:
                case ObjectType.Cooler:
                    properties = new[] { "Eficiencia" };
                    break;
                case ObjectType.Compressor:
                case ObjectType.Expander:
                    properties = new[] { Text(obj, "ProcessPath") == "Polytropic" ? "PolytropicEfficiency" : "AdiabaticEfficiency" };
                    break;
                default:
                    return;
            }

            foreach (var property in properties)
            {
                var value = Num(obj, property);
                if (!value.HasValue) continue;
                if (value.Value >= 1.0 && value.Value <= 100.0) continue;

                var shown = value.Value.ToString("G4", CultureInfo.InvariantCulture);
                if (value.Value > 0.0 && value.Value < 1.0)
                {
                    // Nobody builds a machine at half a percent; 0.75 is 75 % typed as a fraction.
                    findings.Add(new Finding(FlowsheetCodes.EfficiencyOutOfRange, DiagnosticSeverity.Warning, tag,
                        "The efficiency of this " + type + " is " + shown + " %, which looks like a fraction typed where a percentage was expected.",
                        "Efficiencies are entered in percent: 75 means 75 %, not 0.75."));
                    continue;
                }

                findings.Add(new Finding(FlowsheetCodes.EfficiencyOutOfRange, DiagnosticSeverity.Warning, tag,
                    "The efficiency of this " + type + " is " + shown + " %, outside 0 to 100 %.",
                    "Efficiencies are entered in percent, above 0 and at most 100. Zero divides the duty by nothing."));
            }
        }

        private static void CheckSplitter(ISimulationObject obj, string tag, ObjectType type, List<Finding> findings)
        {
            if (type != ObjectType.Splitter) return;
            if (Text(obj, "OperationMode") != "SplitRatios") return;

            var outlets = obj.GraphicObject.OutputConnectors.Count(c => c.IsAttached && c.Type == ConType.ConOut);
            if (outlets == 0) return;

            var ratios = new List<double>();
            var list = Prop(obj, "Ratios") as IEnumerable;
            if (list == null) return;
            foreach (var item in list)
            {
                double value;
                if (TryDouble(item, out value)) ratios.Add(value);
            }

            var sum = ratios.Take(outlets).Sum();
            if (Math.Abs(sum - 1.0) < 1e-6 && ratios.Take(outlets).All(r => r >= 0.0)) return;

            findings.Add(new Finding(FlowsheetCodes.SplitterRatiosNotNormalised, DiagnosticSeverity.Warning, tag,
                "The split ratios of the " + outlets + " connected outlet(s) sum to " +
                sum.ToString("G4", CultureInfo.InvariantCulture) + ", not 1.",
                "Set one ratio per outlet, between 0 and 1, adding up to 1; the outlet flows are the feed times each ratio."));
        }

        private static void CheckReactor(IFlowsheet flowsheet, ISimulationObject obj, string tag, ObjectType type, List<Finding> findings)
        {
            switch (type)
            {
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_PFR:
                    break;
                default:
                    return;
            }

            var setId = Text(obj, "ReactionSetID");
            var active = 0;
            try
            {
                IReactionSet set;
                if (!string.IsNullOrEmpty(setId) && flowsheet.ReactionSets.TryGetValue(setId, out set) && set != null)
                    active = set.Reactions.Values.Count(r => r.IsActive && flowsheet.Reactions.ContainsKey(r.ReactionID));
            }
            catch (Exception) { return; }

            if (active > 0) return;

            findings.Add(new Finding(FlowsheetCodes.ReactorNoReactions, DiagnosticSeverity.Blocker, tag,
                "This reactor has no active reaction to compute, so it can only pass its feed through.",
                "Create the reactions in the reaction manager, put them in a reaction set, and select that set on the reactor."));
        }

        private static void CheckLogicalObjects(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                var tag = TagOf(obj);

                if (graphic.ObjectType == ObjectType.OT_Recycle)
                {
                    CheckRecycle(flowsheet, obj, tag, findings);
                }
                else if (graphic.ObjectType == ObjectType.OT_Adjust)
                {
                    CheckSpecOrAdjust(obj, tag, "adjust", "ManipulatedObjectData", "ControlledObjectData", findings);
                }
                else if (graphic.ObjectType == ObjectType.OT_Spec)
                {
                    CheckSpecOrAdjust(obj, tag, "specification", "SourceObjectData", "TargetObjectData", findings);
                }
            }
        }

        private static void CheckRecycle(IFlowsheet flowsheet, ISimulationObject recycle, string tag, List<Finding> findings)
        {
            // A recycle tears the loop and iterates from the values on its outlet stream. Starting
            // from nothing is legal, but it converges slowly, and on a tight loop it may not
            // converge at all.
            var outlet = FirstAttached(flowsheet, recycle.GraphicObject.OutputConnectors, ConType.ConOut);
            if (outlet == null) return;

            double flow;
            try { flow = outlet.GetMassFlow(); } catch (Exception) { return; }

            if (flow <= 0.0)
            {
                findings.Add(new Finding(FlowsheetCodes.RecycleNoEstimate, DiagnosticSeverity.Info, tag,
                    "This recycle starts from a zero estimate, which converges slowly on a tight loop.",
                    "Set the outlet stream of the recycle to a rough guess of the converged values."));
            }
        }

        private static void CheckSpecOrAdjust(ISimulationObject obj, string tag, string what,
            string sourceProperty, string targetProperty, List<Finding> findings)
        {
            // Both address a source and a target object by id. Either one left empty makes the
            // object a no-op, and the solver reports no error about it.
            if (ObjectInfoIsSet(Prop(obj, sourceProperty)) && ObjectInfoIsSet(Prop(obj, targetProperty))) return;

            findings.Add(new Finding(FlowsheetCodes.LogicalTargetMissing, DiagnosticSeverity.Warning, tag,
                "This " + what + " does not name both the object it reads and the one it writes, so it does nothing.",
                "Set its source and target objects and properties."));
        }

        private static bool ObjectInfoIsSet(object info)
        {
            if (info == null) return false;
            return !string.IsNullOrEmpty(Text(info, "ID")) && !string.IsNullOrEmpty(Text(info, "PropertyName"));
        }

        // ── After solving ─────────────────────────────────────────────────────

        private static void DiagnoseExceptions(IFlowsheet flowsheet, List<Exception> errors, List<Finding> findings)
        {
            foreach (var error in errors)
            {
                var message = Innermost(error).Message ?? "";

                // The solver reports a cycle it could not order as an infinite loop. That is a
                // topology fault rather than a numerical one, and the fix is a different one.
                if (message.IndexOf("Infinite loop", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    findings.Add(new Finding(FlowsheetCodes.InfiniteLoop, DiagnosticSeverity.Blocker, "",
                        "The solver found a cycle it cannot order, which means a loop with no recycle to tear it.",
                        "Put a Recycle block on one stream of the loop."));
                    continue;
                }

                // The pump refuses a feed with no liquid in it. That is a process fault with a
                // remedy of its own, so it gets its own code.
                if (message.IndexOf("nothing for a pump to move", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    findings.Add(new Finding(FlowsheetCodes.PumpVaporInlet, DiagnosticSeverity.Blocker,
                        OwnerOf(flowsheet, error), message,
                        "Feed the pump from a liquid stream, or use a compressor for a gas."));
                    continue;
                }

                findings.Add(new Finding(FlowsheetCodes.SolverException, DiagnosticSeverity.Blocker,
                    OwnerOf(flowsheet, error), message,
                    "Check that object's specification, and its feed."));
            }
        }

        private static void DiagnoseUnconverged(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;
                if (graphic.ObjectType == ObjectType.MaterialStream) continue;
                if (graphic.ObjectType == ObjectType.EnergyStream) continue;
                if (PortlessTypes.Contains(graphic.ObjectType)) continue;

                if (obj.Calculated) continue;

                var tag = TagOf(obj);
                var reason = string.IsNullOrEmpty(obj.ErrorMessage)
                    ? "This unit operation was never solved."
                    : obj.ErrorMessage;

                findings.Add(new Finding(FlowsheetCodes.NotConverged, DiagnosticSeverity.Blocker, tag, reason,
                    "Check its specification, and that everything upstream of it solved."));
            }
        }

        /// <summary>Equipment that is supposed to change the stream passing through it.</summary>
        private static readonly HashSet<ObjectType> ShouldChangeSomething = new HashSet<ObjectType>
        {
            ObjectType.Heater, ObjectType.Cooler, ObjectType.Pump,
            ObjectType.Compressor, ObjectType.Expander, ObjectType.Valve
        };

        /// <summary>
        /// Reports equipment whose outlet came out the same as its inlet.
        /// </summary>
        /// <remarks>
        /// This catches the most expensive mistake a caller can make with DWSIM: setting a target
        /// without setting the calculation mode that reads it. A cooler is born in heat-duty mode,
        /// so giving it an outlet temperature and nothing else leaves it with a duty of zero. It
        /// solves, reports no error, and does nothing, which is far worse than failing.
        /// </remarks>
        private static void DiagnoseIneffectiveUnits(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active || !obj.Calculated) continue;
                if (!ShouldChangeSomething.Contains(graphic.ObjectType)) continue;

                var inlet = FirstAttached(flowsheet, graphic.InputConnectors, ConType.ConIn);
                var outlet = FirstAttached(flowsheet, graphic.OutputConnectors, ConType.ConOut);
                if (inlet == null || outlet == null) continue;

                double tIn, tOut, pIn, pOut;
                try
                {
                    tIn = inlet.GetTemperature(); tOut = outlet.GetTemperature();
                    pIn = inlet.GetPressure(); pOut = outlet.GetPressure();
                }
                catch (Exception) { continue; }

                if (pIn <= 0.0 || tIn <= 0.0) continue;

                // Relative, because a hundredth of a degree matters on a cryogenic duty and not at
                // all on a furnace. A tenth of a percent is below anything deliberate.
                var changedT = Math.Abs(tOut - tIn) / tIn > 1e-3;
                var changedP = Math.Abs(pOut - pIn) / pIn > 1e-3;

                if (changedT || changedP) continue;

                // An evaporator or a condenser sitting on the saturation line does its whole job at
                // constant temperature and pressure: what moves is the vapour fraction. Reporting
                // one of those as ineffective would be exactly wrong.
                if (ChangedPhase(inlet, outlet)) continue;

                findings.Add(new Finding(FlowsheetCodes.UnitHadNoEffect, DiagnosticSeverity.Warning,
                    TagOf(obj),
                    "This " + graphic.ObjectType + " left its outlet at the same temperature and pressure " +
                    "as its inlet, so it did nothing.",
                    "Its calculation mode probably does not read the specification you set: a cooler " +
                    "given an outlet temperature still needs CalcMode = OutletTemperature. Check the " +
                    "mode, then the value."));
            }
        }

        /// <summary>
        /// Results that a real process could not produce: heat flowing the wrong way, pressure
        /// moving the wrong way, a column below its minimum reflux.
        /// </summary>
        /// <remarks>
        /// The solver is happy to compute a heater with a negative duty; it is arithmetic. A
        /// student is not, and these findings say so before the report is handed in. They are
        /// warnings: the numbers are consistent, they are just not what the equipment is for.
        /// </remarks>
        private static void DiagnosePlausibility(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active || !obj.Calculated) continue;

                var tag = TagOf(obj);

                switch (graphic.ObjectType)
                {
                    case ObjectType.Heater:
                    case ObjectType.Cooler:
                        CheckHeatDirection(flowsheet, obj, tag, graphic.ObjectType == ObjectType.Heater, findings);
                        break;
                    case ObjectType.Pump:
                        CheckPumpInlet(flowsheet, obj, tag, findings);
                        CheckPressureDirection(flowsheet, obj, tag, graphic.ObjectType, findings);
                        break;
                    case ObjectType.Compressor:
                    case ObjectType.Expander:
                    case ObjectType.Valve:
                        CheckPressureDirection(flowsheet, obj, tag, graphic.ObjectType, findings);
                        break;
                    case ObjectType.HeatExchanger:
                        CheckHeatExchanger(flowsheet, obj, tag, findings);
                        break;
                    case ObjectType.ShortcutColumn:
                        CheckShortcutReflux(obj, tag, findings);
                        break;
                    case ObjectType.Mixer:
                        CheckMixerPressures(flowsheet, obj, tag, findings);
                        break;
                    case ObjectType.MaterialStream:
                        CheckFreezing(obj as IMaterialStream, tag, findings);
                        break;
                }
            }
        }

        private static void CheckHeatDirection(IFlowsheet flowsheet, ISimulationObject obj, string tag, bool heater, List<Finding> findings)
        {
            var inlet = FirstAttached(flowsheet, obj.GraphicObject.InputConnectors, ConType.ConIn);
            var outlet = FirstAttached(flowsheet, obj.GraphicObject.OutputConnectors, ConType.ConOut);
            if (inlet == null || outlet == null) return;

            double tIn, tOut;
            try { tIn = inlet.GetTemperature(); tOut = outlet.GetTemperature(); }
            catch (Exception) { return; }
            if (tIn <= 0.0 || tOut <= 0.0) return;

            // A phase change at constant temperature is not a wrong direction; only a real drop or
            // rise counts, and a hundredth of a percent is below anything deliberate.
            if (Math.Abs(tOut - tIn) / tIn < 1e-4) return;

            var wrong = heater ? tOut < tIn : tOut > tIn;
            if (!wrong) return;

            var change = (tOut - tIn).ToString("G4", CultureInfo.InvariantCulture);
            if (heater)
            {
                findings.Add(new Finding(FlowsheetCodes.HeaterCooled, DiagnosticSeverity.Warning, tag,
                    "This heater lowered the temperature of its stream by " + change.TrimStart('-') + " K.",
                    "A heater adds heat, so its duty is positive and its outlet temperature is above its inlet. " +
                    "A negative duty, or an outlet temperature below the inlet, calls for a cooler."));
            }
            else
            {
                findings.Add(new Finding(FlowsheetCodes.CoolerHeated, DiagnosticSeverity.Warning, tag,
                    "This cooler raised the temperature of its stream by " + change + " K.",
                    "A cooler removes heat, so its duty is positive and its outlet temperature is below its inlet. " +
                    "An outlet temperature above the inlet calls for a heater."));
            }
        }

        /// <summary>
        /// A pump whose feed is partly vapour. A feed with no liquid at all stops the solve and is
        /// reported from the exception instead.
        /// </summary>
        private static void CheckPumpInlet(IFlowsheet flowsheet, ISimulationObject obj, string tag, List<Finding> findings)
        {
            var inlet = FirstAttached(flowsheet, obj.GraphicObject.InputConnectors, ConType.ConIn);
            if (inlet == null) return;

            double vapour;
            try { vapour = inlet.GetPhase("Vapor").Properties.molarfraction.GetValueOrDefault(); }
            catch (Exception) { return; }
            if (double.IsNaN(vapour) || vapour <= 0.001) return;

            findings.Add(new Finding(FlowsheetCodes.PumpVaporInlet, DiagnosticSeverity.Warning, tag,
                "Its feed is " + (vapour * 100.0).ToString("0.#") + " mol% vapour; the head and the power were computed from the liquid part alone.",
                "Cool or pressurise the feed until it is all liquid, or take the pump feed from the liquid product of a separator."));
        }

        private static void CheckPressureDirection(IFlowsheet flowsheet, ISimulationObject obj, string tag, ObjectType type, List<Finding> findings)
        {
            var inlet = FirstAttached(flowsheet, obj.GraphicObject.InputConnectors, ConType.ConIn);
            var outlet = FirstAttached(flowsheet, obj.GraphicObject.OutputConnectors, ConType.ConOut);
            if (inlet == null || outlet == null) return;

            double pIn, pOut;
            try { pIn = inlet.GetPressure(); pOut = outlet.GetPressure(); }
            catch (Exception) { return; }
            if (pIn <= 0.0 || pOut <= 0.0) return;
            if (Math.Abs(pOut - pIn) / pIn < 1e-4) return;

            var raises = type == ObjectType.Pump || type == ObjectType.Compressor;
            var wrong = raises ? pOut < pIn : pOut > pIn;
            if (!wrong) return;

            var what = type.ToString().ToLowerInvariant();
            var fix = raises
                ? "A " + what + " raises the pressure; its outlet pressure or pressure increase must be above the inlet pressure. " +
                  "To lower a pressure, use a valve or an expander."
                : "A " + (type == ObjectType.Valve ? "valve" : "expander") + " lowers the pressure; its outlet pressure or pressure drop " +
                  "must leave the outlet below the inlet. To raise a pressure, use a pump or a compressor.";

            findings.Add(new Finding(FlowsheetCodes.PressureWrongDirection, DiagnosticSeverity.Warning, tag,
                "This " + what + " took the pressure from " + pIn.ToString("G5", CultureInfo.InvariantCulture) + " Pa to " +
                pOut.ToString("G5", CultureInfo.InvariantCulture) + " Pa, the wrong way for this equipment.",
                fix));
        }

        private static void CheckHeatExchanger(IFlowsheet flowsheet, ISimulationObject obj, string tag, List<Finding> findings)
        {
            var graphic = obj.GraphicObject;
            var inlets = AttachedStreams(flowsheet, graphic.InputConnectors, ConType.ConIn).Take(2).ToList();
            var outlets = AttachedStreams(flowsheet, graphic.OutputConnectors, ConType.ConOut).Take(2).ToList();
            if (inlets.Count < 2 || outlets.Count < 2) return;

            double t1In, t2In, t1Out, t2Out;
            try
            {
                t1In = inlets[0].GetTemperature(); t2In = inlets[1].GetTemperature();
                t1Out = outlets[0].GetTemperature(); t2Out = outlets[1].GetTemperature();
            }
            catch (Exception) { return; }
            if (t1In <= 0.0 || t2In <= 0.0 || t1Out <= 0.0 || t2Out <= 0.0) return;

            // Port i in feeds port i out. The hotter inlet is the hot side.
            var hotIn = t1In >= t2In ? t1In : t2In;
            var hotOut = t1In >= t2In ? t1Out : t2Out;
            var coldIn = t1In >= t2In ? t2In : t1In;
            var coldOut = t1In >= t2In ? t2Out : t1Out;

            const double tolerance = 0.01;

            if (hotOut > hotIn + tolerance || coldOut < coldIn - tolerance)
            {
                findings.Add(new Finding(FlowsheetCodes.HeatExchangerHeatFlowReversed, DiagnosticSeverity.Warning, tag,
                    "The hot side left hotter than it came in, or the cold side left colder: heat flowed from the cold side to the hot side.",
                    "Heat flows from hot to cold on its own. Check which outlet temperature you specified and on which side; " +
                    "a specified duty must be positive."));
                return;
            }

            if (hotOut < coldIn - tolerance || coldOut > hotIn + tolerance)
            {
                findings.Add(new Finding(FlowsheetCodes.HeatExchangerTemperatureCross, DiagnosticSeverity.Warning, tag,
                    "An outlet crossed the other side's inlet: hot outlet " + hotOut.ToString("G5", CultureInfo.InvariantCulture) +
                    " K against cold inlet " + coldIn.ToString("G5", CultureInfo.InvariantCulture) + " K, cold outlet " +
                    coldOut.ToString("G5", CultureInfo.InvariantCulture) + " K against hot inlet " +
                    hotIn.ToString("G5", CultureInfo.InvariantCulture) + " K.",
                    "No exchanger can cool the hot side below the cold inlet, or heat the cold side above the hot inlet. " +
                    "Relax the specified outlet temperature or the duty, or check the flow rates."));
            }
        }

        private static void CheckShortcutReflux(ISimulationObject obj, string tag, List<Finding> findings)
        {
            var rMin = FieldNum(obj, "m_Rmin");
            var r = FieldNum(obj, "m_refluxratio");
            if (!rMin.HasValue || !r.HasValue) return;
            if (rMin.Value <= 0.0 || double.IsNaN(rMin.Value) || double.IsInfinity(rMin.Value)) return;
            if (r.Value >= rMin.Value) return;

            findings.Add(new Finding(FlowsheetCodes.ColumnRefluxBelowMinimum, DiagnosticSeverity.Warning, tag,
                "The reflux ratio " + r.Value.ToString("G4", CultureInfo.InvariantCulture) + " is below the minimum of " +
                rMin.Value.ToString("G4", CultureInfo.InvariantCulture) + " that Underwood computes for this separation.",
                "Below the minimum reflux the separation is impossible with any number of stages. Use 1.2 to 1.5 times the minimum."));
        }

        private static void CheckMixerPressures(IFlowsheet flowsheet, ISimulationObject obj, string tag, List<Finding> findings)
        {
            var pressures = new List<double>();
            foreach (var inlet in AttachedStreams(flowsheet, obj.GraphicObject.InputConnectors, ConType.ConIn))
            {
                double p, w;
                try { p = inlet.GetPressure(); w = inlet.GetMassFlow(); } catch (Exception) { continue; }
                if (p > 0.0 && w > 0.0) pressures.Add(p);
            }
            if (pressures.Count < 2) return;

            var min = pressures.Min();
            var max = pressures.Max();
            if ((max - min) / max < 0.10) return;

            findings.Add(new Finding(FlowsheetCodes.MixerPressureMismatch, DiagnosticSeverity.Info, tag,
                "The inlets arrive between " + min.ToString("G5", CultureInfo.InvariantCulture) + " Pa and " +
                max.ToString("G5", CultureInfo.InvariantCulture) + " Pa; the outlet takes the " +
                Text(obj, "PressureCalculation").ToLowerInvariant() + ".",
                "Streams mix at one pressure. If the drop surprises you, put a valve on the high-pressure inlet " +
                "or a pump on the low-pressure one, so the mixing pressure is a decision and not an accident."));
        }

        private static void CheckFreezing(IMaterialStream stream, string tag, List<Finding> findings)
        {
            if (stream == null) return;

            double t, liquidFraction, solidFraction, massFlow;
            try
            {
                t = stream.GetTemperature();
                massFlow = stream.GetMassFlow();
                liquidFraction = stream.Phases[1].Properties.molarfraction.GetValueOrDefault();
                solidFraction = stream.Phases[7].Properties.molarfraction.GetValueOrDefault();
            }
            catch (Exception) { return; }

            if (t <= 0.0 || massFlow <= 0.0) return;
            if (liquidFraction < 0.5 || solidFraction > 1e-6) return;

            // The main liquid compound, by mole fraction in the liquid.
            ICompound main = null;
            try
            {
                main = stream.Phases[1].Compounds.Values
                    .OrderByDescending(c => c.MoleFraction.GetValueOrDefault())
                    .FirstOrDefault();
            }
            catch (Exception) { return; }
            if (main == null || main.MoleFraction.GetValueOrDefault() < 0.5) return;

            double fusion;
            try { fusion = main.ConstantProperties.TemperatureOfFusion; } catch (Exception) { return; }
            if (fusion <= 0.0 || double.IsNaN(fusion)) return;

            // Freezing-point depression by the other compounds is real; only a clear margin counts.
            if (t > fusion - 1.0) return;

            findings.Add(new Finding(FlowsheetCodes.TemperatureBelowFreezing, DiagnosticSeverity.Info, tag,
                "This liquid is at " + t.ToString("G5", CultureInfo.InvariantCulture) + " K, below the melting point of " +
                main.ConstantProperties.Name + " (" + fusion.ToString("G5", CultureInfo.InvariantCulture) + " K).",
                "The property package does not model solids, so it reports a supercooled liquid where ice or a solid would form. " +
                "Check the temperature, or expect a solid there."));
        }

        /// <summary>Whether the vapour fraction moved between the two streams.</summary>
        private static bool ChangedPhase(IMaterialStream inlet, IMaterialStream outlet)
        {
            try
            {
                var vIn = inlet.Phases[2].Properties.molarfraction.GetValueOrDefault();
                var vOut = outlet.Phases[2].Properties.molarfraction.GetValueOrDefault();
                return Math.Abs(vOut - vIn) > 1e-4;
            }
            catch (Exception)
            {
                // No vapour phase to read is not evidence either way, and a false blocker costs
                // more than a missed warning.
                return true;
            }
        }

        /// <summary>The stream attached to the first port of the given kind.</summary>
        private static IMaterialStream FirstAttached(IFlowsheet flowsheet,
            IEnumerable<IConnectionPoint> ports, ConType kind)
        {
            return AttachedStreams(flowsheet, ports, kind).FirstOrDefault();
        }

        /// <summary>The material streams attached to ports of the given kind, in port order.</summary>
        private static IEnumerable<IMaterialStream> AttachedStreams(IFlowsheet flowsheet,
            IEnumerable<IConnectionPoint> ports, ConType kind)
        {
            foreach (var port in ports.Where(p => p.IsAttached && p.Type == kind))
            {
                var connector = port.AttachedConnector;
                if (connector == null) continue;

                var other = kind == ConType.ConIn ? connector.AttachedFrom : connector.AttachedTo;
                if (other == null) continue;

                ISimulationObject stream;
                if (!flowsheet.SimulationObjects.TryGetValue(other.Name, out stream)) continue;

                var material = stream as IMaterialStream;
                if (material != null) yield return material;
            }
        }

        private static void DiagnoseResults(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;
                if (graphic.ObjectType != ObjectType.MaterialStream) continue;

                var stream = obj as IMaterialStream;
                if (stream == null || !obj.Calculated) continue;

                double massFlow;
                try { massFlow = stream.GetMassFlow(); }
                catch (Exception) { continue; }

                var tag = TagOf(obj);

                if (double.IsNaN(massFlow) || double.IsInfinity(massFlow))
                {
                    findings.Add(new Finding(FlowsheetCodes.StreamNotFinite, DiagnosticSeverity.Blocker, tag,
                        "This stream carries a flow that is not a finite number.",
                        "Something upstream produced an invalid result; start at the first unconverged unit."));
                    continue;
                }

                if (massFlow < 0.0)
                {
                    // Mass cannot flow backwards. A negative rate means a specification is taking
                    // more out of a unit than goes into it.
                    findings.Add(new Finding(FlowsheetCodes.NegativeFlow, DiagnosticSeverity.Warning, tag,
                        "This stream carries a negative flow of " +
                        massFlow.ToString("G4", CultureInfo.InvariantCulture) + " kg/s.",
                        "A split ratio or a component recovery upstream is taking out more than comes in."));
                    continue;
                }

                if (massFlow <= 0.0) continue;

                double t, p;
                try { t = stream.GetTemperature(); p = stream.GetPressure(); }
                catch (Exception) { continue; }

                if (t <= 0.0 || p <= 0.0 || double.IsNaN(t) || double.IsNaN(p))
                {
                    findings.Add(new Finding(FlowsheetCodes.StateNotPhysical, DiagnosticSeverity.Blocker, tag,
                        "This stream was solved to " + t.ToString("G5", CultureInfo.InvariantCulture) + " K and " +
                        p.ToString("G5", CultureInfo.InvariantCulture) + " Pa.",
                        "Absolute temperature and pressure are positive. A pressure drop larger than the inlet pressure, " +
                        "or a duty the stream cannot absorb, drives the state below zero; check the unit upstream."));
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IReadOnlyList<Finding> Order(List<Finding> findings)
        {
            return findings
                .OrderByDescending(f => (int)f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ThenBy(f => f.ObjectTag, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }

        private static string TagOf(ISimulationObject obj)
        {
            return obj.GraphicObject != null && !string.IsNullOrEmpty(obj.GraphicObject.Tag)
                ? obj.GraphicObject.Tag
                : obj.Name;
        }

        private static object Prop(object target, string name)
        {
            if (target == null) return null;
            var property = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property == null) return null;
            try { return property.GetValue(target, null); } catch (Exception) { return null; }
        }

        private static string Text(object target, string name)
        {
            var value = Prop(target, name);
            return value == null ? "" : value.ToString();
        }

        private static double? Num(object target, string name)
        {
            double value;
            return TryDouble(Prop(target, name), out value) ? value : (double?)null;
        }

        private static double? FieldNum(object target, string name)
        {
            if (target == null) return null;
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (field == null) return null;
            double value;
            try { return TryDouble(field.GetValue(target), out value) ? value : (double?)null; }
            catch (Exception) { return null; }
        }

        private static bool TryDouble(object value, out double result)
        {
            result = 0;
            if (value == null || value is string) return false;
            try { result = Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch (Exception) { return false; }
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }

        private static Exception Innermost(Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            return error;
        }

        /// <summary>The tag of the object an exception names, empty when it names none.</summary>
        private static string OwnerOf(IFlowsheet flowsheet, Exception error)
        {
            var text = error.ToString();

            // Solver exceptions are raised inside an object's own Calculate and carry its tag in
            // the message. Matching longest-first keeps "V-01" from claiming "V-011"'s error.
            var candidates = flowsheet.SimulationObjects.Values
                .Select(TagOf)
                .Where(t => !string.IsNullOrEmpty(t))
                .OrderByDescending(t => t.Length);

            foreach (var tag in candidates)
            {
                if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0) return tag;
            }

            return "";
        }
    }
}
