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

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The solids of each kind of equipment, sized in mm. Heads and skirts are separate bodies:
    /// SolidCraft's kernel can fail to unite a sphere with the cylinder it is tangent to, and one
    /// failed step stops the whole script.
    /// </summary>
    public static class EquipmentTemplates
    {
        public const string Steel = "#b9c0c8";
        public const string Skirt = "#6b7075";
        public const string Concrete = "#a3a39d";
        public const string ColumnShell = "#c9ced4";
        public const string ExchangerShell = "#4f7cac";
        public const string ExchangerHead = "#2f5a85";
        public const string PumpCasing = "#2e7d5b";
        public const string Motor = "#3d4f63";
        public const string Skid = "#4a4f55";
        public const string MachineCasing = "#5a7894";
        public const string Valve = "#c0392b";
        public const string Reactor = "#d08a3c";
        public const string Tank = "#e3e5e8";
        public const string Furnace = "#8a4b3a";
        public const string Generic = "#8e9aa8";

        public const string CarbonSteelLook = "starter.metal.carbon-steel-polished";
        public const string StainlessLook = "starter.metal.stainless-soft-satin";

        /// <summary>
        /// The finish of a part: bare metal parts take a library metal look (stainless for reactors,
        /// carbon steel otherwise) and every part a physical material, so SolidCraft can weigh the
        /// plant; painted parts keep their colour.
        /// </summary>
        public static void ApplyFinish(Primitive p)
        {
            if (p.Color == Concrete) p.Material = null;
            else if (p.Color == PumpCasing) p.Material = "Cast Iron";
            if (!p.Metallic || p.Look != null) return;
            if (p.Color == Reactor)
            {
                p.Look = StainlessLook;
                p.Material = "Stainless Steel";
            }
            else
            {
                p.Look = CarbonSteelLook;
            }
        }

        public static EquipmentItem VerticalVessel(double d, double tangent, double skirt, string color)
        {
            var item = new EquipmentItem();
            // a painted carbon steel skirt; the shell is bare metal
            item.Parts.Add(Primitive.Cylinder("skirt", new Vec3(0, 0, 0), "Z", d, skirt, Skirt));
            item.Parts.Add(Primitive.Cylinder("shell", new Vec3(0, 0, skirt), "Z", d, tangent, color, true));
            item.Parts.Add(Primitive.Sphere("top head", new Vec3(0, 0, skirt + tangent), d, color, true));
            item.Envelope = new Envelope
            {
                Kind = EnvelopeKind.VerticalCylinder,
                HalfX = d / 2,
                HalfY = d / 2,
                ZBottom = skirt,
                ZTop = skirt + tangent + d / 2,
                HeadedTop = true
            };
            return item;
        }

        public static EquipmentItem HorizontalVessel(double d, double tangent, double elevation, string color)
        {
            var item = new EquipmentItem();
            var zc = elevation + d / 2;
            item.Parts.Add(Primitive.Cylinder("shell", new Vec3(-tangent / 2, 0, zc), "X", d, tangent, color, true));
            item.Parts.Add(Primitive.Sphere("head A", new Vec3(-tangent / 2, 0, zc), d, color, true));
            item.Parts.Add(Primitive.Sphere("head B", new Vec3(tangent / 2, 0, zc), d, color, true));
            AddSaddles(item, tangent, d, zc);
            item.Envelope = new Envelope
            {
                Kind = EnvelopeKind.HorizontalCylinder,
                HalfX = tangent / 2 + d / 2,
                HalfY = d / 2,
                ZBottom = zc - d / 2,
                ZTop = zc + d / 2
            };
            return item;
        }

        static void AddSaddles(EquipmentItem item, double length, double d, double zc)
        {
            var w = Math.Max(200, 0.12 * d);
            item.Parts.Add(Primitive.Box("saddle A", new Vec3(-0.3 * length, 0, zc / 2), w, 0.8 * d, zc, Skirt));
            item.Parts.Add(Primitive.Box("saddle B", new Vec3(0.3 * length, 0, zc / 2), w, 0.8 * d, zc, Skirt));
        }

        public static EquipmentItem StorageTank(double d, double h)
        {
            var item = new EquipmentItem();
            item.Parts.Add(Primitive.Cylinder("foundation", new Vec3(0, 0, 0), "Z", d * 1.06, 200, Concrete));
            item.Parts.Add(Primitive.Cylinder("shell", new Vec3(0, 0, 200), "Z", d, h, Tank));
            item.Parts.Add(Primitive.Cylinder("roof", new Vec3(0, 0, 200 + h), "Z", d * 1.01, Math.Max(50, d * 0.02), Skirt));
            item.Envelope = new Envelope
            {
                Kind = EnvelopeKind.VerticalCylinder,
                HalfX = d / 2,
                HalfY = d / 2,
                ZBottom = 200,
                ZTop = 200 + h
            };
            return item;
        }

        /// <summary>A shell-and-tube exchanger on saddles: channel head on the -X end.</summary>
        public static EquipmentItem ShellAndTube(double d, double length)
        {
            var item = new EquipmentItem();
            var zc = 900 + d / 2;
            var channel = Math.Max(300, 0.6 * d);
            item.Parts.Add(Primitive.Cylinder("shell", new Vec3(-length / 2, 0, zc), "X", d, length, ExchangerShell));
            item.Parts.Add(Primitive.Cylinder("channel", new Vec3(-length / 2 - channel, 0, zc), "X", d * 1.1, channel, ExchangerHead));
            item.Parts.Add(Primitive.Sphere("rear head", new Vec3(length / 2, 0, zc), d, ExchangerShell));
            AddSaddles(item, length, d, zc);
            item.Envelope = new Envelope
            {
                Kind = EnvelopeKind.HorizontalCylinder,
                HalfX = length / 2 + Math.Max(channel, d / 2),
                HalfY = d * 0.55,
                ZBottom = zc - d / 2,
                ZTop = zc + d / 2
            };
            return item;
        }

        /// <summary>A box fired heater with its stack, scaled with the duty.</summary>
        public static EquipmentItem FiredHeater(double s)
        {
            var item = new EquipmentItem();
            double w = 4000 * s, h = 8000 * s;
            item.Parts.Add(Primitive.Box("firebox", new Vec3(0, 0, 500 + h / 2), w, w, h, Furnace));
            item.Parts.Add(Primitive.Box("floor", new Vec3(0, 0, 250), w * 1.1, w * 1.1, 500, Skirt));
            item.Parts.Add(Primitive.Cylinder("stack", new Vec3(0, 0, 500 + h), "Z", 1200 * s, 10000 * s, Skirt));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = w / 2, HalfY = w / 2, ZBottom = 500, ZTop = 500 + h };
            return item;
        }

        /// <summary>An air cooler bay: tube bundle on legs with two fans on top.</summary>
        public static EquipmentItem AirCooler(double s)
        {
            var item = new EquipmentItem();
            double l = 9000 * s, w = 3000 * s, z = 4000;
            item.Parts.Add(Primitive.Box("bundle", new Vec3(0, 0, z + 300), l, w, 600, ExchangerShell));
            foreach (var x in new[] { -0.45, 0.45 })
                foreach (var y in new[] { -0.45, 0.45 })
                    item.Parts.Add(Primitive.Box("leg " + (x > 0 ? "B" : "A") + (y > 0 ? "2" : "1"), new Vec3(x * l, y * w, z / 2), 200, 200, z, Skirt));
            item.Parts.Add(Primitive.Cylinder("fan A", new Vec3(-l / 4, 0, z + 600), "Z", 0.8 * w, 400, Skid));
            item.Parts.Add(Primitive.Cylinder("fan B", new Vec3(l / 4, 0, z + 600), "Z", 0.8 * w, 400, Skid));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = l / 2, HalfY = w / 2, ZBottom = z, ZTop = z + 600 };
            return item;
        }

        /// <summary>A centrifugal pump on its skid: casing on the -X end, motor towards +X.</summary>
        public static EquipmentItem Pump(double s)
        {
            var item = new EquipmentItem();
            double skid = 250;
            item.Parts.Add(Primitive.Box("skid", new Vec3(0, 0, skid / 2), 1400 * s, 650 * s, skid, Skid));
            var zc = skid + 260 * s;
            item.Parts.Add(Primitive.Cylinder("casing", new Vec3(-380 * s, -150 * s, zc), "Y", 520 * s, 300 * s, PumpCasing));
            item.Parts.Add(Primitive.Cylinder("motor", new Vec3(-120 * s, 0, zc), "X", 400 * s, 780 * s, Motor));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = 700 * s, HalfY = 325 * s, ZBottom = skid, ZTop = skid + 520 * s };
            return item;
        }

        /// <summary>A compressor or expander train: casing on the -X side, driver or generator on +X.</summary>
        public static EquipmentItem Machine(double s, bool expander)
        {
            var item = new EquipmentItem();
            double skid = 400;
            item.Parts.Add(Primitive.Box("skid", new Vec3(0, 0, skid / 2), 5000 * s, 2200 * s, skid, Skid));
            var zc = skid + 800 * s;
            item.Parts.Add(Primitive.Cylinder("casing", new Vec3(-2400 * s, 0, zc), "X", 1300 * s, 2200 * s, MachineCasing));
            item.Parts.Add(Primitive.Box(expander ? "generator" : "driver", new Vec3(1300 * s, 0, skid + 750 * s), 1800 * s, 1400 * s, 1500 * s, Motor));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = 2500 * s, HalfY = 1100 * s, ZBottom = skid, ZTop = skid + 1600 * s };
            return item;
        }

        /// <summary>An inline item at pipe level: a valve body with its actuator, or an orifice plate.</summary>
        public static EquipmentItem InlineValve(bool orifice)
        {
            var item = new EquipmentItem();
            const double z = 1500;
            if (orifice)
            {
                item.Parts.Add(Primitive.Cylinder("plate", new Vec3(-50, 0, z), "X", 420, 100, Valve));
            }
            else
            {
                item.Parts.Add(Primitive.Sphere("body", new Vec3(0, 0, z), 360, Valve));
                item.Parts.Add(Primitive.Cylinder("actuator stem", new Vec3(0, 0, z + 150), "Z", 80, 300, Skid));
                item.Parts.Add(Primitive.Cylinder("actuator", new Vec3(0, 0, z + 450), "Z", 380, 220, Valve));
            }
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = 220, HalfY = 220, ZBottom = z - 200, ZTop = z + 200 };
            return item;
        }

        /// <summary>A mixer or splitter: a header piece at pipe level.</summary>
        public static EquipmentItem Header()
        {
            var item = new EquipmentItem();
            const double z = 1500;
            item.Parts.Add(Primitive.Sphere("header", new Vec3(0, 0, z), 420, Steel, true));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = 230, HalfY = 230, ZBottom = z - 210, ZTop = z + 210 };
            return item;
        }

        /// <summary>A pipe segment or a single-tube reactor: a straight run on two supports.</summary>
        public static EquipmentItem PipeSpool(double od, double length, string color)
        {
            var item = new EquipmentItem();
            var zc = Math.Max(1500, od / 2 + 800);
            item.Parts.Add(Primitive.Cylinder("pipe", new Vec3(-length / 2, 0, zc), "X", od, length, color, true));
            item.Parts.Add(Primitive.Box("support A", new Vec3(-0.35 * length, 0, (zc - od / 2) / 2), 200, Math.Max(300, od), zc - od / 2, Skirt));
            item.Parts.Add(Primitive.Box("support B", new Vec3(0.35 * length, 0, (zc - od / 2) / 2), 200, Math.Max(300, od), zc - od / 2, Skirt));
            item.Envelope = new Envelope
            {
                Kind = EnvelopeKind.HorizontalCylinder,
                HalfX = length / 2,
                HalfY = Math.Max(150, od / 2),
                ZBottom = zc - od / 2,
                ZTop = zc + od / 2
            };
            return item;
        }

        /// <summary>Any other unit operation: a box on a slab.</summary>
        public static EquipmentItem Block(double sx, double sy, double sz)
        {
            var item = new EquipmentItem();
            item.Parts.Add(Primitive.Box("slab", new Vec3(0, 0, 75), sx + 400, sy + 400, 150, Concrete));
            item.Parts.Add(Primitive.Box("body", new Vec3(0, 0, 150 + sz / 2), sx, sy, sz, Generic));
            item.Envelope = new Envelope { Kind = EnvelopeKind.Box, HalfX = sx / 2, HalfY = sy / 2, ZBottom = 150, ZTop = 150 + sz };
            return item;
        }

        /// <summary>A recycle block or another node with no solids, at pipe level.</summary>
        public static EquipmentItem Node()
        {
            var item = new EquipmentItem();
            item.Envelope = new Envelope { Kind = EnvelopeKind.Point, ZBottom = 1500, ZTop = 1500 };
            return item;
        }

        /// <summary>Adds a reflux drum on the -Y side and a reboiler on the +Y side of a column, with their lines.</summary>
        public static void AddColumnAuxiliaries(EquipmentItem column, double d, bool condenser, bool reboiler)
        {
            var env = column.Envelope;
            var footY = d / 2;

            if (condenser)
            {
                var dd = Math.Max(600, Math.Min(2500, 0.5 * d));
                var ld = 3 * dd;
                var y = -(d / 2 + 1500 + dd / 2);
                var zc = 3000 + dd / 2;
                var drum = HorizontalVessel(dd, ld, 3000, Steel);
                foreach (var p in drum.Parts)
                {
                    var q = p.Moved(new Vec3(0, y, 0));
                    q.Name = "reflux drum " + p.Name;
                    column.Parts.Add(q);
                }
                var od = PipeSizes.NearestByOuterDiameter(0.3 * d);
                column.InternalPipes.Add(new PipeRun
                {
                    Tag = "overhead",
                    Phase = "Vapor",
                    Size = od.Name,
                    OuterDiameter = od.OD,
                    Wall = od.Wall,
                    Color = PipeSizes.ColorFor("Vapor"),
                    Velocity = 10,
                    Path = new List<Vec3>
                    {
                        new Vec3(0, 0, env.ZTop),
                        new Vec3(0, 0, env.ZTop + 800),
                        new Vec3(0, y, env.ZTop + 800),
                        new Vec3(0, y, zc + dd / 2)
                    }
                });
                footY = Math.Max(footY, -y + dd / 2);
            }

            if (reboiler)
            {
                var dr = Math.Max(500, Math.Min(2000, 0.5 * d));
                var lr = Math.Max(3000, 4 * dr);
                var y = d / 2 + 1500 + dr * 0.55;
                var hx = ShellAndTube(dr, lr);
                foreach (var p in hx.Parts)
                {
                    var q = p.Moved(new Vec3(0, y, 0));
                    q.Name = "reboiler " + p.Name;
                    column.Parts.Add(q);
                }
                var zTop = hx.Envelope.ZTop;
                var zReturn = env.ZBottom + Math.Min(1000, 0.2 * (env.ZTop - env.ZBottom));
                var od = PipeSizes.NearestByOuterDiameter(0.25 * d);
                column.InternalPipes.Add(new PipeRun
                {
                    Tag = "reboiler return",
                    Phase = "Two-phase",
                    Size = od.Name,
                    OuterDiameter = od.OD,
                    Wall = od.Wall,
                    Color = PipeSizes.ColorFor("Two-phase"),
                    Velocity = 3,
                    Path = new List<Vec3>
                    {
                        new Vec3(0, y, zTop),
                        new Vec3(0, y, zReturn),
                        new Vec3(0, d / 2, zReturn)
                    }
                });
                footY = Math.Max(footY, y + dr * 0.55);
            }

            column.FootprintY = footY;
        }
    }

    /// <summary>
    /// Nominal pipe sizes, outside diameter and schedule 40 (STD above 24") wall in mm. The smallest is
    /// 2": process lines below it are rare and would not show at the scale of a plant.
    /// </summary>
    public static class PipeSizes
    {
        public struct Size
        {
            public string Name;
            public double OD, Wall;
            public double ID => OD - 2 * Wall;
        }

        public static readonly Size[] Table =
        {
            new Size { Name = "2\"", OD = 60.3, Wall = 3.91 },
            new Size { Name = "3\"", OD = 88.9, Wall = 5.49 },
            new Size { Name = "4\"", OD = 114.3, Wall = 6.02 },
            new Size { Name = "6\"", OD = 168.3, Wall = 7.11 },
            new Size { Name = "8\"", OD = 219.1, Wall = 8.18 },
            new Size { Name = "10\"", OD = 273.1, Wall = 9.27 },
            new Size { Name = "12\"", OD = 323.8, Wall = 10.31 },
            new Size { Name = "14\"", OD = 355.6, Wall = 11.13 },
            new Size { Name = "16\"", OD = 406.4, Wall = 12.7 },
            new Size { Name = "18\"", OD = 457.0, Wall = 14.27 },
            new Size { Name = "20\"", OD = 508.0, Wall = 15.09 },
            new Size { Name = "24\"", OD = 610.0, Wall = 17.48 },
            new Size { Name = "30\"", OD = 762.0, Wall = 12.7 },
            new Size { Name = "36\"", OD = 914.0, Wall = 12.7 },
            new Size { Name = "42\"", OD = 1067.0, Wall = 12.7 },
            new Size { Name = "48\"", OD = 1219.0, Wall = 12.7 }
        };

        /// <summary>The smallest size whose inside diameter (mm) is at least the one required.</summary>
        public static Size ForInsideDiameter(double id)
        {
            foreach (var s in Table)
                if (s.ID >= id) return s;
            return Table[Table.Length - 1];
        }

        public static Size NearestByOuterDiameter(double od)
        {
            var best = Table[0];
            foreach (var s in Table)
                if (Math.Abs(s.OD - od) < Math.Abs(best.OD - od)) best = s;
            return best;
        }

        public static string ColorFor(string phase)
        {
            switch (phase)
            {
                case "Vapor": return "#d9a441";
                case "Liquid": return "#2f6db5";
                default: return "#3a9a6a";
            }
        }
    }
}
