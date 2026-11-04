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
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The drawing conventions a sheet follows: its sizes, borders and title block.
    /// <list type="bullet">
    /// <item>ISO: A-series sheets (ISO 5457), a 180 mm title block with the fields of ISO 7200.</item>
    /// <item>ASME: ANSI sheets in inches (ASME Y14.1), approvals, CAGE code and a revision block.</item>
    /// <item>Petrobras: A-series sheets with the legend fields of N-381 and the document code of N-1710.</item>
    /// </list>
    /// </summary>
    public enum DrawingStyle { ISO, ASME, Petrobras }

    /// <summary>One sheet size, in millimetres, landscape.</summary>
    public class SheetSize
    {
        public string Name;
        public double Width, Height;
        public SheetSize(string name, double w, double h) { Name = name; Width = w; Height = h; }

        public static SheetSize[] For(DrawingStyle style) => style == DrawingStyle.ASME
            ? new[] { new SheetSize("B", 431.8, 279.4), new SheetSize("C", 558.8, 431.8), new SheetSize("D", 863.6, 558.8), new SheetSize("E", 1117.6, 863.6) }
            : new[] { new SheetSize("A3", 420, 297), new SheetSize("A2", 594, 420), new SheetSize("A1", 841, 594), new SheetSize("A0", 1189, 841) };
    }

    /// <summary>What goes in the title block; empty fields are left blank to fill in by hand.</summary>
    public class TitleData
    {
        public string Owner = "", Project = "", Area = "", Title = "", Subtitle = "", Number = "", Revision = "0",
            DocumentType = "", Status = "", Scale = "NOT TO SCALE", Date = "", Sheet = "1/1",
            PreparedBy = "", CheckedBy = "", ApprovedBy = "", Department = "", Language = "en";
    }

    /// <summary>
    /// A minimal DXF writer (an ENTITIES section only, which CADCraft reads as a block definition
    /// when inserted from a file): enough for symbols and title blocks.
    /// </summary>
    public class Dxf
    {
        readonly StringBuilder sb = new StringBuilder();

        static string N(double v) => Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);

        void G(int code, object value) => sb.Append(code).Append('\n').Append(value is double d ? N(d) : value.ToString()).Append('\n');

        public string Layer = "0";

        public void Line(double x1, double y1, double x2, double y2)
        {
            G(0, "LINE"); G(8, Layer); G(10, x1); G(20, y1); G(30, 0.0); G(11, x2); G(21, y2); G(31, 0.0);
        }

        public void Poly(bool closed, params double[] xy)
        {
            var n = xy.Length / 2;
            for (var i = 0; i + 1 < n; i++) Line(xy[2 * i], xy[2 * i + 1], xy[2 * i + 2], xy[2 * i + 3]);
            if (closed && n > 2) Line(xy[2 * n - 2], xy[2 * n - 1], xy[0], xy[1]);
        }

        public void Rect(double x0, double y0, double x1, double y1) => Poly(true, x0, y0, x1, y0, x1, y1, x0, y1);

        public void Circle(double cx, double cy, double r)
        {
            G(0, "CIRCLE"); G(8, Layer); G(10, cx); G(20, cy); G(30, 0.0); G(40, r);
        }

        /// <summary>An arc, counter-clockwise from <paramref name="a0"/> to <paramref name="a1"/> degrees.</summary>
        public void Arc(double cx, double cy, double r, double a0, double a1)
        {
            G(0, "ARC"); G(8, Layer); G(10, cx); G(20, cy); G(30, 0.0); G(40, r); G(50, a0); G(51, a1);
        }

        /// <summary>An elliptical arc: semi-axis <paramref name="a"/> along X, <paramref name="b"/> along Y, angles in radians.</summary>
        public void Ellipse(double cx, double cy, double a, double b, double t0, double t1)
        {
            // the major axis is the longer one; the parameters turn with it
            if (a >= b)
            {
                G(0, "ELLIPSE"); G(8, Layer); G(10, cx); G(20, cy); G(30, 0.0); G(11, a); G(21, 0.0); G(31, 0.0);
                G(210, 0.0); G(220, 0.0); G(230, 1.0); G(40, b / a); G(41, t0); G(42, t1);
            }
            else
            {
                G(0, "ELLIPSE"); G(8, Layer); G(10, cx); G(20, cy); G(30, 0.0); G(11, 0.0); G(21, b); G(31, 0.0);
                G(210, 0.0); G(220, 0.0); G(230, 1.0); G(40, a / b); G(41, t0 - Math.PI / 2); G(42, t1 - Math.PI / 2);
            }
        }

        /// <summary>A filled triangle or quadrilateral.</summary>
        public void Solid(params double[] xy)
        {
            var q = xy.Length == 6 ? xy.Concat(new[] { xy[4], xy[5] }).ToArray() : xy;
            G(0, "SOLID"); G(8, Layer);
            G(10, q[0]); G(20, q[1]); G(30, 0.0); G(11, q[2]); G(21, q[3]); G(31, 0.0);
            G(12, q[6]); G(22, q[7]); G(32, 0.0); G(13, q[4]); G(23, q[5]); G(33, 0.0);
        }

        /// <summary>Text; <paramref name="just"/> is L, C or R, with B, M or T in front (as "MC").</summary>
        public void Text(double x, double y, string s, double h, string just = "BL")
        {
            G(0, "TEXT"); G(8, Layer); G(10, x); G(20, y); G(30, 0.0); G(40, h); G(1, Clean(s)); G(7, DrawingSheets.TextStyle);
            Justify(x, y, just, false);
        }

        /// <summary>An attribute definition: a field filled in when the block is inserted, and edited later in CAD.</summary>
        public void Attribute(string tag, double x, double y, double h, string just = "BL")
        {
            G(0, "ATTDEF"); G(8, Layer); G(10, x); G(20, y); G(30, 0.0); G(40, h); G(1, ""); G(3, tag); G(2, tag); G(70, 0); G(7, DrawingSheets.TextStyle);
            Justify(x, y, just, true);
        }

        void Justify(double x, double y, string just, bool attribute)
        {
            var hj = "LCR".IndexOf(just[just.Length - 1]);
            var vj = just.Length == 2 ? " BMT".IndexOf(just[0]) : 0;
            if (vj < 0) vj = 0;
            if (hj > 0 || vj > 0) { G(72, Math.Max(hj, 0)); G(11, x); G(21, y); G(31, 0.0); }
            if (vj > 0) G(attribute ? 74 : 73, vj);
        }

        static string Clean(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ");

        public void Save(string path) => File.WriteAllText(path, "0\nSECTION\n2\nENTITIES\n" + sb + "0\nENDSEC\n0\nEOF\n", new UTF8Encoding(false));
    }

    /// <summary>The border, the reference zones and the title block of a sheet, per style.</summary>
    public static class DrawingSheets
    {
        /// <summary>The text style of the drawings.</summary>
        public const string TextStyle = "DRAFTING";

        /// <summary>
        /// The lettering: osifont, the free ISO 3098 font for CAD drawings. Where it is not
        /// installed, CADCraft (and other CAD programs) put their own lettering in its place.
        /// </summary>
        public const string Font = "osifont";

        /// <summary>The files of a drawing: the DXF, then the DWG, PDF and PNG saved next to it.</summary>
        public static readonly string[] Extensions = { ".dxf", ".dwg", ".pdf", ".png" };

        /// <summary>
        /// Saves a drawing: plots its layout to PDF and PNG and saves the DWG, as chosen in the
        /// settings, and always the DXF. The files of an earlier export are removed first, so none is
        /// left behind that no longer matches the drawing. Returns the DXF, or null when it was not saved.
        /// </summary>
        internal static string Save(CADCraftSession session, string outputBase, string layout, string what)
        {
            var o = ExportOptions.Load();
            foreach (var ext in Extensions) if (File.Exists(outputBase + ext)) File.Delete(outputBase + ext);
            // a drawing of many keeps its "drawing 3 of 10" in the window
            if (string.IsNullOrEmpty(what)) CadJob.Report("CADCraft: saving " + FormatsText(o));
            if (o.SavePdf) session.Execute("plot", new JObject { ["path"] = outputBase + ".pdf", ["layout"] = layout }, 300);
            if (o.SavePng) session.Execute("plot", new JObject { ["path"] = outputBase + ".png", ["layout"] = layout, ["device"] = "CADCraft PNG" }, 300);
            if (o.SaveDwg) session.Tool("save", new JObject { ["path"] = outputBase + ".dwg" }, "save DWG" + (string.IsNullOrEmpty(what) ? "" : " of " + what));
            session.Tool("save", new JObject { ["path"] = outputBase + ".dxf" }, "save DXF" + (string.IsNullOrEmpty(what) ? "" : " of " + what));
            return File.Exists(outputBase + ".dxf") ? outputBase + ".dxf" : null;
        }

        /// <summary>"DXF, DWG, PDF and PNG": the files a drawing is saved as.</summary>
        public static string FormatsText(ExportOptions o)
        {
            var f = new List<string> { "DXF" };
            if (o.SaveDwg) f.Add("DWG");
            if (o.SavePdf) f.Add("PDF");
            if (o.SavePng) f.Add("PNG");
            return f.Count == 1 ? f[0] : string.Join(", ", f.Take(f.Count - 1)) + " and " + f.Last();
        }

        /// <summary>Whether osifont is installed for this user or for the computer.</summary>
        public static bool FontInstalled()
        {
            var folders = new List<string>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Path.DirectorySeparatorChar == '\\')
            {
                folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
                folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"));
            }
            else
            {
                folders.AddRange(new[] { Path.Combine(home, "Library", "Fonts"), "/Library/Fonts", Path.Combine(home, ".local", "share", "fonts"),
                    Path.Combine(home, ".fonts"), "/usr/share/fonts", "/usr/local/share/fonts" });
            }
            foreach (var f in folders.Where(f => !string.IsNullOrEmpty(f) && Directory.Exists(f)))
            {
                try
                {
                    if (Directory.EnumerateFiles(f, "osifont*", SearchOption.AllDirectories).Any()) return true;
                }
                catch (Exception) { }
            }
            return false;
        }

        /// <summary>Writes the osifont file the extension carries into a folder; returns its path, or null.</summary>
        public static string WriteFont(string folder)
        {
            var asm = typeof(DrawingSheets).Assembly;
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".osifont.ttf", StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;
            var path = Path.Combine(folder, "osifont.ttf");
            using (var s = asm.GetManifestResourceStream(name))
            using (var f = File.Create(path)) s.CopyTo(f);
            return path;
        }

        /// <summary>Space between the sheet edge and the border: left (binding edge), and the other sides.</summary>
        public static void Margins(DrawingStyle style, out double left, out double other)
        {
            if (style == DrawingStyle.ASME) { left = 12.7; other = 12.7; }
            else { left = 20; other = 10; }
        }

        public const double TitleWidth = 180;

        /// <summary>
        /// The border with its zone references (numbers along the top and bottom, letters along the
        /// sides, about every 100 mm) and the trimming marks of the sheet.
        /// </summary>
        public static Dxf Border(DrawingStyle style, SheetSize size)
        {
            var d = new Dxf();
            Margins(style, out var left, out var other);
            double x0 = left, y0 = other, x1 = size.Width - other, y1 = size.Height - other;
            d.Rect(x0, y0, x1, y1);
            d.Rect(x0 - 5 > 0 ? x0 - 5 : 0, y0 - 5, x1 + 5, y1 + 5);

            var cols = Math.Max(2, (int)Math.Round((x1 - x0) / 100));
            var rows = Math.Max(2, (int)Math.Round((y1 - y0) / 100));
            double cw = (x1 - x0) / cols, rh = (y1 - y0) / rows;
            for (var i = 0; i < cols; i++)
            {
                var x = x0 + i * cw;
                if (i > 0) { d.Line(x, y0, x, y0 - 5); d.Line(x, y1, x, y1 + 5); }
                var label = (style == DrawingStyle.ASME ? cols - i : i + 1).ToString(CultureInfo.InvariantCulture);
                d.Text(x + cw / 2, y0 - 2.5, label, 2.5, "MC");
                d.Text(x + cw / 2, y1 + 2.5, label, 2.5, "MC");
            }
            for (var j = 0; j < rows; j++)
            {
                var y = y0 + j * rh;
                if (j > 0) { d.Line(x0, y, x0 - 5, y); d.Line(x1, y, x1 + 5, y); }
                var letter = ((char)('A' + (style == DrawingStyle.ASME ? j : rows - 1 - j))).ToString();
                d.Text(x0 - 2.5, y + rh / 2, letter, 2.5, "MC");
                d.Text(x1 + 2.5, y + rh / 2, letter, 2.5, "MC");
            }
            return d;
        }

        /// <summary>
        /// The title block with its revision table above it, drawn from its lower right corner at
        /// (0, 0) to the left and up; its fields are attributes named after <see cref="TitleData"/>.
        /// </summary>
        public static Dxf TitleBlock(DrawingStyle style, out double height)
        {
            var d = new Dxf();
            const double w = TitleWidth, x0 = -w;
            switch (style)
            {
                case DrawingStyle.ASME:
                    {
                        // approvals and scale on the left, company, title and drawing number on the right (ASME Y14.1)
                        const double aw = 70;
                        double ax = x0, rx = x0 + aw;
                        height = 44;
                        d.Rect(x0, 0, 0, height);
                        d.Line(rx, 0, rx, height);
                        foreach (var y in new[] { 8.0, 17, 26, 35 }) d.Line(ax, y, rx, y);
                        d.Line(ax + 48, 8, ax + 48, height);
                        Cell(d, ax, 8, "SCALE"); d.Attribute("SCALE", ax + 2, 1.5, 2.5);
                        Cell(d, ax, 17, "APPROVED"); d.Attribute("APPROVEDBY", ax + 2, 9.5, 2.5);
                        Cell(d, ax, 26, "CHECKED"); d.Attribute("CHECKEDBY", ax + 2, 18.5, 2.5);
                        Cell(d, ax, 35, "DRAWN"); d.Attribute("PREPAREDBY", ax + 2, 27.5, 2.5);
                        d.Attribute("DATE", ax + 50, 27.5, 2.5);
                        d.Text(ax + 24, 39.5, "APPROVALS", 2.5, "MC");
                        d.Text(ax + 59, 39.5, "DATE", 2.5, "MC");

                        foreach (var y in new[] { 8.0, 17, 30 }) d.Line(rx, y, 0, y);
                        d.Attribute("OWNER", rx + 55, 37, 4, "MC");
                        Cell(d, rx, 30, "TITLE");
                        d.Attribute("TITLE", rx + 55, 23.5, 4, "MC");
                        d.Attribute("SUBTITLE", rx + 55, 19.3, 2.5, "MC");
                        double[] cx = { rx, rx + 14, rx + 36, rx + 96 };
                        for (var i = 1; i < 4; i++) d.Line(cx[i], 8, cx[i], 17);
                        Cell(d, cx[0], 17, "SIZE"); d.Attribute("SIZE", cx[0] + 7, 9, 3.5, "BC");
                        Cell(d, cx[1], 17, "CAGE CODE");
                        Cell(d, cx[2], 17, "DWG NO."); d.Attribute("NUMBER", cx[2] + 30, 9, 3.5, "BC");
                        Cell(d, cx[3], 17, "REV"); d.Attribute("REVISION", cx[3] + 7, 9, 3.5, "BC");
                        d.Line(rx + 70, 0, rx + 70, 8);
                        Cell(d, rx, 8, "PROJECT"); d.Attribute("PROJECT", rx + 2, 1, 2.5);
                        Cell(d, rx + 70, 8, "SHEET"); d.Attribute("SHEET", rx + 72, 1, 2.5);
                        break;
                    }

                case DrawingStyle.Petrobras:
                    {
                        // the legend of N-381: code number, sheet and revision at the bottom, then
                        // scale, document type, date and department, the people, the title, the
                        // area and the project, the brand and the client
                        height = 62;
                        d.Rect(x0, 0, 0, height);
                        foreach (var y in new[] { 10.0, 18, 26, 40, 50 }) d.Line(x0, y, 0, y);
                        d.Line(x0 + 110, 0, x0 + 110, 10); d.Line(x0 + 145, 0, x0 + 145, 10);
                        Cell(d, x0, 10, "No."); d.Attribute("NUMBER", x0 + 55, 2.5, 4, "BC");
                        Cell(d, x0 + 110, 10, "SHEET"); d.Attribute("SHEET", x0 + 127.5, 2, 3, "BC");
                        Cell(d, x0 + 145, 10, "REV."); d.Attribute("REVISION", x0 + 162.5, 2, 3.5, "BC");
                        for (var i = 1; i < 4; i++) d.Line(x0 + 45 * i, 10, x0 + 45 * i, 18);
                        Cell(d, x0, 18, "SCALE"); d.Attribute("SCALE", x0 + 22.5, 11.2, 2.5, "BC");
                        Cell(d, x0 + 45, 18, "DOCUMENT TYPE"); d.Attribute("DOCUMENTTYPE", x0 + 67.5, 11.2, 2.5, "BC");
                        Cell(d, x0 + 90, 18, "DATE"); d.Attribute("DATE", x0 + 112.5, 11.2, 2.5, "BC");
                        Cell(d, x0 + 135, 18, "DEPARTMENT"); d.Attribute("DEPARTMENT", x0 + 157.5, 11.2, 2.5, "BC");
                        for (var i = 1; i < 3; i++) d.Line(x0 + 60 * i, 18, x0 + 60 * i, 26);
                        Cell(d, x0, 26, "PREPARED"); d.Attribute("PREPAREDBY", x0 + 30, 19.2, 2.5, "BC");
                        Cell(d, x0 + 60, 26, "CHECKED"); d.Attribute("CHECKEDBY", x0 + 90, 19.2, 2.5, "BC");
                        Cell(d, x0 + 120, 26, "APPROVED"); d.Attribute("APPROVEDBY", x0 + 150, 19.2, 2.5, "BC");
                        Cell(d, x0, 40, "TITLE");
                        d.Attribute("TITLE", x0 + 90, 33.5, 4, "MC");
                        d.Attribute("SUBTITLE", x0 + 90, 28.5, 2.5, "MC");
                        d.Line(x0 + 90, 40, x0 + 90, 50);
                        Cell(d, x0, 50, "AREA / UNIT"); d.Attribute("AREA", x0 + 45, 41.5, 2.5, "BC");
                        Cell(d, x0 + 90, 50, "PROGRAM / PROJECT"); d.Attribute("PROJECT", x0 + 135, 41.5, 2.5, "BC");
                        d.Line(x0 + 60, 50, x0 + 60, height);
                        d.Text(x0 + 30, 56, "DWSIM", 4, "MC");
                        Cell(d, x0 + 60, height, "CLIENT"); d.Attribute("OWNER", x0 + 120, 54.5, 3.5, "MC");
                        break;
                    }

                default:
                    {
                        // ISO 7200: identification number, revision, date of issue, language and
                        // sheet at the bottom, then the people and the document type and status,
                        // the title, and the department, technical reference and legal owner
                        height = 45;
                        d.Rect(x0, 0, 0, height);
                        foreach (var y in new[] { 9.0, 18, 33 }) d.Line(x0, y, 0, y);
                        foreach (var x in new[] { 70.0, 100, 145, 160 }) d.Line(x0 + x, 0, x0 + x, 9);
                        Cell(d, x0, 9, "IDENTIFICATION NUMBER"); d.Attribute("NUMBER", x0 + 35, 1.5, 3.5, "BC");
                        Cell(d, x0 + 70, 9, "REV."); d.Attribute("REVISION", x0 + 85, 1.5, 3.5, "BC");
                        Cell(d, x0 + 100, 9, "DATE OF ISSUE"); d.Attribute("DATE", x0 + 122.5, 1.5, 2.5, "BC");
                        Cell(d, x0 + 145, 9, "LANG."); d.Attribute("LANGUAGE", x0 + 152.5, 1.5, 2.5, "BC");
                        Cell(d, x0 + 160, 9, "SHEET"); d.Attribute("SHEET", x0 + 170, 1.5, 2.5, "BC");
                        foreach (var x in new[] { 45.0, 90, 135 }) d.Line(x0 + x, 9, x0 + x, 18);
                        Cell(d, x0, 18, "CREATED BY"); d.Attribute("PREPAREDBY", x0 + 22.5, 10.5, 2.5, "BC");
                        Cell(d, x0 + 45, 18, "APPROVED BY"); d.Attribute("APPROVEDBY", x0 + 67.5, 10.5, 2.5, "BC");
                        Cell(d, x0 + 90, 18, "DOCUMENT TYPE"); d.Attribute("DOCUMENTTYPE", x0 + 112.5, 10.5, 2.5, "BC");
                        Cell(d, x0 + 135, 18, "DOCUMENT STATUS"); d.Attribute("STATUS", x0 + 157.5, 10.5, 2.5, "BC");
                        Cell(d, x0, 33, "TITLE, SUPPLEMENTARY TITLE");
                        d.Attribute("TITLE", x0 + 90, 26, 4, "MC");
                        d.Attribute("SUBTITLE", x0 + 90, 21, 2.5, "MC");
                        foreach (var x in new[] { 45.0, 90 }) d.Line(x0 + x, 33, x0 + x, height);
                        Cell(d, x0, height, "RESPONSIBLE DEPT."); d.Attribute("DEPARTMENT", x0 + 22.5, 34.5, 2.5, "BC");
                        Cell(d, x0 + 45, height, "TECHNICAL REFERENCE"); d.Attribute("PROJECT", x0 + 67.5, 34.5, 2.5, "BC");
                        Cell(d, x0 + 90, height, "LEGAL OWNER"); d.Attribute("OWNER", x0 + 135, 37, 3.5, "MC");
                        break;
                    }
            }

            // the revision table above the title block: header and the first issue
            var rh = 6.0;
            double[] col = style == DrawingStyle.ASME ? new[] { 0, 14, 110, 140, 180.0 } : new[] { 0, 14, 96, 115, 134, 153, 180.0 };
            string[] head = style == DrawingStyle.ASME
                ? new[] { "REV", "DESCRIPTION", "DATE", "APPROVED" }
                : new[] { "REV.", "DESCRIPTION", "BY", "CHECK.", "APPR.", "DATE" };
            var top = height + 4 * rh;
            d.Rect(x0, height, 0, top);
            for (var i = 1; i < 4; i++) d.Line(x0, height + i * rh, 0, height + i * rh);
            for (var i = 1; i < col.Length - 1; i++) d.Line(x0 + col[i], height, x0 + col[i], top);
            for (var i = 0; i < head.Length; i++) d.Text(x0 + (col[i] + col[i + 1]) / 2, top - rh / 2, head[i], 2.5, "MC");
            d.Attribute("REVISION0", x0 + col[0] + 7, top - rh * 1.5, 2.5, "MC");
            d.Attribute("REVISIONTEXT", x0 + col[1] + 2, top - rh * 1.5, 2.5, "ML");
            var dateCol = style == DrawingStyle.ASME ? 2 : col.Length - 2;
            d.Attribute("REVISIONDATE", x0 + (col[dateCol] + col[dateCol + 1]) / 2, top - rh * 1.5, 2.5, "MC");
            height = top;
            return d;
        }

        /// <summary>A field label in the upper left corner of the cell whose top is at <paramref name="yTop"/>.</summary>
        static void Cell(Dxf d, double x, double yTop, string label) => d.Text(x + 1, yTop - 1, label, 1.8, "TL");

        static string Fit(string s, int n) => s == null || s.Length <= n ? s ?? "" : s.Substring(0, n - 1) + ".";

        /// <summary>The attribute values of the title block.</summary>
        public static Dictionary<string, string> Attributes(TitleData t, SheetSize size, DrawingStyle style) => new Dictionary<string, string>
        {
            ["PROJECT"] = Fit(t.Project, style == DrawingStyle.ISO ? 20 : style == DrawingStyle.ASME ? 30 : 40),
            ["OWNER"] = Fit(t.Owner, 28),
            ["AREA"] = t.Area,
            ["TITLE"] = t.Title,
            ["SUBTITLE"] = t.Subtitle,
            ["NUMBER"] = Fit(t.Number, style == DrawingStyle.ISO ? 20 : 24),
            ["REVISION"] = t.Revision,
            ["DOCUMENTTYPE"] = t.DocumentType,
            ["STATUS"] = t.Status,
            ["SCALE"] = t.Scale,
            ["DATE"] = t.Date,
            ["SHEET"] = t.Sheet,
            ["SIZE"] = size.Name,
            ["LANGUAGE"] = t.Language,
            ["PREPAREDBY"] = t.PreparedBy,
            ["CHECKEDBY"] = t.CheckedBy,
            ["APPROVEDBY"] = t.ApprovedBy,
            ["DEPARTMENT"] = t.Department,
            ["REVISION0"] = t.Revision,
            ["REVISIONTEXT"] = "ORIGINAL ISSUE",
            ["REVISIONDATE"] = t.Date
        };
    }
}
