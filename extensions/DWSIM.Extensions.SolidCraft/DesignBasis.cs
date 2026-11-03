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

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Preliminary design conditions from the operating ones: design pressure 10 % (at least 1 bar)
    /// over the operating, design temperature 25 C over (cold service: 5 C under, with 50 C for
    /// upsets), the materials by temperature, the flange class from the ASME B16.5 ratings and the
    /// hydrostatic test at 1.5 times the design pressure (ASME B31.3).
    /// </summary>
    internal class DesignBasis
    {
        /// <summary>Operating temperature (C) and pressure (bar g); NaN when unknown.</summary>
        public double OperatingT = double.NaN, OperatingP = double.NaN;

        /// <summary>Design temperatures (C), design and test pressures (bar g).</summary>
        public double MaxT, MinT, Pressure, Test;
        public bool Cold;

        /// <summary>Pipe, fitting, flange, plate (vessel shell) and casting (pump and valve casing) materials, and the corrosion allowance (mm).</summary>
        public string Pipe, Fitting, Flange, Plate, Casting, Class;

        /// <summary>Heat exchanger tubes of the same material.</summary>
        public string Tubes;
        public double CorrosionAllowance;

        static readonly double[] Temps = { 38, 50, 100, 150, 200, 250, 300, 325, 350, 375, 400, 425, 450, 475, 500, 538 };
        static readonly (int Class, double[] Bar)[] Ratings =
        {
            (150, new[] { 19.6, 19.2, 17.7, 15.8, 13.8, 12.1, 10.2, 9.3, 8.4, 7.4, 6.5, 5.5, 4.6, 3.7, 2.8, 1.4 }),
            (300, new[] { 51.1, 50.1, 46.6, 45.1, 43.8, 41.9, 39.8, 38.7, 37.6, 36.4, 34.7, 28.8, 23.0, 17.4, 11.8, 5.9 }),
            (600, new[] { 102.1, 100.2, 93.2, 90.2, 87.6, 83.9, 79.6, 77.4, 75.1, 72.7, 69.4, 57.5, 46.0, 34.9, 23.5, 11.8 }),
            (900, new[] { 153.2, 150.4, 139.8, 135.2, 131.4, 125.8, 119.5, 116.1, 112.7, 109.1, 104.2, 86.3, 69.0, 52.3, 35.3, 17.7 }),
            (1500, new[] { 255.3, 250.6, 233.0, 225.4, 219.0, 209.7, 199.1, 193.6, 187.8, 181.8, 173.6, 143.8, 115.0, 87.2, 58.8, 29.5 }),
            (2500, new[] { 425.5, 417.7, 388.3, 375.6, 365.0, 349.5, 331.8, 322.6, 313.0, 303.1, 289.3, 239.7, 191.7, 145.3, 97.9, 49.2 }),
        };

        /// <summary>Austenitic stainless steel (group 2.3) up to 38 C, relative to carbon steel (group 1.1).</summary>
        const double StainlessFactor = 15.9 / 19.6;

        /// <summary>The design basis of a line, from its fluid.</summary>
        public static DesignBasis For(PipeRun run)
            => For(run.Temperature > 0 ? run.Temperature - 273.15 : double.NaN, run.Pressure > 0 ? run.Pressure / 1e5 - 1.01325 : double.NaN);

        /// <summary>
        /// The design basis for an operating temperature (C) and pressure (bar g), either NaN when
        /// unknown, and the lowest operating temperature when an item also runs below zero.
        /// </summary>
        public static DesignBasis For(double temperature, double pressure, double lowest = double.NaN)
        {
            var d = new DesignBasis { OperatingT = temperature, OperatingP = pressure };
            var t = double.IsNaN(temperature) ? 25 : temperature;
            var p = double.IsNaN(pressure) ? 0 : pressure;
            d.Cold = t < 0;
            d.MaxT = d.Cold ? 50 : Math.Max(50, t + 25);
            d.MinT = d.Cold ? t - 5 : Math.Min(0, t);
            if (!double.IsNaN(lowest) && lowest < 0) { d.MinT = Math.Min(d.MinT, lowest - 5); d.Cold = true; }
            d.Pressure = Math.Max(3.5, Math.Max(1.1 * p, p + 1));
            d.Test = 1.5 * d.Pressure;

            var stainless = false;
            if (d.MinT < -46) { d.Pipe = "SS A312 TP304L"; d.Fitting = "A403 WP304L"; d.Flange = "A182 F304L"; d.Plate = "SA-240 304L"; d.Casting = "A351 CF3"; d.Tubes = "SA-213 TP304L"; d.CorrosionAllowance = 0; stainless = true; }
            else if (d.MinT < -29) { d.Pipe = "LTCS A333 Gr.6"; d.Fitting = "A420 WPL6"; d.Flange = "A350 LF2"; d.Plate = "SA-516 Gr.70 (impact tested)"; d.Casting = "A352 LCB"; d.Tubes = "SA-334 Gr.6"; d.CorrosionAllowance = 3; }
            else if (d.MaxT > 425) { d.Pipe = "1.25Cr-0.5Mo A335 P11"; d.Fitting = "A234 WP11"; d.Flange = "A182 F11"; d.Plate = "SA-387 Gr.11 Cl.2"; d.Casting = "A217 WC6"; d.Tubes = "SA-213 T11"; d.CorrosionAllowance = 3; }
            else { d.Pipe = "CS A106 Gr.B"; d.Fitting = "A234 WPB"; d.Flange = "A105"; d.Plate = "SA-516 Gr.70"; d.Casting = "A216 WCB"; d.Tubes = "SA-179"; d.CorrosionAllowance = 3; }

            d.Class = "special";
            if (d.MaxT <= 538)
                foreach (var (cls, bar) in Ratings)
                {
                    var rating = Interpolate(bar, d.MaxT) * (stainless ? StainlessFactor : 1);
                    if (rating >= d.Pressure) { d.Class = "CL" + cls; break; }
                }
            return d;
        }

        static double Interpolate(double[] bar, double t)
        {
            if (t <= Temps[0]) return bar[0];
            for (var i = 1; i < Temps.Length; i++)
                if (t <= Temps[i]) return bar[i - 1] + (bar[i] - bar[i - 1]) * (t - Temps[i - 1]) / (Temps[i] - Temps[i - 1]);
            return bar[bar.Length - 1];
        }
    }
}
