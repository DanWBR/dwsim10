namespace DWSIM.Extensions.SolidCraft
{
    partial class CadToolProgress
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.lblStep = new System.Windows.Forms.Label();
            this.pbProgress = new System.Windows.Forms.ProgressBar();
            this.lblElapsed = new System.Windows.Forms.Label();
            this.flpButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.tmrUpdate = new System.Windows.Forms.Timer(this.components);
            this.tlpRoot.SuspendLayout();
            this.flpButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.AutoSize = true;
            this.tlpRoot.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.lblStep, 0, 0);
            this.tlpRoot.Controls.Add(this.pbProgress, 0, 1);
            this.tlpRoot.Controls.Add(this.lblElapsed, 0, 2);
            this.tlpRoot.Controls.Add(this.flpButtons, 0, 3);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(8, 8);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(0);
            this.tlpRoot.RowCount = 4;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.Size = new System.Drawing.Size(466, 125);
            this.tlpRoot.TabIndex = 0;
            //
            // lblStep
            //
            this.lblStep.AutoSize = true;
            this.lblStep.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStep.Location = new System.Drawing.Point(3, 3);
            this.lblStep.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.lblStep.MaximumSize = new System.Drawing.Size(460, 0);
            this.lblStep.MinimumSize = new System.Drawing.Size(460, 30);
            this.lblStep.Name = "lblStep";
            this.lblStep.Size = new System.Drawing.Size(460, 30);
            this.lblStep.TabIndex = 0;
            this.lblStep.Text = "Preparing...";
            //
            // pbProgress
            //
            this.pbProgress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pbProgress.Location = new System.Drawing.Point(3, 42);
            this.pbProgress.MarqueeAnimationSpeed = 30;
            this.pbProgress.Name = "pbProgress";
            this.pbProgress.Size = new System.Drawing.Size(460, 18);
            this.pbProgress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.pbProgress.TabIndex = 1;
            //
            // lblElapsed
            //
            this.lblElapsed.AutoSize = true;
            this.lblElapsed.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblElapsed.Location = new System.Drawing.Point(3, 66);
            this.lblElapsed.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.lblElapsed.Name = "lblElapsed";
            this.lblElapsed.Size = new System.Drawing.Size(60, 13);
            this.lblElapsed.TabIndex = 2;
            this.lblElapsed.Text = "Elapsed: 0 s";
            //
            // flpButtons
            //
            this.flpButtons.AutoSize = true;
            this.flpButtons.Controls.Add(this.btnCancel);
            this.flpButtons.Controls.Add(this.btnClose);
            this.flpButtons.Controls.Add(this.btnOpenFolder);
            this.flpButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flpButtons.Location = new System.Drawing.Point(3, 90);
            this.flpButtons.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this.flpButtons.Name = "flpButtons";
            this.flpButtons.Size = new System.Drawing.Size(460, 29);
            this.flpButtons.TabIndex = 3;
            this.flpButtons.WrapContents = false;
            //
            // btnCancel
            //
            this.btnCancel.AutoSize = true;
            this.btnCancel.Location = new System.Drawing.Point(382, 3);
            this.btnCancel.MinimumSize = new System.Drawing.Size(75, 23);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // btnClose
            //
            this.btnClose.AutoSize = true;
            this.btnClose.Location = new System.Drawing.Point(301, 3);
            this.btnClose.MinimumSize = new System.Drawing.Size(75, 23);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Visible = false;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // btnOpenFolder
            //
            this.btnOpenFolder.AutoSize = true;
            this.btnOpenFolder.Location = new System.Drawing.Point(205, 3);
            this.btnOpenFolder.MinimumSize = new System.Drawing.Size(90, 23);
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Size = new System.Drawing.Size(90, 23);
            this.btnOpenFolder.TabIndex = 2;
            this.btnOpenFolder.Text = "Open Folder";
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Visible = false;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            //
            // tmrUpdate
            //
            this.tmrUpdate.Interval = 250;
            this.tmrUpdate.Tick += new System.EventHandler(this.tmrUpdate_Tick);
            //
            // CadToolProgress
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(482, 141);
            this.Controls.Add(this.tlpRoot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CadToolProgress";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "CAD Tools";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CadToolProgress_FormClosing);
            this.Load += new System.EventHandler(this.CadToolProgress_Load);
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.flpButtons.ResumeLayout(false);
            this.flpButtons.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.Label lblStep;
        private System.Windows.Forms.ProgressBar pbProgress;
        private System.Windows.Forms.Label lblElapsed;
        private System.Windows.Forms.FlowLayoutPanel flpButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOpenFolder;
        private System.Windows.Forms.Timer tmrUpdate;
    }
}
