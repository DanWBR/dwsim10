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
using System.Globalization;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// A screening of the pressure drop along each routed line, which the simulation leaves out (a
    /// stream has one pressure). Darcy-Weisbach with the Churchill (1977) friction factor on commercial
    /// steel, the bends as equivalent lengths, one entrance and one exit per item end, and the static
    /// head from the height change; a two-phase line is taken as homogeneous. Meant to point at the
    /// lines that need a proper hydraulic calculation, not to replace one.
    /// </summary>
    public static class LineHydraulics
    {
        public class Result
        {
            /// <summary>Route length along the centre line, m, the bends and the height change (end minus start), m.</summary>
            public double Length, Rise;
            public int Bends;

            public double Reynolds, FrictionFactor;

            /// <summary>Pressure drops, bar: friction (with bends and ends), static head and their sum.</summary>
            public double Friction, Static, Total;

            /// <summary>What to look at, empty when nothing stands out.</summary>
            public string Check = "";
        }

        const double Roughness = 4.6e-5;      // m, commercial steel
        const double BendLD = 20;             // equivalent length of a long-radius 90 degree bend, in diameters
        const double EndK = 0.5 + 1.0;        // one entrance and one exit
        const double G = 9.80665;

        public static Result Compute(PipeRun run)
        {
            var r = new Result();
            var path = run.Path;
            for (var i = 1; i < path.Count; i++) r.Length += (path[i] - path[i - 1]).Length / 1000;
            r.Bends = Math.Max(0, path.Count - 2);
            r.Rise = path.Count > 1 ? (path[path.Count - 1].Z - path[0].Z) / 1000 : 0;

            var d = (run.OuterDiameter - 2 * run.Wall) / 1000;
            if (!(run.Density > 0) || !(run.Viscosity > 0) || !(run.Velocity > 0) || !(d > 0))
            {
                r.Reynolds = r.FrictionFactor = r.Friction = r.Static = r.Total = double.NaN;
                return r;
            }

            var v = run.Velocity;
            r.Reynolds = run.Density * v * d / run.Viscosity;
            r.FrictionFactor = Churchill(r.Reynolds, Roughness / d);

            var ends = (run.From != "battery limit" ? 1 : 0) + (run.To != "battery limit" ? 1 : 0);
            var k = r.FrictionFactor * (r.Length / d + r.Bends * BendLD) + EndK * ends / 2;
            var dynamic = run.Density * v * v / 2;
            r.Friction = k * dynamic / 1e5;
            r.Static = run.Density * G * r.Rise / 1e5;
            r.Total = r.Friction + r.Static;

            var notes = new List<string>();
            if (run.Pressure > 0 && r.Total > 0.1 * run.Pressure / 1e5)
                notes.Add("pressure drop " + (100 * r.Total / (run.Pressure / 1e5)).ToString("0", CultureInfo.InvariantCulture) + " % of the line pressure");
            if (run.Phase == "Liquid" && v > 3) notes.Add("liquid velocity above 3 m/s");
            if (run.Phase == "Vapor" && v > 30) notes.Add("gas velocity above 30 m/s");
            if (run.Phase == "Two-phase")
            {
                // erosional velocity, API RP 14E with C = 100 (US units): ve = C / sqrt(rho in lb/ft3) ft/s
                var ve = 100 / Math.Sqrt(run.Density * 0.062428) * 0.3048;
                if (v > ve) notes.Add("above the erosional velocity (" + ve.ToString("0.#", CultureInfo.InvariantCulture) + " m/s)");
            }
            r.Check = string.Join("; ", notes);
            return r;
        }

        /// <summary>Darcy friction factor, Churchill (1977): laminar, transition and turbulent in one expression.</summary>
        public static double Churchill(double re, double relativeRoughness)
        {
            var a = Math.Pow(2.457 * Math.Log(1 / (Math.Pow(7 / re, 0.9) + 0.27 * relativeRoughness)), 16);
            var b = Math.Pow(37530 / re, 16);
            return 8 * Math.Pow(Math.Pow(8 / re, 12) + 1 / Math.Pow(a + b, 1.5), 1.0 / 12);
        }
    }
}
