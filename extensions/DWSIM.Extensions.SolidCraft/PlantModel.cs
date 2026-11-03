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
    // The 3D plant model: equipment made of simple solids and pipe runs between their nozzles.
    // Every length is in mm, Z points up and the ground is Z = 0.

    public struct Vec3
    {
        public double X, Y, Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double f) => new Vec3(a.X * f, a.Y * f, a.Z * f);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized() { var l = Length; return l > 0 ? this * (1 / l) : this; }

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// Whole millimetres: SolidCraft fails to sweep a straight pipe tens of metres from the origin
        /// when its ends carry fractions of a millimetre (42958.4 fails where 42958 works).
        /// </summary>
        public double[] ToArray() => new[] { Math.Round(X), Math.Round(Y), Math.Round(Z) };

        public static readonly Vec3 PlusX = new Vec3(1, 0, 0), MinusX = new Vec3(-1, 0, 0);

        /// <summary>The vector turned about Z by a whole number of quarter turns (degrees, counterclockwise from above).</summary>
        public Vec3 TurnedZ(int degrees)
        {
            switch (((degrees / 90) % 4 + 4) % 4)
            {
                case 1: return new Vec3(-Y, X, Z);
                case 2: return new Vec3(-X, -Y, Z);
                case 3: return new Vec3(Y, -X, Z);
                default: return this;
            }
        }
        public static readonly Vec3 PlusZ = new Vec3(0, 0, 1), MinusZ = new Vec3(0, 0, -1);
    }

    public enum PrimitiveKind { Cylinder, Box, Sphere, Torus, Head }

    /// <summary>Where an item stands in the 3D model: its origin on the ground (mm) and its turn about Z (degrees).</summary>
    public class Placement
    {
        public double X, Y, Z;
        public int Rot;
    }

    /// <summary>One solid of an equipment item, in the item's local coordinates.</summary>
    public class Primitive
    {
        public PrimitiveKind Kind;
        public string Name;
        public string Color;
        public bool Metallic;

        /// <summary>A SolidCraft library look (starter.metal.*) used instead of the colour.</summary>
        public string Look;

        /// <summary>The physical material (mass, bill of materials); null leaves the default.</summary>
        public string Material = "Steel";

        /// <summary>Cylinder: centre of the base. Box and sphere: centre.</summary>
        public Vec3 Point;

        /// <summary>Cylinder axis: X, Y or Z.</summary>
        public string Axis = "Z";

        /// <summary>The cylinder axis as a vector, once the part has been turned; null to use <see cref="Axis"/>.</summary>
        public Vec3? AxisVector;

        /// <summary>Head: depth along its axis (<see cref="AxisVector"/>, outward from the shell), mm; the diameter is the shell's.</summary>
        public double Depth;

        /// <summary>Torus radii (major to the tube centre, minor of the tube), about a vertical axis.</summary>
        public double Major, Minor;

        /// <summary>Platforms, ladders and rails: lines pass through their openings, so the clash check leaves them out.</summary>
        public bool NoClash;

        /// <summary>Cylinder and sphere diameter.</summary>
        public double Diameter;

        /// <summary>Cylinder height along its axis.</summary>
        public double Height;

        /// <summary>Box sizes along X, Y and Z.</summary>
        public double SizeX, SizeY, SizeZ;

        public static Primitive Cylinder(string name, Vec3 baseCentre, string axis, double diameter, double height, string color, bool metallic = false)
            => new Primitive { Kind = PrimitiveKind.Cylinder, Name = name, Point = baseCentre, Axis = axis, Diameter = diameter, Height = height, Color = color, Metallic = metallic };

        public static Primitive Box(string name, Vec3 centre, double sx, double sy, double sz, string color, bool metallic = false)
            => new Primitive { Kind = PrimitiveKind.Box, Name = name, Point = centre, SizeX = sx, SizeY = sy, SizeZ = sz, Color = color, Metallic = metallic };

        public static Primitive Torus(string name, Vec3 centre, double major, double minor, string color)
            => new Primitive { Kind = PrimitiveKind.Torus, Name = name, Point = centre, Major = major, Minor = minor, Color = color, NoClash = true };

        public static Primitive Sphere(string name, Vec3 centre, double diameter, string color, bool metallic = false)
            => new Primitive { Kind = PrimitiveKind.Sphere, Name = name, Point = centre, Diameter = diameter, Color = color, Metallic = metallic };

        public Primitive Moved(Vec3 offset)
        {
            var p = (Primitive)MemberwiseClone();
            p.Point = Point + offset;
            return p;
        }
    }

    public enum EnvelopeKind { VerticalCylinder, HorizontalCylinder, Box, Point }

    /// <summary>
    /// The outer shape the nozzles sit on. A connector of the 2D icon is carried to the face of the
    /// envelope on the same side of the icon, at the same relative height.
    /// </summary>
    public class Envelope
    {
        public EnvelopeKind Kind;

        /// <summary>Half the size along X (a horizontal cylinder: tangent half-length plus the head).</summary>
        public double HalfX;

        /// <summary>Half the size along Y.</summary>
        public double HalfY;

        /// <summary>Bottom and top of the part the nozzles may use (above a skirt or legs).</summary>
        public double ZBottom, ZTop;

        /// <summary>Vertical cylinder: the top is a hemispherical head of radius HalfX.</summary>
        public bool HeadedTop;

        /// <summary>Depth of the top head of a vertical cylinder, or of the end heads of a horizontal one, mm.</summary>
        public double HeadDepth;

        public double ZCentre => (ZBottom + ZTop) / 2;

        /// <summary>
        /// Nozzle position (local) and outward direction for a connector at (u, v) of the icon, both
        /// 0 to 1 from the top left corner.
        /// </summary>
        public void Locate(double u, double v, out Vec3 position, out Vec3 direction)
        {
            u = Clamp(u, 0, 1);
            v = Clamp(v, 0, 1);

            if (Kind == EnvelopeKind.Point)
            {
                position = new Vec3(0, 0, ZCentre);
                direction = u < 0.5 ? Vec3.MinusX : Vec3.PlusX;
                return;
            }

            // which edge of the icon the connector is nearest to
            var dLeft = u; var dRight = 1 - u; var dTop = v; var dBottom = 1 - v;
            var min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));

            var h = ZTop - ZBottom;

            if (min == dTop && dTop < 0.2)
            {
                switch (Kind)
                {
                    case EnvelopeKind.VerticalCylinder:
                        var x = (u - 0.5) * HalfX;
                        var z = HeadedTop ? ZTop - HeadDepth + HeadDepth * Math.Sqrt(Math.Max(0, 1 - x * x / (HalfX * HalfX))) : ZTop;
                        position = new Vec3(x, 0, z);
                        break;
                    case EnvelopeKind.HorizontalCylinder:
                        position = new Vec3((u - 0.5) * 1.6 * Math.Max(HalfX - HalfY, HalfY), 0, ZTop);
                        break;
                    default:
                        position = new Vec3((u - 0.5) * 1.6 * HalfX, 0, ZTop);
                        break;
                }
                direction = Vec3.PlusZ;
                return;
            }

            if (min == dBottom && dBottom < 0.2 && ZBottom > 500)
            {
                // an elevated item takes bottom nozzles from below; a vertical vessel's bottom sits
                // inside its skirt, so its bottom nozzles go out of the side just above the skirt
                if (Kind == EnvelopeKind.HorizontalCylinder || Kind == EnvelopeKind.Box)
                {
                    var hx = Kind == EnvelopeKind.HorizontalCylinder ? Math.Max(HalfX - HalfY, HalfY) : HalfX;
                    position = new Vec3((u - 0.5) * 1.6 * hx, 0, ZBottom);
                    direction = Vec3.MinusZ;
                    return;
                }
            }

            var left = u < 0.5;
            double zs;
            if (min == dBottom) zs = ZBottom + Math.Min(300, 0.1 * h);
            else if (min == dTop) zs = ZTop - Math.Min(300, 0.1 * h);
            else zs = ZTop - v * h;

            switch (Kind)
            {
                case EnvelopeKind.HorizontalCylinder:
                    // a connector high or low on the side of the icon becomes a nozzle on the top or
                    // the bottom of the shell near that end (two side connectors of an exchanger icon
                    // would otherwise share the head); one at mid height goes into the head
                    var shellEnd = 0.8 * Math.Max(HalfX - HalfY, HalfY);
                    if (v < 0.35)
                    {
                        position = new Vec3(left ? -shellEnd : shellEnd, 0, ZTop);
                        direction = Vec3.PlusZ;
                        return;
                    }
                    if (v > 0.65)
                    {
                        position = new Vec3(left ? -shellEnd : shellEnd, 0, ZBottom);
                        direction = Vec3.MinusZ;
                        return;
                    }
                    position = new Vec3(left ? -HalfX : HalfX, 0, ZCentre);
                    break;
                case EnvelopeKind.VerticalCylinder:
                    var top = HeadedTop ? ZTop - HeadDepth : ZTop;
                    zs = Clamp(zs, ZBottom + Math.Min(300, 0.1 * h), top - Math.Min(300, 0.1 * h));
                    position = new Vec3(left ? -HalfX : HalfX, 0, zs);
                    break;
                default:
                    zs = Clamp(zs, ZBottom + Math.Min(150, 0.1 * h), ZTop - Math.Min(150, 0.1 * h));
                    position = new Vec3(left ? -HalfX : HalfX, 0, zs);
                    break;
            }
            direction = left ? Vec3.MinusX : Vec3.PlusX;
        }

        internal static double Clamp(double x, double a, double b) => x < a ? a : (x > b ? b : x);
    }

    /// <summary>One flowsheet unit operation as a group of solids.</summary>
    public class EquipmentItem
    {
        public string Name;
        public string Tag;
        public string Description;

        /// <summary>Centre of the item on the ground, in world coordinates.</summary>
        public Vec3 Origin;

        /// <summary>Centre of the 2D icon on the flowsheet canvas.</summary>
        public double CanvasX, CanvasY;

        public Envelope Envelope;
        public List<Primitive> Parts = new List<Primitive>();
        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        /// <summary>The 3D label over the item: tag and operating conditions, one line each.</summary>
        public string Label;

        /// <summary>Lines that belong to the item (a column's overhead line), in local coordinates.</summary>
        public List<PipeRun> InternalPipes = new List<PipeRun>();

        /// <summary>A point node (a recycle block) has no solids: lines only pass through it.</summary>
        public bool Visible => Parts.Count > 0;

        /// <summary>Half the footprint along X and Y, for the layout. Defaults to the envelope.</summary>
        public double FootprintX, FootprintY;

        /// <summary>Turn about Z, in quarter turns (0, 90, 180, 270), and whether the place comes from a saved layout.</summary>
        public int Rotation;
        public bool Placed;

        /// <summary>
        /// Turns the item about its origin by quarter turns: parts, the column's own lines and the
        /// footprint. The envelope stays in the item's own frame; nozzles are turned as they are placed.
        /// </summary>
        public void Turn(int degrees)
        {
            Rotation = ((degrees % 360) + 360) % 360;
            if (Rotation == 0) return;
            var odd = (Rotation / 90) % 2 == 1;
            foreach (var p in Parts)
            {
                p.Point = p.Point.TurnedZ(Rotation);
                if (p.AxisVector.HasValue)
                {
                    p.AxisVector = p.AxisVector.Value.TurnedZ(Rotation);
                }
                else if (p.Kind == PrimitiveKind.Cylinder)
                {
                    var axis = p.Axis == "X" ? Vec3.PlusX : p.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ;
                    p.AxisVector = axis.TurnedZ(Rotation);
                }
                if (p.Kind == PrimitiveKind.Box && odd)
                {
                    var sx = p.SizeX;
                    p.SizeX = p.SizeY;
                    p.SizeY = sx;
                }
            }
            foreach (var run in InternalPipes)
                for (var i = 0; i < run.Path.Count; i++) run.Path[i] = run.Path[i].TurnedZ(Rotation);
            if (odd)
            {
                var fx = FootprintX;
                FootprintX = FootprintY;
                FootprintY = fx;
            }
        }
    }

    /// <summary>A pipe between two nozzles, or from a nozzle to the battery limit.</summary>
    public class PipeRun
    {
        public string Tag;
        public string Phase;
        public string Size;
        public double OuterDiameter;
        public double Wall;
        public string Color;
        public List<Vec3> Path = new List<Vec3>();

        /// <summary>Mean velocity in the line, m/s (0 when the stream has no flow).</summary>
        public double Velocity;

        /// <summary>Where the line comes from and goes to: an item's tag, or "battery limit".</summary>
        public string From = "battery limit", To = "battery limit";

        /// <summary>The fluid: K, Pa, kg/s, kg/m3 (homogeneous) and Pa.s.</summary>
        public double Temperature, Pressure, MassFlow, Density, Viscosity;

        /// <summary>Insulation thickness, mm (0 for none), and what it is for: "hot" or "cold".</summary>
        public double Insulation;
        public string InsulationService = "";

        /// <summary>The hydraulics along the route (see <see cref="LineHydraulics"/>); NaN until worked out.</summary>
        public LineHydraulics.Result Hydraulics;

        /// <summary>Operating conditions of the stream, saved as properties of the line.</summary>
        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        /// <summary>The 3D label along the line; null for none.</summary>
        public string Label;

        /// <summary>Flanges along the line: the face position and the direction the flange faces.</summary>
        public List<Tuple<Vec3, Vec3>> Flanges = new List<Tuple<Vec3, Vec3>>();

        /// <summary>
        /// The line runs on the pipe rack: its path is laid out once every rack line is known, from
        /// the nozzles (A, B) and the points just off them (A1, B1).
        /// </summary>
        internal bool OnRack;
        internal Vec3 A, A1, B, B1;

        /// <summary>
        /// For a nozzle on the top or the bottom of an item: the Y, clear of the item on the rack
        /// side, where the line turns to go up or down; NaN for a side nozzle.
        /// </summary>
        internal double AClearY = double.NaN, BClearY = double.NaN;
    }

    /// <summary>A single-tier pipe rack along X: the lines run on top of its beams, one lane each.</summary>
    public class PipeRack
    {
        /// <summary>Ends along X, the -Y side, the width along Y and the top of the steel, mm.</summary>
        public double X0, X1, Y0, Width, Top;

        public const double BayLength = 6000;
        public const double BeamHeight = 250;
    }

    public class PlantModel
    {
        public string Name;
        public List<EquipmentItem> Equipment = new List<EquipmentItem>();
        public List<PipeRun> Pipes = new List<PipeRun>();
        public List<string> Warnings = new List<string>();

        /// <summary>The pipe rack, when a line runs on it.</summary>
        public PipeRack Rack;

        /// <summary>mm of plot plan per unit of canvas, and the canvas point placed at the world origin.</summary>
        public double Scale, CanvasOriginX, CanvasOriginY;
    }
}
