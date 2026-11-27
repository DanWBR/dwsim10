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
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The unit operations in the order the material goes through them: first the ones the feeds
    /// enter, then, unit by unit, the ones their products go to. A recycle does not take the tour
    /// back; units the material does not reach come last. Ties go left to right, then top to
    /// bottom, as on the flowsheet drawing. The flyby of the 3D window visits them in this order.
    /// </summary>
    public static class ProcessOrder
    {
        public static List<string> Of(IFlowsheet fs, ICollection<string> only = null)
        {
            var units = fs.SimulationObjects.Values
                .Where(o => o.GraphicObject != null && IsUnit(o.GraphicObject.ObjectType) && (only == null || only.Contains(o.Name)))
                .ToDictionary(o => o.Name, o => o.GraphicObject);
            var down = units.Keys.ToDictionary(k => k, k => new List<string>());
            var fedByUnit = new HashSet<string>();
            foreach (var kv in units)
                foreach (var next in Downstream(kv.Value).Where(n => units.ContainsKey(n) && n != kv.Key).Distinct())
                {
                    down[kv.Key].Add(next);
                    fedByUnit.Add(next);
                }

            Func<string, (float, float)> place = id => (units[id].X, units[id].Y);
            var order = new List<string>();
            var seen = new HashSet<string>();
            var starts = units.Keys.Where(k => !fedByUnit.Contains(k)).OrderBy(k => place(k)).ToList();
            var rest = units.Keys.OrderBy(k => place(k)).ToList();
            foreach (var start in starts.Concat(rest))
            {
                if (seen.Contains(start)) continue;
                var queue = new Queue<string>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    order.Add(id);
                    foreach (var next in down[id].OrderBy(n => place(n)))
                        if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return order;
        }

        /// <summary>The units the material streams leaving a unit enter (through any number of streams).</summary>
        static IEnumerable<string> Downstream(IGraphicObject unit)
        {
            foreach (var cp in unit.OutputConnectors)
            {
                if (cp == null || !cp.IsAttached || cp.AttachedConnector?.AttachedTo == null) continue;
                var stream = cp.AttachedConnector.AttachedTo;
                if (stream.ObjectType != ObjectType.MaterialStream)
                {
                    yield return stream.Name;
                    continue;
                }
                foreach (var out_ in stream.OutputConnectors)
                    if (out_ != null && out_.IsAttached && out_.AttachedConnector?.AttachedTo != null)
                        yield return out_.AttachedConnector.AttachedTo.Name;
            }
        }

        /// <summary>The objects with a body in the plant: unit operations, not streams, logical blocks or annotations.</summary>
        static bool IsUnit(ObjectType t)
        {
            switch (t)
            {
                case ObjectType.MaterialStream:
                case ObjectType.EnergyStream:
                case ObjectType.OT_Recycle:
                case ObjectType.OT_EnergyRecycle:
                case ObjectType.OT_Adjust:
                case ObjectType.OT_Spec:
                case ObjectType.Nenhum:
                    return false;
            }
            var s = t.ToString();
            return !s.StartsWith("GO_") && !s.StartsWith("Controller_") && !SceneBuilder.IsBadge(t)
                && !s.EndsWith("Gauge") && t != ObjectType.OT_InformationCarrier;
        }
    }
}
