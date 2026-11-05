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
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The isometric import wizard in the Windows interface (see <see cref="IsometricImportSession"/>).</summary>
    public partial class IsometricImportWizard : Form
    {
        readonly IsometricImportSession session;
        CancellationTokenSource reading;
        int page;
        bool loading;

        public IsometricImportWizard(IFlowsheet fs)
        {
            session = new IsometricImportSession(fs);
            InitializeComponent();
        }

        private void IsometricImportWizard_Load(object sender, EventArgs e)
        {
            this.ChangeDefaultFont();
            lstSteps.Items.AddRange(IsometricImportSession.Steps.Select((s, i) => (object)((i + 1) + ". " + s)).ToArray());
            ShowPage(0);
        }

        #region pages

        void ShowPage(int p)
        {
            if (session.Reading == null) p = 0;
            if (page == 1 && p != 1) TakeRoute();
            page = p;
            tlpDrawing.Visible = p == 0;
            splitRoute.Visible = p == 1;
            tlpPipe.Visible = p == 2;
            lblStepTitle.Text = IsometricImportSession.Steps[p];
            lblStepText.Text = IsometricImportSession.StepTexts[p];
            loading = true;
            lstSteps.SelectedIndex = p;
            loading = false;
            btnBack.Enabled = p > 0;
            btnNext.Visible = p < 2;
            btnNext.Enabled = session.Reading != null && reading == null;
            if (p == 1) FillRoute();
            if (p == 2) FillPipe();
        }

        private void btnNext_Click(object sender, EventArgs e) => ShowPage(Math.Min(2, page + 1));

        private void btnBack_Click(object sender, EventArgs e) => ShowPage(Math.Max(0, page - 1));

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void lstSteps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loading || lstSteps.SelectedIndex < 0 || lstSteps.SelectedIndex == page) return;
            ShowPage(lstSteps.SelectedIndex);
        }

        private void IsometricImportWizard_FormClosing(object sender, FormClosingEventArgs e) => reading?.Cancel();

        #endregion

        #region isometric

        private void txtFile_TextChanged(object sender, EventArgs e) => btnRead.Enabled = File.Exists(txtFile.Text.Trim()) && reading == null;

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "Isometrics (*.png, *.jpg, *.pdf, *.dxf, *.dwg)|" + string.Join(";", DrawingImportClient.Extensions.Select(x => "*" + x)) + "|All files|*.*" })
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
                if (ext == ".pdf" || ext == ".dxf" || ext == ".dwg") { picDrawing.Image = null; return; }
                using (var fs = File.OpenRead(file)) picDrawing.Image = Image.FromStream(fs);
            }
            catch (Exception) { picDrawing.Image = null; }
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
                lblReadStatus.Text = string.Format(CultureInfo.CurrentCulture, "{0} legs read in {1:0} s. Check them on the next page.",
                    session.Reading.Legs.Count, (DateTime.Now - started).TotalSeconds);
                Loaded();
            }
            catch (Exception ex) when (token.IsCancellationRequested || ex is OperationCanceledException)
            {
                lblReadStatus.Text = "Reading cancelled.";
            }
            catch (Exception ex)
            {
                lblReadStatus.Text = "The isometric could not be read: " + ex.GetBaseException().Message;
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
            using (var d = new OpenFileDialog { Filter = "Isometric readings (*.json)|*.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    session.Open(File.ReadAllText(d.FileName));
                    txtFile.Text = session.File;
                    lblReadStatus.Text = session.Reading.Legs.Count + " legs in the saved reading.";
                    Loaded();
                }
                catch (Exception ex) { lblReadStatus.Text = "The reading could not be opened: " + ex.Message; }
            }
        }

        private void btnSaveReading_Click(object sender, EventArgs e)
        {
            if (session.Reading == null) return;
            if (page == 1) TakeRoute();
            using (var d = new SaveFileDialog { Filter = "Isometric readings (*.json)|*.json", FileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                session.SaveReading(d.FileName);
            }
        }

        #endregion

        #region route

        static string N(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.CurrentCulture) : "";

        static double? Parse(object v)
        {
            var s = Convert.ToString(v, CultureInfo.CurrentCulture)?.Trim() ?? "";
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var x)) return x;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ? x : (double?)null;
        }

        void FillRoute()
        {
            var r = session.Reading;
            loading = true;
            txtLine.Text = r.LineNumber;
            txtSize.Text = r.Size;
            txtSchedule.Text = r.Schedule;
            txtOd.Text = N(r.OuterDiameter);
            txtWall.Text = N(r.Wall);
            txtMaterial.Text = r.Material;
            lblEndsValue.Text = (r.From.Length > 0 ? r.From : "?") + "  >  " + (r.To.Length > 0 ? r.To : "?");
            dgvLegs.Rows.Clear();
            for (var i = 0; i < r.Legs.Count; i++)
            {
                var l = r.Legs[i];
                var dir = colLegDirection.Items.Cast<string>().FirstOrDefault(d => string.Equals(d, l.Direction, StringComparison.OrdinalIgnoreCase));
                if (dir == null && l.Direction.Length > 0) { colLegDirection.Items.Add(l.Direction); dir = l.Direction; }
                dgvLegs.Rows.Add(l.Include, i + 1, dir, N(l.Length), N(l.Rise), string.Join(", ", l.Fittings));
            }
            loading = false;
            picRoute.Invalidate();
        }

        /// <summary>The route and the line data as the user left them.</summary>
        void TakeRoute()
        {
            var r = session.Reading;
            if (r == null) return;
            r.LineNumber = txtLine.Text.Trim();
            r.Size = txtSize.Text.Trim();
            r.Schedule = txtSchedule.Text.Trim();
            r.OuterDiameter = Parse(txtOd.Text);
            r.Wall = Parse(txtWall.Text);
            r.Material = txtMaterial.Text.Trim();
            var legs = new List<IsoLeg>();
            foreach (DataGridViewRow row in dgvLegs.Rows)
            {
                if (row.IsNewRow) continue;
                legs.Add(new IsoLeg
                {
                    Include = row.Cells[0].Value is bool b ? b : true,
                    Direction = Convert.ToString(row.Cells[2].Value) ?? "",
                    Length = Parse(row.Cells[3].Value),
                    Rise = Parse(row.Cells[4].Value),
                    Fittings = (Convert.ToString(row.Cells[5].Value) ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
                });
            }
            r.Legs = legs;
        }

        private void LineField_Leave(object sender, EventArgs e) { if (!loading) TakeRoute(); }

        private void dgvLegs_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (loading) return;
            TakeRoute();
            for (var i = 0; i < dgvLegs.Rows.Count; i++) if (!dgvLegs.Rows[i].IsNewRow) dgvLegs.Rows[i].Cells[1].Value = i + 1;
            picRoute.Invalidate();
        }

        private void dgvLegs_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // the check box and the direction take effect at once
            if (dgvLegs.IsCurrentCellDirty && (dgvLegs.CurrentCell.ColumnIndex == 0 || dgvLegs.CurrentCell.ColumnIndex == 2))
                dgvLegs.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgvLegs_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
        {
            TakeRoute();
            picRoute.Invalidate();
        }

        private void picRoute_Resize(object sender, EventArgs e) => picRoute.Invalidate();

        /// <summary>The route in isometric projection, scaled to the box: the legs, the start and the end, the fittings as dots.</summary>
        private void picRoute_Paint(object sender, PaintEventArgs e)
        {
            var r = session.Reading;
            if (r == null) return;
            var points = IsometricImport.Route(r).Select(IsometricImport.Project).ToList();
            if (points.Count < 2) return;
            double x0 = points.Min(p => p[0]), x1 = points.Max(p => p[0]), y0 = points.Min(p => p[1]), y1 = points.Max(p => p[1]);
            var margin = 30f;
            var scale = Math.Min((picRoute.Width - 2 * margin) / Math.Max(1, x1 - x0), (picRoute.Height - 2 * margin) / Math.Max(1, y1 - y0));
            PointF At(double[] p) => new PointF((float)(margin + (p[0] - x0) * scale + (picRoute.Width - 2 * margin - (x1 - x0) * scale) / 2),
                (float)(picRoute.Height - margin - (p[1] - y0) * scale - (picRoute.Height - 2 * margin - (y1 - y0) * scale) / 2));
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(40, 40, 40), 2.5f))
            using (var faint = new Pen(Color.FromArgb(200, 200, 200), 1f) { DashStyle = DashStyle.Dot })
            using (var font = new Font(Font.FontFamily, 8f))
            {
                // the isometric axes through the start
                var o = At(points[0]);
                foreach (var axis in new[] { new[] { 1000.0, 0, 0 }, new[] { 0, 1000.0, 0 }, new[] { 0, 0, 1000.0 } })
                {
                    var a = IsometricImport.Project(axis);
                    g.DrawLine(faint, o, new PointF(o.X + (float)(a[0] * scale), o.Y - (float)(a[1] * scale)));
                }
                var legs = r.Legs.Where(l => l.Include).ToList();
                for (var i = 1; i < points.Count; i++)
                {
                    var a = At(points[i - 1]);
                    var b = At(points[i]);
                    g.DrawLine(pen, a, b);
                    var leg = legs[i - 1];
                    var label = (i).ToString(CultureInfo.CurrentCulture) + (leg.Length.HasValue ? ": " + leg.Length.Value.ToString("0", CultureInfo.CurrentCulture) : "");
                    g.DrawString(label, font, Brushes.SteelBlue, (a.X + b.X) / 2 + 4, (a.Y + b.Y) / 2 - 14);
                    if (leg.Fittings.Any(f => IsometricImport.PipeFitting(f) != null)) g.FillEllipse(Brushes.DarkOrange, b.X - 4, b.Y - 4, 8, 8);
                }
                var s = At(points[0]);
                var t = At(points[points.Count - 1]);
                g.FillEllipse(Brushes.SeaGreen, s.X - 5, s.Y - 5, 10, 10);
                g.FillEllipse(Brushes.IndianRed, t.X - 5, t.Y - 5, 10, 10);
                g.DrawString(r.From.Length > 0 ? r.From : "start", font, Brushes.SeaGreen, s.X + 6, s.Y + 2);
                g.DrawString(r.To.Length > 0 ? r.To : "end", font, Brushes.IndianRed, t.X + 6, t.Y + 2);
                g.DrawString("N", font, Brushes.Gray, 8, 8);
                g.DrawLine(Pens.Gray, 10, 40, 30, 28);
            }
        }

        #endregion

        #region pipe segment

        void FillPipe()
        {
            session.Propose();
            dgvChecks.Rows.Clear();
            foreach (var i in session.Issues)
            {
                var k = dgvChecks.Rows.Add(i.Level == ImportIssue.Levels.Error ? "Error" : i.Level == ImportIssue.Levels.Warning ? "Warning" : "Note", i.Subject, i.Text);
                dgvChecks.Rows[k].DefaultCellStyle.ForeColor = i.Level == ImportIssue.Levels.Error ? Color.DarkRed : i.Level == ImportIssue.Levels.Warning ? Color.DarkGoldenrod : SystemColors.ControlText;
            }
            var targets = session.Targets();
            var keep = session.Applied?.GraphicObject.Tag ?? cbTarget.SelectedItem as string;
            cbTarget.Items.Clear();
            cbTarget.Items.AddRange(targets.Cast<object>().ToArray());
            cbTarget.SelectedItem = keep != null && targets.Contains(keep) ? keep : targets[0];
            dgvSections.Rows.Clear();
            foreach (var s in session.Sections)
                dgvSections.Rows.Add(s.TipoSegmento == "Tubulaosimples" ? "Straight pipe" : s.TipoSegmento, s.Quantidade,
                    s.TipoSegmento == "Tubulaosimples" ? s.Comprimento.ToString("0.000", CultureInfo.CurrentCulture) : "",
                    s.TipoSegmento == "Tubulaosimples" ? s.Elevacao.ToString("0.000", CultureInfo.CurrentCulture) : "",
                    s.DE.ToString("0.###", CultureInfo.CurrentCulture) + " / " + s.DI.ToString("0.###", CultureInfo.CurrentCulture), s.Material);
            btnApply.Enabled = session.Sections.Count > 0 && !session.Issues.Any(i => i.Level == ImportIssue.Levels.Error);
            lblSummary.Text = session.Sections.Count == 0 ? "No sections: correct the route first." : IsometricImport.Summary(session.Sections) +
                (session.Issues.Any(i => i.Level == ImportIssue.Levels.Error) ? ". Correct the errors above first." : ".");
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            try
            {
                var obj = session.Apply(cbTarget.SelectedItem as string ?? IsometricImportSession.NewPipe);
                FillPipe();
                lblSummary.Text = "The hydraulic profile of " + obj.GraphicObject.Tag + " now has " + session.Sections.Count + " sections: " + IsometricImport.Summary(session.Sections) + ".";
                btnClose.Text = "Finish";
            }
            catch (Exception ex) { lblSummary.Text = "The profile could not be made: " + ex.GetBaseException().Message; }
        }

        #endregion
    }

    /// <summary>Opens the isometric import in the Windows interface.</summary>
    internal static class IsometricImportPrompt
    {
        internal static void Show(IFlowsheet fs)
        {
            using (var wizard = new IsometricImportWizard(fs)) wizard.ShowDialog(Form.ActiveForm);
        }
    }
}
