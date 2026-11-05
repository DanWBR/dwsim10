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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The data sheet import wizard in the Windows interface (see <see cref="DataSheetImportSession"/>).</summary>
    public partial class DataSheetImportWizard : Form
    {
        readonly DataSheetImportSession session;
        CancellationTokenSource reading;
        int page;
        bool loading;

        public DataSheetImportWizard(IFlowsheet fs)
        {
            session = new DataSheetImportSession(fs);
            InitializeComponent();
        }

        private void DataSheetImportWizard_Load(object sender, EventArgs e)
        {
            this.ChangeDefaultFont();
            lstSteps.Items.AddRange(DataSheetImportSession.Steps.Select((s, i) => (object)((i + 1) + ". " + s)).ToArray());
            colFieldKey.Items.AddRange(DataSheetImportSession.Keys.Cast<object>().ToArray());
            ShowPage(0);
        }

        #region pages

        void ShowPage(int p)
        {
            if (session.Reading == null) p = 0;
            if (page == 1 && p != 1) TakeValues();
            page = p;
            tlpDrawing.Visible = p == 0;
            tlpValues.Visible = p == 1;
            tlpEquipment.Visible = p == 2;
            lblStepTitle.Text = DataSheetImportSession.Steps[p];
            lblStepText.Text = DataSheetImportSession.StepTexts[p];
            loading = true;
            lstSteps.SelectedIndex = p;
            loading = false;
            btnBack.Enabled = p > 0;
            btnNext.Visible = p < 2;
            btnNext.Enabled = session.Reading != null && reading == null;
            if (p == 1) FillValues();
            if (p == 2) FillEquipment();
        }

        private void btnNext_Click(object sender, EventArgs e) => ShowPage(Math.Min(2, page + 1));

        private void btnBack_Click(object sender, EventArgs e) => ShowPage(Math.Max(0, page - 1));

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void lstSteps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loading || lstSteps.SelectedIndex < 0 || lstSteps.SelectedIndex == page) return;
            ShowPage(lstSteps.SelectedIndex);
        }

        private void DataSheetImportWizard_FormClosing(object sender, FormClosingEventArgs e) => reading?.Cancel();

        #endregion

        #region data sheet

        private void txtFile_TextChanged(object sender, EventArgs e) => btnRead.Enabled = File.Exists(txtFile.Text.Trim()) && reading == null;

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "Data sheets (*.pdf, *.png, *.jpg, *.dxf, *.dwg)|" + string.Join(";", DrawingImportClient.Extensions.Select(x => "*" + x)) + "|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                txtFile.Text = d.FileName;
                try
                {
                    var ext = Path.GetExtension(d.FileName).ToLowerInvariant();
                    if (ext == ".pdf" || ext == ".dxf" || ext == ".dwg") picDrawing.Image = null;
                    else using (var fs = File.OpenRead(d.FileName)) picDrawing.Image = Image.FromStream(fs);
                }
                catch (Exception) { picDrawing.Image = null; }
            }
        }

        private void lnkAssistantSettings_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var why = DrawingImportClient.OpenAssistantSettings(session.Flowsheet, true);
            if (why != null) MessageBox.Show(this, why, Text);
        }

        private async void btnRead_Click(object sender, EventArgs e)
        {
            session.File = txtFile.Text.Trim();
            reading = new CancellationTokenSource();
            var token = reading.Token;
            SetBusy(true);
            var started = DateTime.Now;
            try
            {
                await Task.Run(() => session.Read(token, s => BeginInvoke((Action)(() => lblReadStatus.Text = s))));
                lblReadStatus.Text = string.Format(CultureInfo.CurrentCulture, "A {0} data sheet ({1}), {2} values read in {3:0} s. Check them on the next page.",
                    session.Reading.EquipmentType.Replace("_", " "), session.Reading.Tag, session.Reading.Fields.Count, (DateTime.Now - started).TotalSeconds);
                Loaded();
            }
            catch (Exception ex) when (token.IsCancellationRequested || ex is OperationCanceledException)
            {
                lblReadStatus.Text = "Reading cancelled.";
            }
            catch (Exception ex)
            {
                lblReadStatus.Text = "The data sheet could not be read: " + ex.GetBaseException().Message;
            }
            finally
            {
                reading = null;
                SetBusy(false);
            }
        }

        void Loaded()
        {
            if (session.Reading.Overview != null)
                try { picDrawing.Image = Image.FromStream(new MemoryStream(session.Reading.Overview)); } catch (Exception) { }
            SetBusy(false);
        }

        void SetBusy(bool busy)
        {
            btnRead.Enabled = !busy && File.Exists(txtFile.Text.Trim());
            btnCancelRead.Enabled = busy;
            btnOpenReading.Enabled = !busy;
            btnSaveReading.Enabled = !busy && session.Reading != null;
            btnBrowse.Enabled = !busy;
            btnNext.Enabled = !busy && session.Reading != null;
            UseWaitCursor = busy;
        }

        private void btnCancelRead_Click(object sender, EventArgs e) => reading?.Cancel();

        private void btnOpenReading_Click(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "Data sheet readings (*.json)|*.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    session.Open(File.ReadAllText(d.FileName));
                    txtFile.Text = session.File;
                    lblReadStatus.Text = session.Reading.Fields.Count + " values in the saved reading.";
                    Loaded();
                }
                catch (Exception ex) { lblReadStatus.Text = "The reading could not be opened: " + ex.Message; }
            }
        }

        private void btnSaveReading_Click(object sender, EventArgs e)
        {
            if (session.Reading == null) return;
            if (page == 1) TakeValues();
            using (var d = new SaveFileDialog { Filter = "Data sheet readings (*.json)|*.json", FileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                session.SaveReading(d.FileName);
            }
        }

        #endregion

        #region values

        static string N(double? v) => v.HasValue ? v.Value.ToString("0.#####", CultureInfo.CurrentCulture) : "";

        static double? Parse(object v)
        {
            var s = Convert.ToString(v, CultureInfo.CurrentCulture)?.Trim() ?? "";
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var x)) return x;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ? x : (double?)null;
        }

        void FillValues()
        {
            var r = session.Reading;
            loading = true;
            if (!cbType.Items.Contains(r.EquipmentType)) cbType.Items.Add(r.EquipmentType);
            cbType.SelectedItem = r.EquipmentType;
            txtTag.Text = r.Tag;
            dgvFields.Rows.Clear();
            foreach (var f in r.Fields)
            {
                if (!colFieldKey.Items.Contains(f.Key)) colFieldKey.Items.Add(f.Key);
                dgvFields.Rows.Add(f.Include, f.Label, f.Value, N(f.Number), f.Unit, f.Key, f.Case, f.Side, f.Stream);
            }
            loading = false;
        }

        /// <summary>The values as the user left them.</summary>
        void TakeValues()
        {
            var r = session.Reading;
            if (r == null) return;
            r.EquipmentType = cbType.SelectedItem as string ?? r.EquipmentType;
            r.Tag = txtTag.Text.Trim();
            var fields = new List<SheetField>();
            foreach (DataGridViewRow row in dgvFields.Rows)
            {
                if (row.IsNewRow) continue;
                string S(int i) => Convert.ToString(row.Cells[i].Value) ?? "";
                fields.Add(new SheetField
                {
                    Include = row.Cells[0].Value is bool b ? b : true,
                    Label = S(1), Value = S(2), Number = Parse(row.Cells[3].Value), Unit = S(4), Key = S(5), Case = S(6), Side = S(7).ToLowerInvariant(), Stream = S(8),
                });
            }
            r.Fields = fields;
            r.Cases = r.Fields.Where(f => f.Key.Length > 0 && !f.Key.StartsWith("design")).Select(f => f.Case).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (session.Case.Length > 0 && !r.Cases.Contains(session.Case)) session.Case = DataSheetImport.DefaultCase(r);
        }

        private void SheetField_Changed(object sender, EventArgs e) { if (!loading) TakeValues(); }

        private void dgvFields_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvFields.IsCurrentCellDirty && (dgvFields.CurrentCell.ColumnIndex == 0 || dgvFields.CurrentCell.ColumnIndex == 5))
                dgvFields.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgvFields_DataError(object sender, DataGridViewDataErrorEventArgs e) => e.ThrowException = false;

        #endregion

        #region equipment

        void FillEquipment()
        {
            loading = true;
            var targets = session.Targets();
            cbTarget.Items.Clear();
            cbTarget.Items.AddRange(targets.Cast<object>().ToArray());
            var current = session.Target?.GraphicObject.Tag ?? DataSheetImportSession.NewObject;
            cbTarget.SelectedItem = targets.Contains(current) ? current : targets.FirstOrDefault();
            cbCase.Items.Clear();
            cbCase.Items.Add("");
            cbCase.Items.AddRange(session.Reading.Cases.Cast<object>().ToArray());
            cbCase.SelectedItem = session.Case;
            loading = false;
            Propose();
        }

        private void Target_Changed(object sender, EventArgs e)
        {
            if (loading) return;
            session.Choose(cbTarget.SelectedItem as string ?? DataSheetImportSession.NewObject);
            session.Case = cbCase.SelectedItem as string ?? "";
            Propose();
        }

        void Propose()
        {
            session.Propose();
            loading = true;
            dgvChanges.Rows.Clear();
            foreach (var c in session.Changes)
            {
                var k = dgvChanges.Rows.Add(c.Use, c.What, c.Current, c.New);
                dgvChanges.Rows[k].Tag = c;
            }
            loading = false;
            Fill(session.Issues);
            var supported = DataSheetImport.Supported(session.Reading.EquipmentType);
            btnApply.Enabled = supported && (session.Target == null || session.Changes.Count > 0);
            btnApply.Text = session.Target == null ? "Make the new equipment" : "Make the ticked changes";
            lblSummary.Text = !supported ? "This kind of equipment is not set up from a data sheet yet." :
                session.Target == null ? "A new " + session.Reading.EquipmentType.Replace("_", " ") + " with its inlet and outlet streams, set up from the sheet." :
                session.Changes.Count(c => c.Use) + " of " + session.Changes.Count + " changes ticked for " + session.Target.GraphicObject.Tag + ".";
        }

        void Fill(IEnumerable<ImportIssue> issues)
        {
            dgvChecks.Rows.Clear();
            foreach (var i in issues)
            {
                var k = dgvChecks.Rows.Add(i.Level == ImportIssue.Levels.Error ? "Error" : i.Level == ImportIssue.Levels.Warning ? "Warning" : "Note", i.Subject, i.Text);
                dgvChecks.Rows[k].DefaultCellStyle.ForeColor = i.Level == ImportIssue.Levels.Error ? Color.DarkRed : i.Level == ImportIssue.Levels.Warning ? Color.DarkGoldenrod : SystemColors.ControlText;
            }
        }

        private void dgvChanges_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvChanges.IsCurrentCellDirty && dgvChanges.CurrentCell.ColumnIndex == 0) dgvChanges.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgvChanges_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (loading || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            if (dgvChanges.Rows[e.RowIndex].Tag is SheetChange c) c.Use = dgvChanges.Rows[e.RowIndex].Cells[0].Value is bool b && b;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            UseWaitCursor = true;
            btnApply.Enabled = false;
            lblSummary.Text = "Making the changes and solving the flowsheet...";
            lblSummary.Refresh();
            try
            {
                session.Apply();
                lblSummary.Text = session.Results.FirstOrDefault()?.Text + (session.Results.Any(i => i.Level == ImportIssue.Levels.Warning) ? "; the outlet differs from the sheet where marked below." : ".");
                Fill(session.Results.Concat(session.Issues.Where(i => i.Level != ImportIssue.Levels.Info)));
                btnClose.Text = "Finish";
                loading = true;
                var targets = session.Targets();
                cbTarget.Items.Clear();
                cbTarget.Items.AddRange(targets.Cast<object>().ToArray());
                cbTarget.SelectedItem = session.Target.GraphicObject.Tag;
                loading = false;
            }
            catch (Exception ex) { lblSummary.Text = "The changes could not be made: " + ex.GetBaseException().Message; }
            finally
            {
                UseWaitCursor = false;
                btnApply.Enabled = true;
                btnApply.Text = "Make the ticked changes";
            }
        }

        #endregion
    }

    /// <summary>Opens the data sheet import in the Windows interface.</summary>
    internal static class DataSheetImportPrompt
    {
        internal static void Show(IFlowsheet fs)
        {
            using (var wizard = new DataSheetImportWizard(fs)) wizard.ShowDialog(Form.ActiveForm);
        }
    }
}
