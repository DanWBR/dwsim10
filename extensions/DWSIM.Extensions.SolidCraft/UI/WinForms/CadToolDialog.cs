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
            chkOpenInApp.Text = "Open in " + (r.Program ?? "") + " when finished";
            chkOpenInApp.Checked = r.OpenInApp;

            gbDrawing.Visible = r.IsDrawing;
            cbStandard.Items.AddRange(CadTools.Standards);
            cbStandard.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.Standards, s => string.Equals(s, o.DrawingStandard, StringComparison.OrdinalIgnoreCase)));
            tbOwner.Text = o.DrawingOwner ?? "";
            lblClearance.Visible = tbClearance.Visible = r.ShowsClearance;
            tbClearance.Text = o.LineClearance.ToString(CultureInfo.CurrentCulture);

            gbModel.Visible = gbRender.Visible = r.ShowsModelOptions;
            chkLabels.Checked = o.Labels;
            chkFlowAnimation.Checked = o.FlowAnimation;
            chkInsulation.Checked = o.Insulation;
            chkPlatforms.Checked = o.Platforms;
            chkInstruments.Checked = o.Instruments;
            chkDynamicReplay.Checked = o.DynamicReplay;
            tbRackHeight.Text = o.RackHeight.ToString(CultureInfo.CurrentCulture);
            tbItemGap.Text = o.ItemGap.ToString(CultureInfo.CurrentCulture);
            chkRender.Checked = o.Render;

            // a size chosen elsewhere (an edited settings file) is offered as it is
            var sizes = CadTools.RenderSizes.ToList();
            if (!sizes.Any(s => s.Width == o.RenderWidth && s.Height == o.RenderHeight))
                sizes.Add((o.RenderWidth, o.RenderHeight, o.RenderWidth + " x " + o.RenderHeight));
            cbResolution.Items.AddRange(sizes.Select(s => s.Text).ToArray());
            cbResolution.Tag = sizes;
            cbResolution.SelectedIndex = sizes.FindIndex(s => s.Width == o.RenderWidth && s.Height == o.RenderHeight);
            var samples = CadTools.RenderSamples.ToList();
            if (!samples.Any(s => s.Samples == o.RenderSamples)) samples.Add((o.RenderSamples, o.RenderSamples.ToString(CultureInfo.CurrentCulture)));
            cbSamples.Items.AddRange(samples.Select(s => s.Text).ToArray());
            cbSamples.Tag = samples;
            cbSamples.SelectedIndex = samples.FindIndex(s => s.Samples == o.RenderSamples);
            chkRender_CheckedChanged(chkRender, EventArgs.Empty);

            UpdateProgramStatus();
            ActiveControl = btnRun;
            this.ChangeDefaultFont();
        }

        void UpdateProgramStatus()
        {
            if (!Request.HasProgram) return;
            var exe = CADCraftSession.Exe(Request.ProgramExe);
            var found = Request.ProgramFoundIn(tbProgramFolder.Text);
            lblProgramStatus.ForeColor = found ? Color.DarkGreen : Color.Firebrick;
            lblProgramStatus.Text = found
                ? exe + " found in this folder."
                : string.IsNullOrWhiteSpace(tbProgramFolder.Text)
                    ? Request.Program + " was not found on this computer: choose the folder that holds " + exe + "."
                    : exe + " is not in this folder.";
        }

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

        private void chkRender_CheckedChanged(object sender, EventArgs e)
        {
            cbResolution.Enabled = cbSamples.Enabled = chkRender.Checked;
        }

        static bool TryRead(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private void btnRun_Click(object sender, EventArgs e)
        {
            var r = Request;
            var o = r.Options;
            r.ProgramFolder = tbProgramFolder.Text.Trim();
            r.Output = tbOutput.Text.Trim();
            if (r.OutputIsFile && r.Output.Length > 0 && !r.Output.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) r.Output += ".dxf";
            r.OpenInApp = chkOpenInApp.Checked;

            if (r.IsDrawing)
            {
                o.DrawingStandard = CadTools.Standards[Math.Max(0, cbStandard.SelectedIndex)];
                o.DrawingOwner = tbOwner.Text;
                if (r.ShowsClearance)
                {
                    if (!TryRead(tbClearance.Text, out var clearance)) { Problem("The line clearance is not a number."); return; }
                    o.LineClearance = Math.Max(1, Math.Min(15, clearance));
                }
            }
            if (r.ShowsModelOptions)
            {
                o.Labels = chkLabels.Checked;
                o.FlowAnimation = chkFlowAnimation.Checked;
                o.Insulation = chkInsulation.Checked;
                o.Platforms = chkPlatforms.Checked;
                o.Instruments = chkInstruments.Checked;
                o.DynamicReplay = chkDynamicReplay.Checked;
                if (!TryRead(tbRackHeight.Text, out var rack)) { Problem("The top of the pipe rack is not a number."); return; }
                if (!TryRead(tbItemGap.Text, out var gap)) { Problem("The gap between items is not a number."); return; }
                o.RackHeight = Math.Max(2.5, Math.Min(30, rack));
                o.ItemGap = Math.Max(0.5, Math.Min(20, gap));
                o.Render = chkRender.Checked;
                var sizes = (System.Collections.Generic.List<(int Width, int Height, string Text)>)cbResolution.Tag;
                var size = sizes[Math.Max(0, cbResolution.SelectedIndex)];
                o.RenderWidth = size.Width;
                o.RenderHeight = size.Height;
                var samples = (System.Collections.Generic.List<(int Samples, string Text)>)cbSamples.Tag;
                o.RenderSamples = samples[Math.Max(0, cbSamples.SelectedIndex)].Samples;
            }

            var problem = CadTools.Validate(r);
            if (problem != null) { Problem(problem); return; }
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

        /// <summary>Shows the window of a running tool over the main window.</summary>
        internal static void Watch(CadJob job)
        {
            var owner = Form.ActiveForm;
            if (owner == null || !owner.Visible || owner is CadToolDialog || owner is CadToolProgress)
                owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible && f.TopLevel && !(f is CadToolDialog) && !(f is CadToolProgress));
            new CadToolProgress(job).Show(owner);
        }
    }
}
