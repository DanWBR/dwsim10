using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Automation;
using DWSIM.Automation.FluentAPI;
using DWSIM.MCPServer.Sessions;

namespace DWSIM.MCPServer.Tools.Thermo
{
    public class ThermoTools
    {
        private readonly SessionManager _sessions;
        private readonly Automation3 _automation;

        public ThermoTools(SessionManager sessions, Automation3 automation)
        {
            _sessions = sessions;
            _automation = automation;
        }

        [McpTool("dwsim_thermo_list_compounds", "List all available compounds in the DWSIM database. Returns compound names that can be used with add_compounds.")]
        public JObject ListCompounds()
        {
            var compounds = _automation.AvailableCompounds;
            var arr = new JArray();
            foreach (var name in compounds.Keys.OrderBy(k => k))
                arr.Add(name);
            return new JObject { ["compounds"] = arr, ["count"] = arr.Count };
        }

        [McpTool("dwsim_thermo_add_compounds", "Add one or more compounds to the flowsheet by name (e.g. 'Water', 'Methane', 'Ethanol').")]
        public JObject AddCompounds(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Array of compound names to add", JsonType = "array")] string[] names)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var added = new JArray();
            foreach (var name in names)
            {
                fs.WithCompound(name);
                added.Add(name);
            }
            return new JObject { ["added"] = added };
        }

        [McpTool("dwsim_thermo_list_property_packages", "List all available thermodynamic property packages.")]
        public JObject ListPropertyPackages(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var names = fs.AvailablePropertyPackages;
            var arr = new JArray();
            foreach (var n in names)
                arr.Add(n);
            return new JObject { ["property_packages"] = arr };
        }

        [McpTool("dwsim_thermo_set_property_package", "Set the thermodynamic property package for the flowsheet (e.g. 'Peng-Robinson', 'SRK', 'NRTL', 'UNIQUAC').")]
        public JObject SetPropertyPackage(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Property package name")] string name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            fs.WithPropertyPackage(name);
            return new JObject { ["property_package"] = name };
        }
    }
}
