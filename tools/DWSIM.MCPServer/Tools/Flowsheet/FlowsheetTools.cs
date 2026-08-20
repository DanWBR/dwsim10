using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.MCPServer.Sessions;

namespace DWSIM.MCPServer.Tools.Flowsheet
{
    public class FlowsheetTools
    {
        private readonly SessionManager _sessions;

        public FlowsheetTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_flowsheet_create", "Create a new empty headless DWSIM flowsheet. Returns a flowsheet_id handle for subsequent calls.")]
        public JObject Create(
            [McpParam("Optional name for the simulation", Required = false)] string name = null)
        {
            var id = _sessions.CreateFlowsheet(name);
            return new JObject { ["flowsheet_id"] = id };
        }

        [McpTool("dwsim_flowsheet_load", "Load a flowsheet from a .dwxml or .dwxmz file. Returns a flowsheet_id handle.")]
        public JObject Load(
            [McpParam("Full path to the .dwxml or .dwxmz file")] string filepath)
        {
            var id = _sessions.LoadFlowsheet(filepath);
            var fs = _sessions.GetFlowsheet(id);
            var inner = fs.Inner;
            return new JObject
            {
                ["flowsheet_id"] = id,
                ["name"] = inner.FlowsheetOptions.SimulationName,
                ["compounds"] = new JArray(inner.SelectedCompounds.Keys.ToArray()),
                ["objects_count"] = inner.SimulationObjects.Count
            };
        }

        [McpTool("dwsim_flowsheet_save", "Save a flowsheet to disk as .dwxmz (compressed) or .dwxml.")]
        public JObject Save(
            [McpParam("Flowsheet handle returned by create/load")] string flowsheet_id,
            [McpParam("Full path for the output file")] string filepath,
            [McpParam("Whether to save as compressed .dwxmz (true) or plain .dwxml (false)", Required = false)] bool compressed = true)
        {
            var mcpFs = _sessions.GetMcpFlowsheet(flowsheet_id);
            if (mcpFs != null)
            {
                // Use McpFlowsheet's own save (handles .dwxmz and .dwxml)
                var savePath = compressed && !filepath.EndsWith(".dwxmz", System.StringComparison.OrdinalIgnoreCase)
                    ? System.IO.Path.ChangeExtension(filepath, ".dwxmz")
                    : filepath;
                mcpFs.SaveSimulation(savePath);
                return new JObject { ["saved"] = savePath };
            }
            else
            {
                var fs = _sessions.GetFlowsheet(flowsheet_id);
                fs.Save(filepath, compressed);
                return new JObject { ["saved"] = filepath };
            }
        }

        [McpTool("dwsim_flowsheet_close", "Close a flowsheet and free its resources.")]
        public JObject Close(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var ok = _sessions.CloseFlowsheet(flowsheet_id);
            return new JObject { ["closed"] = ok };
        }

        [McpTool("dwsim_flowsheet_list_objects", "List all simulation objects (streams, unit operations) in the flowsheet.")]
        public JObject ListObjects(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Optional type filter: 'streams', 'unitops', or null for all", Required = false)] string type_filter = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var objects = fs.Inner.SimulationObjects.Values;
            var arr = new JArray();
            foreach (var obj in objects)
            {
                var go = obj.GraphicObject;
                if (go == null) continue;

                bool isStream = go.ObjectType.ToString().Contains("Stream");
                if (type_filter == "streams" && !isStream) continue;
                if (type_filter == "unitops" && isStream) continue;

                arr.Add(new JObject
                {
                    ["name"] = go.Tag,
                    ["id"] = obj.Name,
                    ["type"] = go.ObjectType.ToString(),
                    ["x"] = go.X,
                    ["y"] = go.Y,
                    ["width"] = go.Width,
                    ["height"] = go.Height,
                    ["calculated"] = obj.Calculated,
                    ["error"] = obj.ErrorMessage ?? ""
                });
            }
            return new JObject { ["objects"] = arr };
        }

        [McpTool("dwsim_object_rename", "Rename a simulation object (stream or unit operation) by changing its tag. Accepts the object's current tag or its id.")]
        public JObject Rename(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Current tag or id of the object to rename")] string name,
            [McpParam("New tag for the object")] string new_name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var obj = fs.Inner.SimulationObjects.Values.FirstOrDefault(
                o => o.GraphicObject != null && (o.GraphicObject.Tag == name || o.Name == name));
            if (obj == null)
                throw new ArgumentException($"No simulation object with tag or id '{name}'.");

            obj.GraphicObject.Tag = new_name;
            return new JObject
            {
                ["id"] = obj.Name,
                ["name"] = new_name
            };
        }

        [McpTool("dwsim_flowsheet_summary", "Get a high-level summary of the flowsheet: compounds, property package, object counts, solver status.")]
        public JObject Summary(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var inner = fs.Inner;

            var compounds = new JArray(inner.SelectedCompounds.Keys.ToArray());
            var ppNames = new JArray();
            foreach (var pp in inner.PropertyPackages.Values)
                ppNames.Add(pp.Name);

            int nStreams = 0, nUnitOps = 0;
            foreach (var obj in inner.SimulationObjects.Values)
            {
                if (obj.GraphicObject?.ObjectType.ToString().Contains("Stream") == true)
                    nStreams++;
                else
                    nUnitOps++;
            }

            return new JObject
            {
                ["name"] = inner.FlowsheetOptions.SimulationName,
                ["compounds"] = compounds,
                ["property_packages"] = ppNames,
                ["streams_count"] = nStreams,
                ["unitops_count"] = nUnitOps,
                ["total_objects"] = inner.SimulationObjects.Count
            };
        }

        [McpTool("dwsim_flowsheet_get_xml", "Export the flowsheet as XML string (for inspection/debug). May be large for complex flowsheets.")]
        public JObject GetXml(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var xml = fs.Inner.SaveToXML().ToString();
            const int maxLen = 500000;
            if (xml.Length > maxLen)
            {
                return new JObject
                {
                    ["truncated"] = true,
                    ["xml"] = xml.Substring(0, maxLen),
                    ["message"] = $"XML truncated at {maxLen} chars. Use dwsim_flowsheet_save to get the full file."
                };
            }
            return new JObject { ["xml"] = xml };
        }
    }
}
