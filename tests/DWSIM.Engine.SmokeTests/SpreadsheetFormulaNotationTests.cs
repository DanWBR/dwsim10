//    Locale-independent storage of spreadsheet formulas.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NUnit.Framework;
using unvell.ReoGrid;
using unvell.ReoGrid.Formula;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The spreadsheet stores formulas with ";" between arguments and "." as decimal mark, whatever
    /// the locale, and shows them in the form of the locale: "," and "." on en-US, ";" and "," on
    /// pt-BR and de-DE. Files written before, by this UI on any locale or by the classic UI, still
    /// open on every locale, and an Excel export carries the separators of the Excel file format.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class SpreadsheetFormulaNotationTests
    {
        private static readonly string[] Cultures = { "en-US", "pt-BR", "de-DE" };

        // the test formulas in the canonical form, in C1..C8; A1 = 2.5 and B1 = 4
        private static readonly string[] Canonical =
        {
            "SUM(A1;B1)",
            "IF(A1>1.5;A1*1.5;0)",
            "ROUND(A1/3;2)",
            "\"a,b;c 1.5\"&\"x\"",
            "GETPROPVAL(\"MAT-1,2;3\";\"PROP_MS_0\";\"K\")",
            "SETPROPVAL(\"MAT-1\";\"PROP_MS_0\";A1)",
            "-0.5+SUM(1.25;2;C1)",
            "LEN(\"1,5;2.5\")",
        };

        private static readonly object[] Expected =
        {
            6.5, 3.75, 0.83, "a,b;c 1.5x", "MAT-1,2;3|PROP_MS_0|K", "MAT-1|PROP_MS_0|2.5", 9.25, 7.0,
        };

        private static IEnumerable<TestCaseData> CulturePairs() =>
            from a in Cultures from b in Cultures select new TestCaseData(a, b);

        // ------------------------------------------------------------------------------------
        // culture and probe functions
        // ------------------------------------------------------------------------------------

        private static void InCulture(string name, Action action)
        {
            var thread = Thread.CurrentThread;
            var oldCulture = thread.CurrentCulture;
            var oldParameter = FormulaExtension.ParameterSeparator;
            var oldDecimal = FormulaExtension.NumberDecimalSeparator;
            var culture = name.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(name, false);
            try
            {
                thread.CurrentCulture = culture;
                // what ReoGrid takes from the culture when it starts on that machine
                FormulaExtension.ParameterSeparator = culture.TextInfo.ListSeparator;
                FormulaExtension.NumberDecimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
                action();
            }
            finally
            {
                thread.CurrentCulture = oldCulture;
                FormulaExtension.ParameterSeparator = oldParameter;
                FormulaExtension.NumberDecimalSeparator = oldDecimal;
            }
        }

        private Dictionary<string, Func<Cell, object[], object>> savedFunctions;

        [SetUp]
        public void RegisterProbeFunctions()
        {
            // GETPROPVAL and SETPROPVAL stand-ins that show how the arguments were split
            savedFunctions = new Dictionary<string, Func<Cell, object[], object>>();
            foreach (var name in new[] { "GETPROPVAL", "SETPROPVAL" })
            {
                if (FormulaExtension.CustomFunctions.TryGetValue(name, out var f)) savedFunctions[name] = f;
                FormulaExtension.CustomFunctions[name] = (cell, args) =>
                    string.Join("|", args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture)));
            }
        }

        [TearDown]
        public void RestoreFunctions()
        {
            if (savedFunctions == null) return;
            foreach (var name in new[] { "GETPROPVAL", "SETPROPVAL" })
            {
                if (savedFunctions.TryGetValue(name, out var f)) FormulaExtension.CustomFunctions[name] = f;
                else FormulaExtension.CustomFunctions.Remove(name);
            }
        }

        // ------------------------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------------------------

        private static FormulaSeparators FormOf(string culture) =>
            FormulaSeparators.FromCulture(culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture, false));

        private static string[] InForm(FormulaSeparators form) =>
            Canonical.Select(f => FormulaNotation.Translate(f, FormulaSeparators.Canonical, form)).ToArray();

        /// <summary>A worksheet with the test formulas typed in the form of the current locale.</summary>
        private static Worksheet NewSheet()
        {
            var ws = ReoGridControl.CreateMemoryWorkbook().Worksheets[0];
            ws["A1"] = 2.5;
            ws["B1"] = 4.0;
            var typed = InForm(FormulaSeparators.Current);
            for (int i = 0; i < typed.Length; i++) ws.Cells["C" + (i + 1)].Formula = typed[i];
            ws.Recalculate();
            return ws;
        }

        private static string SaveRgf(Worksheet ws)
        {
            using var ms = new MemoryStream();
            ws.SaveRGF(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static Worksheet LoadRgf(string rgf)
        {
            var ws = ReoGridControl.CreateMemoryWorkbook().Worksheets[0];
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rgf));
            ws.LoadRGF(ms);
            ws.Recalculate();
            return ws;
        }

        private static string[] StoredFormulas(string rgf) =>
            XDocument.Parse(rgf.TrimStart((char)0xFEFF)).Descendants("formula").Select(e => e.Value).ToArray();

        private static void AssertSheet(Worksheet ws, string because)
        {
            var form = FormulaSeparators.Current;
            var shown = InForm(form);
            for (int i = 0; i < Canonical.Length; i++)
            {
                var cell = ws.GetCell("C" + (i + 1));
                Assert.That(cell, Is.Not.Null, $"{because}: C{i + 1} is missing");
                Assert.That(cell.Formula, Is.EqualTo(shown[i]), $"{because}: C{i + 1} formula in the {form} form");
                if (Expected[i] is double d)
                    Assert.That(Convert.ToDouble(cell.Data), Is.EqualTo(d).Within(1e-12), $"{because}: C{i + 1} value of {cell.Formula}");
                else
                    Assert.That(Convert.ToString(cell.Data, CultureInfo.InvariantCulture), Is.EqualTo(Expected[i]), $"{because}: C{i + 1} value of {cell.Formula}");
            }
        }

        // ------------------------------------------------------------------------------------
        // the conversion itself
        // ------------------------------------------------------------------------------------

        [Test]
        public void SeparatorsOfTheTestLocales()
        {
            Assert.That(FormOf("en-US"), Is.EqualTo(new FormulaSeparators(",", ".")));
            Assert.That(FormOf("pt-BR"), Is.EqualTo(new FormulaSeparators(";", ",")));
            Assert.That(FormOf("de-DE"), Is.EqualTo(new FormulaSeparators(";", ",")));
        }

        [TestCase("SUM(A1,B1)", "SUM(A1;B1)")]
        [TestCase("IF(A1>1.5,A1*1.5,0)", "IF(A1>1.5;A1*1.5;0)")]
        [TestCase("GETPROPVAL(\"MAT-1,2;3\",\"PROP_MS_0\",\"K\")", "GETPROPVAL(\"MAT-1,2;3\";\"PROP_MS_0\";\"K\")")]
        [TestCase("\"a,b;c 1.5\"&\"x\"", "\"a,b;c 1.5\"&\"x\"")]
        [TestCase("SUM( A1 , B1 )", "SUM( A1 ; B1 )")]
        [TestCase("\"say \"\"1,5\"\"\",2", "\"say \"\"1,5\"\"\";2")]
        public void EnglishFormToCanonical(string english, string canonical)
        {
            Assert.That(FormulaNotation.Translate(english, new FormulaSeparators(",", "."), FormulaSeparators.Canonical), Is.EqualTo(canonical));
            Assert.That(FormulaNotation.Translate(canonical, FormulaSeparators.Canonical, new FormulaSeparators(",", ".")), Is.EqualTo(english));
        }

        [TestCase("SUM(A1;B1)", "SUM(A1;B1)")]
        [TestCase("IF(A1>1,5;A1*1,5;0)", "IF(A1>1.5;A1*1.5;0)")]
        [TestCase("-0,5+SUM(1,25;2;C1)", "-0.5+SUM(1.25;2;C1)")]
        [TestCase("GETPROPVAL(\"MAT-1,2;3\";\"PROP_MS_0\";\"K\")", "GETPROPVAL(\"MAT-1,2;3\";\"PROP_MS_0\";\"K\")")]
        [TestCase("LEN(\"1,5;2.5\")", "LEN(\"1,5;2.5\")")]
        public void PortugueseFormToCanonical(string portuguese, string canonical)
        {
            Assert.That(FormulaNotation.Translate(portuguese, new FormulaSeparators(";", ","), FormulaSeparators.Canonical), Is.EqualTo(canonical));
            Assert.That(FormulaNotation.Translate(canonical, FormulaSeparators.Canonical, new FormulaSeparators(";", ",")), Is.EqualTo(portuguese));
        }

        [Test]
        public void ACharacterForeignToTheFormIsReported()
        {
            Assert.That(FormulaNotation.TryTranslate("SUM(A1,B1)", FormulaSeparators.Canonical, FormulaSeparators.Canonical, out _), Is.False);
            Assert.That(FormulaNotation.TryTranslate("SUM(A1;B1)", new FormulaSeparators(",", "."), FormulaSeparators.Canonical, out _), Is.False);
            Assert.That(FormulaNotation.TryTranslate("CONCAT(\"a;b\",\"c\")", new FormulaSeparators(",", "."), FormulaSeparators.Canonical, out var t), Is.True);
            Assert.That(t, Is.EqualTo("CONCAT(\"a;b\";\"c\")"));
        }

        // ------------------------------------------------------------------------------------
        // RGF: round trip, cross-locale, files written before, classic files
        // ------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Cultures))]
        public void RoundTripStoresTheCanonicalFormAndShowsTheLocaleForm(string culture)
        {
            InCulture(culture, () =>
            {
                var ws = NewSheet();
                AssertSheet(ws, $"typed on {culture}");

                var rgf = SaveRgf(ws);
                Assert.That(StoredFormulas(rgf), Is.EqualTo(Canonical), $"stored on {culture}");
                Assert.That(XDocument.Parse(rgf.TrimStart((char)0xFEFF)).Descendants("notation").Single().Value, Is.EqualTo("canonical"));
                Assert.That(XDocument.Parse(rgf.TrimStart((char)0xFEFF)).Descendants("cell").First().Value, Is.EqualTo("2.5"), "numbers in the invariant culture");

                AssertSheet(LoadRgf(rgf), $"saved and reopened on {culture}");
            });
        }

        [TestCaseSource(nameof(CulturePairs))]
        public void AFileSavedOnOneLocaleOpensOnAnother(string saveCulture, string loadCulture)
        {
            string rgf = null;
            InCulture(saveCulture, () => rgf = SaveRgf(NewSheet()));
            InCulture(loadCulture, () => AssertSheet(LoadRgf(rgf), $"saved on {saveCulture}, opened on {loadCulture}"));
        }

        [TestCaseSource(nameof(CulturePairs))]
        public void AFileSavedBeforeOnOneLocaleOpensOnAnother(string saveCulture, string loadCulture)
        {
            // what this UI wrote until now: formulas and numbers in the locale form, without the notation mark
            string rgf = null;
            InCulture(saveCulture, () =>
            {
                FormulaNotation.CanonicalStorage = false;
                try { rgf = SaveRgf(NewSheet()); }
                finally { FormulaNotation.CanonicalStorage = true; }
            });
            Assert.That(StoredFormulas(rgf), Is.EqualTo(InForm(FormOf(saveCulture))), $"legacy file of {saveCulture}");
            Assert.That(rgf, Does.Not.Contain("<notation>"));

            InCulture(loadCulture, () => AssertSheet(LoadRgf(rgf), $"legacy file of {saveCulture}, opened on {loadCulture}"));
        }

        [TestCaseSource(nameof(Cultures))]
        public void AFileSavedBeforeOnTheInvariantCultureOpens(string loadCulture)
        {
            // the file of issue #101 was written with an empty culture name
            string rgf = null;
            InCulture("", () =>
            {
                FormulaNotation.CanonicalStorage = false;
                try { rgf = SaveRgf(NewSheet()); }
                finally { FormulaNotation.CanonicalStorage = true; }
            });
            Assert.That(StoredFormulas(rgf)[0], Is.EqualTo("SUM(A1,B1)"));

            InCulture(loadCulture, () => AssertSheet(LoadRgf(rgf), $"invariant legacy file, opened on {loadCulture}"));
        }

        /// <summary>
        /// An RGF sheet as the classic DWSIM spreadsheet writes it (unvell.ReoGrid 2.1.0.0, the
        /// .NET Framework build in DWSIM\References\ReoGrid.WinForms): ";" between arguments on every
        /// locale, and the decimal mark of the locale. Captured from that DLL on en-US and pt-BR.
        /// </summary>
        private static string ClassicRgf(string culture)
        {
            var form = new FormulaSeparators(";", FormOf(culture).Decimal);
            var cells = new StringBuilder();
            var formulas = InForm(form);
            string[] data = { "6.5", "3.75", "0.83", "a,b;c 1.5x", "0", "0", "9.25", "7" };
            for (int i = 0; i < formulas.Length; i++)
            {
                cells.AppendFormat("    <cell row=\"{0}\" col=\"2\">{1}<formula>{2}</formula></cell>\n",
                    i, System.Security.SecurityElement.Escape(data[i]), System.Security.SecurityElement.Escape(formulas[i]));
            }

            return "<?xml version=\"1.0\"?>\n"
                + "<grid xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n"
                + "  <head>\n    <meta>\n      <culture>" + culture + "</culture>\n      <editor>ReoGrid Core</editor>\n"
                + "      <core-ver>2.1.0.0</core-ver>\n    </meta>\n    <rows>200</rows>\n    <cols>100</cols>\n"
                + "    <default-row-height>20</default-row-height>\n    <default-col-width>70</default-col-width>\n"
                + "    <settings meta=\"30475026398\" />\n    <script />\n  </head>\n"
                + "  <style font=\"Arial\" font-size=\"10\" align=\"general\" valign=\"middle\" />\n"
                + "  <rows />\n  <cols />\n  <v-borders />\n  <h-borders />\n  <cells>\n"
                + "    <cell row=\"0\" col=\"0\">2.5</cell>\n    <cell row=\"0\" col=\"1\">4</cell>\n"
                + cells
                + "  </cells>\n</grid>\n";
        }

        [TestCase("en-US", "en-US")]
        [TestCase("en-US", "pt-BR")]
        [TestCase("en-US", "de-DE")]
        [TestCase("pt-BR", "en-US")]
        [TestCase("pt-BR", "pt-BR")]
        [TestCase("pt-BR", "de-DE")]
        public void AClassicFileOpens(string classicCulture, string loadCulture)
        {
            var rgf = ClassicRgf(classicCulture);
            InCulture(loadCulture, () => AssertSheet(LoadRgf(rgf), $"classic file of {classicCulture}, opened on {loadCulture}"));
        }

        [Test]
        public void AFormulaThatParsesInNoFormIsKeptAsText()
        {
            InCulture("en-US", () =>
            {
                var ws = ReoGridControl.CreateMemoryWorkbook().Worksheets[0];
                ws.Cells["A1"].Formula = "SUM(1,,";
                var rgf = SaveRgf(ws);
                var back = LoadRgf(rgf);
                Assert.That(back.GetCell("A1")?.Formula, Is.EqualTo("SUM(1,,"));
            });
        }

        // ------------------------------------------------------------------------------------
        // xlsx
        // ------------------------------------------------------------------------------------

        [TestCaseSource(nameof(CulturePairs))]
        public void AnExcelExportCarriesTheExcelSeparators(string saveCulture, string loadCulture)
        {
            byte[] xlsx = null;
            InCulture(saveCulture, () =>
            {
                var ws = NewSheet();
                using var ms = new MemoryStream();
                ws.Workbook.Save(ms, unvell.ReoGrid.IO.FileFormat.Excel2007);
                xlsx = ms.ToArray();
            });

            using (var zip = new ZipArchive(new MemoryStream(xlsx)))
            {
                var entry = zip.Entries.First(e => e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml"));
                using var reader = new StreamReader(entry.Open());
                var xml = XDocument.Parse(reader.ReadToEnd());
                var stored = xml.Descendants().Where(e => e.Name.LocalName == "f").Select(e => e.Value).ToArray();
                Assert.That(stored, Is.EqualTo(InForm(FormulaSeparators.Excel)), $"formulas in the xlsx written on {saveCulture}");
            }

            InCulture(loadCulture, () =>
            {
                var wb = ReoGridControl.CreateMemoryWorkbook();
                wb.Load(new MemoryStream(xlsx), unvell.ReoGrid.IO.FileFormat.Excel2007);
                var ws = wb.Worksheets[0];
                ws.Recalculate();
                AssertSheet(ws, $"xlsx written on {saveCulture}, opened on {loadCulture}");
            });
        }

        // ------------------------------------------------------------------------------------
        // the flowsheet file, through DWSIM.Automation
        // ------------------------------------------------------------------------------------

        [Test]
        public void AFlowsheetSpreadsheetSavedOnPortugueseOpensOnEnglish()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;

            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");
            var file = Path.Combine(folder, "tests", "flowsheets", "SO2OxidationEquilibrium.dwxmz");

            // the probes would shadow the flowsheet's own functions; the teardown puts back
            // whatever the flowsheets below register
            RestoreFunctions();
            savedFunctions = null;

            string saved = null;
            string streamId = null;
            double kelvin = 0;

            InCulture("pt-BR", () =>
            {
                var fs = new DWSIM.Automation.Flowsheet2(null, null);
                fs.Init();
                fs.LoadZippedXML(file);
                var stream = fs.SimulationObjects.Values.First(o => o is DWSIM.Thermodynamics.Streams.MaterialStream);
                streamId = stream.Name;
                kelvin = Convert.ToDouble(stream.GetPropertyValue("PROP_MS_0"));

                var ws = ((IWorkbook)fs.GetSpreadsheetObjectFunc()).Worksheets[0];
                ws.Cells["A1"].Formula = $"GETPROPVAL(\"{streamId}\";\"PROP_MS_0\";\"K\")";
                ws.Cells["A2"].Formula = $"ROUND(GETPROPVAL(\"{streamId}\";\"PROP_MS_0\";\"C\")*1,5;2)";
                // 10.25 is also the classic default font size, which the loader swaps for 10
                ws["B1"] = 10.25;
                ws.Cells["A3"].Formula = "B1*10,25";
                ws.Recalculate();
                Assert.That(Convert.ToDouble(ws.GetCell("A1").Data), Is.EqualTo(kelvin).Within(1e-9));

                saved = fs.SaveToXML().ToString();
            });

            var rgfData = XDocument.Parse(saved).Root.Element("Spreadsheet").Element("RGFData").Value;
            Assert.That(rgfData, Does.Contain("*1.5;2)"), "the decimal and the separators are stored in the canonical form");

            InCulture("en-US", () =>
            {
                var fs = new DWSIM.Automation.Flowsheet2(null, null);
                fs.Init();
                fs.LoadFromXML(XDocument.Parse(saved));
                var ws = ((IWorkbook)fs.GetSpreadsheetObjectFunc()).Worksheets[0];
                ws.Recalculate();
                Assert.That(ws.GetCell("A1").Formula, Is.EqualTo($"GETPROPVAL(\"{streamId}\",\"PROP_MS_0\",\"K\")"));
                Assert.That(Convert.ToDouble(ws.GetCell("A1").Data), Is.EqualTo(kelvin).Within(1e-9));
                Assert.That(ws.GetCell("A2").Formula, Is.EqualTo($"ROUND(GETPROPVAL(\"{streamId}\",\"PROP_MS_0\",\"C\")*1.5,2)"));
                Assert.That(Convert.ToDouble(ws.GetCell("A2").Data), Is.EqualTo(Math.Round((kelvin - 273.15) * 1.5, 2)).Within(1e-9));
                Assert.That(Convert.ToDouble(ws.GetCell("B1").Data), Is.EqualTo(10.25));
                Assert.That(ws.GetCell("A3").Formula, Is.EqualTo("B1*10.25"));
                Assert.That(Convert.ToDouble(ws.GetCell("A3").Data), Is.EqualTo(10.25 * 10.25).Within(1e-12));
            });
        }
    }
}
