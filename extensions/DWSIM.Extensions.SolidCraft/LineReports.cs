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
using System.IO;
using System.Linq;
using System.Text;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The line list and the material take-off of the 3D model, as CSV files in the user's regional
    /// format (list separator and decimal mark), so a spreadsheet opens them with the columns split
    /// and the numbers as numbers.
    /// </summary>
    public static class LineReports
    {
        const double SteelDensity = 7850;     // kg/m3
        const double RackBeamMass = 42.5;     // kg/m, the 250 x 150 x 8 x 12 I-beam of the rack

        public static void WriteLineList(PlantModel model, string path, CultureInfo culture)
        {
            var csv = new Csv(culture);
            csv.Row("Line", "From", "To", "Size", "OD (mm)", "Wall (mm)", "Phase", "Temperature (C)", "Pressure (bar)",
                "Mass flow (kg/h)", "Density (kg/m3)", "Viscosity (cP)", "Velocity (m/s)", "Length (m)", "Bends", "Flanges",
                "Rise (m)", "Reynolds", "Friction factor", "Friction and fittings dP (bar)", "Static dP (bar)", "Total dP (bar)",
                "dP per 100 m (bar)", "On pipe rack", "Check");
            foreach (var run in model.Pipes)
            {
                var h = run.Hydraulics ?? new LineHydraulics.Result { Reynolds = double.NaN, FrictionFactor = double.NaN, Friction = double.NaN, Static = double.NaN, Total = double.NaN };
                csv.Row(run.Tag, run.From, run.To, run.Size, Num(run.OuterDiameter, 1), Num(run.Wall, 2), run.Phase,
                    Num(run.Temperature > 0 ? run.Temperature - 273.15 : double.NaN, 1), Num(run.Pressure > 0 ? run.Pressure / 1e5 : double.NaN, 3),
                    Num(run.MassFlow * 3600, 2), Num(run.Density, 2), Num(run.Viscosity * 1000, 4), Num(run.Velocity, 2),
                    Num(h.Length, 1), h.Bends, run.Flanges.Count, Num(h.Rise, 1), Num(h.Reynolds, 0), Num(h.FrictionFactor, 4),
                    Num(h.Friction, 4), Num(h.Static, 4), Num(h.Total, 4), Num(h.Length > 0 ? h.Friction / h.Length * 100 : double.NaN, 3),
                    run.OnRack ? "yes" : "no", h.Check);
            }
            csv.Save(path);
        }

        public static void WriteTakeOff(PlantModel model, string path, CultureInfo culture)
        {
            var csv = new Csv(culture);

            csv.Row("Piping");
            csv.Row("Size", "OD (mm)", "Wall (mm)", "Length (m)", "Mass (kg)", "Bends", "Flanges", "Lines");
            foreach (var g in model.Pipes.GroupBy(r => r.Size).OrderBy(g => g.First().OuterDiameter))
            {
                var od = g.First().OuterDiameter / 1000;
                var id = od - 2 * g.First().Wall / 1000;
                var length = g.Sum(r => r.Hydraulics?.Length ?? 0);
                var mass = SteelDensity * Math.PI / 4 * (od * od - id * id) * length;
                csv.Row(g.Key, Num(od * 1000, 1), Num(g.First().Wall, 2), Num(length, 1), Num(mass, 0),
                    g.Sum(r => r.Hydraulics?.Bends ?? 0), g.Sum(r => r.Flanges.Count), g.Count());
            }
            var total = model.Pipes.Sum(r => r.Hydraulics?.Length ?? 0);
            csv.Row("Total", "", "", Num(total, 1));

            if (model.Rack != null)
            {
                var rack = model.Rack;
                var bays = Math.Max(1, (int)Math.Ceiling((rack.X1 - rack.X0) / PipeRack.BayLength));
                var columns = 2 * (bays + 1) * (rack.Top - PipeRack.BeamHeight / 2) / 1000;
                var crossBeams = (bays + 1) * rack.Width / 1000;
                var sideBeams = 2 * (rack.X1 - rack.X0) / 1000;
                csv.Row();
                csv.Row("Pipe rack");
                csv.Row("Member", "Count", "Length (m)", "Mass (kg)");
                csv.Row("Columns (I-beam 250 x 150)", 2 * (bays + 1), Num(columns, 1), Num(columns * RackBeamMass, 0));
                csv.Row("Cross beams (I-beam 250 x 150)", bays + 1, Num(crossBeams, 1), Num(crossBeams * RackBeamMass, 0));
                csv.Row("Side beams (I-beam 250 x 150)", 2, Num(sideBeams, 1), Num(sideBeams * RackBeamMass, 0));
            }

            csv.Row();
            csv.Row("Equipment");
            csv.Row("Tag", "Type", "Data");
            foreach (var item in model.Equipment.Where(e => e.Visible))
                csv.Row(item.Tag, item.Description, string.Join("; ", item.Properties.Select(kv => kv.Key + " " + kv.Value)));

            csv.Save(path);
        }

        static object Num(double x, int decimals) => double.IsNaN(x) || double.IsInfinity(x) ? (object)"" : Math.Round(x, decimals);

        /// <summary>A CSV writer in a given culture: its list separator, its decimal mark, fields quoted when needed.</summary>
        class Csv
        {
            readonly CultureInfo culture;
            readonly string sep;
            readonly StringBuilder sb = new StringBuilder();

            public Csv(CultureInfo culture)
            {
                this.culture = culture;
                sep = culture.TextInfo.ListSeparator;
                if (string.IsNullOrEmpty(sep) || sep == culture.NumberFormat.NumberDecimalSeparator) sep = ";";
            }

            public void Row(params object[] fields)
            {
                sb.AppendLine(string.Join(sep, fields.Select(Field)));
            }

            string Field(object f)
            {
                var s = f is IFormattable fm ? fm.ToString(null, culture) : (f?.ToString() ?? "");
                return s.Contains(sep) || s.Contains("\"") || s.Contains("\n") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            }

            // with a byte order mark, so a spreadsheet reads the degree sign and accents right
            public void Save(string path) => File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
