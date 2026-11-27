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

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// What the user can build round the plant in the 3D window, as in the build bar of SimCity
    /// 3000: buildings, roads and paving, fences, trees, vehicles, safety and utility items. None of
    /// them takes part in the simulation; they are kept with the flowsheet by <see cref="SiteLayout"/>.
    /// </summary>
    /// <remarks>
    /// A point object stands at one place and turns (a building, a tree, a hydrant); a line object
    /// follows the points the user drags (a road, a fence, a row of trees); an area object fills the
    /// outline the user drags (paving, gravel, woods, a parking lot). The costs are rough estimates
    /// in US dollars of 2026, for the budget panel only: per unit, per metre of a line or per square
    /// metre of an area.
    /// </remarks>
    public static class SiteCatalog
    {
        public enum Shape { Point, Line, Area }

        public sealed class Entry
        {
            public string Kind, Name, Category;
            public Shape Shape;
            /// <summary>Width, depth and height, m: the footprint and height of a point object, or the width and height of a line.</summary>
            public double W, D, H;
            /// <summary>The estimated cost, USD: each, per m or per m2 (see <see cref="Unit"/>).</summary>
            public double Cost;

            public string Unit => Shape == Shape.Point ? "each" : Shape == Shape.Line ? "m" : "m2";
        }

        public const string Buildings = "Buildings", Roads = "Roads and paving", Fences = "Fences and barriers",
            Nature = "Nature", Vehicles = "Vehicles", Safety = "Safety", Utilities = "Utilities";

        /// <summary>The categories in the order of the build bar.</summary>
        public static readonly string[] Categories = { Buildings, Roads, Fences, Nature, Vehicles, Safety, Utilities };

        static Entry P(string kind, string name, string cat, double w, double d, double h, double cost)
            => new Entry { Kind = kind, Name = name, Category = cat, Shape = Shape.Point, W = w, D = d, H = h, Cost = cost };

        static Entry L(string kind, string name, string cat, double w, double h, double cost)
            => new Entry { Kind = kind, Name = name, Category = cat, Shape = Shape.Line, W = w, H = h, Cost = cost };

        static Entry A(string kind, string name, string cat, double cost)
            => new Entry { Kind = kind, Name = name, Category = cat, Shape = Shape.Area, Cost = cost };

        public static readonly Entry[] Entries =
        {
            P("controlroom", "Control Room", Buildings, 12, 7, 3.8, 450000),
            P("admin", "Administration Building", Buildings, 20, 12, 8, 1200000),
            P("workshop", "Maintenance Workshop", Buildings, 16, 10, 6, 380000),
            P("warehouse", "Warehouse", Buildings, 24, 16, 8, 900000),
            P("substation", "Electrical Substation", Buildings, 12, 8, 4, 650000),
            P("canteen", "Canteen", Buildings, 14, 10, 4, 300000),
            P("guardhouse", "Guardhouse", Buildings, 4, 4, 3, 45000),

            L("road", "Road", Roads, 7, 0, 450),
            L("path", "Footpath", Roads, 1.5, 0, 60),
            A("asphalt", "Asphalt", Roads, 55),
            A("concrete", "Concrete Paving", Roads, 80),
            A("gravel", "Gravel", Roads, 18),
            A("parking", "Parking Lot", Roads, 70),

            L("fence", "Chain-link Fence", Fences, 0.1, 2.4, 85),
            L("wall", "Concrete Wall", Fences, 0.25, 2.5, 260),
            P("gate", "Gate", Fences, 6, 0.3, 2.4, 6500),
            P("barrier", "Road Barrier", Fences, 2, 0.6, 0.8, 400),
            A("bund", "Tank Bund", Fences, 120),

            P("tree", "Tree", Nature, 4, 4, 7, 250),
            L("treerow", "Row of Trees", Nature, 4, 7, 50),
            L("hedge", "Hedge", Nature, 1, 1.2, 40),
            A("woods", "Woods", Nature, 3),
            A("grass", "Lawn", Nature, 6),
            P("rock", "Rock", Nature, 1.5, 1.5, 1, 150),

            P("suv", "Car", Vehicles, 4.8, 2, 1.8, 45000),
            P("delivery", "Delivery Van", Vehicles, 5.5, 2.1, 2.4, 55000),
            P("truck", "Flatbed Truck", Vehicles, 8, 2.5, 3, 120000),
            P("firetruck", "Fire Truck", Vehicles, 9, 2.5, 3.2, 550000),
            P("loader", "Front Loader", Vehicles, 6, 2.6, 3.2, 280000),

            P("hydrant", "Fire Hydrant", Safety, 0.5, 0.5, 0.9, 3500),
            P("extinguisher", "Fire Extinguisher", Safety, 0.4, 0.4, 0.7, 250),
            P("shower", "Safety Shower", Safety, 1.2, 1.2, 2.4, 4000),
            P("assembly", "Assembly Point", Safety, 1, 0.2, 2.5, 800),
            P("windsock", "Windsock", Safety, 1, 1, 8, 2500),
            P("cone", "Traffic Cone", Safety, 0.4, 0.4, 0.7, 30),

            P("lamp", "Lamp Post", Utilities, 0.4, 0.4, 9, 4500),
            P("flag", "Flagpole", Utilities, 0.3, 0.3, 10, 3000),
            P("lpgtank", "LPG Tank", Utilities, 3, 1.2, 1.4, 9000),
            P("utilitybox", "Electrical Cabinet", Utilities, 1, 0.5, 1.5, 2500),
            P("barrel", "Drum", Utilities, 0.6, 0.6, 0.9, 80),
            P("crate", "Crate", Utilities, 1.2, 1.2, 1.2, 120),
        };

        public static Entry Find(string kind) => Entries.FirstOrDefault(e => e.Kind == kind);

        public static string NameOf(string kind) => Find(kind)?.Name ?? kind;

        /// <summary>The catalog as the window gets it in the welcome message.</summary>
        public static JArray ToJson() => new JArray(Entries.Select(e => new JObject
        {
            ["kind"] = e.Kind,
            ["name"] = e.Name,
            ["cat"] = e.Category,
            ["shape"] = e.Shape.ToString().ToLowerInvariant(),
            ["w"] = e.W,
            ["d"] = e.D,
            ["h"] = e.H,
            ["cost"] = e.Cost,
            ["unit"] = e.Unit
        }));
    }
}
