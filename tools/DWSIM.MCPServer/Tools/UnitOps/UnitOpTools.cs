using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Automation.FluentAPI;
using DWSIM.MCPServer.Sessions;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.MCPServer.Tools.UnitOps
{
    public class UnitOpTools
    {
        private readonly SessionManager _sessions;

        private static readonly Dictionary<string, Action<FluentFlowsheet, string>> UnitOpFactory =
            new Dictionary<string, Action<FluentFlowsheet, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mixer"] = (fs, tag) => fs.AddMixer(tag),
                ["Splitter"] = (fs, tag) => fs.AddSplitter(tag),
                ["Heater"] = (fs, tag) => fs.AddHeater(tag),
                ["Cooler"] = (fs, tag) => fs.AddCooler(tag),
                ["Pump"] = (fs, tag) => fs.AddPump(tag),
                ["Compressor"] = (fs, tag) => fs.AddCompressor(tag),
                ["Expander"] = (fs, tag) => fs.AddExpander(tag),
                ["Valve"] = (fs, tag) => fs.AddValve(tag),
                ["Pipe"] = (fs, tag) => fs.AddPipe(tag),
                ["HeatExchanger"] = (fs, tag) => fs.AddHeatExchanger(tag),
                ["ComponentSeparator"] = (fs, tag) => fs.AddComponentSeparator(tag),
                ["Tank"] = (fs, tag) => fs.AddTank(tag),
                ["Vessel"] = (fs, tag) => fs.AddSeparator(tag),
                ["OrificePlate"] = (fs, tag) => fs.AddOrificePlate(tag),
                ["Filter"] = (fs, tag) => fs.AddFilter(tag),
                ["SolidsSeparator"] = (fs, tag) => fs.AddSolidsSeparator(tag),
                ["ShortcutColumn"] = (fs, tag) => fs.AddShortcutColumn(tag),
                ["DistillationColumn"] = (fs, tag) => fs.AddDistillationColumn(tag),
                ["AbsorptionColumn"] = (fs, tag) => fs.AddAbsorptionColumn(tag),
                ["ConversionReactor"] = (fs, tag) => fs.AddConversionReactor(tag),
                ["EquilibriumReactor"] = (fs, tag) => fs.AddEquilibriumReactor(tag),
                ["GibbsReactor"] = (fs, tag) => fs.AddGibbsReactor(tag),
                ["CSTR"] = (fs, tag) => fs.AddCSTR(tag),
                ["PFR"] = (fs, tag) => fs.AddPFR(tag),
                ["WindTurbine"] = (fs, tag) => fs.AddWindTurbine(tag),
                ["HydroelectricTurbine"] = (fs, tag) => fs.AddHydroelectricTurbine(tag),
                ["SolarPanel"] = (fs, tag) => fs.AddSolarPanel(tag),
                ["WaterElectrolyzer"] = (fs, tag) => fs.AddWaterElectrolyzer(tag),
                ["PEMFuelCell"] = (fs, tag) => fs.AddPEMFuelCell(tag),
                ["ReaktoroGibbsReactor"] = (fs, tag) => fs.AddReaktoroGibbsReactor(tag),
                ["BioReactor"] = (fs, tag) => fs.AddBioReactor(tag),
                ["AnaerobicDigester"] = (fs, tag) => fs.AddAnaerobicDigester(tag),
                ["CFBFastPyrolysis"] = (fs, tag) => fs.AddCFBFastPyrolysisReactor(tag),
                ["Pretreatment"] = (fs, tag) => fs.AddPretreatmentReactor(tag),
                ["BiogasUpgrader"] = (fs, tag) => fs.AddBiogasUpgrader(tag),
                ["CellLysis"] = (fs, tag) => fs.AddCellLysis(tag),
                ["Centrifuge"] = (fs, tag) => fs.AddCentrifuge(tag),
                ["Chromatography"] = (fs, tag) => fs.AddChromatographyColumn(tag),
                ["CrossflowUF"] = (fs, tag) => fs.AddCrossflowUF(tag),
                ["Crystallizer"] = (fs, tag) => fs.AddCrystallizer(tag),
                ["Recycle"] = (fs, tag) => fs.AddUnitOperation(ObjectType.OT_Recycle, tag),
                ["EnergyRecycle"] = (fs, tag) => fs.AddUnitOperation(ObjectType.OT_EnergyRecycle, tag),
                ["Spec"] = (fs, tag) => fs.AddUnitOperation(ObjectType.OT_Spec, tag),
                ["Adjust"] = (fs, tag) => fs.AddUnitOperation(ObjectType.OT_Adjust, tag),
            };

        public UnitOpTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_unitop_add", "Add a unit operation to the flowsheet. Type can be: Mixer, Splitter, Heater, Cooler, Pump, Compressor, Expander, Valve, Pipe, HeatExchanger, ComponentSeparator, Tank, Vessel, OrificePlate, Filter, SolidsSeparator, ShortcutColumn, DistillationColumn, AbsorptionColumn, ConversionReactor, EquilibriumReactor, GibbsReactor, CSTR, PFR, WindTurbine, HydroelectricTurbine, SolarPanel, WaterElectrolyzer, PEMFuelCell, ReaktoroGibbsReactor, BioReactor, AnaerobicDigester, CFBFastPyrolysis, Pretreatment, BiogasUpgrader, CellLysis, Centrifuge, Chromatography, CrossflowUF, Crystallizer, and the logical blocks Recycle, EnergyRecycle, Spec and Adjust. A flowsheet with a loop needs a Recycle on one of its streams to tear it.")]
        public JObject Add(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Unit operation type name")] string type,
            [McpParam("Tag/name for the unit operation")] string name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            if (!UnitOpFactory.TryGetValue(type, out var factory))
                throw new ArgumentException($"Unknown unit operation type: {type}. Use dwsim.unitop.list_types to see available types.");

            factory(fs, name);
            return new JObject { ["unitop"] = name, ["type"] = type };
        }

        [McpTool("dwsim_unitop_add_external", "Add an external unit operation by its display name (for Plus/extension unit operations).")]
        public JObject AddExternal(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Display name of the external unit operation")] string display_name,
            [McpParam("Tag/name for the unit operation")] string name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            fs.AddExternalUnitOperation(display_name, name);
            return new JObject { ["unitop"] = name, ["type"] = display_name };
        }

        [McpTool("dwsim_unitop_connect", "Connect streams to a unit operation's ports. Specify feed and/or product material and energy streams by name.")]
        public JObject Connect(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Unit operation tag/name")] string unitop,
            [McpParam("Feed material stream tag", Required = false)] string feed_stream = null,
            [McpParam("Feed stream port index (default 0)", Required = false)] int feed_port = 0,
            [McpParam("Product material stream tag", Required = false)] string product_stream = null,
            [McpParam("Product stream port index (default 0)", Required = false)] int product_port = 0,
            [McpParam("Energy feed stream tag", Required = false)] string energy_feed = null,
            [McpParam("Energy feed port index (default 0)", Required = false)] int energy_feed_port = 0,
            [McpParam("Energy product stream tag", Required = false)] string energy_product = null,
            [McpParam("Energy product port index (default 0)", Required = false)] int energy_product_port = 0)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var uo = fs.Inner.SimulationObjects.Values
                .First(o => o.GraphicObject?.Tag == unitop);

            var connections = new JArray();

            if (!string.IsNullOrEmpty(feed_stream))
            {
                var stream = fs.Inner.SimulationObjects.Values
                    .First(o => o.GraphicObject?.Tag == feed_stream);
                uo.ConnectFeedMaterialStream(stream, feed_port);
                connections.Add($"feed:{feed_stream}->port{feed_port}");
            }

            if (!string.IsNullOrEmpty(product_stream))
            {
                var stream = fs.Inner.SimulationObjects.Values
                    .First(o => o.GraphicObject?.Tag == product_stream);
                uo.ConnectProductMaterialStream(stream, product_port);
                connections.Add($"product:{product_stream}->port{product_port}");
            }

            if (!string.IsNullOrEmpty(energy_feed))
            {
                var stream = fs.Inner.SimulationObjects.Values
                    .First(o => o.GraphicObject?.Tag == energy_feed);
                uo.ConnectFeedEnergyStream(stream, energy_feed_port);
                connections.Add($"energy_feed:{energy_feed}->port{energy_feed_port}");
            }

            if (!string.IsNullOrEmpty(energy_product))
            {
                var stream = fs.Inner.SimulationObjects.Values
                    .First(o => o.GraphicObject?.Tag == energy_product);
                uo.ConnectProductEnergyStream(stream, energy_product_port);
                connections.Add($"energy_product:{energy_product}->port{energy_product_port}");
            }

            return new JObject { ["unitop"] = unitop, ["connections"] = connections };
        }

        [McpTool("dwsim_unitop_set",
            "Configure a unit operation: outlet pressure, outlet temperature, efficiency, calculation " +
            "mode, and anything else the model exposes. Names are matched against the property system, " +
            "the dynamic properties and the model's own properties, so both 'PROP_CO_1' and " +
            "'OutletPressure' work; an enum is given by name. A name that matches nothing comes back " +
            "with the ones that would have. Call dwsim_unitop_get_results to see what a unit reports.")]
        public JObject Set(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Unit operation tag")] string name,
            [McpParam("Properties to set, as {name: value}. Values are in the flowsheet unit system.", JsonType = "object")]
            JObject properties)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            var obj = fs.Inner.SimulationObjects.Values.FirstOrDefault(
                o => o.GraphicObject != null && (o.GraphicObject.Tag == name || o.Name == name));

            if (obj == null)
                throw new ArgumentException($"No unit operation with tag or id '{name}'.");

            var applied = PropertySetter.Apply(obj, properties, fs.Inner.FlowsheetOptions.SelectedUnitSystem);

            return new JObject
            {
                ["unitop"] = name,
                ["applied"] = applied
            };
        }

        [McpTool("dwsim_unitop_get_results", "Get calculated results for a unit operation.")]
        public JObject GetResults(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Unit operation tag/name")] string name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var obj = fs.Inner.SimulationObjects.Values
                .First(o => o.GraphicObject?.Tag == name);

            var result = new JObject
            {
                ["name"] = name,
                ["type"] = obj.GraphicObject?.ObjectType.ToString(),
                ["calculated"] = obj.Calculated,
                ["error"] = obj.ErrorMessage ?? ""
            };

            var props = new JObject();

            // The key properties are what a unit chooses to report, when it reports any. Most
            // report none, so fall back to its calculated properties, named as the property
            // grid names them - a duty nobody can read is a duty nobody can check.
            if (obj is DWSIM.Interfaces.IUnitOperation uo)
            {
                try
                {
                    foreach (var propName in uo.GetKeyPropertyNames())
                    {
                        try
                        {
                            props[propName] = new JObject
                            {
                                ["value"] = uo.GetKeyPropertyValue(propName),
                                ["units"] = uo.GetKeyPropertyUnits(propName)
                            };
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (props.Count == 0)
            {
                var units = fs.Inner.FlowsheetOptions.SelectedUnitSystem;

                // GetProperties reports the dynamic-mode settings alongside the real ones, and
                // there are enough of them - hold-up volume, flow conductance - to crowd the duty
                // out of the list. The extra-property bag is the authority on which is which.
                var dynamicNames = new HashSet<string>(
                    ((IDictionary<string, object>)obj.ExtraPropertiesDescriptions).Keys,
                    StringComparer.Ordinal);

                foreach (var entry in PropertyCatalog.For(obj, units, PropertyType.RO))
                {
                    if (entry.Value == null) continue;
                    if (dynamicNames.Contains(entry.Id)) continue;

                    var label = string.IsNullOrEmpty(entry.Description) ? entry.Id : entry.Description;
                    props[label] = new JObject { ["value"] = entry.Value.ToString(), ["units"] = entry.Units };
                }
            }

            result["properties"] = props;

            // Which specifications the unit actually reads depends on its calculation mode, and
            // there is no way to guess the names. Listing them here is what stops a caller from
            // setting a target the unit will ignore.
            var modes = PropertySetter.CalculationModes(obj);
            if (modes.Count > 0)
            {
                result["calculation_modes"] = new JArray(modes.Keys);
                result["calculation_mode"] = modes
                    .Where(m => m.Value == CurrentMode(obj))
                    .Select(m => m.Key)
                    .FirstOrDefault() ?? "";
            }

            return result;
        }

        /// <summary>The unit's current calculation mode as an id, or -1 when it has none.</summary>
        private static int CurrentMode(ISimulationObject obj)
        {
            var property = obj.GetType().GetProperty("CalcMode");
            if (property == null) return -1;

            try { return Convert.ToInt32(property.GetValue(obj)); }
            catch (Exception) { return -1; }
        }

        [McpTool("dwsim_unitop_list_types", "List all available unit operation types that can be used with dwsim_unitop_add.")]
        public JObject ListTypes()
        {
            var arr = new JArray();
            foreach (var key in UnitOpFactory.Keys.OrderBy(k => k))
                arr.Add(key);
            return new JObject { ["types"] = arr, ["count"] = arr.Count };
        }
    }
}
