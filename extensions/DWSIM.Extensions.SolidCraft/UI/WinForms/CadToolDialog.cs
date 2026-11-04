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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DWSIM.ExtensionMethods;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The dialog of a CAD Tools item in the Windows interface (see <see cref="CadToolRequest"/>).</summary>
    public partial class CadToolDialog : Form
    {
        readonly CadToolRequest Request;

        public CadToolDialog(CadToolRequest request)
        {
            Request = request;
            InitializeComponent();
        }

        /// <summary>The sample counts offered, with a count chosen elsewhere (an edited settings file) added.</summary>
        System.Collections.Generic.List<(int Samples, string Text)> samples;

        private void CadToolDialog_Load(object sender, EventArgs e)
        {
            var r = Request;
            var o = r.Options;
            Text = r.Title;
            using (var icon = r.Icon())
            {
                if (icon != null)
                {
                    Icon = new Icon(icon);
                    ShowIcon = true;
                }
            }
            lblDescription.Text = r.Description;
            btnRun.Text = r.RunText;

            gbProgram.Visible = r.HasProgram;
            gbProgram.Text = r.Program ?? "";
            tbProgramFolder.Text = r.ProgramFolder ?? "";

            lblOutput.Text = r.OutputLabel;
            tbOutput.Text = r.Output ?? "";
            chkOpenInApp.Visible = r.ShowsOpenInApp;
            chkOpenInApp.Text = r.OpenInAppText;
            chkOpenInApp.Checked = r.OpenInApp;

            gbDrawing.Visible = gbTitle.Visible = r.IsDrawing;
            cbStandard.Items.AddRange(CadTools.Standards);
            cbStatus.Items.AddRange(CadTools.Statuses);
            lblClearance.Visible = tbClearance.Visible = r.ShowsClearance;
            chkDrawingSet.Visible = r.ShowsDrawingSet;

            gbItems.Visible = r.ShowsItems;
            if (r.ShowsItems)
            {
                gbItems.Text = r.Tool == CadTool.PipingIsometrics ? "Lines to draw" : r.Tool == CadTool.DataSheets ? "Data sheets to write" : "Items to draw";
                foreach (var item in r.Items) clbItems.Items.Add(item, item.Checked);
                clbItems.ItemCheck += (s, args) => ResetOverwrite();
            }

            gbDesign.Visible = r.Design != null;
            lblDesign.Text = r.DesignText;
            lblDesign.ForeColor = r.DesignChanged ? Color.DarkOrange : SystemColors.ControlText;
            chkReadMoves.Checked = r.ReadMoves;

            gbModel.Visible = gbRender.Visible = r.ShowsModelOptions;
            lblRackHeight.Text = "Top of the pipe rack (" + r.LengthUnits + ")";
            lblItemGap.Text = "Smallest gap between items (" + r.LengthUnits + ")";
            cbResolution.Items.AddRange(CadTools.RenderSizes.Select(s => s.Text).ToArray());
            samples = CadTools.RenderSamples.ToList();
            if (!samples.Any(s => s.Samples == o.RenderSamples)) samples.Add((o.RenderSamples, o.RenderSamples.ToString(CultureInfo.CurrentCulture)));
            cbSamples.Items.AddRange(samples.Select(s => s.Text).ToArray());
            cbView.Items.AddRange(CadTools.RenderViews.Select(v => v.Text).ToArray());

            ShowOptions(o);
            UpdateProgramStatus();
            UpdatePreview();
            ActiveControl = btnRun;
            this.ChangeDefaultFont();
        }

        /// <summary>Puts the options in the controls (on opening, and for the Defaults button).</summary>
        void ShowOptions(ExportOptions o)
        {
            var r = Request;
            cbStandard.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.Standards, s => string.Equals(s, o.DrawingStandard, StringComparison.OrdinalIgnoreCase)));
            tbOwner.Text = o.DrawingOwner ?? "";
            tbClearance.Text = o.LineClearance.ToString(CultureInfo.CurrentCulture);
            chkDwg.Checked = o.SaveDwg;
            chkPdf.Checked = o.SavePdf;
            chkPng.Checked = o.SavePng;
            chkDrawingSet.Checked = o.DrawingSet;

            tbProjectNumber.Text = o.ProjectNumber ?? "";
            tbDepartment.Text = o.Department ?? "";
            tbPreparedBy.Text = o.PreparedBy ?? "";
            tbArea.Text = o.Area ?? "";
            tbCheckedBy.Text = o.CheckedBy ?? "";
            tbRevision.Text = o.Revision ?? "";
            tbApprovedBy.Text = o.ApprovedBy ?? "";
            cbStatus.Text = o.Status ?? "";
            tbRevisionText.Text = o.RevisionText ?? "";
            tbLogo.Text = o.Logo ?? "";

            chkLabels.Checked = o.Labels;
            chkFlowAnimation.Checked = o.FlowAnimation;
            chkInsulation.Checked = o.Insulation;
            chkPlatforms.Checked = o.Platforms;
            chkInstruments.Checked = o.Instruments;
            chkDynamicReplay.Checked = o.DynamicReplay;
            tbRackHeight.Text = r.ToLength(o.RackHeight).ToString("0.###", CultureInfo.CurrentCulture);
            tbItemGap.Text = r.ToLength(o.ItemGap).ToString("0.###", CultureInfo.CurrentCulture);
            chkRender.Checked = o.Render;
            cbResolution.Text = CadTools.SizeText(o.RenderWidth, o.RenderHeight);
            cbSamples.SelectedIndex = Math.Max(0, samples.FindIndex(s => s.Samples == o.RenderSamples));
            cbView.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.RenderViews, v => v.Id == o.RenderView));
            chkRender_CheckedChanged(chkRender, EventArgs.Empty);
            // the boxes that take typing keep no text selected
            cbResolution.SelectionLength = cbStatus.SelectionLength = 0;
        }

        void UpdateProgramStatus()
        {
            if (!Request.HasProgram) return;
            lblProgramStatus.Text = Request.ProgramStatus(tbProgramFolder.Text, out var level);
            lblProgramStatus.ForeColor = level == 0 ? Color.DarkGreen : level == 1 ? Color.DarkOrange : Color.Firebrick;
        }

        /// <summary>The preview needs the design the 3D model saved last, in the output folder.</summary>
        void UpdatePreview() => btnPreview.Enabled = chkRender.Checked && Request.CanPreview(tbOutput.Text);

        private void tbProgramFolder_TextChanged(object sender, EventArgs e) => UpdateProgramStatus();

        private void btnProgramFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "The folder that holds " + CADCraftSession.Exe(Request.ProgramExe), ShowNewFolderButton = false })
            {
                if (Directory.Exists(tbProgramFolder.Text)) dialog.SelectedPath = tbProgramFolder.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) tbProgramFolder.Text = dialog.SelectedPath;
            }
        }

        private void btnOutput_Click(object sender, EventArgs e)
        {
            if (Request.OutputIsFile)
            {
                using (var dialog = new SaveFileDialog { Filter = "DXF drawing (DWG, PDF and PNG are saved beside it)|*.dxf", DefaultExt = "dxf", OverwritePrompt = false })
                {
                    try
                    {
                        dialog.FileName = Path.GetFileName(tbOutput.Text);
                        var folder = Path.GetDirectoryName(tbOutput.Text);
                        if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
                    }
                    catch (Exception) { }
                    if (dialog.ShowDialog(this) == DialogResult.OK) tbOutput.Text = dialog.FileName;
                }
            }
            else
            {
                using (var dialog = new FolderBrowserDialog { Description = Request.OutputLabel, ShowNewFolderButton = true })
                {
                    if (Directory.Exists(tbOutput.Text)) dialog.SelectedPath = tbOutput.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) tbOutput.Text = dialog.SelectedPath;
                }
            }
        }

        private void btnLogo_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Drawings (*.dxf, *.dwg)|*.dxf;*.dwg", Title = "The logo for the title block" })
            {
                try
                {
                    var folder = Path.GetDirectoryName(tbLogo.Text);
                    if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
                }
                catch (Exception) { }
                if (dialog.ShowDialog(this) == DialogResult.OK) tbLogo.Text = dialog.FileName;
            }
        }

        private void btnAllItems_Click(object sender, EventArgs e) => CheckAll(true);

        private void btnNoItems_Click(object sender, EventArgs e) => CheckAll(false);

        void CheckAll(bool value)
        {
            for (var i = 0; i < clbItems.Items.Count; i++) clbItems.SetItemChecked(i, value);
            ResetOverwrite();
        }

        /// <summary>Another output or choice of items: the warning about the old files is worked out again.</summary>
        void ResetOverwrite()
        {
            if (!gbOverwrite.Visible) return;
            gbOverwrite.Visible = false;
            btnRun.Text = Request.RunText;
        }

        private void tbOutput_TextChanged(object sender, EventArgs e)
        {
            ResetOverwrite();
            UpdatePreview();
        }

        private void chkRender_CheckedChanged(object sender, EventArgs e)
        {
            cbResolution.Enabled = cbSamples.Enabled = cbView.Enabled = chkRender.Checked;
            UpdateRenderTime();
            UpdatePreview();
        }

        private void cbRender_SelectedIndexChanged(object sender, EventArgs e) => UpdateRenderTime();

        /// <summary>The estimate of the render time for the size and samples chosen.</summary>
        void UpdateRenderTime()
        {
            if (samples == null || cbSamples.SelectedIndex < 0 || !CadTools.TryReadSize(cbResolution.Text, out var width, out var height))
            {
                lblRenderTime.Text = "";
                return;
            }
            var n = samples[cbSamples.SelectedIndex].Samples;
            lblRenderTime.Enabled = chkRender.Checked;
            lblRenderTime.ForeColor = CadTools.RenderSeconds(width, height, n) > CadTools.LongRenderSeconds ? Color.DarkOrange : SystemColors.GrayText;
            lblRenderTime.Text = "Render time: " + CadTools.RenderTimeText(width, height, n) + ", longer on a slower one.";
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            var problem = Collect();
            if (problem != null) Problem(problem);
            else CadTools.Preview(Request);
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            ShowOptions(new ExportOptions { CADCraftPath = Request.Options.CADCraftPath });
            if (!string.IsNullOrEmpty(Request.DefaultOutput)) tbOutput.Text = Request.DefaultOutput;
        }

        static bool TryRead(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>Takes what the controls hold into the request and its options; returns the problem, or null.</summary>
        string Collect()
        {
            var r = Request;
            var o = r.Options;
            r.ProgramFolder = tbProgramFolder.Text.Trim();
            r.Output = tbOutput.Text.Trim();
            if (r.OutputIsFile && r.Output.Length > 0 && !r.Output.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) r.Output += ".dxf";
            r.OpenInApp = chkOpenInApp.Checked;

            if (r.ShowsItems)
                for (var i = 0; i < clbItems.Items.Count; i++) ((CadToolItem)clbItems.Items[i]).Checked = clbItems.GetItemChecked(i);
            r.ReadMoves = r.Design != null && chkReadMoves.Checked;

            if (r.IsDrawing)
            {
                o.SaveDwg = chkDwg.Checked;
                o.SavePdf = chkPdf.Checked;
                o.SavePng = chkPng.Checked;
                if (r.ShowsDrawingSet) o.DrawingSet = chkDrawingSet.Checked;
                o.DrawingStandard = CadTools.Standards[Math.Max(0, cbStandard.SelectedIndex)];
                o.DrawingOwner = tbOwner.Text;
                if (r.ShowsClearance)
                {
                    if (!TryRead(tbClearance.Text, out var clearance)) return "The line clearance is not a number.";
                    o.LineClearance = Math.Max(1, Math.Min(15, clearance));
                }
                o.ProjectNumber = tbProjectNumber.Text.Trim();
                o.Department = tbDepartment.Text.Trim();
                o.PreparedBy = tbPreparedBy.Text.Trim();
                o.Area = tbArea.Text.Trim();
                o.CheckedBy = tbCheckedBy.Text.Trim();
                o.Revision = tbRevision.Text.Trim();
                o.ApprovedBy = tbApprovedBy.Text.Trim();
                o.Status = cbStatus.Text.Trim();
                o.RevisionText = tbRevisionText.Text.Trim();
                o.Logo = tbLogo.Text.Trim();
                if (o.Logo.Length > 0 && !File.Exists(o.Logo)) return "The logo was not found: " + o.Logo;
            }
            if (r.ShowsModelOptions)
            {
                o.Labels = chkLabels.Checked;
                o.FlowAnimation = chkFlowAnimation.Checked;
                o.Insulation = chkInsulation.Checked;
                o.Platforms = chkPlatforms.Checked;
                o.Instruments = chkInstruments.Checked;
                o.DynamicReplay = chkDynamicReplay.Checked;
                if (!TryRead(tbRackHeight.Text, out var rack)) return "The top of the pipe rack is not a number.";
                if (!TryRead(tbItemGap.Text, out var gap)) return "The gap between items is not a number.";
                o.RackHeight = Math.Max(2.5, Math.Min(30, r.FromLength(rack)));
                o.ItemGap = Math.Max(0.5, Math.Min(20, r.FromLength(gap)));
                o.Render = chkRender.Checked;
                if (!CadTools.TryReadSize(cbResolution.Text, out var width, out var height)) return "The resolution is the width and the height in pixels, such as 2000 x 1250.";
                o.RenderWidth = width;
                o.RenderHeight = height;
                o.RenderSamples = samples[Math.Max(0, cbSamples.SelectedIndex)].Samples;
                o.RenderView = CadTools.RenderViews[Math.Max(0, cbView.SelectedIndex)].Id;
            }
            return null;
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            var r = Request;
            var problem = Collect() ?? CadTools.Validate(r);
            if (problem != null) { Problem(problem); return; }

            // files of an earlier export: say so first, and go on only on the second click
            var existing = CadTools.ExistingOutputs(r);
            if (existing.Count > 0 && !gbOverwrite.Visible)
            {
                lblOverwrite.Text = CadTools.OverwriteWarning(existing);
                gbOverwrite.Visible = true;
                btnRun.Text = "Replace";
                return;
            }
            r.KeepOldCopies = existing.Count > 0 && chkKeepCopy.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        void Problem(string text) => MessageBox.Show(this, text, Request.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Shows the dialog of a CAD tool and runs the tool when the user confirms it.</summary>
    internal static class CadToolPrompt
    {
        internal static void Ask(CadToolRequest request, Action<CadToolRequest> run)
        {
            using (var dialog = new CadToolDialog(request))
            {
                if (dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK) run(request);
            }
        }

        /// <summary>Shows the window of a running tool over the window in front (a tool's dialog for its preview, else the main window).</summary>
        internal static void Watch(CadJob job)
        {
            var owner = Form.ActiveForm;
            if (owner == null || !owner.Visible || owner is CadToolProgress)
                owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible && f.TopLevel && !(f is CadToolDialog) && !(f is CadToolProgress));
            new CadToolProgress(job).Show(owner);
        }
    }
}
