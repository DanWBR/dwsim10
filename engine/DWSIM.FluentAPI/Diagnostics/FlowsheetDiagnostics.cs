using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>
    /// Checks a flowsheet before it is solved, and explains what went wrong after it is.
    /// </summary>
    /// <remarks>
    /// The rules encode the mistakes that keep a freshly assembled flowsheet from solving: a feed
    /// with no composition, a port left dangling, a recycle with nothing to start from, a loop with
    /// nothing to tear it. Each finding carries a fix, so a caller can act on it without having
    /// read the model.
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
            ObjectType.Input, ObjectType.Switch
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
            CheckConnectivity(flowsheet, findings);
            CheckFeeds(flowsheet, findings);
            CheckLogicalObjects(flowsheet, findings);

            return Order(findings);
        }

        /// <summary>
        /// Explains a solve that failed, or that finished with objects left unconverged.
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

                    // A stream attached at neither end is in the flowsheet but not in the process.
                    if (!attachedIn && !attachedOut)
                    {
                        findings.Add(new Finding(FlowsheetCodes.StreamDangling, DiagnosticSeverity.Blocker, tag,
                            "This stream is connected to nothing at either end.",
                            "Connect it to a unit operation, or remove it."));
                    }
                    else if (type == ObjectType.EnergyStream && !(attachedIn && attachedOut))
                    {
                        // An energy stream carries duty between two units; one loose end means the
                        // duty comes from nowhere, or goes nowhere.
                        findings.Add(new Finding(FlowsheetCodes.EnergyStreamHalfConnected, DiagnosticSeverity.Warning, tag,
                            "This energy stream is attached at one end only, so its duty has no source or no destination.",
                            "Connect both ends, or delete it if the unit computes its own duty."));
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

            if (pressure <= 0.0 || double.IsNaN(pressure))
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoPressure, DiagnosticSeverity.Blocker, tag,
                    "This feed has no pressure, so it cannot be flashed.",
                    "Set its pressure."));
            }

            if (temperature <= 0.0 || double.IsNaN(temperature))
            {
                findings.Add(new Finding(FlowsheetCodes.FeedNoTemperature, DiagnosticSeverity.Blocker, tag,
                    "This feed has no temperature, so it cannot be flashed.",
                    "Set its temperature, or specify a vapour fraction instead."));
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

        private static void CheckLogicalObjects(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                var tag = TagOf(obj);

                if (graphic.ObjectType == ObjectType.OT_Recycle)
                {
                    CheckRecycle(obj, tag, findings);
                }
                else if (graphic.ObjectType == ObjectType.OT_Adjust || graphic.ObjectType == ObjectType.OT_Spec)
                {
                    CheckSpecOrAdjust(obj, tag, graphic.ObjectType, findings);
                }
            }
        }

        private static void CheckRecycle(ISimulationObject recycle, string tag, List<Finding> findings)
        {
            // A recycle tears the loop and iterates from its stored values. Starting from nothing is
            // legal, but it converges slowly, and on a tight loop it may not converge at all.
            double flow;
            try
            {
                var value = recycle.GetPropertyValue("PROP_RY_2");
                flow = value == null ? 0.0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return;
            }

            if (flow <= 0.0)
            {
                findings.Add(new Finding(FlowsheetCodes.RecycleNoEstimate, DiagnosticSeverity.Info, tag,
                    "This recycle starts from a zero estimate, which converges slowly on a tight loop.",
                    "Set its stream values to a rough guess of the converged ones."));
            }
        }

        private static void CheckSpecOrAdjust(ISimulationObject obj, string tag, ObjectType type, List<Finding> findings)
        {
            // Both address a source and a target object by id. Either one left empty makes the
            // object a no-op, and the solver reports no error about it.
            var source = AsText(TryProperty(obj, "PROP_SP_0"));
            var target = AsText(TryProperty(obj, "PROP_SP_1"));

            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target)) return;

            var what = type == ObjectType.OT_Adjust ? "adjust" : "specification";
            findings.Add(new Finding(FlowsheetCodes.LogicalTargetMissing, DiagnosticSeverity.Warning, tag,
                "This " + what + " does not name both the object it reads and the one it writes, so it does nothing.",
                "Set its source and target objects and properties."));
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
        /// solves, reports no error, and does nothing — which is far worse than failing.
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
                    "Its calculation mode probably does not read the specification you set - a cooler " +
                    "given an outlet temperature still needs CalcMode = OutletTemperature. Check the " +
                    "mode, then the value."));
            }
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
            foreach (var port in ports.Where(p => p.IsAttached && p.Type == kind))
            {
                var connector = port.AttachedConnector;
                if (connector == null) continue;

                var other = kind == ConType.ConIn ? connector.AttachedFrom : connector.AttachedTo;
                if (other == null) continue;

                ISimulationObject stream;
                if (flowsheet.SimulationObjects.TryGetValue(other.Name, out stream))
                    return stream as IMaterialStream;
            }
            return null;
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
                }
                else if (massFlow < 0.0)
                {
                    // Mass cannot flow backwards. A negative rate means a specification is taking
                    // more out of a unit than goes into it.
                    findings.Add(new Finding(FlowsheetCodes.NegativeFlow, DiagnosticSeverity.Warning, tag,
                        "This stream carries a negative flow of " +
                        massFlow.ToString("G4", CultureInfo.InvariantCulture) + " kg/s.",
                        "A split ratio or a component recovery upstream is taking out more than comes in."));
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

        private static object TryProperty(ISimulationObject obj, string id)
        {
            try { return obj.GetPropertyValue(id); }
            catch (Exception) { return null; }
        }

        private static string AsText(object value)
        {
            return value == null ? "" : value.ToString();
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
