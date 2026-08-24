using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.MCPServer.Sessions;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Drawing.SkiaSharp.GraphicObjects;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.MCPServer.Tools.Graphics
{
    public class GraphicTools
    {
        private readonly SessionManager _sessions;

        public GraphicTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_graphic_list",
            "List all annotation/display graphic objects on the flowsheet surface (text labels, tables, master tables, charts, buttons, rectangles). Returns name, type, position (x, y), and size (width, height).")]
        public JObject List(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            var arr = new JArray();

            foreach (var gobj in surface.DrawingObjects)
            {
                var ot = gobj.ObjectType;
                if (ot == ObjectType.GO_Text || ot == ObjectType.GO_HTMLText ||
                    ot == ObjectType.GO_Table || ot == ObjectType.GO_MasterTable ||
                    ot == ObjectType.GO_SpreadsheetTable || ot == ObjectType.GO_Chart ||
                    ot == ObjectType.GO_Button || ot == ObjectType.GO_Rectangle ||
                    ot == ObjectType.GO_Image)
                {
                    arr.Add(new JObject
                    {
                        ["name"] = gobj.Name,
                        ["type"] = ot.ToString(),
                        ["x"] = gobj.X,
                        ["y"] = gobj.Y,
                        ["width"] = gobj.Width,
                        ["height"] = gobj.Height
                    });
                }
            }
            return new JObject { ["objects"] = arr };
        }

        [McpTool("dwsim_graphic_add",
            "Add a graphic annotation object to the flowsheet surface. Supported types: text, htmltext, button, rectangle, table, mastertable, spreadsheettable. " +
            "For mastertable: automatically shows Temperature, Pressure, Mass Flow, Molar Flow for all streams. " +
            "For table: shows properties of a single object (set 'tag' to the object name).")]
        public JObject Add(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Type: text, htmltext, button, rectangle, table, mastertable, spreadsheettable")] string object_type,
            [McpParam("X position in pixels", Required = false)] int x = 50,
            [McpParam("Y position in pixels", Required = false)] int y = 50,
            [McpParam("Text content (for text/htmltext/button)", Required = false)] string text = "",
            [McpParam("Font size in points", Required = false)] double font_size = 12,
            [McpParam("Tag/identifier - for table type, set to the object name to display", Required = false)] string tag = "")
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var inner = fs.Inner;
            GraphicObject gobj = null;

            switch ((object_type ?? "").ToLowerInvariant())
            {
                case "text":
                    var tg = new TextGraphic(x, y, string.IsNullOrEmpty(text) ? "Text" : text);
                    tg.Size = font_size;
                    gobj = tg;
                    break;

                case "htmltext":
                    var htg = new TextGraphic(x, y, string.IsNullOrEmpty(text) ? "<b>HTML Text</b>" : text);
                    htg.Size = font_size;
                    htg.ObjectType = ObjectType.GO_HTMLText;
                    gobj = htg;
                    break;

                case "button":
                    var bg = new DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ButtonGraphic();
                    bg.X = x; bg.Y = y;
                    bg.Text = string.IsNullOrEmpty(text) ? "Button" : text;
                    gobj = bg;
                    break;

                case "rectangle":
                    var rg = new DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RectangleGraphic();
                    rg.X = x; rg.Y = y;
                    rg.Text = text ?? "";
                    gobj = rg;
                    break;

                case "table":
                    var tb = new TableGraphic(x, y);
                    tb.Flowsheet = inner;
                    gobj = tb;
                    break;

                case "mastertable":
                    var mt = new MasterTableGraphic(x, y);
                    mt.Flowsheet = inner;
                    gobj = mt;
                    break;

                case "spreadsheettable":
                    var st = new SpreadsheetTableGraphic(x, y);
                    st.Flowsheet = inner;
                    gobj = st;
                    break;

                default:
                    throw new ArgumentException($"Unknown graphic object type: {object_type}. Supported: text, htmltext, button, rectangle, table, mastertable, spreadsheettable.");
            }

            gobj.Name = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(tag)) gobj.Tag = tag;

            inner.AddGraphicObject(gobj);

            return new JObject
            {
                ["name"] = gobj.Name,
                ["object_type"] = object_type,
                ["x"] = gobj.X,
                ["y"] = gobj.Y
            };
        }

        [McpTool("dwsim_graphic_edit",
            "Edit properties of an existing graphic object: position (x, y), size (width, height), text, font_size, or tag.")]
        public JObject Edit(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Tag or id of the graphic or simulation object to edit")] string name,
            [McpParam("New X position", Required = false)] int x = -1,
            [McpParam("New Y position", Required = false)] int y = -1,
            [McpParam("New width", Required = false)] int width = -1,
            [McpParam("New height", Required = false)] int height = -1,
            [McpParam("New text content", Required = false)] string text = null,
            [McpParam("New font size", Required = false)] double font_size = -1,
            [McpParam("New tag value", Required = false)] string tag = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            var gobj = surface.DrawingObjects.FirstOrDefault(o => o.Name == name || o.Tag == name);
            if (gobj == null)
                throw new ArgumentException($"Graphic object not found: {name}");

            if (x >= 0) gobj.X = x;
            if (y >= 0) gobj.Y = y;
            if (width > 0) gobj.Width = width;
            if (height > 0) gobj.Height = height;

            if (tag != null) gobj.Tag = tag;

            if (text != null && gobj is TextGraphic tg2)
                tg2.Text = text;

            if (font_size > 0 && gobj is TextGraphic tg3)
                tg3.Size = font_size;

            return new JObject
            {
                ["name"] = gobj.Name,
                ["tag"] = gobj.Tag,
                ["x"] = gobj.X,
                ["y"] = gobj.Y,
                ["width"] = gobj.Width,
                ["height"] = gobj.Height
            };
        }

        [McpTool("dwsim_graphic_remove", "Remove a graphic annotation object from the flowsheet surface by name.")]
        public JObject Remove(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Name of the graphic object to remove")] string name)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var surface = (GraphicsSurface)fs.Inner.GetSurface();
            var gobj = surface.DrawingObjects.FirstOrDefault(o => o.Name == name);
            if (gobj == null)
                throw new ArgumentException($"Graphic object not found: {name}");

            surface.DeleteSelectedObject(gobj);
            return new JObject { ["removed"] = true, ["name"] = name };
        }

        [McpTool("dwsim_graphic_screenshot",
            "Render the flowsheet (PFD) to a PNG image and return it as a base64-encoded string.")]
        public JObject Screenshot(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            try
            {
                fs.SaveScreenshot(tmpFile);
                var bytes = File.ReadAllBytes(tmpFile);
                var base64 = Convert.ToBase64String(bytes);
                return new JObject
                {
                    ["base64_png"] = base64,
                    ["size_bytes"] = bytes.Length
                };
            }
            finally
            {
                try { File.Delete(tmpFile); } catch { }
            }
        }

        [McpTool("dwsim_graphic_screenshot_to_file",
           "Render the flowsheet (PFD) to a PNG image and save it to the specified PNG file path.")]
        public JObject ScreenshotToFile(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Path to save the PNG file")] string file_path)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var tmpFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            try
            {
                fs.SaveScreenshot(tmpFile);
                File.Copy(tmpFile, file_path, true);
                var bytes = File.ReadAllBytes(file_path);
                return new JObject
                {
                    ["file_path"] = file_path,
                    ["size_bytes"] = bytes.Length
                };
            }
            finally
            {
                try { File.Delete(tmpFile); } catch { }
            }
        }
    }
}
