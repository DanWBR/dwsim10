// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AeroWizard;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The drawing import wizard in the Windows interface (see <see cref="DrawingImportSession"/>).</summary>
    public partial class DrawingImportWizard : Form
    {
        const string BatteryLimit = "(battery limit)";

        readonly DrawingImportSession session;
        CancellationTokenSource reading;
        WorkIndicator work;
        bool loading;
        Image overview;

        public DrawingImportWizard(IFlowsheet fs)
        {
            session = new DrawingImportSession(fs);
            InitializeComponent();
        }

        private void DrawingImportWizard_Load(object sender, EventArgs e)
        {
            this.ChangeDefaultFont();
            cbKind.SelectedIndex = 0;
            colItemCategory.Items.AddRange(DrawingImport.Categories.Cast<object>().ToArray());
            cbUnitT.Items.AddRange(DrawingImportSession.TemperatureUnits.Cast<object>().ToArray());
            cbUnitP.Items.AddRange(DrawingImportSession.PressureUnits.Cast<object>().ToArray());
            cbUnitW.Items.AddRange(DrawingImportSession.MassFlowUnits.Cast<object>().ToArray());
            cbUnitE.Items.AddRange(DrawingImportSession.EnergyUnits.Cast<object>().ToArray());
            cbClass.Items.Add("(not known)");
            cbClass.Items.AddRange(DrawingImport.ProcessClasses.Select(c => (object)(c.Class + ": " + c.Description)).ToArray());
            cbPackage.Items.AddRange(session.AvailablePackages().Cast<object>().ToArray());
            if (session.Flowsheet.SimulationObjects.Count > 0) chkUnits.Checked = false;
            wpDrawing.AllowNext = false;
            work = new WorkIndicator(lblReadStatus, prgRead) { ModelLabel = lblModel };
            work.ShowModel();
        }

        #region pages

        private void wpItems_Initialize(object sender, WizardPageInitEventArgs e) => FillItems();

        private void wpLines_Initialize(object sender, WizardPageInitEventArgs e) => FillLines();

        private void wpProcess_Initialize(object sender, WizardPageInitEventArgs e) => FillProcess();

        /// <summary>The process page's choices are taken when it is left, forwards or back.</summary>
        private void wpProcess_Commit(object sender, WizardPageConfirmEventArgs e) => TakeProcessPage();

        private void wpBuild_Initialize(object sender, WizardPageInitEventArgs e) => FillChecks();

        private void DrawingImportWizard_FormClosing(object sender, FormClosingEventArgs e)
        {
            reading?.Cancel();
            work?.Dispose();
        }

        #endregion

        #region drawing

        private void txtFile_TextChanged(object sender, EventArgs e) => btnRead.Enabled = File.Exists(txtFile.Text.Trim()) && reading == null;

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = DrawingImportSession.FileFilterText + "|" + string.Join(";", DrawingImportSession.FileExtensions.Select(x => "*" + x)) + "|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                txtFile.Text = d.FileName;
                ShowPreview(d.FileName);
            }
        }

        void ShowPreview(string file)
        {
            try
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".pdf" || ext == ".dxf" || ext == ".dwg" || DexpiImport.IsDexpi(file)) { picDrawing.Image = null; return; }
                using (var fs = File.OpenRead(file)) picDrawing.Image = Image.FromStream(fs);
            }
            catch (Exception) { picDrawing.Image = null; }
        }

        private async void btnRead_Click(object sender, EventArgs e)
        {
            session.File = txtFile.Text.Trim();
            session.Kind = cbKind.SelectedIndex == 1 ? "pfd" : cbKind.SelectedIndex == 2 ? "pid" : "auto";
            if (DexpiImport.IsDexpi(session.File) && DexpiImport.FindConverter() == null)
            {
                lblReadStatus.Text = DexpiImport.MissingText;
                if (MessageBox.Show(this, DexpiImport.MissingText + Environment.NewLine + Environment.NewLine + "Open the download page?", Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    DexpiImport.OpenDownloadPage();
                return;
            }
            reading = new CancellationTokenSource();
            var token = reading.Token;
            SetBusy(true);
            var dexpi = DexpiImport.IsDexpi(session.File);
            work.Start(dexpi ? "Reading the DEXPI model" : "Checking the AI Assistant's model");
            try
            {
                var model = dexpi ? (object)DexpiImport.Program : await work.Model(token, "a drawing");
                if (model == null) { btnRead.Text = "Read with the local model"; return; }
                btnRead.Text = "Read the drawing";
                await Task.Run(() => session.Read(token, s => BeginInvoke((Action)(() => { if (!token.IsCancellationRequested) work.Step(s); }))));
                work.Step("Matching the compounds and the property package");
                session.Propose();
                work.Stop();
                lblReadStatus.Text = string.Format(CultureInfo.CurrentCulture, "{0} items and {1} lines read by {3} in {2}. Check them on the next pages.",
                    session.Reading.Items.Count, session.Reading.Lines.Count, DrawingImportClient.Elapsed(work.Elapsed), model);
                Loaded();
            }
            catch (Exception ex) when (token.IsCancellationRequested || ex is OperationCanceledException)
            {
                work.Stop();
                lblReadStatus.Text = "Reading cancelled.";
            }
            catch (Exception ex)
            {
                work.Stop();
                lblReadStatus.Text = "The drawing could not be read: " + ex.GetBaseException().Message + " (the details are in the flowsheet's log)";
                session.Flowsheet.ShowMessage("Import Flowsheet from Drawing: " + ex, IFlowsheet.MessageType.GeneralError);
            }
            finally
            {
                reading = null;
                SetBusy(false);
            }
        }

        void SetBusy(bool busy)
        {
            btnRead.Enabled = !busy && File.Exists(txtFile.Text.Trim());
            btnCancelRead.Enabled = busy;
            btnOpenReading.Enabled = !busy;
            btnSaveReading.Enabled = !busy && session.Reading != null;
            btnBrowse.Enabled = !busy;
            wpDrawing.AllowNext = !busy && session.Reading != null;
        }

        private void btnCancelRead_Click(object sender, EventArgs e)
        {
            if (reading == null) return;
            reading.Cancel();
            btnCancelRead.Enabled = false;
            work?.Step("Cancelling");
        }

        private void lnkAssistantSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var why = DrawingImportClient.OpenAssistantSettings(session.Flowsheet, true);
            if (why != null) MessageBox.Show(this, why, Text);
            work.ShowModel();
        }

        private void btnOpenReading_Click(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "Drawing readings (*.json)|*.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    session.Open(File.ReadAllText(d.FileName));
                    txtFile.Text = session.File;
                    lblReadStatus.Text = session.Reading.Items.Count + " items and " + session.Reading.Lines.Count + " lines in the saved reading.";
                    Loaded();
                }
                catch (Exception ex) { lblReadStatus.Text = "The reading could not be opened: " + ex.Message; }
            }
        }

        private void btnSaveReading_Click(object sender, EventArgs e)
        {
            if (session.Reading == null) return;
            TakeProcessPage();
            using (var d = new SaveFileDialog { Filter = "Drawing readings (*.json)|*.json", FileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                session.SaveReading(d.FileName);
            }
        }

        void Loaded()
        {
            overview?.Dispose();
            overview = null;
            if (session.Reading.Overview != null)
                try { overview = Image.FromStream(new MemoryStream(session.Reading.Overview)); } catch (Exception) { }
            if (overview != null) picDrawing.Image = overview;
            SetBusy(false);
        }

        #endregion

        #region equipment

        void FillItems()
        {
            loading = true;
            dgvItems.Rows.Clear();
            foreach (var item in session.Reading.Items)
            {
                var i = dgvItems.Rows.Add(item.Include, item.Tag, null, null);
                var row = dgvItems.Rows[i];
                row.Tag = item;
                var cat = (DataGridViewComboBoxCell)row.Cells[colItemCategory.Index];
                if (!cat.Items.Contains(item.Category)) cat.Items.Add(item.Category);
                cat.Value = item.Category;
                SetTypes(row, item);
            }
            loading = false;
            picItems.Invalidate();
        }

        static void SetTypes(DataGridViewRow row, ReadItem item)
        {
            var cell = (DataGridViewComboBoxCell)row.Cells[3];
            cell.Items.Clear();
            var types = DrawingImport.TypesFor(item.Category).ToList();
            if (!types.Contains(item.Type)) types.Insert(0, item.Type);
            cell.Items.AddRange(types.Cast<object>().ToArray());
            cell.Value = item.Type;
        }

        private void dgvItems_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (loading || e.RowIndex < 0) return;
            var row = dgvItems.Rows[e.RowIndex];
            var item = (ReadItem)row.Tag;
            var value = row.Cells[e.ColumnIndex].Value;
            if (e.ColumnIndex == colItemInclude.Index) item.Include = value is bool b && b;
            else if (e.ColumnIndex == colItemTag.Index)
            {
                var tag = (value ?? "").ToString().Trim();
                if (tag.Length == 0) return;
                // the lines follow the item
                foreach (var l in session.Reading.Lines)
                {
                    if (string.Equals(l.From, item.Tag, StringComparison.OrdinalIgnoreCase)) l.From = tag;
                    if (string.Equals(l.To, item.Tag, StringComparison.OrdinalIgnoreCase)) l.To = tag;
                }
                item.Tag = tag;
            }
            else if (e.ColumnIndex == colItemCategory.Index)
            {
                item.Category = (value ?? "Generic").ToString();
                item.Type = DrawingImport.DefaultType(item.Category);
                loading = true;
                SetTypes(row, item);
                loading = false;
            }
            else if (e.ColumnIndex == colItemType.Index) item.Type = (value ?? item.Type).ToString();
            picItems.Invalidate();
        }

        private void dgvItems_SelectionChanged(object sender, EventArgs e) => picItems.Invalidate();

        private void Grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            var grid = (DataGridView)sender;
            if (grid.IsCurrentCellDirty && (grid.CurrentCell is DataGridViewComboBoxCell || grid.CurrentCell is DataGridViewCheckBoxCell))
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e) => e.ThrowException = false;

        #endregion

        #region lines

        void FillLines()
        {
            loading = true;
            var u = session.Reading.Units;
            colLineT.HeaderText = "T (" + u.Temperature + ")";
            colLineP.HeaderText = "P (" + u.Pressure + ")";
            colLineW.HeaderText = "Mass flow (" + u.MassFlow + ")";
            var ends = new List<object> { BatteryLimit };
            ends.AddRange(session.Reading.Items.Select(i => (object)i.Tag));
            colLineFrom.Items.Clear(); colLineFrom.Items.AddRange(ends.ToArray());
            colLineTo.Items.Clear(); colLineTo.Items.AddRange(ends.ToArray());
            dgvLines.Rows.Clear();
            foreach (var l in session.Reading.Lines)
            {
                var from = l.Feed ? BatteryLimit : l.From;
                var to = l.Product ? BatteryLimit : l.To;
                if (!colLineFrom.Items.Contains(from)) { colLineFrom.Items.Add(from); colLineTo.Items.Add(from); }
                if (!colLineTo.Items.Contains(to)) { colLineFrom.Items.Add(to); colLineTo.Items.Add(to); }
                var i = dgvLines.Rows.Add(l.Include, session.Reading.StreamTag(l), from, to, Num(l.T), Num(l.P), Num(l.W), Num(l.VaporFraction), l.Compounds.Count > 0 ? l.Compounds.Count.ToString() : "");
                dgvLines.Rows[i].Tag = l;
                if (l.Compounds.Count > 0)
                    dgvLines.Rows[i].Cells[colLineComp.Index].ToolTipText = string.Join("\n", l.Compounds.Select(c => c.Key + ": " + c.Value.ToString("G4", CultureInfo.CurrentCulture)));
            }
            loading = false;
            picLines.Invalidate();
        }

        static string Num(double? v) => v.HasValue ? v.Value.ToString("G6", CultureInfo.CurrentCulture) : "";

        static double? Parse(object v)
        {
            var s = (v ?? "").ToString().Trim();
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var d)) return d;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        private void dgvLines_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (loading || e.RowIndex < 0) return;
            var row = dgvLines.Rows[e.RowIndex];
            var l = (ReadLine)row.Tag;
            var v = row.Cells[e.ColumnIndex].Value;
            var c = e.ColumnIndex;
            if (c == colLineInclude.Index) l.Include = v is bool b && b;
            else if (c == colLineName.Index) { l.Name = (v ?? "").ToString().Trim(); l.LineNumber = ""; }
            else if (c == colLineFrom.Index) l.From = (v ?? "").ToString() == BatteryLimit ? "" : (v ?? "").ToString();
            else if (c == colLineTo.Index) l.To = (v ?? "").ToString() == BatteryLimit ? "" : (v ?? "").ToString();
            else if (c == colLineT.Index) l.T = Parse(v);
            else if (c == colLineP.Index) l.P = Parse(v);
            else if (c == colLineW.Index) l.W = Parse(v);
            else if (c == colLineVF.Index) l.VaporFraction = Parse(v);
            picLines.Invalidate();
        }

        private void dgvLines_SelectionChanged(object sender, EventArgs e) => picLines.Invalidate();

        #endregion

        #region the reading on the drawing

        private void Picture_Resize(object sender, EventArgs e) => ((Control)sender).Invalidate();

        /// <summary>Where the sheet sits in a box, scaled to fit.</summary>
        RectangleF Fit(Rectangle box)
        {
            var w = overview?.Width ?? 1400; var h = overview?.Height ?? 1000;
            var k = Math.Min(box.Width / (float)w, box.Height / (float)h);
            return new RectangleF(box.X + (box.Width - w * k) / 2, box.Y + (box.Height - h * k) / 2, w * k, h * k);
        }

        void DrawSheet(Graphics g, Rectangle box, out RectangleF r)
        {
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            r = Fit(box);
            if (overview != null) g.DrawImage(overview, r);
            using (var veil = new SolidBrush(Color.FromArgb(150, Color.White))) g.FillRectangle(veil, r);
        }

        PointF At(RectangleF r, ReadItem i, int k, int n)
        {
            // items without a place go in a row along the bottom
            if (double.IsNaN(i.X) || double.IsNaN(i.Y)) return new PointF(r.X + r.Width * (k + 1) / (n + 1), r.Bottom - 12);
            return new PointF(r.X + (float)i.X * r.Width, r.Y + (float)i.Y * r.Height);
        }

        Dictionary<string, PointF> Places(RectangleF r)
        {
            var d = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
            var items = session.Reading.Items;
            for (var k = 0; k < items.Count; k++) if (!d.ContainsKey(items[k].Tag)) d[items[k].Tag] = At(r, items[k], k, items.Count);
            return d;
        }

        private void picItems_Paint(object sender, PaintEventArgs e)
        {
            if (session.Reading == null) return;
            DrawSheet(e.Graphics, picItems.ClientRectangle, out var r);
            var selected = dgvItems.CurrentRow?.Tag as ReadItem;
            var places = Places(r);
            using (var font = new Font(Font.FontFamily, 8f))
                foreach (var item in session.Reading.Items)
                {
                    var p = places[item.Tag];
                    var mark = item == selected ? Color.Red : !item.Include ? Color.Gray : item.Category == "Mixer" || item.Category == "Splitter" ? Color.DarkOrange : Color.RoyalBlue;
                    var rad = item == selected ? 8 : 5;
                    using (var b = new SolidBrush(mark)) e.Graphics.FillEllipse(b, p.X - rad, p.Y - rad, 2 * rad, 2 * rad);
                    using (var b = new SolidBrush(Color.FromArgb(item == selected ? 255 : 200, Color.Black))) e.Graphics.DrawString(item.Tag, font, b, p.X + rad + 1, p.Y - 7);
                }
        }

        private void picLines_Paint(object sender, PaintEventArgs e)
        {
            if (session.Reading == null) return;
            DrawSheet(e.Graphics, picLines.ClientRectangle, out var r);
            var selected = dgvLines.CurrentRow?.Tag as ReadLine;
            var places = Places(r);
            foreach (var l in session.Reading.Lines.OrderBy(x => x == selected))
            {
                var hasFrom = places.TryGetValue(l.From, out var a);
                var hasTo = places.TryGetValue(l.To, out var b);
                if (!hasFrom && !hasTo) continue;
                if (!hasFrom) a = new PointF(b.X - 40, b.Y);
                if (!hasTo) b = new PointF(a.X + 40, a.Y);
                var color = l == selected ? Color.Red : !l.Include ? Color.LightGray : l.Feed || l.Product ? Color.SeaGreen : Color.RoyalBlue;
                using (var pen = new Pen(color, l == selected ? 3f : 1.6f) { CustomEndCap = new AdjustableArrowCap(4, 5) })
                    e.Graphics.DrawLine(pen, a, b);
            }
            foreach (var kv in places)
                using (var dot = new SolidBrush(Color.Black)) e.Graphics.FillEllipse(dot, kv.Value.X - 3, kv.Value.Y - 3, 6, 6);
            if (selected != null)
                using (var font = new Font(Font.FontFamily, 8f, FontStyle.Bold))
                    e.Graphics.DrawString(session.Reading.StreamTag(selected) + ": " + (selected.Feed ? BatteryLimit : selected.From) + " > " + (selected.Product ? BatteryLimit : selected.To),
                        font, Brushes.Red, r.X + 4, r.Y + 4);
        }

        #endregion

        #region process

        void FillProcess()
        {
            loading = true;
            var p = session.Reading.Process;
            txtDescription.Text = (session.Reading.TitleBlock.Count > 0 ? "Drawing: " + session.Reading.DrawingName() + Environment.NewLine : "") +
                (p.Description.Length > 0 ? p.Description : "The drawing's text does not tell the process.") +
                (p.Evidence.Length > 0 ? Environment.NewLine + "Evidence: " + p.Evidence : "") +
                (p.Confidence.Length > 0 ? Environment.NewLine + "Confidence: " + p.Confidence : "");
            var k = Array.FindIndex(DrawingImport.ProcessClasses, c => string.Equals(c.Class, p.Class, StringComparison.OrdinalIgnoreCase));
            cbClass.SelectedIndex = k + 1;
            cbPackage.SelectedItem = session.Setup.PropertyPackage;
            var u = session.Reading.Units;
            cbUnitT.Text = u.Temperature; cbUnitP.Text = u.Pressure; cbUnitW.Text = u.MassFlow; cbUnitE.Text = u.Energy;
            lstCompounds.Items.Clear();
            lstCompounds.Items.AddRange(session.Setup.Compounds.Cast<object>().ToArray());
            loading = false;
            txtSearch_TextChanged(null, EventArgs.Empty);
        }

        void TakeProcessPage()
        {
            if (session.Reading == null || cbUnitT.Text.Trim().Length == 0) return;
            var u = session.Reading.Units;
            u.Temperature = cbUnitT.Text.Trim(); u.Pressure = cbUnitP.Text.Trim(); u.MassFlow = cbUnitW.Text.Trim(); u.Energy = cbUnitE.Text.Trim();
            session.Setup.Compounds = lstCompounds.Items.Cast<string>().ToList();
            if (cbPackage.SelectedItem != null) session.Setup.PropertyPackage = cbPackage.SelectedItem.ToString();
        }

        private void cbClass_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loading || cbClass.SelectedIndex < 0) return;
            session.Reading.Process.Class = cbClass.SelectedIndex == 0 ? "" : DrawingImport.ProcessClasses[cbClass.SelectedIndex - 1].Class;
            var pp = session.Package(session.Reading.Process.Class);
            if (pp != null) { session.Setup.PropertyPackage = pp; cbPackage.SelectedItem = pp; }
        }

        private void cbPackage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!loading && cbPackage.SelectedItem != null) session.Setup.PropertyPackage = cbPackage.SelectedItem.ToString();
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            lstFound.BeginUpdate();
            lstFound.Items.Clear();
            lstFound.Items.AddRange(session.AvailableCompounds(txtSearch.Text).Where(c => !lstCompounds.Items.Contains(c)).Cast<object>().ToArray());
            lstFound.EndUpdate();
        }

        private void btnAddCompound_Click(object sender, EventArgs e)
        {
            foreach (var c in lstFound.SelectedItems.Cast<string>().ToList())
                if (!lstCompounds.Items.Contains(c)) lstCompounds.Items.Add(c);
            session.Setup.Compounds = lstCompounds.Items.Cast<string>().ToList();
            txtSearch_TextChanged(null, EventArgs.Empty);
        }

        private void btnRemoveCompound_Click(object sender, EventArgs e)
        {
            foreach (var c in lstCompounds.SelectedItems.Cast<string>().ToList()) lstCompounds.Items.Remove(c);
            session.Setup.Compounds = lstCompounds.Items.Cast<string>().ToList();
            txtSearch_TextChanged(null, EventArgs.Empty);
        }

        /// <summary>The application's Simulation Wizard, on the flowsheet with the proposed compounds and package in it.</summary>
        private void btnSimWizard_Click(object sender, EventArgs e)
        {
            TakeProcessPage();
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetTypes().FirstOrDefault(t => t.Name == "FormSimulWizard" && typeof(Form).IsAssignableFrom(t)); } catch (Exception) { return null; } })
                .FirstOrDefault(t => t != null);
            if (type == null) { MessageBox.Show(this, "The Simulation Wizard is not available here.", Text); return; }
            try
            {
                session.ApplyCompoundsAndPackage();
                using (var wizard = (Form)Activator.CreateInstance(type))
                {
                    type.GetField("CurrentFlowsheet")?.SetValue(wizard, session.Flowsheet);
                    wizard.StartPosition = FormStartPosition.CenterParent;
                    wizard.ShowDialog(this);
                }
                session.TakeCompoundsAndPackage();
                FillProcess();
            }
            catch (Exception ex) { MessageBox.Show(this, "The Simulation Wizard could not be opened: " + ex.GetBaseException().Message, Text); }
        }

        #endregion

        #region check and build

        void FillChecks()
        {
            var issues = session.Check();
            Fill(dgvIssues, issues);
            btnBuild.Enabled = session.Built == null && !issues.Any(i => i.Level == ImportIssue.Levels.Error);
            if (session.Built == null) lblSummary.Text = issues.Any(i => i.Level == ImportIssue.Levels.Error)
                ? "Correct the errors on the earlier pages before building."
                : "Ready to build" + (issues.Count > 0 ? "; the warnings above are worth a look." : ".");
        }

        static void Fill(DataGridView grid, IEnumerable<ImportIssue> issues)
        {
            grid.Rows.Clear();
            foreach (var i in issues)
            {
                var k = grid.Rows.Add(i.Level == ImportIssue.Levels.Error ? "Error" : i.Level == ImportIssue.Levels.Warning ? "Warning" : "Note", i.Subject, i.Text);
                grid.Rows[k].DefaultCellStyle.ForeColor = i.Level == ImportIssue.Levels.Error ? Color.DarkRed : i.Level == ImportIssue.Levels.Warning ? Color.DarkGoldenrod : SystemColors.ControlText;
            }
        }

        private void btnBuild_Click(object sender, EventArgs e)
        {
            session.Setup.SpecsFromTable = chkSpecs.Checked;
            session.Setup.UseDrawingUnits = chkUnits.Checked;
            btnBuild.Enabled = false;
            UseWaitCursor = true;
            Application.DoEvents();
            try
            {
                session.Build(chkSolve.Checked, s => { lblSummary.Text = s + "..."; lblSummary.Refresh(); });
                lblSummary.Text = session.Summary();
                Fill(dgvResults, session.Built.Issues.Concat(session.Solve));
                FillFit();
            }
            catch (Exception ex)
            {
                lblSummary.Text = "The flowsheet could not be built: " + ex.GetBaseException().Message;
            }
            finally { UseWaitCursor = false; }
        }

        /// <summary>The parameters the fit may move (Patreon edition), shown once the flowsheet is built and solved.</summary>
        void FillFit()
        {
            flpFit.Visible = session.CanFit && chkSolve.Checked;
            clbFit.Items.Clear();
            foreach (var p in session.FitParameters) clbFit.Items.Add(p, p.Use);
        }

        private async void btnFit_Click(object sender, EventArgs e)
        {
            if (reading != null) { reading.Cancel(); return; }
            for (var i = 0; i < clbFit.Items.Count; i++) ((DrawingImportFit.Parameter)clbFit.Items[i]).Use = clbFit.GetItemChecked(i);
            reading = new CancellationTokenSource();
            var token = reading.Token;
            btnFit.Text = "Stop";
            clbFit.Enabled = wpBuild.AllowBack = wpBuild.AllowNext = wpBuild.AllowCancel = false;
            UseWaitCursor = true;
            try
            {
                await Task.Run(() => session.Fit(token, s => BeginInvoke((Action)(() => lblSummary.Text = s.Trim()))));
                lblSummary.Text = "Solving the flowsheet at the fitted values...";
                lblSummary.Refresh();
                session.FinishFit();
                lblSummary.Text = session.Summary();
                Fill(dgvResults, session.Built.Issues.Concat(session.Solve));
                FillFit();
            }
            catch (Exception ex)
            {
                lblSummary.Text = "The fit failed: " + ex.GetBaseException().Message;
            }
            finally
            {
                reading = null;
                btnFit.Text = "Fit to the table";
                clbFit.Enabled = wpBuild.AllowBack = wpBuild.AllowNext = wpBuild.AllowCancel = true;
                UseWaitCursor = false;
            }
        }

        #endregion
    }

    /// <summary>Opens the drawing import in the Windows interface.</summary>
    internal static class DrawingImportPrompt
    {
        internal static void Show(IFlowsheet fs)
        {
            using (var wizard = new DrawingImportWizard(fs)) wizard.ShowDialog(Form.ActiveForm);
        }
    }
}
