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
using DWSIM.Interfaces.Enums;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The instruments of the flowsheet in the 3D model: a transmitter for what each controller
    /// measures and a local indicator for each gauge, placed where the measurement is taken (on the
    /// stream's line, or beside the item), with ISA letters from the kind of the measured property.
    /// The valves a controller moves are drawn as control valves.
    /// </summary>
    public static class Instruments
    {
        const string InstrumentBody = "#4a6fa5";

        /// <summary>ISA first letter for a measured property: temperature, pressure, flow, level, otherwise analysis.</summary>
        public static string Letter(UnitOfMeasure kind)
        {
            var k = kind.ToString().ToLowerInvariant();
            if (k.Contains("temperature")) return "T";
            if (k.Contains("pressure") || k == "deltap") return "P";
            if (k.Contains("flow") && !k.Contains("heat")) return "F";
            if (k == "distance") return "L";
            return "A";
        }

        static bool IsController(ObjectType t) => t.ToString().StartsWith("Controller_");

        /// <summary>
        /// The letter of a measurement: from the kind of its property; when that is not set, from the
        /// property's name (in English), and last from the instrument's own tag (LC-01 is a level loop).
        /// </summary>
        internal static string Letter(IFlowsheet fs, UnitOfMeasure kind, string property, string tag)
        {
            var letter = Letter(kind);
            if (letter != "A") return letter;
            string name = property ?? "";
            try { name = fs.GetTranslatedString(property, "en") ?? name; } catch { }
            name = (name + " " + property).ToLowerInvariant();
            if (name.Contains("level")) return "L";
            if (name.Contains("temperature")) return "T";
            if (name.Contains("pressure")) return "P";
            if (name.Contains("flow")) return "F";
            var t = (tag ?? "").Trim().ToUpperInvariant();
            if (t.Length > 1 && "LTPF".IndexOf(t[0]) >= 0 && "CIT".IndexOf(t[1]) >= 0) return t[0].ToString();
            return "A";
        }

        /// <summary>The valves moved by a controller, by object name, with the letter of what the loop controls.</summary>
        public static Dictionary<string, string> ControlledValves(IFlowsheet fs)
        {
            var valves = new Dictionary<string, string>();
            foreach (var obj in fs.SimulationObjects.Values)
            {
                if (obj.GraphicObject == null || !IsController(obj.GraphicObject.ObjectType) || !(obj is IAdjust c)) continue;
                var mv = c.ManipulatedObjectData?.ID;
                if (string.IsNullOrEmpty(mv)) continue;
                valves[mv] = Letter(fs, c.ControlledObjectData?.UnitsType ?? UnitOfMeasure.none, c.ControlledObjectData?.PropertyName, obj.GraphicObject.Tag);
            }
            return valves;
        }

        /// <summary>Adds a transmitter per controller and an indicator per gauge, at their measuring points.</summary>
        public static void Add(PlantModel model, IFlowsheet fs)
        {
            var itemsByName = model.Equipment.Where(e => e.Name != null).ToDictionary(e => e.Name, e => e);
            var perItem = new Dictionary<string, int>();
            var perLine = new Dictionary<string, int>();
            var linesByTag = model.Pipes.GroupBy(r => r.Tag).ToDictionary(g => g.Key, g => g.First());

            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null) continue;

                string measuredId, property, letters;
                UnitOfMeasure kind;
                if (IsController(go.ObjectType) && obj is IAdjust c)
                {
                    measuredId = c.ControlledObjectData?.ID;
                    property = c.ControlledObjectData?.PropertyName ?? "";
                    kind = c.ControlledObjectData?.UnitsType ?? UnitOfMeasure.none;
                    letters = Letter(fs, kind, property, go.Tag) + "T";
                }
                else if (obj is IIndicator g)
                {
                    measuredId = g.SelectedObjectID;
                    property = g.SelectedProperty ?? "";
                    kind = g.SelectedPropertyType;
                    letters = Letter(fs, kind, property, go.Tag) + "I";
                }
                else continue;

                if (string.IsNullOrEmpty(measuredId) || !fs.SimulationObjects.TryGetValue(measuredId, out var measured)) continue;

                Vec3 at;
                Vec3 mount;
                if (linesByTag.TryGetValue(measured.GraphicObject?.Tag ?? "", out var line) && line.Path.Count >= 2)
                {
                    // on top of the line, 40 % along its route, the next one further along
                    perLine.TryGetValue(line.Tag, out var nl);
                    perLine[line.Tag] = nl + 1;
                    var p = PointAlong(line.Path, Math.Min(0.9, 0.4 + 0.15 * nl));
                    at = p + new Vec3(0, 0, line.OuterDiameter / 2 + line.Insulation);
                    mount = Vec3.PlusZ;
                }
                else if (itemsByName.TryGetValue(measured.Name, out var item) && item.Visible)
                {
                    // on the front of the item, at mid height
                    var env = item.Envelope;
                    var front = env.Kind == EnvelopeKind.HorizontalCylinder || env.Kind == EnvelopeKind.Box
                        ? (item.Rotation % 180 == 0 ? env.HalfY : env.HalfX)
                        : env.HalfX;
                    // spread along the front: 400 mm apart, alternating left and right of the centre
                    perItem.TryGetValue(item.Name, out var ni);
                    perItem[item.Name] = ni + 1;
                    var dx = (ni % 2 == 0 ? 1 : -1) * 400.0 * ((ni + 1) / 2);
                    at = item.Origin + new Vec3(dx, -front, (env.ZBottom + env.ZTop) / 2);
                    mount = new Vec3(0, -1, 0);
                }
                else continue;

                var tag = letters + " " + go.Tag;
                var inst = letters.EndsWith("I") ? Indicator(mount) : Transmitter(mount);
                inst.Name = obj.Name;
                inst.Tag = tag;
                inst.Description = letters.EndsWith("I") ? "Local indicator" : "Transmitter";
                inst.IsInstrument = true;
                inst.Placed = true;
                inst.Origin = at;
                inst.Properties["Measures"] = (measured.GraphicObject?.Tag ?? measured.Name) + (property.Length > 0 ? ", " + property : "");
                inst.Properties["Kind"] = kind.ToString();
                if (obj is IAdjust ctl && !string.IsNullOrEmpty(ctl.ManipulatedObjectData?.ID) && fs.SimulationObjects.TryGetValue(ctl.ManipulatedObjectData.ID, out var mv))
                    inst.Properties["Moves"] = (mv.GraphicObject?.Tag ?? mv.Name) + ", " + ctl.ManipulatedObjectData.PropertyName;
                inst.Label = tag;
                inst.FootprintX = inst.FootprintY = 200;
                model.Equipment.Add(inst);
            }
        }

        static Vec3 PointAlong(List<Vec3> path, double fraction)
        {
            double total = 0;
            for (var i = 1; i < path.Count; i++) total += (path[i] - path[i - 1]).Length;
            var target = fraction * total;
            for (var i = 1; i < path.Count; i++)
            {
                var l = (path[i] - path[i - 1]).Length;
                if (target <= l) return path[i - 1] + (path[i] - path[i - 1]) * (l > 0 ? target / l : 0);
                target -= l;
            }
            return path[path.Count - 1];
        }

        /// <summary>A transmitter on a short stem, mounted along <paramref name="mount"/> from the measuring point.</summary>
        static EquipmentItem Transmitter(Vec3 mount)
        {
            var item = new EquipmentItem();
            var stem = 150.0;
            item.Parts.Add(new Primitive { Kind = PrimitiveKind.Cylinder, Name = "stem", Point = new Vec3(0, 0, 0), AxisVector = mount, Diameter = 40, Height = stem, Color = EquipmentTemplates.Steel, NoClash = true });
            var head = mount * (stem + 90);
            item.Parts.Add(new Primitive { Kind = PrimitiveKind.Box, Name = "transmitter", Point = head, SizeX = 160, SizeY = 160, SizeZ = 180, Color = InstrumentBody, Material = null, NoClash = true });
            item.Envelope = new Envelope { Kind = EnvelopeKind.Point, ZBottom = head.Z + 90, ZTop = head.Z + 90 };
            return item;
        }

        /// <summary>A local indicator: a dial on a short stem, facing the front.</summary>
        static EquipmentItem Indicator(Vec3 mount)
        {
            var item = new EquipmentItem();
            var stem = 150.0;
            item.Parts.Add(new Primitive { Kind = PrimitiveKind.Cylinder, Name = "stem", Point = new Vec3(0, 0, 0), AxisVector = mount, Diameter = 30, Height = stem, Color = EquipmentTemplates.Steel, NoClash = true });
            var head = mount * (stem + 80);
            item.Parts.Add(new Primitive { Kind = PrimitiveKind.Cylinder, Name = "dial", Point = head + new Vec3(0, 30, 0), AxisVector = new Vec3(0, -1, 0), Diameter = 160, Height = 60, Color = "#f2f2f2", Material = null, NoClash = true });
            item.Envelope = new Envelope { Kind = EnvelopeKind.Point, ZBottom = head.Z + 80, ZTop = head.Z + 80 };
            return item;
        }
    }
}
