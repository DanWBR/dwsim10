using System;
using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>The longer story behind a diagnostic code, written for someone learning the subject.</summary>
    public sealed class FindingExplanation
    {
        public FindingExplanation(string code, string title, string meaning, string why, string howToFix, string learnMore)
        {
            Code = code;
            Title = title;
            Meaning = meaning;
            Why = why;
            HowToFix = howToFix;
            LearnMore = learnMore ?? "";
        }

        /// <summary>The code this explains, e.g. <c>FEED_NO_TEMPERATURE</c>.</summary>
        public string Code { get; }

        /// <summary>A short human title, e.g. <c>Feed without a temperature</c>.</summary>
        public string Title { get; }

        /// <summary>What the finding means, in one or two sentences.</summary>
        public string Meaning { get; }

        /// <summary>Why it happens: the concept a student is missing, or the habit that causes it.</summary>
        public string Why { get; }

        /// <summary>How to fix it, step by step where it helps.</summary>
        public string HowToFix { get; }

        /// <summary>
        /// A page of the tutorials site, relative to the language root, e.g.
        /// <c>beginner/01-your-first-simulation.html</c>. See <see cref="FindingExplanations.LearnMoreUrl"/>.
        /// </summary>
        public string LearnMore { get; }
    }

    /// <summary>
    /// Explains every diagnostic code in words a student can act on: what it means, why it
    /// happens, how to fix it, and where to read more.
    /// </summary>
    /// <remarks>
    /// The one-line <see cref="Finding.Message"/> says what is wrong with this object right now.
    /// The explanation says what the code means in general, which is what a student needs the
    /// first time they meet it and what an instructor wants to point at. The two are kept apart so
    /// the finding stays short and the explanation can afford a paragraph.
    /// </remarks>
    public static class FindingExplanations
    {
        /// <summary>Where the tutorials site lives; language and page are appended.</summary>
        public const string TutorialsRoot = "https://dwsim.org/tutorials/";

        /// <summary>The page every code is listed on, so a code without a page of its own still links somewhere.</summary>
        public const string CataloguePage = "reference/diagnostics.html";

        /// <summary>
        /// The explanation for a code. Never null: a code without a written explanation gets one
        /// built from its one-line description, so a caller can always render something.
        /// </summary>
        public static FindingExplanation For(string code)
        {
            FindingExplanation explanation;
            if (!string.IsNullOrEmpty(code) && All.TryGetValue(code, out explanation)) return explanation;

            string summary;
            if (string.IsNullOrEmpty(code) || !(FlowsheetCodes.All.TryGetValue(code, out summary) || DiagnosticCodes.All.TryGetValue(code, out summary)))
                summary = "No description is available for this code.";

            return new FindingExplanation(code ?? "", Titleise(code), summary,
                "This code has no written explanation yet.",
                "Follow the fix on the finding itself.",
                CataloguePage + "#" + (code ?? "").ToLowerInvariant());
        }

        /// <summary>The explanation for a finding; see <see cref="For(string)"/>.</summary>
        public static FindingExplanation For(Finding finding)
        {
            return For(finding == null ? "" : finding.Code);
        }

        /// <summary>
        /// The absolute URL of the page to read about a code, in the given language of the
        /// tutorials site (<c>en</c> or <c>pt-BR</c>). A language the site does not have falls
        /// back to English.
        /// </summary>
        public static string LearnMoreUrl(string code, string language = "en")
        {
            var explanation = For(code);
            var page = string.IsNullOrEmpty(explanation.LearnMore)
                ? CataloguePage + "#" + explanation.Code.ToLowerInvariant()
                : explanation.LearnMore;
            return TutorialsRoot + Localise(page, language);
        }

        /// <summary>The URL of the catalogue entry for a code, whatever its own page is.</summary>
        public static string CatalogueUrl(string code, string language = "en")
        {
            return TutorialsRoot + Localise(CataloguePage + "#" + (code ?? "").ToLowerInvariant(), language);
        }

        /// <summary>
        /// The Portuguese site keeps its own page names, so an English path is translated before
        /// the language root is put in front of it. Anchors are the codes, the same on both sites.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> PortuguesePages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "beginner/01-your-first-simulation.html", "iniciante/01-sua-primeira-simulacao.html" },
            { "beginner/02-mixer-basics.html", "iniciante/02-misturador-basico.html" },
            { "beginner/03-heater-cooler.html", "iniciante/03-aquecedor-resfriador.html" },
            { "beginner/04-simple-flash-drum.html", "iniciante/04-vaso-flash-simples.html" },
            { "intermediate/01-distillation-column.html", "intermediario/01-coluna-destilacao.html" },
            { "intermediate/02-heat-exchanger-design.html", "intermediario/02-projeto-trocador-calor.html" },
            { "intermediate/03-reaction-systems.html", "intermediario/03-sistemas-reacao.html" },
            { "intermediate/04-recycle-loops.html", "intermediario/04-loops-reciclo.html" },
            { "intermediate/05-phase-envelope.html", "intermediario/05-envelope-fases.html" },
            { "reference/property-packages-guide.html", "referencia/guia-pacotes-termodinamicos.html" },
            { "reference/troubleshooting.html", "referencia/solucao-problemas.html" },
            { CataloguePage, "referencia/diagnosticos.html" }
        };

        private static string Localise(string page, string language)
        {
            if (string.IsNullOrEmpty(language) || !language.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
                return "en/" + page;

            var hash = page.IndexOf('#');
            var path = hash < 0 ? page : page.Substring(0, hash);
            var anchor = hash < 0 ? "" : page.Substring(hash);

            string translated;
            return PortuguesePages.TryGetValue(path, out translated)
                ? "pt-BR/" + translated + anchor
                : "en/" + page;
        }

        private static string Titleise(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var words = code.ToLowerInvariant().Split('_');
            words[0] = char.ToUpperInvariant(words[0][0]) + words[0].Substring(1);
            return string.Join(" ", words);
        }

        private static FindingExplanation Entry(string code, string title, string meaning, string why, string fix, string learnMore = null)
        {
            return new FindingExplanation(code, title, meaning, why, fix, learnMore ?? CataloguePage + "#" + code.ToLowerInvariant());
        }

        /// <summary>Every written explanation, by code.</summary>
        public static readonly IReadOnlyDictionary<string, FindingExplanation> All = Build();

        private static IReadOnlyDictionary<string, FindingExplanation> Build()
        {
            var list = new List<FindingExplanation>
            {
                // ── Setup ──────────────────────────────────────────────────────
                Entry(FlowsheetCodes.EmptyFlowsheet, "Empty flowsheet",
                    "There is nothing on the flowsheet to solve.",
                    "A simulation is a set of streams and unit operations connected in the order the material flows. " +
                    "Until at least one stream exists there is nothing for the solver to compute.",
                    "Add the compounds, choose a property package, then insert a material stream and the first unit operation.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.NoCompounds, "No compounds",
                    "The flowsheet has no chemical compounds, so no stream can carry any material.",
                    "Every stream is a mixture of the compounds selected for the flowsheet. Compositions, flows and " +
                    "properties all refer to that list, so it has to exist before anything else.",
                    "Open the simulation settings and add the compounds. Add every species that appears anywhere in the " +
                    "process, including products of reactions.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.NoPropertyPackage, "No property package",
                    "The flowsheet has no thermodynamic model, so no stream can be flashed and no property can be computed.",
                    "A property package is the set of equations that turns temperature, pressure and composition into " +
                    "phases, enthalpies, densities and everything else. Without one the simulator cannot say whether a " +
                    "stream is liquid or vapour.",
                    "Add a property package in the simulation settings. The choice depends on the compounds and the " +
                    "conditions: an equation of state such as Peng-Robinson for hydrocarbons and gases, an activity " +
                    "coefficient model such as NRTL or UNIQUAC for polar liquids at low pressure, Steam Tables for water only.",
                    "reference/property-packages-guide.html"),

                Entry(FlowsheetCodes.DuplicateTag, "Duplicate tag",
                    "Two or more objects have the same name.",
                    "Names are how adjusts, specifications, scripts and the automation tools refer to an object. When two " +
                    "objects share one, whichever is found first wins and the other is unreachable by name.",
                    "Rename the objects so every tag is unique. A prefix per equipment type (H-101, P-201) keeps them tidy.",
                    null),

                // ── Connectivity ───────────────────────────────────────────────
                Entry(FlowsheetCodes.StreamDangling, "Dangling stream",
                    "A stream is not connected to anything at either end.",
                    "A stream only has meaning as the inlet or outlet of a unit operation. One that touches nothing is " +
                    "not part of the process; it is a leftover from an edit, or a stream that was meant to be connected " +
                    "and was not.",
                    "Drag the stream onto a port of the unit it belongs to, or delete it. A feed is connected at its " +
                    "outlet only; a product at its inlet only.",
                    "beginner/02-mixer-basics.html"),

                Entry(FlowsheetCodes.EnergyStreamHalfConnected, "Energy stream with a loose end",
                    "An energy stream is attached to a unit at one end only.",
                    "An energy stream carries a duty: heat into a heater, work into a pump, heat out of a cooler. A unit " +
                    "in Energy Stream mode reads its duty from the stream, so the stream needs a value from somewhere. " +
                    "One loose end means the duty comes from nowhere, or goes nowhere.",
                    "Connect the other end, set the duty on the stream by hand if it is a boundary energy input, or delete " +
                    "the stream if the unit computes its own duty from a temperature specification.",
                    "beginner/03-heater-cooler.html"),

                Entry(FlowsheetCodes.UnitUnconnected, "Unconnected unit operation",
                    "A unit operation has no stream attached to any of its ports.",
                    "The solver walks from the feeds through the units in flow order. A unit with no connections is " +
                    "not on any path, so it never receives anything to compute.",
                    "Connect an inlet stream and an outlet stream, or remove the unit.",
                    "beginner/02-mixer-basics.html"),

                Entry(FlowsheetCodes.UnitNoFeed, "Unit without a feed",
                    "A unit operation has an outlet but no inlet stream.",
                    "A unit transforms what enters it. With no inlet there is no material, no temperature and no " +
                    "composition to work from, so the outlet cannot be computed.",
                    "Connect a material stream to one of the inlet ports. If the unit is the first in the process, that " +
                    "stream is a feed and you specify it by hand.",
                    "beginner/02-mixer-basics.html"),

                Entry(FlowsheetCodes.UnitNoProduct, "Unit without a product",
                    "A unit operation has an inlet but no outlet stream.",
                    "The result of the unit is written to its outlet streams. With none attached the result has nowhere " +
                    "to go, and the units downstream have nothing to read.",
                    "Connect a material stream to the outlet port. A separator needs one outlet per phase it produces.",
                    "beginner/02-mixer-basics.html"),

                // ── Feeds ──────────────────────────────────────────────────────
                Entry(FlowsheetCodes.FeedNoPressure, "Feed without a pressure",
                    "A feed stream has no pressure, so its state cannot be computed.",
                    "A feed is where you tell the simulator what enters the process. Its state needs two intensive " +
                    "variables, and pressure is one of them in almost every specification pair.",
                    "Open the stream and enter its pressure. Remember it is absolute: 1 atm is 101325 Pa, and a gauge " +
                    "reading of 0 means 1 atm.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.FeedNoTemperature, "Feed without a temperature",
                    "A feed stream is specified by temperature and has none.",
                    "With temperature and pressure the simulator runs a flash and finds the phases, the enthalpy and " +
                    "every other property. Without the temperature it cannot start.",
                    "Enter the temperature, or change the specification to pressure and vapour fraction if what you know " +
                    "is that the feed is a saturated liquid or vapour.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.FeedNoFlow, "Feed without a flow",
                    "A feed stream carries no material, so everything downstream of it is empty.",
                    "Flow is the extensive variable of a stream. The state (temperature, pressure, composition) says what " +
                    "the material is; the flow says how much of it. Zero flow gives zero duties and zero products " +
                    "everywhere downstream, with no error.",
                    "Enter a mass, molar or volumetric flow. One basis is enough; the others follow.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.FeedNoComposition, "Feed without a composition",
                    "Every compound in a feed stream is at zero.",
                    "The composition says which compounds the stream carries and in what proportion. All zeros is no " +
                    "material at all, so the flash has nothing to work with.",
                    "Open the stream, choose a basis (mole or mass fractions, or flows per compound) and enter the " +
                    "composition. The fractions are normalised to sum to 1.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.FeedCompositionNotNormalised, "Composition does not sum to 1",
                    "The mole fractions of a feed do not add up to 1.",
                    "Fractions are relative amounts, so they have to sum to one. This usually happens after editing one " +
                    "compound without adjusting the others, or after entering percentages where fractions were expected.",
                    "Re-enter the composition and let the editor normalise it, or enter flows per compound instead and " +
                    "let the simulator compute the fractions.",
                    "beginner/01-your-first-simulation.html"),

                Entry(FlowsheetCodes.VaporFractionOutOfRange, "Vapour fraction outside 0 to 1",
                    "A feed is specified by a vapour fraction below 0 or above 1.",
                    "The vapour fraction is the mole fraction of the stream that is vapour: 0 for a saturated liquid, 1 " +
                    "for a saturated vapour, anything between for a two-phase mixture. Values outside that range have no " +
                    "physical meaning, and 50 was probably meant as 0.5.",
                    "Enter a value between 0 and 1. If the feed is a subcooled liquid or a superheated vapour, specify " +
                    "temperature and pressure instead.",
                    "beginner/04-simple-flash-drum.html"),

                // ── Specifications ─────────────────────────────────────────────
                Entry(FlowsheetCodes.SpecMissing, "Missing specification",
                    "A unit operation is in a calculation mode that reads a value you have not given.",
                    "Each unit has degrees of freedom: the number of values you must supply before it can be solved. The " +
                    "calculation mode decides which values those are. A heater in Outlet Temperature mode needs the " +
                    "outlet temperature; the same heater in Heat Added mode needs the duty instead. Setting a value the " +
                    "mode does not read leaves the real specification empty.",
                    "Open the editor, look at the calculation mode, and fill in the values listed for it. If you know a " +
                    "different value, change the mode to the one that reads it. The Degrees of Freedom panel lists every " +
                    "slot per object.",
                    "beginner/03-heater-cooler.html"),

                Entry(FlowsheetCodes.EfficiencyOutOfRange, "Efficiency outside 0 to 100 %",
                    "A pump, compressor, expander, heater or cooler has an efficiency at or below 0 % or above 100 %.",
                    "Efficiency compares the real duty with the ideal one. It is entered in percent, so 75 means 75 %. " +
                    "Entering 0.75 gives an almost useless machine, and 0 divides by nothing.",
                    "Enter the efficiency as a percentage between 0 and 100. Typical values: 70 to 85 % for pumps and " +
                    "compressors, 100 % for a heater whose losses you ignore.",
                    null),

                Entry(FlowsheetCodes.SplitterRatiosNotNormalised, "Split ratios do not sum to 1",
                    "The split ratios of a splitter do not add up to 1 over its connected outlets.",
                    "A splitter divides one stream into several with the same state and composition. The ratios are the " +
                    "fractions of the feed flow going to each outlet, so they have to sum to one; otherwise mass is " +
                    "created or lost.",
                    "Set one ratio per connected outlet, between 0 and 1, adding up to 1. If you know a flow instead, " +
                    "switch the splitter to a flow specification mode.",
                    "beginner/02-mixer-basics.html"),

                Entry(FlowsheetCodes.ReactorNoReactions, "Reactor without reactions",
                    "A reactor has no reaction set, or its set has no active reaction.",
                    "A reactor is a flash with chemistry. The chemistry lives in the reaction manager: each reaction has " +
                    "stoichiometry and a conversion, an equilibrium constant or a rate. The reactor only points at a set " +
                    "of those reactions. Without the set it is an expensive pipe.",
                    "Create the reactions in the reaction manager, add them to a reaction set, and select that set on the " +
                    "reactor. Check that the reaction type matches the reactor type: conversion reactions for a " +
                    "conversion reactor, kinetic ones for a CSTR or PFR.",
                    "intermediate/03-reaction-systems.html"),

                // ── Logical objects ────────────────────────────────────────────
                Entry(FlowsheetCodes.RecycleNoEstimate, "Recycle without an estimate",
                    "A recycle block starts iterating from an empty stream.",
                    "A recycle breaks a loop: the solver guesses the recycled stream, solves the loop, compares the " +
                    "result with the guess and repeats until they agree. Starting from zero flow is a poor guess, so " +
                    "the loop takes many iterations, and on a sensitive process it may never settle.",
                    "Solve the flowsheet once without the recycle stream to see roughly what comes back, then enter " +
                    "those values on the recycle outlet. Even a rough estimate cuts the iterations sharply.",
                    "intermediate/04-recycle-loops.html"),

                Entry(FlowsheetCodes.LogicalTargetMissing, "Adjust or specification without a target",
                    "An adjust or a specification block does not name both the variable it reads and the one it writes.",
                    "An adjust changes one variable (the manipulated one) until another (the controlled one) reaches a " +
                    "set-point. A specification block copies a value from a source to a target through an expression. " +
                    "Either one with a side missing simply does nothing, and the solver does not complain.",
                    "Open the block and select both objects and both properties. For an adjust, also set the set-point " +
                    "and sensible bounds for the manipulated variable.",
                    "intermediate/04-recycle-loops.html"),

                // ── Solve ──────────────────────────────────────────────────────
                Entry(FlowsheetCodes.SolverException, "Solver exception",
                    "A unit operation raised an error while being calculated.",
                    "The message comes from the unit itself and names what it could not do: a flash that did not " +
                    "converge, a pressure that went negative, a specification it cannot meet. The cause is usually one " +
                    "step upstream, in the feed or the specification of that unit.",
                    "Read the message, open the unit it names, and check its specification against its feed. Solve the " +
                    "flowsheet up to that unit and look at the inlet stream: is it in the phase and at the conditions " +
                    "you expected?",
                    "reference/troubleshooting.html"),

                Entry(FlowsheetCodes.InfiniteLoop, "Loop without a recycle",
                    "The solver found a cycle of units it cannot order.",
                    "A sequential-modular solver computes units one after another, each from its inlets. When a unit's " +
                    "inlet depends on its own outlet, through a loop, there is no first unit to start from. A recycle " +
                    "block tears the loop by supplying a guess for one stream.",
                    "Insert a Recycle block on one stream of the loop, usually the one with the smallest flow or the one " +
                    "you can estimate best.",
                    "intermediate/04-recycle-loops.html"),

                Entry(FlowsheetCodes.NotConverged, "Unit not solved",
                    "A unit operation finished the run without a converged result.",
                    "The unit either never received a solved inlet, because something upstream failed, or its own " +
                    "iteration hit the limit without meeting the tolerance. The first unconverged unit in flow order is " +
                    "the one to look at; the ones after it are usually casualties.",
                    "Find the first unit in the list, read its error message, and check its specification and its feed. " +
                    "For a column, look at the initial estimates and the specifications; for a recycle, the tolerances.",
                    "reference/troubleshooting.html"),

                Entry(FlowsheetCodes.StreamNotFinite, "Stream with an invalid number",
                    "A solved stream carries a flow that is not a finite number.",
                    "NaN or infinity in a result means an operation divided by zero or overflowed somewhere upstream. " +
                    "The stream that shows it is rarely the one that produced it.",
                    "Start at the first unconverged unit, or the first stream with zero flow, and work forward.",
                    "reference/troubleshooting.html"),

                Entry(FlowsheetCodes.NegativeFlow, "Negative flow",
                    "A solved stream carries a negative mass flow.",
                    "Mass does not flow backwards. A negative flow means a specification asked a unit to take more out " +
                    "than came in: a split ratio above 1, a component recovery above 100 %, a product flow on a column " +
                    "above its feed.",
                    "Check the split fractions, recoveries and product flow specifications of the unit upstream against " +
                    "its feed flow.",
                    null),

                Entry(FlowsheetCodes.StateNotPhysical, "Temperature or pressure at or below zero",
                    "A solved stream has an absolute temperature or pressure at or below zero.",
                    "Absolute temperature and pressure are positive quantities. A pressure drop larger than the inlet " +
                    "pressure, or a duty the stream cannot absorb, drives the state out of the physical range.",
                    "Look at the unit upstream: compare its pressure drop with the inlet pressure, and its duty with " +
                    "what the stream can give up.",
                    null),

                Entry(FlowsheetCodes.UnitHadNoEffect, "Unit had no effect",
                    "A heater, cooler, pump, compressor, expander or valve left its outlet identical to its inlet.",
                    "Each unit reads its specification from the field its calculation mode names. A cooler is created " +
                    "in Heat Removed mode, so giving it an outlet temperature and leaving the mode alone leaves the duty " +
                    "at zero: the unit solves, reports no error, and does nothing.",
                    "Open the unit, set the calculation mode to the one that reads the value you know, then check the " +
                    "value itself.",
                    "beginner/03-heater-cooler.html"),

                // ── Plausibility ───────────────────────────────────────────────
                Entry(FlowsheetCodes.HeaterCooled, "Heater that cooled its stream",
                    "A heater lowered the temperature of the stream passing through it.",
                    "A heater adds heat. Its duty is positive and its outlet is hotter than its inlet. The solver does " +
                    "not enforce that: a negative duty, or an outlet temperature below the inlet, gives a heater that " +
                    "behaves as a cooler, with the wrong sign on the energy balance.",
                    "If you meant to cool, replace the heater with a cooler. If you meant to heat, check the sign of " +
                    "the duty and the outlet temperature against the inlet.",
                    "beginner/03-heater-cooler.html"),

                Entry(FlowsheetCodes.CoolerHeated, "Cooler that heated its stream",
                    "A cooler raised the temperature of the stream passing through it.",
                    "A cooler removes heat. Its duty is entered as a positive number and its outlet is colder than its " +
                    "inlet. An outlet temperature above the inlet, or a negative duty, turns it into a heater.",
                    "If you meant to heat, use a heater. Otherwise check the outlet temperature and the sign of the duty.",
                    "beginner/03-heater-cooler.html"),

                Entry(FlowsheetCodes.PressureWrongDirection, "Pressure moved the wrong way",
                    "A pump or compressor lowered the pressure, or a valve or expander raised it.",
                    "Pumps and compressors add work to raise the pressure; valves and expanders let it fall. The " +
                    "simulator computes whatever the specification says, so an outlet pressure below the inlet on a " +
                    "pump gives negative work, and a valve with a negative pressure drop gives free compression.",
                    "Check the outlet pressure or the pressure change against the inlet pressure. If the pressure " +
                    "really has to move the other way, use the equipment that does that.",
                    "beginner/03-heater-cooler.html"),

                Entry(FlowsheetCodes.HeatExchangerTemperatureCross, "Temperature cross in a heat exchanger",
                    "One outlet of a heat exchanger went past the inlet temperature of the other side.",
                    "Heat flows from hot to cold, so the hot outlet can approach the cold inlet and the cold outlet can " +
                    "approach the hot inlet, and neither can pass it. In a real exchanger that limit is the minimum " +
                    "temperature approach; a cross means the specified outlet temperature or duty asks for more heat " +
                    "than the temperature driving force allows.",
                    "Relax the outlet temperature or the duty, increase the flow of the side that limits, or check that " +
                    "the streams are on the sides you intended.",
                    "intermediate/02-heat-exchanger-design.html"),

                Entry(FlowsheetCodes.HeatExchangerHeatFlowReversed, "Heat flowing from cold to hot",
                    "The hot side of a heat exchanger left hotter, or the cold side left colder, than it came in.",
                    "Without work, heat flows only from the hotter stream to the colder one. A reversed flow means a " +
                    "specified outlet temperature was put on the wrong side, or a duty was entered with the wrong sign.",
                    "Check which stream is on which side, then the outlet temperature specification and the sign of the duty.",
                    "intermediate/02-heat-exchanger-design.html"),

                Entry(FlowsheetCodes.TemperatureBelowFreezing, "Liquid below its freezing point",
                    "A liquid stream is colder than the melting point of its main compound.",
                    "Most property packages model vapour and liquid only. Below the freezing point they keep reporting a " +
                    "liquid, with properties extrapolated from above the melting point, where a real process would have " +
                    "ice, wax or a solid deposit.",
                    "Check the temperature. If the stream really is that cold, expect a solid there, and use a property " +
                    "package with solids if the solid matters to the result.",
                    "reference/property-packages-guide.html"),

                Entry(FlowsheetCodes.ColumnRefluxBelowMinimum, "Reflux below the minimum",
                    "A shortcut column is set to a reflux ratio below the minimum for the separation.",
                    "The minimum reflux ratio is the reflux at which the separation would need infinitely many stages. " +
                    "Below it no number of stages achieves the specified key recoveries. Real columns run at 1.2 to 1.5 " +
                    "times the minimum, trading reflux (energy) against stages (capital).",
                    "Raise the reflux ratio above the minimum the column reports, or loosen the key recoveries.",
                    "intermediate/01-distillation-column.html"),

                Entry(FlowsheetCodes.MixerPressureMismatch, "Mixer inlets at different pressures",
                    "The streams entering a mixer arrive at noticeably different pressures.",
                    "Streams can only mix at one pressure. The mixer takes the lowest inlet pressure by default, so a " +
                    "high-pressure stream is silently let down to the lowest one, and the pressure drop happens with " +
                    "no valve to account for it.",
                    "Put a valve on the high-pressure inlet, or a pump on the low-pressure one, so the mixing pressure " +
                    "is a decision. If the drop is intended, the finding can be ignored.",
                    "beginner/02-mixer-basics.html")
            };

            var map = new Dictionary<string, FindingExplanation>(StringComparer.Ordinal);
            foreach (var entry in list) map[entry.Code] = entry;
            return map;
        }
    }
}
