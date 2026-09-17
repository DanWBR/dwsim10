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
    /// <summary>One value an object needs from the user before it can be solved.</summary>
    public sealed class SpecificationSlot
    {
        public SpecificationSlot(string name, string property, double? value, string units,
            bool required, bool isSet, string note)
        {
            Name = name;
            Property = property ?? "";
            Value = value;
            Units = units ?? "";
            Required = required;
            IsSet = isSet;
            Note = note ?? "";
        }

        /// <summary>What the value is, in plain words, e.g. <c>Outlet temperature</c>.</summary>
        public string Name { get; }

        /// <summary>The object property that holds it, for tooling.</summary>
        public string Property { get; }

        /// <summary>The current value in SI units, when it is a number.</summary>
        public double? Value { get; }

        /// <summary>SI units of <see cref="Value"/>, empty for dimensionless or non-numeric slots.</summary>
        public string Units { get; }

        /// <summary>Whether the current calculation mode reads this value.</summary>
        public bool Required { get; }

        /// <summary>Whether the value is usable as it stands.</summary>
        public bool IsSet { get; }

        /// <summary>Anything a student should know about this slot.</summary>
        public string Note { get; }

        public override string ToString()
        {
            var value = Value.HasValue ? Value.Value.ToString("G6", CultureInfo.InvariantCulture) + " " + Units : "";
            var state = Required ? (IsSet ? "set" : "MISSING") : "optional";
            return Name + " = " + value.Trim() + " [" + state + "]";
        }
    }

    /// <summary>What one object still needs before the solver can compute it.</summary>
    public sealed class ObjectDegreesOfFreedom
    {
        public ObjectDegreesOfFreedom(string objectTag, string objectType, string mode, bool supported,
            IReadOnlyList<SpecificationSlot> slots, string note)
        {
            ObjectTag = objectTag ?? "";
            ObjectType = objectType ?? "";
            Mode = mode ?? "";
            Supported = supported;
            Slots = slots ?? new List<SpecificationSlot>();
            Note = note ?? "";
        }

        public string ObjectTag { get; }

        /// <summary>The object type, as the <see cref="DWSIM.Interfaces.Enums.GraphicObjects.ObjectType"/> enum names it.</summary>
        public string ObjectType { get; }

        /// <summary>The calculation mode the slots were derived for, empty when the type has none.</summary>
        public string Mode { get; }

        /// <summary>
        /// False when the analysis does not know the specifications of this type. The object may
        /// still be perfectly well specified; its editor is the place to check.
        /// </summary>
        public bool Supported { get; }

        public IReadOnlyList<SpecificationSlot> Slots { get; }

        public string Note { get; }

        /// <summary>Specifications the current mode reads.</summary>
        public int Required { get { return Slots.Count(s => s.Required); } }

        /// <summary>Of those, how many carry a usable value.</summary>
        public int Specified { get { return Slots.Count(s => s.Required && s.IsSet); } }

        /// <summary>Specifications still to be given; zero means the object is fully specified.</summary>
        public int Remaining { get { return Required - Specified; } }

        /// <summary>The required slots without a value.</summary>
        public IEnumerable<SpecificationSlot> Missing { get { return Slots.Where(s => s.Required && !s.IsSet); } }
    }

    /// <summary>The degrees of freedom of the whole flowsheet, object by object.</summary>
    public sealed class FlowsheetDegreesOfFreedom
    {
        public FlowsheetDegreesOfFreedom(IReadOnlyList<ObjectDegreesOfFreedom> objects)
        {
            Objects = objects ?? new List<ObjectDegreesOfFreedom>();
        }

        public IReadOnlyList<ObjectDegreesOfFreedom> Objects { get; }

        /// <summary>Specifications still to be given across every supported object.</summary>
        public int Remaining { get { return Objects.Sum(o => o.Remaining); } }

        /// <summary>Objects whose specifications the analysis cannot enumerate.</summary>
        public int Unsupported { get { return Objects.Count(o => !o.Supported); } }

        /// <summary>True when every supported object has all the values its mode reads.</summary>
        public bool IsFullySpecified { get { return Remaining == 0; } }
    }

    /// <summary>
    /// Lists, for each object, the specifications its calculation mode reads and whether each one
    /// has a value.
    /// </summary>
    /// <remarks>
    /// A sequential-modular simulator solves one unit at a time from its feeds and its
    /// specifications, so the degrees of freedom of a unit are the specifications it still needs.
    /// That is what this reports: a checklist per object, in the words of the editor, with the
    /// count of what is missing. It reads the objects through their public properties, so it
    /// needs no reference to the unit operations assembly.
    /// </remarks>
    public static class DegreesOfFreedomAnalysis
    {
        /// <summary>The analysis of every active object in the flowsheet.</summary>
        public static FlowsheetDegreesOfFreedom Analyze(IFlowsheet flowsheet)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            var objects = new List<ObjectDegreesOfFreedom>();
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;
                if (IsDecoration(graphic.ObjectType)) continue;

                var result = Analyze(flowsheet, obj);
                if (result != null) objects.Add(result);
            }

            return new FlowsheetDegreesOfFreedom(objects
                .OrderByDescending(o => o.Remaining)
                .ThenBy(o => o.ObjectTag, StringComparer.Ordinal)
                .ToList());
        }

        /// <summary>The analysis of one object, or null when the object is not part of the process.</summary>
        public static ObjectDegreesOfFreedom Analyze(IFlowsheet flowsheet, ISimulationObject obj)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var graphic = obj.GraphicObject;
            if (graphic == null) return null;

            var tag = TagOf(obj);
            var type = graphic.ObjectType;

            try
            {
                switch (type)
                {
                    case ObjectType.MaterialStream: return MaterialStream(obj, tag);
                    case ObjectType.EnergyStream: return EnergyStream(obj, tag);
                    case ObjectType.Mixer: return Mixer(obj, tag);
                    case ObjectType.Splitter: return Splitter(obj, tag);
                    case ObjectType.Heater: return HeaterOrCooler(obj, tag, true);
                    case ObjectType.Cooler: return HeaterOrCooler(obj, tag, false);
                    case ObjectType.Pump: return Pump(obj, tag);
                    case ObjectType.Compressor: return CompressorOrExpander(obj, tag, true);
                    case ObjectType.Expander: return CompressorOrExpander(obj, tag, false);
                    case ObjectType.Valve: return Valve(obj, tag);
                    case ObjectType.Vessel: return Vessel(obj, tag);
                    case ObjectType.HeatExchanger: return HeatExchanger(obj, tag);
                    case ObjectType.ShortcutColumn: return ShortcutColumn(obj, tag);
                    case ObjectType.DistillationColumn:
                    case ObjectType.AbsorptionColumn:
                    case ObjectType.ReboiledAbsorber:
                    case ObjectType.RefluxedAbsorber:
                        return RigorousColumn(obj, tag, type);
                    case ObjectType.RCT_Conversion:
                    case ObjectType.RCT_Equilibrium:
                    case ObjectType.RCT_Gibbs:
                    case ObjectType.RCT_CSTR:
                    case ObjectType.RCT_PFR:
                        return Reactor(flowsheet, obj, tag, type);
                    case ObjectType.ComponentSeparator: return ComponentSeparator(obj, tag);
                    case ObjectType.OT_Adjust: return Adjust(obj, tag);
                    case ObjectType.OT_Spec: return Spec(obj, tag);
                    case ObjectType.OT_Recycle: return Recycle(obj, tag);
                    default:
                        return new ObjectDegreesOfFreedom(tag, type.ToString(), "", false, null,
                            "The specifications of this type are not catalogued; check its editor.");
                }
            }
            catch (Exception ex)
            {
                // An object the reflection cannot read is reported as unknown, never as missing.
                return new ObjectDegreesOfFreedom(tag, type.ToString(), "", false, null,
                    "Could not read this object's specifications: " + ex.Message);
            }
        }

        // ── Streams ───────────────────────────────────────────────────────────

        private static ObjectDegreesOfFreedom MaterialStream(ISimulationObject obj, string tag)
        {
            var stream = obj as IMaterialStream;
            var graphic = obj.GraphicObject;

            // Only a boundary feed is specified by hand; everything downstream is computed.
            if (stream == null || graphic.InputConnectors.Any(c => c.IsAttached))
            {
                return new ObjectDegreesOfFreedom(tag, "MaterialStream", "Computed", true,
                    new List<SpecificationSlot>(),
                    "This stream is the product of a unit operation, so the solver sets every value on it.");
            }

            var slots = new List<SpecificationSlot>();
            var spec = stream.SpecType;
            var mode = spec.ToString().Replace("_and_", " and ").Replace("VaporFraction", "vapour fraction")
                .Replace("SolidFraction", "solid fraction");

            double t = 0, p = 0;
            try { t = stream.GetTemperature(); } catch (Exception) { }
            try { p = stream.GetPressure(); } catch (Exception) { }

            double? h = null, s = null, vf = null;
            try { h = stream.Phases[0].Properties.enthalpy; } catch (Exception) { }
            try { s = stream.Phases[0].Properties.entropy; } catch (Exception) { }
            try { vf = stream.Phases[2].Properties.molarfraction; } catch (Exception) { }

            var temperature = new SpecificationSlot("Temperature", "Temperature", t, "K", true, t > 0,
                "Absolute temperature; 0 K means it was never set.");
            var pressure = new SpecificationSlot("Pressure", "Pressure", p, "Pa", true, p > 0,
                "Absolute pressure; 0 Pa means it was never set.");
            var enthalpy = new SpecificationSlot("Mass enthalpy", "Enthalpy", h, "kJ/kg", true, h.HasValue,
                "Relative to the property package reference state, so negative values are normal.");
            var entropy = new SpecificationSlot("Mass entropy", "Entropy", s, "kJ/[kg.K]", true, s.HasValue, "");
            var vapourFraction = new SpecificationSlot("Vapour fraction", "VaporFraction", vf, "", true,
                vf.HasValue && vf.Value >= 0 && vf.Value <= 1,
                "Molar basis, 0 for a saturated liquid and 1 for a saturated vapour.");

            switch (spec)
            {
                case StreamSpec.Temperature_and_Pressure:
                    slots.Add(temperature); slots.Add(pressure); break;
                case StreamSpec.Pressure_and_Enthalpy:
                    slots.Add(pressure); slots.Add(enthalpy); break;
                case StreamSpec.Pressure_and_Entropy:
                    slots.Add(pressure); slots.Add(entropy); break;
                case StreamSpec.Pressure_and_VaporFraction:
                    slots.Add(pressure); slots.Add(vapourFraction); break;
                case StreamSpec.Temperature_and_VaporFraction:
                    slots.Add(temperature); slots.Add(vapourFraction); break;
                default:
                    // The volume-based and solid-fraction specifications are rare; report the
                    // two that are always readable and let the editor cover the rest.
                    slots.Add(temperature); slots.Add(pressure); break;
            }

            double mass = 0, mole = 0, volume = 0;
            try { mass = stream.GetMassFlow(); } catch (Exception) { }
            try { mole = stream.GetMolarFlow(); } catch (Exception) { }
            try { volume = stream.GetVolumetricFlow(); } catch (Exception) { }

            // The solver reads the basis the stream says it was defined on; the other two are
            // derived from it, and shown so a stale one is visible.
            double flow; string flowName, flowUnits;
            switch (stream.DefinedFlow)
            {
                case FlowSpec.Mole: flow = mole; flowName = "Molar flow"; flowUnits = "mol/s"; break;
                case FlowSpec.Volumetric: flow = volume; flowName = "Volumetric flow"; flowUnits = "m3/s"; break;
                default: flow = mass; flowName = "Mass flow"; flowUnits = "kg/s"; break;
            }

            var others = "One basis is enough; the others follow from it. Mass " + Fmt(mass) + " kg/s, molar " +
                Fmt(mole) + " mol/s, volumetric " + Fmt(volume) + " m3/s.";

            double usable;
            var hasFlow = TryDouble(flow, out usable) && usable > 0;
            slots.Add(new SpecificationSlot(flowName, "Flow", hasFlow ? flow : (double?)null, flowUnits, true, hasFlow, others));

            double total = 0;
            try { total = stream.Phases[0].Compounds.Values.Sum(c => c.MoleFraction.GetValueOrDefault()); }
            catch (Exception) { }

            slots.Add(new SpecificationSlot("Composition", "Composition", total, "", true, total > 0,
                "Sum of the overall mole fractions; the fractions are normalised on the way in."));

            return new ObjectDegreesOfFreedom(tag, "MaterialStream", mode, true, slots,
                "A feed needs two intensive state variables, one flow and a composition.");
        }

        private static ObjectDegreesOfFreedom EnergyStream(ISimulationObject obj, string tag)
        {
            var graphic = obj.GraphicObject;
            var stream = obj as IEnergyStream;

            var fedByUnit = graphic.InputConnectors.Any(c => c.IsAttached);
            var feedsUnit = graphic.OutputConnectors.Any(c => c.IsAttached);

            if (fedByUnit || !feedsUnit)
            {
                return new ObjectDegreesOfFreedom(tag, "EnergyStream", "Computed", true,
                    new List<SpecificationSlot>(),
                    "The unit this stream comes from sets its duty.");
            }

            double duty = 0;
            try { if (stream != null) duty = stream.GetEnergyFlow(); } catch (Exception) { }

            var slots = new List<SpecificationSlot>
            {
                new SpecificationSlot("Energy flow", "EnergyFlow", duty, "kW", true, Math.Abs(duty) > 0,
                    "Read only by a unit whose calculation mode is Energy Stream.")
            };

            return new ObjectDegreesOfFreedom(tag, "EnergyStream", "Specified", true, slots,
                "An energy stream feeding a unit carries the duty the unit will apply.");
        }

        // ── Simple units ──────────────────────────────────────────────────────

        private static ObjectDegreesOfFreedom Mixer(ISimulationObject obj, string tag)
        {
            var behaviour = Text(obj, "PressureCalculation");
            var slots = new List<SpecificationSlot>
            {
                new SpecificationSlot("Outlet pressure rule", "PressureCalculation", null, "", false, true,
                    "Minimum, maximum or average of the inlet pressures; " + behaviour + " is selected.")
            };
            return new ObjectDegreesOfFreedom(tag, "Mixer", behaviour, true, slots,
                "A mixer is fully determined by its inlets: mass, energy and composition balances fix the outlet.");
        }

        private static ObjectDegreesOfFreedom Splitter(ISimulationObject obj, string tag)
        {
            var mode = Text(obj, "OperationMode");
            var outlets = obj.GraphicObject.OutputConnectors.Count(c => c.IsAttached && c.Type == ConType.ConOut);
            var slots = new List<SpecificationSlot>();

            if (mode == "SplitRatios")
            {
                var ratios = new List<double>();
                var list = Prop(obj, "Ratios") as IEnumerable;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        double value;
                        if (TryDouble(item, out value)) ratios.Add(value);
                    }
                }

                var sum = ratios.Take(Math.Max(outlets, 1)).Sum();
                var normalised = Math.Abs(sum - 1.0) < 1e-6;

                // N outlets take N - 1 independent ratios; the last one is 1 minus the others.
                for (var i = 0; i < Math.Max(outlets, 1); i++)
                {
                    var value = i < ratios.Count ? ratios[i] : (double?)null;
                    var independent = i < outlets - 1;
                    slots.Add(new SpecificationSlot("Split ratio, outlet " + (i + 1), "Ratios[" + i + "]",
                        value, "", independent, independent && normalised && value.HasValue,
                        independent ? "" : "Follows from the others: the ratios sum to 1."));
                }

                return new ObjectDegreesOfFreedom(tag, "Splitter", "Split ratios", true, slots,
                    outlets > 0
                        ? outlets + " outlet(s) connected, so " + (outlets - 1) + " ratio(s) are independent. They sum to " +
                          sum.ToString("G4", CultureInfo.InvariantCulture) + "."
                        : "Connect the outlets first.");
            }

            var units = mode == "StreamMassFlowSpec" ? "kg/s" : mode == "StreamMoleFlowSpec" ? "mol/s" : "m3/s";
            var first = Num(obj, "StreamFlowSpec");
            slots.Add(new SpecificationSlot("Flow of outlet 1", "StreamFlowSpec", first, units, true,
                first.HasValue && first.Value > 0, ""));
            if (outlets >= 3)
            {
                var second = Num(obj, "Stream2FlowSpec");
                slots.Add(new SpecificationSlot("Flow of outlet 2", "Stream2FlowSpec", second, units, true,
                    second.HasValue && second.Value > 0, ""));
            }
            return new ObjectDegreesOfFreedom(tag, "Splitter", mode, true, slots,
                "The last outlet takes whatever flow is left.");
        }

        private static ObjectDegreesOfFreedom HeaterOrCooler(ISimulationObject obj, string tag, bool heater)
        {
            var mode = Text(obj, "CalcMode");
            var slots = new List<SpecificationSlot>();

            var duty = Num(obj, "DeltaQ");
            var tOut = Num(obj, "OutletTemperature");
            var vf = Num(obj, "OutletVaporFraction");
            var dT = Num(obj, "DeltaT");

            switch (mode)
            {
                case "HeatAdded":
                case "HeatRemoved":
                case "HeatAddedRemoved":
                    slots.Add(new SpecificationSlot(heater ? "Heat added" : "Heat removed", "DeltaQ", duty, "kW", true,
                        duty.HasValue && duty.Value != 0, "A duty of zero leaves the stream exactly as it came in."));
                    break;
                case "OutletTemperature":
                    slots.Add(new SpecificationSlot("Outlet temperature", "OutletTemperature", tOut, "K", true,
                        tOut.HasValue && tOut.Value > 0, ""));
                    break;
                case "OutletVaporFraction":
                    slots.Add(new SpecificationSlot("Outlet vapour fraction", "OutletVaporFraction", vf, "", true,
                        vf.HasValue && vf.Value >= 0 && vf.Value <= 1, "0 condenses everything, 1 vaporises everything."));
                    break;
                case "TemperatureChange":
                    slots.Add(new SpecificationSlot("Temperature change", "DeltaT", dT, "K", true,
                        dT.HasValue && dT.Value != 0, ""));
                    break;
                case "EnergyStream":
                    var energy = EnergyInlet(obj);
                    slots.Add(new SpecificationSlot("Duty from the energy stream", "EnergyStream", energy, "kW", true,
                        energy.HasValue && energy.Value != 0,
                        "Connect an energy stream to the unit and set its energy flow."));
                    break;
                default:
                    break;
            }

            slots.Add(Optional("Pressure drop", "DeltaP", Num(obj, "DeltaP"), "Pa", "0 by default."));
            slots.Add(Optional("Efficiency", "Eficiencia", Num(obj, "Eficiencia"), "%", "100 % by default; the duty the stream receives is this fraction of the duty specified."));

            return new ObjectDegreesOfFreedom(tag, heater ? "Heater" : "Cooler", mode, true, slots,
                "One thermal specification and one pressure specification fix the outlet.");
        }

        private static ObjectDegreesOfFreedom Pump(ISimulationObject obj, string tag)
        {
            var mode = Text(obj, "CalcMode");
            var slots = new List<SpecificationSlot>();

            switch (mode)
            {
                case "Delta_P":
                    var dp = Num(obj, "DeltaP");
                    slots.Add(new SpecificationSlot("Pressure increase", "DeltaP", dp, "Pa", true, dp.HasValue && dp.Value > 0, ""));
                    break;
                case "OutletPressure":
                    var pOut = Num(obj, "Pout");
                    slots.Add(new SpecificationSlot("Outlet pressure", "Pout", pOut, "Pa", true, pOut.HasValue && pOut.Value > 0, ""));
                    break;
                case "Power":
                    var power = Num(obj, "DeltaQ");
                    slots.Add(new SpecificationSlot("Power", "DeltaQ", power, "kW", true, power.HasValue && power.Value > 0, ""));
                    break;
                case "EnergyStream":
                    var energy = EnergyInlet(obj);
                    slots.Add(new SpecificationSlot("Power from the energy stream", "EnergyStream", energy, "kW", true,
                        energy.HasValue && energy.Value > 0, "Connect an energy stream to the pump and set its energy flow."));
                    break;
                case "Curves":
                    slots.Add(new SpecificationSlot("Performance curves", "PumpCurveSet", null, "", true, true,
                        "Head and efficiency curves are read at solve time; the pump reports an error if they are empty."));
                    break;
            }

            slots.Add(Optional("Efficiency", "Eficiencia", Num(obj, "Eficiencia"), "%", "75 % by default."));

            return new ObjectDegreesOfFreedom(tag, "Pump", mode, true, slots,
                "One pressure or power specification plus an efficiency fix the outlet.");
        }

        private static ObjectDegreesOfFreedom CompressorOrExpander(ISimulationObject obj, string tag, bool compressor)
        {
            var mode = Text(obj, "CalcMode");
            var path = Text(obj, "ProcessPath");
            var slots = new List<SpecificationSlot>();

            switch (mode)
            {
                case "OutletPressure":
                    var pOut = Num(obj, "POut");
                    slots.Add(new SpecificationSlot("Outlet pressure", "POut", pOut, "Pa", true, pOut.HasValue && pOut.Value > 0, ""));
                    break;
                case "Delta_P":
                    var dp = Num(obj, "DeltaP");
                    slots.Add(new SpecificationSlot(compressor ? "Pressure increase" : "Pressure drop", "DeltaP", dp, "Pa", true,
                        dp.HasValue && dp.Value > 0, ""));
                    break;
                case "PowerRequired":
                case "PowerGenerated":
                    var power = Num(obj, "DeltaQ");
                    slots.Add(new SpecificationSlot("Power", "DeltaQ", power, "kW", true, power.HasValue && power.Value > 0, ""));
                    break;
                case "EnergyStream":
                    var energy = EnergyInlet(obj);
                    slots.Add(new SpecificationSlot("Power from the energy stream", "EnergyStream", energy, "kW", true,
                        energy.HasValue && energy.Value > 0, "Connect an energy stream and set its energy flow."));
                    break;
                case "Head":
                    var polytropic = path == "Polytropic";
                    var head = Num(obj, polytropic ? "PolytropicHead" : "AdiabaticHead");
                    slots.Add(new SpecificationSlot((polytropic ? "Polytropic" : "Adiabatic") + " head",
                        polytropic ? "PolytropicHead" : "AdiabaticHead", head, "m", true, head.HasValue && head.Value > 0, ""));
                    break;
                case "PressureRatio":
                    var ratio = Num(obj, "PressureRatio");
                    slots.Add(new SpecificationSlot("Pressure ratio", "PressureRatio", ratio, "", true,
                        ratio.HasValue && ratio.Value > 0, compressor ? "Outlet over inlet, above 1." : "Inlet over outlet, above 1."));
                    break;
                case "Curves":
                    slots.Add(new SpecificationSlot("Performance curves", "Curves", null, "", true, true,
                        "Read at solve time from the curves database."));
                    break;
            }

            var efficiencyProperty = path == "Polytropic" ? "PolytropicEfficiency" : "AdiabaticEfficiency";
            slots.Add(Optional((path == "Polytropic" ? "Polytropic" : "Adiabatic") + " efficiency", efficiencyProperty,
                Num(obj, efficiencyProperty), "%", "75 % by default."));

            return new ObjectDegreesOfFreedom(tag, compressor ? "Compressor" : "Expander", mode + " (" + path + ")", true, slots,
                "One pressure, power or head specification plus an efficiency fix the outlet.");
        }

        private static ObjectDegreesOfFreedom Valve(ISimulationObject obj, string tag)
        {
            var mode = Text(obj, "CalcMode");
            var slots = new List<SpecificationSlot>();

            switch (mode)
            {
                case "DeltaP":
                    var dp = Num(obj, "DeltaP");
                    slots.Add(new SpecificationSlot("Pressure drop", "DeltaP", dp, "Pa", true, dp.HasValue && dp.Value >= 0,
                        "0 Pa is accepted; the valve then does nothing."));
                    break;
                case "OutletPressure":
                    var pOut = Num(obj, "OutletPressure");
                    slots.Add(new SpecificationSlot("Outlet pressure", "OutletPressure", pOut, "Pa", true,
                        pOut.HasValue && pOut.Value > 0, ""));
                    break;
                default:
                    var kv = Num(obj, "Kv");
                    var opening = Num(obj, "OpeningPct");
                    slots.Add(new SpecificationSlot("Flow coefficient (Kv)", "Kv", kv, "", true, kv.HasValue && kv.Value > 0, ""));
                    slots.Add(new SpecificationSlot("Opening", "OpeningPct", opening, "%", true,
                        opening.HasValue && opening.Value > 0, "The outlet pressure follows from the flow, Kv and opening."));
                    break;
            }

            return new ObjectDegreesOfFreedom(tag, "Valve", mode, true, slots,
                "A valve is isenthalpic: the pressure specification alone fixes the outlet.");
        }

        private static ObjectDegreesOfFreedom Vessel(ISimulationObject obj, string tag)
        {
            var slots = new List<SpecificationSlot>();
            var overrideT = Bool(obj, "OverrideT");
            var overrideP = Bool(obj, "OverrideP");

            var flashT = Num(obj, "FlashTemperature");
            var flashP = Num(obj, "FlashPressure");

            slots.Add(new SpecificationSlot("Separation temperature", "FlashTemperature", flashT, "K", overrideT,
                flashT.HasValue && flashT.Value > 0, overrideT ? "" : "Read only when Override Temperature is on."));
            slots.Add(new SpecificationSlot("Separation pressure", "FlashPressure", flashP, "Pa", overrideP,
                flashP.HasValue && flashP.Value > 0, overrideP ? "" : "Read only when Override Pressure is on."));

            var mode = overrideT || overrideP ? "Override" : "From the inlet";
            return new ObjectDegreesOfFreedom(tag, "Vessel", mode, true, slots,
                "A separator needs nothing from you: it splits the phases the inlet already has, unless you override T or P.");
        }

        private static ObjectDegreesOfFreedom HeatExchanger(ISimulationObject obj, string tag)
        {
            var mode = Text(obj, "CalculationMode");
            var slots = new List<SpecificationSlot>();

            var hotOut = Num(obj, "HotSideOutletTemperature");
            var coldOut = Num(obj, "ColdSideOutletTemperature");
            var q = Num(obj, "Q");
            var u = Num(obj, "OverallCoefficient");
            var area = Num(obj, "Area");

            var hotOutSlot = new SpecificationSlot("Hot side outlet temperature", "HotSideOutletTemperature", hotOut, "K", true,
                hotOut.HasValue && hotOut.Value > 0, "");
            var coldOutSlot = new SpecificationSlot("Cold side outlet temperature", "ColdSideOutletTemperature", coldOut, "K", true,
                coldOut.HasValue && coldOut.Value > 0, "");
            var uSlot = new SpecificationSlot("Overall heat transfer coefficient", "OverallCoefficient", u, "W/[m2.K]", true,
                u.HasValue && u.Value > 0, "");
            var areaSlot = new SpecificationSlot("Heat transfer area", "Area", area, "m2", true, area.HasValue && area.Value > 0, "");

            switch (mode)
            {
                case "CalcTempHotOut": slots.Add(coldOutSlot); break;
                case "CalcTempColdOut": slots.Add(hotOutSlot); break;
                case "CalcBothTemp":
                    slots.Add(new SpecificationSlot("Heat exchanged", "Q", q, "kW", true, q.HasValue && q.Value > 0, ""));
                    break;
                case "CalcBothTemp_UA": slots.Add(uSlot); slots.Add(areaSlot); break;
                case "CalcArea":
                    slots.Add(Text(obj, "DefinedTemperature") == "Hot_Fluid" ? hotOutSlot : coldOutSlot);
                    slots.Add(uSlot);
                    break;
                case "PinchPoint":
                    var mita = Num(obj, "MITA");
                    slots.Add(new SpecificationSlot("Minimum temperature approach", "MITA", mita, "K", true, mita.HasValue && mita.Value > 0, ""));
                    break;
                case "ThermalEfficiency":
                    var efficiency = Num(obj, "ThermalEfficiency");
                    slots.Add(new SpecificationSlot("Thermal efficiency", "ThermalEfficiency", efficiency, "%", true,
                        efficiency.HasValue && efficiency.Value > 0, "Heat exchanged as a fraction of the maximum possible."));
                    break;
                case "OutletVaporFraction1":
                    slots.Add(new SpecificationSlot("Outlet vapour fraction, stream 1", "OutletVaporFraction1", Num(obj, "OutletVaporFraction1"), "", true, true, ""));
                    break;
                case "OutletVaporFraction2":
                    slots.Add(new SpecificationSlot("Outlet vapour fraction, stream 2", "OutletVaporFraction2", Num(obj, "OutletVaporFraction2"), "", true, true, ""));
                    break;
                case "ShellandTube_Rating":
                case "ShellandTube_CalcFoulingFactor":
                    slots.Add(new SpecificationSlot("Shell and tube geometry", "STProperties", null, "", true, true,
                        "Tube count, length, diameters and passes are read from the geometry editor."));
                    if (mode == "ShellandTube_CalcFoulingFactor")
                        slots.Add(Text(obj, "DefinedTemperature") == "Hot_Fluid" ? hotOutSlot : coldOutSlot);
                    break;
            }

            slots.Add(Optional("Hot side pressure drop", "HotSidePressureDrop", Num(obj, "HotSidePressureDrop"), "Pa", "0 by default."));
            slots.Add(Optional("Cold side pressure drop", "ColdSidePressureDrop", Num(obj, "ColdSidePressureDrop"), "Pa", "0 by default."));
            slots.Add(Optional("Heat loss", "HeatLoss", Num(obj, "HeatLoss"), "kW", "0 by default."));

            return new ObjectDegreesOfFreedom(tag, "HeatExchanger", mode, true, slots,
                "Two inlets give four unknowns; the energy balance is one equation, so one more specification closes it, " +
                "or two when the area and U are the unknowns.");
        }

        // ── Columns ───────────────────────────────────────────────────────────

        private static ObjectDegreesOfFreedom ShortcutColumn(ISimulationObject obj, string tag)
        {
            var slots = new List<SpecificationSlot>();

            var lightKey = FieldText(obj, "m_lightkey");
            var heavyKey = FieldText(obj, "m_heavykey");
            var lightFrac = FieldNum(obj, "m_lightkeymolarfrac");
            var heavyFrac = FieldNum(obj, "m_heavykeymolarfrac");
            var reflux = FieldNum(obj, "m_refluxratio");
            var pCond = FieldNum(obj, "m_condenserpressure");
            var pReb = FieldNum(obj, "m_boilerpressure");

            slots.Add(new SpecificationSlot("Light key compound", "m_lightkey", null, "", true, !string.IsNullOrEmpty(lightKey),
                string.IsNullOrEmpty(lightKey) ? "" : lightKey));
            slots.Add(new SpecificationSlot("Heavy key compound", "m_heavykey", null, "", true, !string.IsNullOrEmpty(heavyKey),
                string.IsNullOrEmpty(heavyKey) ? "" : heavyKey));
            slots.Add(new SpecificationSlot("Light key mole fraction in the bottoms", "m_lightkeymolarfrac", lightFrac, "", true,
                lightFrac.HasValue && lightFrac.Value > 0 && lightFrac.Value < 1, ""));
            slots.Add(new SpecificationSlot("Heavy key mole fraction in the distillate", "m_heavykeymolarfrac", heavyFrac, "", true,
                heavyFrac.HasValue && heavyFrac.Value > 0 && heavyFrac.Value < 1, ""));
            slots.Add(new SpecificationSlot("Reflux ratio", "m_refluxratio", reflux, "", true, reflux.HasValue && reflux.Value > 0,
                "Must exceed the minimum reflux ratio Underwood computes."));
            slots.Add(new SpecificationSlot("Condenser pressure", "m_condenserpressure", pCond, "Pa", true, pCond.HasValue && pCond.Value > 0, ""));
            slots.Add(new SpecificationSlot("Reboiler pressure", "m_boilerpressure", pReb, "Pa", true, pReb.HasValue && pReb.Value > 0, ""));
            slots.Add(new SpecificationSlot("Condenser type", "condtype", null, "", false, true, Text(obj, "condtype")));

            return new ObjectDegreesOfFreedom(tag, "ShortcutColumn", "Fenske-Underwood-Gilliland", true, slots,
                "Two key recoveries, a reflux ratio and two pressures fix the column.");
        }

        private static ObjectDegreesOfFreedom RigorousColumn(ISimulationObject obj, string tag, ObjectType type)
        {
            var slots = new List<SpecificationSlot>();

            var stages = Num(obj, "NumberOfStages");
            slots.Add(new SpecificationSlot("Number of stages", "NumberOfStages", stages, "", true, stages.HasValue && stages.Value > 0, ""));

            var stageList = Prop(obj, "Stages") as IEnumerable;
            double? pTop = null, pBottom = null;
            if (stageList != null)
            {
                object first = null, last = null;
                foreach (var stage in stageList) { if (first == null) first = stage; last = stage; }
                if (first != null) pTop = Num(first, "P");
                if (last != null) pBottom = Num(last, "P");
            }
            slots.Add(new SpecificationSlot("Top stage pressure", "Stages[0].P", pTop, "Pa", true, pTop.HasValue && pTop.Value > 0, ""));
            slots.Add(new SpecificationSlot("Bottom stage pressure", "Stages[N].P", pBottom, "Pa", true, pBottom.HasValue && pBottom.Value > 0, ""));

            var hasCondenser = type == ObjectType.DistillationColumn || type == ObjectType.RefluxedAbsorber;
            var hasReboiler = type == ObjectType.DistillationColumn || type == ObjectType.ReboiledAbsorber;

            var specs = Prop(obj, "Specs") as IDictionary;
            if (hasCondenser) slots.Add(ColumnSpec(specs, "C", "Condenser specification"));
            if (hasReboiler) slots.Add(ColumnSpec(specs, "R", "Reboiler specification"));

            if (hasCondenser)
                slots.Add(new SpecificationSlot("Condenser type", "CondenserType", null, "", false, true, Text(obj, "CondenserType")));

            var note = hasCondenser && hasReboiler
                ? "Stages, two pressures, one condenser specification and one reboiler specification fix a distillation column."
                : "An absorber has no condenser or reboiler, so stages and pressures are all it needs; the feeds do the rest.";

            return new ObjectDegreesOfFreedom(tag, type.ToString(), Text(obj, "SolvingMethodName"), true, slots, note);
        }

        private static SpecificationSlot ColumnSpec(IDictionary specs, string key, string name)
        {
            object spec = specs != null && specs.Contains(key) ? specs[key] : null;
            if (spec == null)
                return new SpecificationSlot(name, "Specs[" + key + "]", null, "", true, false, "");

            var kind = Text(spec, "SType").Replace('_', ' ');
            var value = Num(spec, "SpecValue");
            var units = Text(spec, "SpecUnit");
            var compound = Text(spec, "ComponentID");

            // A duty of zero is a legitimate specification for nothing; every other kind needs a value.
            var isSet = value.HasValue && (kind == "Heat Duty" || value.Value > 0);
            var note = kind + (string.IsNullOrEmpty(compound) ? "" : " of " + compound);

            return new SpecificationSlot(name, "Specs[" + key + "]", value, units, true, isSet, note);
        }

        // ── Reactors ──────────────────────────────────────────────────────────

        private static ObjectDegreesOfFreedom Reactor(IFlowsheet flowsheet, ISimulationObject obj, string tag, ObjectType type)
        {
            var mode = Text(obj, "ReactorOperationMode");
            var slots = new List<SpecificationSlot>();

            var setId = Text(obj, "ReactionSetID");
            var active = ActiveReactions(flowsheet, setId);
            var gibbs = type == ObjectType.RCT_Gibbs;

            slots.Add(new SpecificationSlot("Reaction set", "ReactionSetID", active, "", !gibbs, active > 0,
                gibbs ? "A Gibbs reactor can minimise over the elements alone, with no reactions listed." :
                        active + " active reaction(s) in the set."));

            switch (mode)
            {
                case "OutletTemperature":
                    var tOut = Num(obj, "OutletTemperature");
                    slots.Add(new SpecificationSlot("Outlet temperature", "OutletTemperature", tOut, "K", true, tOut.HasValue && tOut.Value > 0, ""));
                    break;
                case "NonIsothermalNonAdiabatic":
                    var duty = Num(obj, "DeltaQ");
                    slots.Add(new SpecificationSlot("Heat duty", "DeltaQ", duty, "kW", true, duty.HasValue, "Positive heats the reactor, negative cools it."));
                    break;
                case "HeatExchange":
                    var ua = Num(obj, "OverallHeatTransferCoefficient");
                    var tc = Num(obj, "CoolantInletTemperature");
                    slots.Add(new SpecificationSlot("Overall heat transfer coefficient", "OverallHeatTransferCoefficient", ua, "W/[m2.K]", true, ua.HasValue && ua.Value > 0, ""));
                    slots.Add(new SpecificationSlot("Coolant inlet temperature", "CoolantInletTemperature", tc, "K", true, tc.HasValue && tc.Value > 0, ""));
                    break;
                default:
                    slots.Add(new SpecificationSlot("Thermal mode", "ReactorOperationMode", null, "", false, true,
                        mode == "Adiabatic" ? "No duty: the outlet temperature follows from the heat of reaction." :
                                              "Outlet at the inlet temperature; the duty is computed."));
                    break;
            }

            if (type == ObjectType.RCT_CSTR)
            {
                var volume = Num(obj, "Volume");
                slots.Add(new SpecificationSlot("Reactor volume", "Volume", volume, "m3", true, volume.HasValue && volume.Value > 0, ""));
                slots.Add(Optional("Headspace", "Headspace", Num(obj, "Headspace"), "m3", "Vapour space above the liquid."));
            }
            else if (type == ObjectType.RCT_PFR)
            {
                var volume = Num(obj, "Volume");
                var length = Num(obj, "Length");
                slots.Add(new SpecificationSlot("Reactor volume", "Volume", volume, "m3", true, volume.HasValue && volume.Value > 0, ""));
                slots.Add(new SpecificationSlot("Reactor length", "Length", length, "m", true, length.HasValue && length.Value > 0,
                    "Volume and length together fix the diameter."));
                slots.Add(Optional("Catalyst loading", "CatalystLoading", Num(obj, "CatalystLoading"), "kg/m3", "Read by heterogeneous kinetics only."));
            }

            slots.Add(Optional("Pressure drop", "DeltaP", Num(obj, "DeltaP"), "Pa", "0 by default."));

            var kind = type.ToString().Replace("RCT_", "");
            var note = kind == "Conversion" ? "The conversion of each reaction is set on the reaction itself, in the reaction manager."
                     : kind == "Equilibrium" ? "The equilibrium constants come from the reactions; the reactor adds only the thermal mode."
                     : kind == "Gibbs" ? "Minimises Gibbs energy at the outlet temperature and pressure."
                     : "Kinetic reactor: the rate expressions on the reactions and the volume fix the conversion.";

            return new ObjectDegreesOfFreedom(tag, kind + " reactor", mode, true, slots, note);
        }

        private static int ActiveReactions(IFlowsheet flowsheet, string setId)
        {
            try
            {
                if (string.IsNullOrEmpty(setId)) return 0;
                IReactionSet set;
                if (!flowsheet.ReactionSets.TryGetValue(setId, out set) || set == null) return 0;
                return set.Reactions.Values.Count(r => r.IsActive && flowsheet.Reactions.ContainsKey(r.ReactionID));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static ObjectDegreesOfFreedom ComponentSeparator(ISimulationObject obj, string tag)
        {
            var slots = new List<SpecificationSlot>();
            var specs = Prop(obj, "ComponentSepSpecs") as IDictionary;
            var anyNonZero = false;
            var count = 0;

            if (specs != null)
            {
                foreach (DictionaryEntry entry in specs)
                {
                    var value = Num(entry.Value, "SpecValue");
                    var kind = Text(entry.Value, "SepSpec").Replace('_', ' ');
                    var units = Text(entry.Value, "SpecUnit");
                    slots.Add(new SpecificationSlot(entry.Key + " to the specified outlet", "ComponentSepSpecs[" + entry.Key + "]",
                        value, units, false, true, kind));
                    if (value.HasValue && value.Value > 0) anyNonZero = true;
                    count++;
                }
            }

            slots.Insert(0, new SpecificationSlot("At least one compound split", "ComponentSepSpecs", count, "", true, anyNonZero,
                anyNonZero ? "" : "Every split is zero, so everything leaves through the other outlet."));

            return new ObjectDegreesOfFreedom(tag, "ComponentSeparator", "Outlet " + (Num(obj, "SpecifiedStreamIndex") + 1), true, slots,
                "One split per compound, as a fraction or a flow, to the outlet you chose.");
        }

        // ── Logical objects ───────────────────────────────────────────────────

        private static ObjectDegreesOfFreedom Adjust(ISimulationObject obj, string tag)
        {
            var manipulated = PropertyId(obj, "PROP_SP_0");
            var controlled = PropertyId(obj, "PROP_SP_1");
            var slots = new List<SpecificationSlot>
            {
                new SpecificationSlot("Manipulated variable", "ManipulatedObjectData", null, "", true, !string.IsNullOrEmpty(manipulated),
                    "The specification the adjust is allowed to change."),
                new SpecificationSlot("Controlled variable", "ControlledObjectData", null, "", true, !string.IsNullOrEmpty(controlled),
                    "The result the adjust drives to the set-point."),
                new SpecificationSlot("Set-point", "AdjustValue", Num(obj, "AdjustValue"), "", true, true, "")
            };
            return new ObjectDegreesOfFreedom(tag, "Adjust", "", true, slots,
                "An adjust frees one specification and imposes one target, so the flowsheet's degrees of freedom do not change.");
        }

        private static ObjectDegreesOfFreedom Spec(ISimulationObject obj, string tag)
        {
            var source = PropertyId(obj, "PROP_SP_0");
            var target = PropertyId(obj, "PROP_SP_1");
            var slots = new List<SpecificationSlot>
            {
                new SpecificationSlot("Source variable", "SourceObjectData", null, "", true, !string.IsNullOrEmpty(source), ""),
                new SpecificationSlot("Target variable", "TargetObjectData", null, "", true, !string.IsNullOrEmpty(target),
                    "Receives the source value through the expression."),
                new SpecificationSlot("Expression", "Expression", null, "", true, true, Text(obj, "Expression"))
            };
            return new ObjectDegreesOfFreedom(tag, "Specification", "", true, slots,
                "A specification block writes one value from another, replacing a specification you would otherwise type.");
        }

        private static ObjectDegreesOfFreedom Recycle(ISimulationObject obj, string tag)
        {
            return new ObjectDegreesOfFreedom(tag, "Recycle", "", true, new List<SpecificationSlot>(),
                "A recycle needs no specification; it tears the loop and iterates until its inlet matches its outlet.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static SpecificationSlot Optional(string name, string property, double? value, string units, string note)
        {
            return new SpecificationSlot(name, property, value, units, false, true, note);
        }

        /// <summary>The duty the energy stream attached to a unit's energy inlet carries, if any.</summary>
        private static double? EnergyInlet(ISimulationObject obj)
        {
            var flowsheet = FlowsheetOf(obj);
            foreach (var port in obj.GraphicObject.InputConnectors.Where(c => c.IsAttached && c.Type == ConType.ConEn))
            {
                var connector = port.AttachedConnector;
                if (connector == null || connector.AttachedFrom == null || flowsheet == null) continue;
                ISimulationObject stream;
                if (!flowsheet.SimulationObjects.TryGetValue(connector.AttachedFrom.Name, out stream)) continue;
                var energy = stream as IEnergyStream;
                if (energy == null) continue;
                try { return energy.GetEnergyFlow(); } catch (Exception) { return null; }
            }
            return null;
        }

        private static IFlowsheet FlowsheetOf(ISimulationObject obj)
        {
            try { return Prop(obj, "FlowSheet") as IFlowsheet; } catch (Exception) { return null; }
        }

        private static bool IsDecoration(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.GO_Table:
                case ObjectType.GO_Text:
                case ObjectType.GO_Image:
                case ObjectType.GO_FloatingTable:
                case ObjectType.GO_MasterTable:
                case ObjectType.GO_SpreadsheetTable:
                case ObjectType.GO_Rectangle:
                case ObjectType.GO_Chart:
                case ObjectType.GO_InputControl:
                case ObjectType.GO_HTMLText:
                case ObjectType.GO_Button:
                case ObjectType.GO_Animation:
                case ObjectType.Nenhum:
                    return true;
                default:
                    return false;
            }
        }

        private static string TagOf(ISimulationObject obj)
        {
            return obj.GraphicObject != null && !string.IsNullOrEmpty(obj.GraphicObject.Tag)
                ? obj.GraphicObject.Tag
                : obj.Name;
        }

        private static string PropertyId(ISimulationObject obj, string id)
        {
            try
            {
                var value = obj.GetPropertyValue(id);
                return value == null ? "" : value.ToString();
            }
            catch (Exception) { return ""; }
        }

        private static object Prop(object target, string name)
        {
            if (target == null) return null;
            var property = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property == null) return null;
            try { return property.GetValue(target, null); } catch (Exception) { return null; }
        }

        private static object Field(object target, string name)
        {
            if (target == null) return null;
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (field == null) return null;
            try { return field.GetValue(target); } catch (Exception) { return null; }
        }

        private static string Text(object target, string name)
        {
            var value = Prop(target, name);
            return value == null ? "" : value.ToString();
        }

        private static string FieldText(object target, string name)
        {
            var value = Field(target, name);
            return value == null ? "" : value.ToString();
        }

        private static double? Num(object target, string name)
        {
            double value;
            return TryDouble(Prop(target, name), out value) ? value : (double?)null;
        }

        private static double? FieldNum(object target, string name)
        {
            double value;
            return TryDouble(Field(target, name), out value) ? value : (double?)null;
        }

        private static bool Bool(object target, string name)
        {
            var value = Prop(target, name);
            return value is bool && (bool)value;
        }

        /// <summary>A number for a note, or "n/a" when it is not one.</summary>
        private static string Fmt(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "n/a" : value.ToString("G5", CultureInfo.InvariantCulture);
        }

        private static bool TryDouble(object value, out double result)
        {
            result = 0;
            if (value == null) return false;
            if (value is string) return false;
            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return false;
            }
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }
    }
}
