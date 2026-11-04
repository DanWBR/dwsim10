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
using System.IO;
using System.Windows.Forms;
using DWSIM.ExtensionMethods;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The window of a CAD tool running in the Windows interface: the step it is on, the time it has
    /// taken and a Cancel button; when it is over, what happened and a button to open the folder.
    /// Closing it while the tool runs leaves the tool running.
    /// </summary>
    public partial class CadToolProgress : Form
    {
        readonly CadJob Job;

        public CadToolProgress(CadJob job)
        {
            Job = job;
            InitializeComponent();
        }

        private void CadToolProgress_Load(object sender, EventArgs e)
        {
            Text = Job.Title;
            using (var icon = Job.Request.Icon())
            {
                if (icon != null)
                {
                    Icon = new Icon(icon);
                    ShowIcon = true;
                }
            }
            this.ChangeDefaultFont();
            UpdateState();
            PerformLayout();
            if (Owner != null)
                Location = new Point(Owner.Left + Math.Max(0, (Owner.Width - Width) / 2), Owner.Top + Math.Max(0, (Owner.Height - Height) / 2));
            tmrUpdate.Start();
        }

        private void tmrUpdate_Tick(object sender, EventArgs e) => UpdateState();

        void UpdateState()
        {
            lblElapsed.Text = (Job.Finished ? "Time: " : "Elapsed: ") + CadJob.Duration(Job.Elapsed);
            if (!Job.Finished)
            {
                lblStep.Text = Job.Step;
                return;
            }
            tmrUpdate.Stop();
            lblStep.Text = CadJob.Shorten(Job.Outcome);
            lblStep.ForeColor = Job.Failed ? Color.Firebrick : Job.Cancelled ? SystemColors.ControlText : Color.DarkGreen;
            pbProgress.Style = ProgressBarStyle.Continuous;
            pbProgress.Value = Job.Failed || Job.Cancelled ? 0 : 100;
            btnCancel.Visible = false;
            btnClose.Visible = true;
            btnOpenFolder.Visible = Directory.Exists(Job.OutputFolder ?? "");
            AcceptButton = CancelButton = btnClose;
            btnClose.Focus();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            btnCancel.Enabled = false;
            Job.Cancel();
            lblStep.Text = Job.Step;
        }

        private void btnClose_Click(object sender, EventArgs e) => Close();

        private void btnOpenFolder_Click(object sender, EventArgs e) => Job.OpenFolder();

        private void CadToolProgress_FormClosing(object sender, FormClosingEventArgs e) => tmrUpdate.Stop();
    }
}
