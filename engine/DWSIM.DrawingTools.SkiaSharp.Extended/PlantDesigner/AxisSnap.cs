using System;
using System.Collections.Generic;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>Which directions a new leg may take: any axis (picked by the mouse), the horizontal axes, the vertical,
    /// or one axis.</summary>
    public enum AxisLock { Auto, Horizontal, Vertical, X, Y, Z }

    /// <summary>The leg the mouse points at from a start point: axis, sign, length and end.</summary>
    public struct AxisChoice
    {
        public bool Valid;
        public Axis Axis;
        public int Sign;
        /// <summary>The length of the leg, m (snapped or typed).</summary>
        public double Length;
        /// <summary>The length the mouse points at before snapping, m.</summary>
        public double RawLength;
        public V3 End;
        /// <summary>True when the axis points at the viewer, so the mouse could not give the length.</summary>
        public bool AxisFacesViewer;
    }

    /// <summary>
    /// Picks the axis and the length of a leg from the mouse. The leg starts at a known plant point and runs along an
    /// axis, so the screen point fixes a single plant point even though the screen is flat: the axis whose screen line
    /// passes nearest the mouse wins, and the length is the distance along it. Lengths snap to whole cube edges of the
    /// grid unless the user typed one.
    /// </summary>
    public static class AxisSnap
    {
        public static IEnumerable<Axis> Allowed(AxisLock lk)
        {
            switch (lk)
            {
                case AxisLock.Horizontal: return new[] { Axis.X, Axis.Y };
                case AxisLock.Vertical: case AxisLock.Z: return new[] { Axis.Z };
                case AxisLock.X: return new[] { Axis.X };
                case AxisLock.Y: return new[] { Axis.Y };
                default: return new[] { Axis.X, Axis.Y, Axis.Z };
            }
        }

        /// <param name="cam">The camera.</param>
        /// <param name="from">The start of the leg.</param>
        /// <param name="sx">Mouse X, px.</param>
        /// <param name="sy">Mouse Y, px.</param>
        /// <param name="lk">The axis lock.</param>
        /// <param name="grid">The grid (steps and snap).</param>
        /// <param name="typedLength">A length the user typed, m; NaN when none.</param>
        /// <param name="minMovePx">The mouse must be this far from the start before a leg shows.</param>
        public static AxisChoice Choose(OrbitCamera cam, V3 from, double sx, double sy, AxisLock lk, PlantGrid grid, double typedLength = double.NaN, double minMovePx = 4.0)
        {
            var res = new AxisChoice();
            var o = cam.Project(from);
            double mdx = sx - o.X, mdy = sy - o.Y, md = Math.Sqrt(mdx * mdx + mdy * mdy);
            if (md < minMovePx && double.IsNaN(typedLength)) return res;

            double bestD = double.MaxValue; Axis best = Axis.X; double bestT = double.NaN; bool found = false;
            var facing = new List<Axis>();
            foreach (var a in Allowed(lk))
            {
                double ax, ay; cam.ScreenVector(V3.Of(a), out ax, out ay);
                // an axis shorter than 15 % of the scale on the screen points too much at the viewer to follow the mouse
                if (Math.Sqrt(ax * ax + ay * ay) < 0.15 * cam.Scale) { facing.Add(a); continue; }
                double d; double t = cam.AlongLine(from, V3.Of(a), sx, sy, out d);
                // when two axes are equally near the mouse, a horizontal one wins
                if (d < bestD - 1e-9 || (Math.Abs(d - bestD) <= 1e-9 && a != Axis.Z)) { bestD = d; best = a; bestT = t; found = true; }
            }
            if (!found)
            {
                if (facing.Count == 0) return res;
                // the only allowed axis faces the viewer (Z in plan): the vertical mouse movement gives the length
                best = facing.Contains(Axis.Z) ? Axis.Z : facing[0];
                bestT = -mdy / cam.Scale;
                res.AxisFacesViewer = true;
            }
            res.Axis = best;
            res.Sign = bestT < 0 ? -1 : 1;
            res.RawLength = Math.Abs(bestT);
            if (!double.IsNaN(typedLength) && typedLength > 0) res.Length = typedLength;
            else if (grid != null && grid.Snap)
            {
                double step = grid.Step(best);
                res.Length = Math.Max(1, Math.Round(res.RawLength / step)) * step;
            }
            else res.Length = res.RawLength;
            if (res.Length <= 0) return res;
            res.End = from + V3.Of(best, res.Sign) * res.Length;
            res.Valid = true;
            return res;
        }

        /// <summary>The grid point of the working level under the mouse (snapped to whole cube edges when the grid
        /// snaps). False when the camera is too close to the horizon.</summary>
        public static bool LevelPoint(OrbitCamera cam, PlantGrid grid, double sx, double sy, out V3 p)
        {
            if (!cam.TryUnprojectToLevel(sx, sy, grid.LevelZ, out p)) return false;
            if (grid.Snap) p = new V3(Math.Round(p.X / grid.StepH) * grid.StepH, Math.Round(p.Y / grid.StepH) * grid.StepH, grid.LevelZ);
            return true;
        }
    }
}
