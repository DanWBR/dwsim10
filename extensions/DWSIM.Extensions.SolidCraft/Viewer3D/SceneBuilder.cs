//    Copyright 2026 Daniel Wagner O. de Medeiros
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
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The scene the DWSIM 3D window draws: the plant model of the flowsheet (see
    /// <see cref="PlantModelBuilder"/>), with the same sizes, places, lines and rack as the
    /// SolidCraft model, as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "items" holds one entry per unit operation: "id" (the object name, which the window sends back
    /// in its commands), "tag", "desc", "type" (the object type), "o" (the origin on the ground),
    /// "rot" (quarter turns, degrees), "placed" (whether the place comes from the saved layout),
    /// "inst", "label", "props" and "parts". The parts are around the origin and already turned, so
    /// the window puts them under a node at "o" and moves that node when the user drags the item.
    /// </para>
    /// <para>
    /// "lines" holds the pipes in world coordinates: "id" (the stream's name; null for an item's own
    /// line), "tag", "desc", "c", "od", "ins", "v", "phase", "from", "to", "path", "fl" and "props".
    /// Lines are routed again whenever an item moves, so a change sends them all.
    /// </para>
    /// </remarks>
    public static class SceneBuilder
    {
        /// <summary>The plant model of the flowsheet, with the saved layout and the SolidCraft settings.</summary>
        public static PlantModel Model(IFlowsheet fs, ExportOptions options = null)
            => PlantModelBuilder.Build(fs, NameOf(fs), PlantLayout.Load(fs), options ?? ExportOptions.Load());

        public static JObject Build(IFlowsheet fs, ExportOptions options = null) => FromModel(Model(fs, options), fs);

        public static JObject FromModel(PlantModel model, IFlowsheet fs = null)
        {
            var items = new JArray();
            var ids = new HashSet<string>();
            foreach (var item in model.Equipment)
            {
                var parts = new JArray();
                foreach (var part in item.Parts)
                {
                    var o = WebViewer.Part(part.Moved(new Vec3(0, 0, 0)));
                    if (o != null) parts.Add(o);
                }
                items.Add(new JObject
                {
                    ["id"] = Unique(ids, item.Name ?? item.Tag ?? "item"),
                    ["tag"] = item.Tag,
                    ["desc"] = item.Description,
                    ["type"] = TypeOf(fs, item.Name),
                    ["o"] = WebViewer.V(item.Origin),
                    ["rot"] = item.Rotation,
                    ["placed"] = item.Placed,
                    ["inst"] = item.IsInstrument,
                    ["label"] = item.Label,
                    ["props"] = WebViewer.Props(item.Properties),
                    ["parts"] = parts
                });
            }

            var lines = new JArray();
            foreach (var run in model.Pipes.Where(r => r.Path.Count > 1))
                lines.Add(new JObject
                {
                    ["id"] = run.Name,
                    ["tag"] = run.Tag,
                    ["desc"] = "Line " + run.Tag + " (" + run.Size + ", " + (run.Phase ?? "").ToLowerInvariant() + "), " + run.From + " to " + run.To,
                    ["c"] = run.Color,
                    ["od"] = run.OuterDiameter,
                    ["ins"] = run.Insulation,
                    ["v"] = Math.Round(run.Velocity, 3),
                    ["phase"] = run.Phase,
                    ["from"] = run.From,
                    ["to"] = run.To,
                    ["path"] = new JArray(run.Path.Select(WebViewer.V)),
                    ["fl"] = new JArray(run.Flanges.Select(f => new JArray(WebViewer.V(f.Item1), WebViewer.V(f.Item2)))),
                    ["props"] = WebViewer.Props(run.Properties)
                });

            var scene = new JObject
            {
                ["name"] = model.Name,
                ["items"] = items,
                ["lines"] = lines,
                ["rack"] = null,
                ["warnings"] = new JArray(model.Warnings)
            };
            if (model.Rack != null)
                scene["rack"] = new JObject
                {
                    ["x0"] = model.Rack.X0, ["x1"] = model.Rack.X1, ["y0"] = model.Rack.Y0, ["w"] = model.Rack.Width,
                    ["top"] = model.Rack.Top, ["bay"] = PipeRack.BayLength, ["beam"] = PipeRack.BeamHeight
                };
            return scene;
        }

        /// <summary>
        /// The canvas point of a plot plan point (mm), the inverse of the builder's own map: through
        /// the columns and rows of a packed layout, or through the scale of a scaled one.
        /// </summary>
        public static void ToCanvas(PlantModel model, double x, double y, out double cx, out double cy)
        {
            if (model.MapCanvasX != null && model.MapCanvasX.Length > 0)
            {
                cx = Unmap(model.MapCanvasX, model.MapWorldX, model.MapHalfX, x);
                cy = Unmap(model.MapCanvasY, model.MapWorldY, model.MapHalfY, -y);
                return;
            }
            var scale = model.Scale > 0 ? model.Scale : 15;
            cx = x / scale + model.CanvasOriginX;
            cy = -y / scale + model.CanvasOriginY;
        }

        /// <summary>The inverse of PlantModelBuilder.Map, with the same 15 mm per canvas unit past the outer columns.</summary>
        static double Unmap(double[] canvas, double[] world, double[] half, double w)
        {
            const double beyond = 15;
            var n = canvas.Length;
            var low = world[0] - half[0];
            var high = world[n - 1] + half[n - 1];
            if (w <= low) return canvas[0] - (low - w) / beyond;
            if (w >= high) return canvas[n - 1] + (w - high) / beyond;
            if (w <= world[0]) return canvas[0];
            for (var k = 1; k < n; k++)
                if (w <= world[k])
                {
                    var span = world[k] - world[k - 1];
                    return span > 0 ? canvas[k - 1] + (w - world[k - 1]) / span * (canvas[k] - canvas[k - 1]) : canvas[k];
                }
            return canvas[n - 1];
        }

        static string NameOf(IFlowsheet fs) => PlantModelExtender.ModelName(fs);

        static string TypeOf(IFlowsheet fs, string name)
        {
            if (fs == null || name == null) return null;
            IGraphicObject go;
            return fs.GraphicObjects.TryGetValue(name, out go) && go != null ? go.ObjectType.ToString() : null;
        }

        static string Unique(HashSet<string> ids, string id)
        {
            var candidate = id;
            for (var k = 2; !ids.Add(candidate); k++) candidate = id + "#" + k;
            return candidate;
        }
    }
}
