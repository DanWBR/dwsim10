using System;
using System.Globalization;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>The three plant axes: X east, Y north, Z up.</summary>
    public enum Axis { X = 0, Y = 1, Z = 2 }

    /// <summary>A point or a direction on the plant, m.</summary>
    public struct V3
    {
        public double X, Y, Z;

        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly V3 Zero = new V3(0, 0, 0);

        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator -(V3 a) { return new V3(-a.X, -a.Y, -a.Z); }
        public static V3 operator *(V3 a, double s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public static V3 operator *(double s, V3 a) { return new V3(a.X * s, a.Y * s, a.Z * s); }

        public static double Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static V3 Cross(V3 a, V3 b) { return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }

        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }

        public V3 Normalized()
        {
            double l = Length;
            return l > 1e-15 ? new V3(X / l, Y / l, Z / l) : Zero;
        }

        /// <summary>The unit vector of an axis, with a sign of +1 or -1.</summary>
        public static V3 Of(Axis a, int sign = 1)
        {
            double s = sign < 0 ? -1.0 : 1.0;
            switch (a)
            {
                case Axis.X: return new V3(s, 0, 0);
                case Axis.Y: return new V3(0, s, 0);
                default: return new V3(0, 0, s);
            }
        }

        public double this[Axis a] { get { return a == Axis.X ? X : a == Axis.Y ? Y : Z; } }

        /// <summary>The axis this direction runs along when it is parallel to one within the tolerance (radians);
        /// otherwise null.</summary>
        public Axis? AlongAxis(double tolRad = 1e-3)
        {
            double l = Length;
            if (l < 1e-12) return null;
            double c = Math.Cos(tolRad);
            if (Math.Abs(X) / l >= c) return Axis.X;
            if (Math.Abs(Y) / l >= c) return Axis.Y;
            if (Math.Abs(Z) / l >= c) return Axis.Z;
            return null;
        }

        public static double Distance(V3 a, V3 b) { return (a - b).Length; }

        public override string ToString()
        {
            return X.ToString("R", CultureInfo.InvariantCulture) + " " + Y.ToString("R", CultureInfo.InvariantCulture) + " " + Z.ToString("R", CultureInfo.InvariantCulture);
        }

        public static V3 Parse(string s)
        {
            var p = (s ?? "").Split(new[] { ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) return Zero;
            return new V3(double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture), double.Parse(p[2], CultureInfo.InvariantCulture));
        }
    }
}
