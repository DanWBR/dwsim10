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

using System.Linq;
using Newtonsoft.Json.Linq;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The objects the DWSIM 3D window can add, by group: the built-in unit operations, which have
    /// a body in the plant model, and the logical blocks, which float as badges. Streams come from
    /// connecting nozzles; the unit operations of extensions join the catalog at run time (see
    /// <see cref="FlowsheetLink"/>); gauges and annotations stay in the 2D drawing.
    /// </summary>
    public static class Palette
    {
        public static readonly (string Group, string Name, ObjectType Type)[] Entries =
        {
            ("Pressure changers", "Pump", ObjectType.Pump),
            ("Pressure changers", "Compressor", ObjectType.Compressor),
            ("Pressure changers", "Expander (Turbine)", ObjectType.Expander),
            ("Pressure changers", "Valve", ObjectType.Valve),
            ("Pressure changers", "Orifice Plate", ObjectType.OrificePlate),
            ("Exchangers", "Heater", ObjectType.Heater),
            ("Exchangers", "Cooler", ObjectType.Cooler),
            ("Exchangers", "Heat Exchanger", ObjectType.HeatExchanger),
            ("Separators", "Gas-Liquid Separator", ObjectType.Vessel),
            ("Separators", "Tank", ObjectType.Tank),
            ("Separators", "Filter", ObjectType.Filter),
            ("Separators", "Compound Separator", ObjectType.ComponentSeparator),
            ("Mixers and splitters", "Stream Mixer", ObjectType.NodeIn),
            ("Mixers and splitters", "Stream Splitter", ObjectType.NodeOut),
            ("Columns", "Distillation Column", ObjectType.DistillationColumn),
            ("Columns", "Absorption Column", ObjectType.AbsorptionColumn),
            ("Columns", "Refluxed Absorber", ObjectType.RefluxedAbsorber),
            ("Columns", "Reboiled Absorber", ObjectType.ReboiledAbsorber),
            ("Columns", "Shortcut Column", ObjectType.ShortcutColumn),
            ("Reactors", "Conversion Reactor", ObjectType.RCT_Conversion),
            ("Reactors", "Equilibrium Reactor", ObjectType.RCT_Equilibrium),
            ("Reactors", "Gibbs Reactor", ObjectType.RCT_Gibbs),
            ("Reactors", "CSTR", ObjectType.RCT_CSTR),
            ("Reactors", "PFR", ObjectType.RCT_PFR),
            ("Lines", "Pipe Segment", ObjectType.Pipe),
            ("Logical blocks", "Adjust", ObjectType.OT_Adjust),
            ("Logical blocks", "Specification", ObjectType.OT_Spec),
            ("Logical blocks", "Recycle", ObjectType.OT_Recycle),
            ("Logical blocks", "Energy Recycle", ObjectType.OT_EnergyRecycle),
            ("Logical blocks", "PID Controller", ObjectType.Controller_PID),
        };

        /// <summary>The catalog as the window gets it in the welcome message, the buildings of the site last.</summary>
        public static JArray ToJson()
        {
            var list = new JArray(Entries.Select(e => new JObject
            {
                ["group"] = e.Group,
                ["name"] = e.Name,
                ["objectType"] = e.Type.ToString()
            }));
            foreach (var k in SiteLayout.Kinds)
                list.Add(new JObject { ["group"] = "Site", ["name"] = k.Name, ["objectType"] = SiteLayout.Prefix + k.Kind });
            return list;
        }
    }
}
