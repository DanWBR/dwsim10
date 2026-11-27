using System;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>The ready-made views of the camera.</summary>
    public enum CameraView { IsoSE, IsoSW, IsoNW, IsoNE, Plan, Front, Back, Left, Right }

    /// <summary>A point projected on the screen: pixels, and the depth towards the viewer (m, larger is nearer).</summary>
    public struct ScreenPoint
    {
        public double X, Y, Depth;
        public ScreenPoint(double x, double y, double depth) { X = x; Y = y; Depth = depth; }
    }

    /// <summary>
    /// The "fake 3D" camera of the plant designer: a parallel projection that orbits around a target point. The azimuth
    /// is the direction the viewer stands in, measured from the south towards the east (0 = looking north from the
    /// south, 90 = looking west from the east); the elevation is the angle of the line of sight above the horizon
    /// (90 = plan). Lengths keep the same scale anywhere on the screen, so the user can compare them.
    /// </summary>
    public sealed class OrbitCamera
    {
        /// <summary>The elevation of the isometric views, deg (atan(1/sqrt 2)).</summary>
        public static readonly double IsoElevationDeg = Math.Atan(1.0 / Math.Sqrt(2.0)) * 180.0 / Math.PI;

        public double AzimuthDeg = 45.0;
        public double ElevationDeg = IsoElevationDeg;
        /// <summary>Pixels per metre.</summary>
        public double Scale = 10.0;
        /// <summary>The plant point at the centre of the screen.</summary>
        public V3 Target = V3.Zero;
        public double Width = 800, Height = 600;
        public double MinElevationDeg = 0.0, MaxElevationDeg = 90.0;
        /// <summary>Below this elevation, deg, the working level is too flat on the screen to place points on it.</summary>
        public double MinPlaneElevationDeg = 10.0;

        private double _fromAz, _fromEl, _toAz, _toEl, _animStart = -1, _animDuration;

        private static double Rad(double d) { return d * Math.PI / 180.0; }

        /// <summary>Unit vector from the target towards the viewer.</summary>
        public V3 Eye
        {
            get
            {
                double t = Rad(AzimuthDeg), p = Rad(ElevationDeg);
                return new V3(Math.Cos(p) * Math.Sin(t), -Math.Cos(p) * Math.Cos(t), Math.Sin(p));
            }
        }

        /// <summary>Unit vector of the screen's right direction on the plant (always horizontal).</summary>
        public V3 Right { get { double t = Rad(AzimuthDeg); return new V3(Math.Cos(t), Math.Sin(t), 0); } }

        /// <summary>Unit vector of the screen's up direction on the plant.</summary>
        public V3 Up { get { return V3.Cross(Eye, Right); } }

        public ScreenPoint Project(V3 p)
        {
            var d = p - Target;
            return new ScreenPoint(Width / 2 + Scale * V3.Dot(d, Right), Height / 2 - Scale * V3.Dot(d, Up), V3.Dot(d, Eye));
        }

        /// <summary>The screen vector, px, of one metre along a plant direction.</summary>
        public void ScreenVector(V3 dir, out double dx, out double dy)
        {
            dx = Scale * V3.Dot(dir, Right);
            dy = -Scale * V3.Dot(dir, Up);
        }

        /// <summary>The plant point under a screen point on the plane through the target facing the viewer.</summary>
        public V3 ViewPlanePoint(double sx, double sy)
        {
            return Target + Right * ((sx - Width / 2) / Scale) + Up * (-(sy - Height / 2) / Scale);
        }

        /// <summary>True when the horizontal planes show enough area on the screen to place points on them.</summary>
        public bool CanPlaceOnLevel { get { return ElevationDeg >= MinPlaneElevationDeg - 1e-9; } }

        /// <summary>The point of the horizontal plane at elevation z under a screen point. False when the camera is too
        /// close to the horizon for that.</summary>
        public bool TryUnprojectToLevel(double sx, double sy, double z, out V3 p)
        {
            p = V3.Zero;
            if (!CanPlaceOnLevel) return false;
            var e = Eye;
            var q = ViewPlanePoint(sx, sy);
            double t = (z - q.Z) / e.Z;
            p = q + e * t;
            return true;
        }

        /// <summary>The distance along a plant line (from origin, along the unit direction dir) whose projection is
        /// nearest the screen point, m; also the screen distance, px, from that projection to the point. NaN when the
        /// line points at the viewer.</summary>
        public double AlongLine(V3 origin, V3 dir, double sx, double sy, out double screenDistance)
        {
            var o = Project(origin);
            double ax, ay; ScreenVector(dir, out ax, out ay);
            double a2 = ax * ax + ay * ay;
            double mx = sx - o.X, my = sy - o.Y;
            if (a2 < 1e-6 * Scale * Scale) { screenDistance = Math.Sqrt(mx * mx + my * my); return double.NaN; }
            double t = (mx * ax + my * ay) / a2;
            double px = mx - t * ax, py = my - t * ay;
            screenDistance = Math.Sqrt(px * px + py * py);
            return t;
        }

        // ------------------------------------------------------------------ moving the camera

        /// <summary>Orbits by a mouse drag: horizontal pixels turn the azimuth, vertical pixels tilt the elevation.</summary>
        public void Orbit(double dxPx, double dyPx, double degPerPx = 0.4)
        {
            StopAnimation();
            AzimuthDeg = Wrap(AzimuthDeg - dxPx * degPerPx);
            ElevationDeg = Clamp(ElevationDeg + dyPx * degPerPx, MinElevationDeg, MaxElevationDeg);
        }

        /// <summary>Moves the view so that the plant follows the mouse by the given pixels.</summary>
        public void Pan(double dxPx, double dyPx)
        {
            Target = Target - Right * (dxPx / Scale) + Up * (dyPx / Scale);
        }

        /// <summary>Zooms by a factor keeping the plant point under the screen point where it is.</summary>
        public void ZoomAt(double sx, double sy, double factor, double minScale = 1e-3, double maxScale = 1e5)
        {
            double s1 = Clamp(Scale * factor, minScale, maxScale);
            var q = ViewPlanePoint(sx, sy);
            Target = q + (Target - q) * (Scale / s1);
            Scale = s1;
        }

        /// <summary>Centres a box on the screen and scales it to fit with a margin, px.</summary>
        public void Fit(V3 min, V3 max, double marginPx = 40)
        {
            Target = (min + max) * 0.5;
            double xmin = double.MaxValue, xmax = double.MinValue, ymin = double.MaxValue, ymax = double.MinValue;
            var save = Scale; Scale = 1.0;
            for (int i = 0; i < 8; i++)
            {
                var c = new V3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                var s = Project(c);
                xmin = Math.Min(xmin, s.X); xmax = Math.Max(xmax, s.X); ymin = Math.Min(ymin, s.Y); ymax = Math.Max(ymax, s.Y);
            }
            Scale = save;
            double w = Math.Max(xmax - xmin, 1e-6), h = Math.Max(ymax - ymin, 1e-6);
            double aw = Math.Max(Width - 2 * marginPx, 10), ah = Math.Max(Height - 2 * marginPx, 10);
            Scale = Clamp(Math.Min(aw / w, ah / h), 1e-3, 1e5);
        }

        public static void ViewAngles(CameraView v, out double az, out double el)
        {
            el = IsoElevationDeg;
            switch (v)
            {
                case CameraView.IsoSE: az = 45; break;
                case CameraView.IsoSW: az = -45; break;
                case CameraView.IsoNW: az = -135; break;
                case CameraView.IsoNE: az = 135; break;
                case CameraView.Plan: az = 0; el = 90; break;
                case CameraView.Front: az = 0; el = 0; break;
                case CameraView.Back: az = 180; el = 0; break;
                case CameraView.Left: az = -90; el = 0; break;
                default: az = 90; el = 0; break;
            }
        }

        /// <summary>Turns the camera to a ready-made view, at once (nowS &lt; 0) or animated from the time nowS, s.</summary>
        public void SetView(CameraView v, double nowS = -1, double durationS = 0.35)
        {
            double az, el; ViewAngles(v, out az, out el);
            AnimateTo(az, el, nowS, durationS);
        }

        /// <summary>Turns the camera by quarter turns (+1 clockwise seen from above).</summary>
        public void Turn(int quarters, double nowS = -1, double durationS = 0.35)
        {
            AnimateTo(_animStart >= 0 ? _toAz + 90.0 * quarters : AzimuthDeg + 90.0 * quarters, _animStart >= 0 ? _toEl : ElevationDeg, nowS, durationS);
        }

        public void AnimateTo(double az, double el, double nowS, double durationS)
        {
            el = Clamp(el, MinElevationDeg, MaxElevationDeg);
            if (nowS < 0 || durationS <= 0) { AzimuthDeg = Wrap(az); ElevationDeg = el; StopAnimation(); return; }
            _fromAz = AzimuthDeg; _fromEl = ElevationDeg;
            _toAz = _fromAz + Wrap(az - _fromAz); _toEl = el;
            _animStart = nowS; _animDuration = durationS;
        }

        public bool Animating { get { return _animStart >= 0; } }

        public void StopAnimation() { _animStart = -1; }

        /// <summary>Advances the animation to the time nowS, s. Returns true while it is still running.</summary>
        public bool Tick(double nowS)
        {
            if (_animStart < 0) return false;
            double t = Clamp((nowS - _animStart) / _animDuration, 0, 1);
            double k = t * t * (3 - 2 * t);
            AzimuthDeg = Wrap(_fromAz + (_toAz - _fromAz) * k);
            ElevationDeg = _fromEl + (_toEl - _fromEl) * k;
            if (t >= 1) { _animStart = -1; return false; }
            return true;
        }

        /// <summary>An angle brought to (-180, 180], deg.</summary>
        public static double Wrap(double deg)
        {
            deg %= 360.0;
            if (deg > 180.0) deg -= 360.0;
            if (deg <= -180.0) deg += 360.0;
            return deg;
        }

        private static double Clamp(double v, double a, double b) { return v < a ? a : v > b ? b : v; }
    }
}
