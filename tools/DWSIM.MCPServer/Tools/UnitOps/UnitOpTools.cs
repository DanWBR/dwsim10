using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Interfaces;
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
            };

        public UnitOpTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_unitop_add", "Add a unit operation to the flowsheet. Type can be: Mixer, Splitter, Heater, Cooler, Pump, Compressor, Expander, Valve, Pipe, HeatExchanger, ComponentSeparator, Tank, Vessel, OrificePlate, Filter, SolidsSeparator, ShortcutColumn, DistillationColumn, AbsorptionColumn, ConversionReactor, EquilibriumReactor, GibbsReactor, CSTR, PFR, WindTurbine, HydroelectricTurbine, SolarPanel, WaterElectrolyzer, PEMFuelCell, ReaktoroGibbsReactor, BioReactor, AnaerobicDigester, CFBFastPyrolysis, Pretreatment, BiogasUpgrader, CellLysis, Centrifuge, Chromatography, CrossflowUF, Crystallizer.")]
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

            if (obj is DWSIM.Interfaces.IUnitOperation uo)
            {
                var props = new JObject();
                try
                {
                    foreach (var propName in uo.GetKeyPropertyNames())
                    {
                        try
                        {
                            var val = uo.GetKeyPropertyValue(propName);
                            var units = uo.GetKeyPropertyUnits(propName);
                            props[propName] = new JObject { ["value"] = val, ["units"] = units };
                        }
                        catch { }
                    }
                }
                catch { }
                result["properties"] = props;
            }

            return result;
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
