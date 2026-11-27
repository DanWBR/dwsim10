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
            P("office", "Office Building", Buildings, 18, 12, 10, 1400000),
            P("factory", "Factory Hall", Buildings, 24, 18, 10, 1800000),
            P("plantoffice", "Plant Office", Buildings, 14, 10, 6, 600000),
            P("storehouse", "Storage Shed", Buildings, 16, 12, 7, 250000),

            L("road", "Road", Roads, 7, 0, 450),
            L("path", "Footpath", Roads, 1.5, 0, 60),
            A("asphalt", "Asphalt", Roads, 55),
            A("concrete", "Concrete Paving", Roads, 80),
            A("gravel", "Gravel", Roads, 18),
            A("parking", "Parking Lot", Roads, 70),

            L("fence", "Chain-link Fence", Fences, 0.1, 2.4, 85),
            L("wall", "Concrete Wall", Fences, 0.25, 2.5, 260),
            P("gate", "Gate", Fences, 8, 0.3, 2.4, 6500),
            P("barrier", "Road Barrier", Fences, 2, 0.6, 0.8, 400),
            A("bund", "Tank Bund", Fences, 120),

            P("tree", "Tree", Nature, 4, 4, 7, 250),
            L("treerow", "Row of Trees", Nature, 4, 7, 50),
            L("hedge", "Hedge", Nature, 1, 1.2, 40),
            A("woods", "Woods", Nature, 3),
            A("grass", "Lawn", Nature, 6),
            P("rock", "Rock", Nature, 1.5, 1.5, 1, 150),
            P("pine", "Pine", Nature, 4, 4, 9, 300),
            P("palm", "Palm Tree", Nature, 4, 4, 8, 400),
            P("bush", "Bush", Nature, 1.5, 1.5, 1.2, 60),
            P("flowers", "Flowers", Nature, 1, 1, 0.4, 40),
            P("autumntree", "Autumn Tree", Nature, 4, 4, 7, 250),
            P("smalltree", "Small Tree", Nature, 2, 2, 3.5, 120),
            P("stone", "Stone", Nature, 0.6, 0.6, 0.3, 30),
            P("tallrock", "Tall Rock", Nature, 2, 2, 3.5, 600),

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
            P("container", "Shipping Container", Utilities, 6.1, 2.44, 2.6, 4000),
            P("dumpster", "Dumpster", Utilities, 2, 1.2, 1.3, 1200),
            P("parasol", "Parasol Table", Utilities, 2.5, 2.5, 2.5, 600),
            P("watertower", "Water Tower", Utilities, 6, 6, 18, 400000),
            P("solar", "Solar Array", Utilities, 10, 6, 2, 25000),
            P("windturbine", "Wind Turbine", Utilities, 8, 8, 30, 1200000),
            L("powerline", "Power Line", Utilities, 2, 12, 120),
        };

        public static Entry Find(string kind) => Entries.FirstOrDefault(e => e.Kind == kind);

        public static string NameOf(string kind) => Find(kind)?.Name ?? kind;

        /// <summary>
        /// The estimated cost of one object of the site, USD: the cost of a point object (times its
        /// scale squared, since a bigger one covers more ground), of a line per metre of its length
        /// or of an area per square metre inside it. An object of an unknown kind costs nothing.
        /// </summary>
        public static double CostOf(SiteLayout.Building b)
        {
            var e = Find(b?.Kind);
            if (e == null) return 0;
            switch (e.Shape)
            {
                case Shape.Line: return b.Points == null ? 0 : e.Cost * SiteLayout.Length(b.Points) / 1000.0;
                case Shape.Area: return b.Points == null || b.Points.Count < 3 ? 0 : e.Cost * SiteLayout.Area(b.Points) / 1e6;
                default:
                    var s = b.Scale > 0 ? b.Scale : 1;
                    return e.Cost * s * s;
            }
        }

        /// <summary>The line of the budget for the ground moved by the terrain brushes.</summary>
        public const string Earthworks = "Earthworks";

        /// <summary>The estimated cost of moving earth (cut and fill), USD of 2026 per m3.</summary>
        public const double EarthworksCostPerM3 = 12;

        /// <summary>The diesel the machines burn to move one m3 of earth, kg.</summary>
        public const double DieselPerM3 = 0.67;

        /// <summary>
        /// Rough cradle-to-gate emissions of the materials, kg CO2-eq: per m3 of ready-mixed concrete,
        /// per t of steel, per t of hot-mix asphalt, per kg of diesel burned (upstream included).
        /// The LCA has its own factors; these give the build bar an estimate.
        /// </summary>
        public const double Co2ePerM3Concrete = 300, Co2ePerTSteel = 1900, Co2ePerTAsphalt = 50, Co2ePerKgDiesel = 3.5;

        /// <summary>
        /// What the objects are made of, per unit, per metre of a line or per m2 of an area:
        /// m3 of concrete, t of steel, t of asphalt. Rough quantities for planning; kinds left out
        /// (plants, lawns, rocks) are made of nothing the LCA counts.
        /// </summary>
        static readonly Dictionary<string, double[]> MaterialsPer = new Dictionary<string, double[]>
        {
            ["controlroom"] = new[] { 50.0, 5, 0 }, ["admin"] = new[] { 220.0, 25, 0 }, ["workshop"] = new[] { 60.0, 18, 0 },
            ["warehouse"] = new[] { 120.0, 35, 0 }, ["substation"] = new[] { 70.0, 32, 0 }, ["canteen"] = new[] { 70.0, 7, 0 },
            ["guardhouse"] = new[] { 8.0, 0.5, 0 }, ["office"] = new[] { 300.0, 35, 0 }, ["factory"] = new[] { 160.0, 60, 0 },
            ["plantoffice"] = new[] { 110.0, 12, 0 }, ["storehouse"] = new[] { 50.0, 15, 0 },
            ["road"] = new[] { 0.0, 0, 1.7 }, ["path"] = new[] { 0.15, 0, 0 }, ["asphalt"] = new[] { 0.0, 0, 0.24 },
            ["concrete"] = new[] { 0.15, 0.005, 0 }, ["parking"] = new[] { 0.0, 0, 0.24 },
            ["fence"] = new[] { 0.02, 0.012, 0 }, ["wall"] = new[] { 0.7, 0.04, 0 }, ["gate"] = new[] { 0.5, 0.4, 0 },
            ["barrier"] = new[] { 0.7, 0.02, 0 }, ["bund"] = new[] { 0.2, 0.01, 0 },
            ["suv"] = new[] { 0.0, 1.5, 0 }, ["delivery"] = new[] { 0.0, 2, 0 }, ["truck"] = new[] { 0.0, 5, 0 },
            ["firetruck"] = new[] { 0.0, 10, 0 }, ["loader"] = new[] { 0.0, 15, 0 },
            ["hydrant"] = new[] { 0.1, 0.15, 0 }, ["extinguisher"] = new[] { 0.0, 0.01, 0 }, ["shower"] = new[] { 0.1, 0.05, 0 },
            ["assembly"] = new[] { 0.05, 0.02, 0 }, ["windsock"] = new[] { 0.3, 0.1, 0 },
            ["lamp"] = new[] { 0.5, 0.3, 0 }, ["flag"] = new[] { 0.3, 0.1, 0 }, ["lpgtank"] = new[] { 0.5, 0.6, 0 },
            ["utilitybox"] = new[] { 0.2, 0.15, 0 }, ["barrel"] = new[] { 0.0, 0.02, 0 }, ["container"] = new[] { 0.0, 2.2, 0 },
            ["dumpster"] = new[] { 0.0, 0.4, 0 }, ["watertower"] = new[] { 40.0, 15, 0 }, ["solar"] = new[] { 1.0, 1, 0 },
            ["windturbine"] = new[] { 150.0, 60, 0 }, ["powerline"] = new[] { 0.03, 0.02, 0 },
        };

        /// <summary>How much of an object there is: 1 for a point object (its scale squared), its length for a line (m), its area for an area (m2).</summary>
        static double Amount(SiteLayout.Building b, Entry e)
        {
            switch (e.Shape)
            {
                case Shape.Line: return b.Points == null ? 0 : SiteLayout.Length(b.Points) / 1000.0;
                case Shape.Area: return b.Points == null || b.Points.Count < 3 ? 0 : SiteLayout.Area(b.Points) / 1e6;
                default:
                    var s = b.Scale > 0 ? b.Scale : 1;
                    return s * s;
            }
        }

        /// <summary>The materials of a site and the earth its relief moved.</summary>
        public sealed class Materials
        {
            public double ConcreteM3, SteelT, AsphaltT, EarthworksM3;
            public double DieselKg => EarthworksM3 * DieselPerM3;
            public double Co2eKg => ConcreteM3 * Co2ePerM3Concrete + SteelT * Co2ePerTSteel + AsphaltT * Co2ePerTAsphalt + DieselKg * Co2ePerKgDiesel;
        }

        /// <summary>The volume of earth the relief moved, cut and fill, m3: the heights of the cells times their area.</summary>
        public static double EarthworksVolume(SiteLayout.Terrain t)
        {
            if (t == null || SiteLayout.Check(t) != null) return 0;
            var h = Convert.FromBase64String(t.Heights);
            var cell = t.Cell / 1000.0;
            double sum = 0;
            for (var i = 0; i < t.Nx * t.Ny; i++) sum += Math.Abs(BitConverter.ToInt16(h, i * 2) / 100.0);
            return sum * cell * cell;
        }

        public static Materials MaterialsOf(IEnumerable<SiteLayout.Building> items, SiteLayout.Terrain terrain = null)
        {
            var m = new Materials { EarthworksM3 = EarthworksVolume(terrain) };
            foreach (var b in items ?? Enumerable.Empty<SiteLayout.Building>())
            {
                var e = Find(b?.Kind);
                double[] q;
                if (e == null || !MaterialsPer.TryGetValue(e.Kind, out q)) continue;
                var a = Amount(b, e);
                m.ConcreteM3 += q[0] * a;
                m.SteelT += q[1] * a;
                m.AsphaltT += q[2] * a;
            }
            return m;
        }

        /// <summary>
        /// The budget of the site: {total, currency, co2e_kg, items: [{cat, count, cost}]} in the
        /// order of the build bar, the categories without objects left out, then the earthworks of
        /// the relief (count: m3) when there are any; costs in whole dollars.
        /// </summary>
        public static JObject Budget(IEnumerable<SiteLayout.Building> items, SiteLayout.Terrain terrain = null)
        {
            var list = (items ?? Enumerable.Empty<SiteLayout.Building>()).Where(b => Find(b?.Kind) != null).ToList();
            var cats = new JArray();
            foreach (var c in Categories)
            {
                var mine = list.Where(b => Find(b.Kind).Category == c).ToList();
                if (mine.Count == 0) continue;
                cats.Add(new JObject { ["cat"] = c, ["count"] = mine.Count, ["cost"] = Math.Round(mine.Sum(CostOf)) });
            }
            var materials = MaterialsOf(list, terrain);
            var total = list.Sum(CostOf);
            if (materials.EarthworksM3 >= 1)
            {
                var cost = materials.EarthworksM3 * EarthworksCostPerM3;
                cats.Add(new JObject { ["cat"] = Earthworks, ["count"] = Math.Round(materials.EarthworksM3), ["cost"] = Math.Round(cost) });
                total += cost;
            }
            return new JObject { ["total"] = Math.Round(total), ["currency"] = "USD", ["co2e_kg"] = Math.Round(materials.Co2eKg), ["items"] = cats };
        }

        /// <summary>
        /// The summary of the site that the TEA and the LCA read from the flowsheet (key
        /// Plant3DSiteSummary): the costs by category (USD of 2026), the earthworks and the
        /// materials. {v, currency, year, total, costs: {category: USD}, earthworks: {m3, cost},
        /// materials: {concrete_m3, steel_t, asphalt_t, diesel_kg}, co2e_kg}.
        /// </summary>
        public static JObject Summary(IEnumerable<SiteLayout.Building> items, SiteLayout.Terrain terrain)
        {
            var budget = Budget(items, terrain);
            var costs = new JObject();
            foreach (var c in budget["items"].OfType<JObject>()) costs[(string)c["cat"]] = c["cost"];
            var m = MaterialsOf(items, terrain);
            return new JObject
            {
                ["v"] = 1,
                ["currency"] = "USD",
                ["year"] = 2026,
                ["total"] = budget["total"],
                ["costs"] = costs,
                ["earthworks"] = new JObject { ["m3"] = Math.Round(m.EarthworksM3, 1), ["cost"] = Math.Round(m.EarthworksM3 * EarthworksCostPerM3) },
                ["materials"] = new JObject
                {
                    ["concrete_m3"] = Math.Round(m.ConcreteM3, 2),
                    ["steel_t"] = Math.Round(m.SteelT, 3),
                    ["asphalt_t"] = Math.Round(m.AsphaltT, 2),
                    ["diesel_kg"] = Math.Round(m.DieselKg, 1)
                },
                ["co2e_kg"] = Math.Round(m.Co2eKg)
            };
        }

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
