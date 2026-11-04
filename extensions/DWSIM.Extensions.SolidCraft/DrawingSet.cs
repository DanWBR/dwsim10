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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The drawing set of Export All Drawings: one PDF that opens with the drawing list, then every
    /// sheet in the order of the list, with a bookmark for each kind of drawing and each sheet.
    /// Written with iTextSharp (the LGPL 4.1.6 release DWSIM already ships).
    /// </summary>
    public static class DrawingSet
    {
        public class Entry
        {
            /// <summary>The sheet's PDF.</summary>
            public string Pdf;

            /// <summary>The kind of drawing ("Piping isometric") and what it shows ("line 12"); the subject may be empty.</summary>
            public string Kind, Subject;

            internal int Pages, FirstPage;
        }

        /// <summary>Binds the PDFs that exist into <paramref name="path"/>; returns the number of sheets bound.</summary>
        public static int Write(string path, string project, string owner, List<Entry> entries)
        {
            var sheets = entries.Where(e => !string.IsNullOrEmpty(e.Pdf) && File.Exists(e.Pdf)).ToList();
            if (sheets.Count == 0) return 0;
            foreach (var e in sheets)
            {
                var reader = new PdfReader(e.Pdf);
                e.Pages = reader.NumberOfPages;
                reader.Close();
            }

            // the list's own length sets the page each sheet starts on: lay it out until that settles
            byte[] list = null;
            var listPages = 1;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var page = listPages + 1;
                foreach (var e in sheets)
                {
                    e.FirstPage = page;
                    page += e.Pages;
                }
                list = DrawingList(project, owner, sheets);
                var reader = new PdfReader(list);
                var pages = reader.NumberOfPages;
                reader.Close();
                if (pages == listPages) break;
                listPages = pages;
            }

            if (File.Exists(path)) File.Delete(path);
            var doc = new Document();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var copy = new PdfCopy(doc, stream);
                doc.Open();
                Append(copy, new PdfReader(list));
                foreach (var e in sheets) Append(copy, new PdfReader(e.Pdf));

                var outline = new ArrayList { Bookmark("Drawing list", 1) };
                foreach (var kind in sheets.GroupBy(e => e.Kind))
                {
                    var mark = Bookmark(kind.Key, kind.First().FirstPage);
                    if (kind.Any(e => !string.IsNullOrEmpty(e.Subject)))
                    {
                        mark["Kids"] = new ArrayList(kind.Select(e => Bookmark(string.IsNullOrEmpty(e.Subject) ? e.Kind : e.Subject, e.FirstPage)).ToList());
                        mark["Open"] = "false";
                    }
                    outline.Add(mark);
                }
                copy.Outlines = outline;
                doc.Close();
            }
            return sheets.Count;
        }

        static Hashtable Bookmark(string title, int page)
            => new Hashtable { ["Title"] = title, ["Action"] = "GoTo", ["Page"] = page.ToString(CultureInfo.InvariantCulture) + " Fit" };

        static void Append(PdfCopy copy, PdfReader reader)
        {
            for (var i = 1; i <= reader.NumberOfPages; i++) copy.AddPage(copy.GetImportedPage(reader, i));
            copy.FreeReader(reader);
            reader.Close();
        }

        /// <summary>The drawing list: an A4 landscape table of the sheets with the page each starts on.</summary>
        static byte[] DrawingList(string project, string owner, List<Entry> sheets)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4.Rotate(), 40, 40, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();
                var title = new Font(Font.HELVETICA, 16, Font.BOLD);
                var head = new Font(Font.HELVETICA, 9, Font.BOLD);
                var body = new Font(Font.HELVETICA, 9, Font.NORMAL);

                doc.Add(new Paragraph("DRAWING LIST", title));
                var lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(project)) lines.Add(project);
                if (!string.IsNullOrWhiteSpace(owner)) lines.Add(owner);
                lines.Add(DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "   " + sheets.Count + " drawings, " + sheets.Sum(e => e.Pages) + " sheets");
                doc.Add(new Paragraph(string.Join("\n", lines), body) { SpacingAfter = 10 });

                var table = new PdfPTable(new float[] { 5, 6, 22, 30, 50 }) { WidthPercentage = 100, HeaderRows = 1 };
                foreach (var h in new[] { "No.", "Page", "Drawing", "Item or line", "File" })
                    table.AddCell(new PdfPCell(new Phrase(h, head)) { BackgroundColor = new Color(225, 228, 232), Padding = 4 });
                var n = 0;
                foreach (var e in sheets)
                {
                    n++;
                    foreach (var text in new[] { n.ToString(CultureInfo.InvariantCulture), e.FirstPage.ToString(CultureInfo.InvariantCulture), e.Kind, e.Subject ?? "", Path.GetFileName(e.Pdf) })
                        table.AddCell(new PdfPCell(new Phrase(text, body)) { Padding = 3 });
                }
                doc.Add(table);
                doc.Close();
                return ms.ToArray();
            }
        }
    }
}
