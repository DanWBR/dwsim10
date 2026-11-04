namespace DWSIM.Extensions.SolidCraft
{
    partial class CadToolDialog
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
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.gbProgram = new System.Windows.Forms.GroupBox();
            this.tlpProgram = new System.Windows.Forms.TableLayoutPanel();
            this.lblProgramFolder = new System.Windows.Forms.Label();
            this.tbProgramFolder = new System.Windows.Forms.TextBox();
            this.btnProgramFolder = new System.Windows.Forms.Button();
            this.lblProgramStatus = new System.Windows.Forms.Label();
            this.gbOutput = new System.Windows.Forms.GroupBox();
            this.tlpOutput = new System.Windows.Forms.TableLayoutPanel();
            this.lblOutput = new System.Windows.Forms.Label();
            this.tbOutput = new System.Windows.Forms.TextBox();
            this.btnOutput = new System.Windows.Forms.Button();
            this.chkOpenInApp = new System.Windows.Forms.CheckBox();
            this.gbDrawing = new System.Windows.Forms.GroupBox();
            this.tlpDrawing = new System.Windows.Forms.TableLayoutPanel();
            this.lblStandard = new System.Windows.Forms.Label();
            this.cbStandard = new System.Windows.Forms.ComboBox();
            this.lblOwner = new System.Windows.Forms.Label();
            this.tbOwner = new System.Windows.Forms.TextBox();
            this.lblClearance = new System.Windows.Forms.Label();
            this.tbClearance = new System.Windows.Forms.TextBox();
            this.gbModel = new System.Windows.Forms.GroupBox();
            this.tlpModel = new System.Windows.Forms.TableLayoutPanel();
            this.chkLabels = new System.Windows.Forms.CheckBox();
            this.chkFlowAnimation = new System.Windows.Forms.CheckBox();
            this.chkInsulation = new System.Windows.Forms.CheckBox();
            this.chkPlatforms = new System.Windows.Forms.CheckBox();
            this.chkInstruments = new System.Windows.Forms.CheckBox();
            this.chkDynamicReplay = new System.Windows.Forms.CheckBox();
            this.lblRackHeight = new System.Windows.Forms.Label();
            this.tbRackHeight = new System.Windows.Forms.TextBox();
            this.lblItemGap = new System.Windows.Forms.Label();
            this.tbItemGap = new System.Windows.Forms.TextBox();
            this.gbRender = new System.Windows.Forms.GroupBox();
            this.tlpRender = new System.Windows.Forms.TableLayoutPanel();
            this.chkRender = new System.Windows.Forms.CheckBox();
            this.lblResolution = new System.Windows.Forms.Label();
            this.cbResolution = new System.Windows.Forms.ComboBox();
            this.lblSamples = new System.Windows.Forms.Label();
            this.cbSamples = new System.Windows.Forms.ComboBox();
            this.flpButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tlpRoot.SuspendLayout();
            this.gbProgram.SuspendLayout();
            this.tlpProgram.SuspendLayout();
            this.gbOutput.SuspendLayout();
            this.tlpOutput.SuspendLayout();
            this.gbDrawing.SuspendLayout();
            this.tlpDrawing.SuspendLayout();
            this.gbModel.SuspendLayout();
            this.tlpModel.SuspendLayout();
            this.gbRender.SuspendLayout();
            this.tlpRender.SuspendLayout();
            this.flpButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.AutoSize = true;
            this.tlpRoot.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.lblDescription, 0, 0);
            this.tlpRoot.Controls.Add(this.gbProgram, 0, 1);
            this.tlpRoot.Controls.Add(this.gbOutput, 0, 2);
            this.tlpRoot.Controls.Add(this.gbDrawing, 0, 3);
            this.tlpRoot.Controls.Add(this.gbModel, 0, 4);
            this.tlpRoot.Controls.Add(this.gbRender, 0, 5);
            this.tlpRoot.Controls.Add(this.flpButtons, 0, 6);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(8);
            this.tlpRoot.RowCount = 7;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.Size = new System.Drawing.Size(584, 561);
            this.tlpRoot.TabIndex = 0;
            //
            // lblDescription
            //
            this.lblDescription.AutoSize = true;
            this.lblDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDescription.Location = new System.Drawing.Point(11, 8);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
            this.lblDescription.MaximumSize = new System.Drawing.Size(560, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(560, 26);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description";
            //
            // gbProgram
            //
            this.gbProgram.AutoSize = true;
            this.gbProgram.Controls.Add(this.tlpProgram);
            this.gbProgram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbProgram.Location = new System.Drawing.Point(11, 45);
            this.gbProgram.Name = "gbProgram";
            this.gbProgram.Size = new System.Drawing.Size(562, 80);
            this.gbProgram.TabIndex = 1;
            this.gbProgram.TabStop = false;
            this.gbProgram.Text = "Program";
            //
            // tlpProgram
            //
            this.tlpProgram.AutoSize = true;
            this.tlpProgram.ColumnCount = 3;
            this.tlpProgram.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProgram.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpProgram.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProgram.Controls.Add(this.lblProgramFolder, 0, 0);
            this.tlpProgram.Controls.Add(this.tbProgramFolder, 1, 0);
            this.tlpProgram.Controls.Add(this.btnProgramFolder, 2, 0);
            this.tlpProgram.Controls.Add(this.lblProgramStatus, 1, 1);
            this.tlpProgram.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpProgram.Location = new System.Drawing.Point(3, 16);
            this.tlpProgram.Name = "tlpProgram";
            this.tlpProgram.RowCount = 2;
            this.tlpProgram.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProgram.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProgram.Size = new System.Drawing.Size(556, 61);
            this.tlpProgram.TabIndex = 0;
            //
            // lblProgramFolder
            //
            this.lblProgramFolder.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblProgramFolder.AutoSize = true;
            this.lblProgramFolder.Location = new System.Drawing.Point(3, 8);
            this.lblProgramFolder.Name = "lblProgramFolder";
            this.lblProgramFolder.Size = new System.Drawing.Size(84, 13);
            this.lblProgramFolder.TabIndex = 0;
            this.lblProgramFolder.Text = "Installed in";
            //
            // tbProgramFolder
            //
            this.tbProgramFolder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbProgramFolder.Location = new System.Drawing.Point(93, 4);
            this.tbProgramFolder.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.tbProgramFolder.Name = "tbProgramFolder";
            this.tbProgramFolder.Size = new System.Drawing.Size(379, 20);
            this.tbProgramFolder.TabIndex = 1;
            this.tbProgramFolder.TextChanged += new System.EventHandler(this.tbProgramFolder_TextChanged);
            //
            // btnProgramFolder
            //
            this.btnProgramFolder.AutoSize = true;
            this.btnProgramFolder.Location = new System.Drawing.Point(478, 3);
            this.btnProgramFolder.Name = "btnProgramFolder";
            this.btnProgramFolder.Size = new System.Drawing.Size(75, 23);
            this.btnProgramFolder.TabIndex = 2;
            this.btnProgramFolder.Text = "Browse...";
            this.btnProgramFolder.UseVisualStyleBackColor = true;
            this.btnProgramFolder.Click += new System.EventHandler(this.btnProgramFolder_Click);
            //
            // lblProgramStatus
            //
            this.lblProgramStatus.AutoSize = true;
            this.tlpProgram.SetColumnSpan(this.lblProgramStatus, 2);
            this.lblProgramStatus.Location = new System.Drawing.Point(93, 29);
            this.lblProgramStatus.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
            this.lblProgramStatus.Name = "lblProgramStatus";
            this.lblProgramStatus.Size = new System.Drawing.Size(37, 13);
            this.lblProgramStatus.TabIndex = 3;
            this.lblProgramStatus.Text = "Status";
            //
            // gbOutput
            //
            this.gbOutput.AutoSize = true;
            this.gbOutput.Controls.Add(this.tlpOutput);
            this.gbOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbOutput.Location = new System.Drawing.Point(11, 131);
            this.gbOutput.Name = "gbOutput";
            this.gbOutput.Size = new System.Drawing.Size(562, 80);
            this.gbOutput.TabIndex = 2;
            this.gbOutput.TabStop = false;
            this.gbOutput.Text = "Output";
            //
            // tlpOutput
            //
            this.tlpOutput.AutoSize = true;
            this.tlpOutput.ColumnCount = 3;
            this.tlpOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpOutput.Controls.Add(this.lblOutput, 0, 0);
            this.tlpOutput.Controls.Add(this.tbOutput, 1, 0);
            this.tlpOutput.Controls.Add(this.btnOutput, 2, 0);
            this.tlpOutput.Controls.Add(this.chkOpenInApp, 1, 1);
            this.tlpOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpOutput.Location = new System.Drawing.Point(3, 16);
            this.tlpOutput.Name = "tlpOutput";
            this.tlpOutput.RowCount = 2;
            this.tlpOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpOutput.Size = new System.Drawing.Size(556, 61);
            this.tlpOutput.TabIndex = 0;
            //
            // lblOutput
            //
            this.lblOutput.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblOutput.AutoSize = true;
            this.lblOutput.Location = new System.Drawing.Point(3, 8);
            this.lblOutput.Name = "lblOutput";
            this.lblOutput.Size = new System.Drawing.Size(71, 13);
            this.lblOutput.TabIndex = 0;
            this.lblOutput.Text = "Output folder";
            //
            // tbOutput
            //
            this.tbOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbOutput.Location = new System.Drawing.Point(80, 4);
            this.tbOutput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.tbOutput.Name = "tbOutput";
            this.tbOutput.Size = new System.Drawing.Size(392, 20);
            this.tbOutput.TabIndex = 1;
            //
            // btnOutput
            //
            this.btnOutput.AutoSize = true;
            this.btnOutput.Location = new System.Drawing.Point(478, 3);
            this.btnOutput.Name = "btnOutput";
            this.btnOutput.Size = new System.Drawing.Size(75, 23);
            this.btnOutput.TabIndex = 2;
            this.btnOutput.Text = "Browse...";
            this.btnOutput.UseVisualStyleBackColor = true;
            this.btnOutput.Click += new System.EventHandler(this.btnOutput_Click);
            //
            // chkOpenInApp
            //
            this.chkOpenInApp.AutoSize = true;
            this.tlpOutput.SetColumnSpan(this.chkOpenInApp, 2);
            this.chkOpenInApp.Location = new System.Drawing.Point(80, 32);
            this.chkOpenInApp.Name = "chkOpenInApp";
            this.chkOpenInApp.Size = new System.Drawing.Size(130, 17);
            this.chkOpenInApp.TabIndex = 3;
            this.chkOpenInApp.Text = "Open it when finished";
            this.chkOpenInApp.UseVisualStyleBackColor = true;
            //
            // gbDrawing
            //
            this.gbDrawing.AutoSize = true;
            this.gbDrawing.Controls.Add(this.tlpDrawing);
            this.gbDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbDrawing.Location = new System.Drawing.Point(11, 217);
            this.gbDrawing.Name = "gbDrawing";
            this.gbDrawing.Size = new System.Drawing.Size(562, 100);
            this.gbDrawing.TabIndex = 3;
            this.gbDrawing.TabStop = false;
            this.gbDrawing.Text = "Drawing";
            //
            // tlpDrawing
            //
            this.tlpDrawing.AutoSize = true;
            this.tlpDrawing.ColumnCount = 2;
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpDrawing.Controls.Add(this.lblStandard, 0, 0);
            this.tlpDrawing.Controls.Add(this.cbStandard, 1, 0);
            this.tlpDrawing.Controls.Add(this.lblOwner, 0, 1);
            this.tlpDrawing.Controls.Add(this.tbOwner, 1, 1);
            this.tlpDrawing.Controls.Add(this.lblClearance, 0, 2);
            this.tlpDrawing.Controls.Add(this.tbClearance, 1, 2);
            this.tlpDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpDrawing.Location = new System.Drawing.Point(3, 16);
            this.tlpDrawing.Name = "tlpDrawing";
            this.tlpDrawing.RowCount = 3;
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.Size = new System.Drawing.Size(556, 81);
            this.tlpDrawing.TabIndex = 0;
            //
            // lblStandard
            //
            this.lblStandard.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblStandard.AutoSize = true;
            this.lblStandard.Location = new System.Drawing.Point(3, 7);
            this.lblStandard.Name = "lblStandard";
            this.lblStandard.Size = new System.Drawing.Size(90, 13);
            this.lblStandard.TabIndex = 0;
            this.lblStandard.Text = "Drawing standard";
            //
            // cbStandard
            //
            this.cbStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbStandard.FormattingEnabled = true;
            this.cbStandard.Location = new System.Drawing.Point(160, 3);
            this.cbStandard.Name = "cbStandard";
            this.cbStandard.Size = new System.Drawing.Size(160, 21);
            this.cbStandard.TabIndex = 1;
            //
            // lblOwner
            //
            this.lblOwner.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblOwner.AutoSize = true;
            this.lblOwner.Location = new System.Drawing.Point(3, 33);
            this.lblOwner.Name = "lblOwner";
            this.lblOwner.Size = new System.Drawing.Size(140, 13);
            this.lblOwner.TabIndex = 2;
            this.lblOwner.Text = "Company in the title block";
            //
            // tbOwner
            //
            this.tbOwner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbOwner.Location = new System.Drawing.Point(160, 30);
            this.tbOwner.Name = "tbOwner";
            this.tbOwner.Size = new System.Drawing.Size(393, 20);
            this.tbOwner.TabIndex = 3;
            //
            // lblClearance
            //
            this.lblClearance.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblClearance.AutoSize = true;
            this.lblClearance.Location = new System.Drawing.Point(3, 59);
            this.lblClearance.Name = "lblClearance";
            this.lblClearance.Size = new System.Drawing.Size(151, 13);
            this.lblClearance.TabIndex = 4;
            this.lblClearance.Text = "Line clearance (mm, 1 to 15)";
            //
            // tbClearance
            //
            this.tbClearance.Location = new System.Drawing.Point(160, 56);
            this.tbClearance.Name = "tbClearance";
            this.tbClearance.Size = new System.Drawing.Size(80, 20);
            this.tbClearance.TabIndex = 5;
            //
            // gbModel
            //
            this.gbModel.AutoSize = true;
            this.gbModel.Controls.Add(this.tlpModel);
            this.gbModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbModel.Location = new System.Drawing.Point(11, 323);
            this.gbModel.Name = "gbModel";
            this.gbModel.Size = new System.Drawing.Size(562, 140);
            this.gbModel.TabIndex = 4;
            this.gbModel.TabStop = false;
            this.gbModel.Text = "3D model";
            //
            // tlpModel
            //
            this.tlpModel.AutoSize = true;
            this.tlpModel.ColumnCount = 2;
            this.tlpModel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpModel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpModel.Controls.Add(this.chkLabels, 0, 0);
            this.tlpModel.Controls.Add(this.chkFlowAnimation, 1, 0);
            this.tlpModel.Controls.Add(this.chkInsulation, 0, 1);
            this.tlpModel.Controls.Add(this.chkPlatforms, 1, 1);
            this.tlpModel.Controls.Add(this.chkInstruments, 0, 2);
            this.tlpModel.Controls.Add(this.chkDynamicReplay, 1, 2);
            this.tlpModel.Controls.Add(this.lblRackHeight, 0, 3);
            this.tlpModel.Controls.Add(this.tbRackHeight, 1, 3);
            this.tlpModel.Controls.Add(this.lblItemGap, 0, 4);
            this.tlpModel.Controls.Add(this.tbItemGap, 1, 4);
            this.tlpModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpModel.Location = new System.Drawing.Point(3, 16);
            this.tlpModel.Name = "tlpModel";
            this.tlpModel.RowCount = 5;
            this.tlpModel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpModel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpModel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpModel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpModel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpModel.Size = new System.Drawing.Size(556, 121);
            this.tlpModel.TabIndex = 0;
            //
            // chkLabels
            //
            this.chkLabels.AutoSize = true;
            this.chkLabels.Location = new System.Drawing.Point(3, 3);
            this.chkLabels.Name = "chkLabels";
            this.chkLabels.Size = new System.Drawing.Size(140, 17);
            this.chkLabels.TabIndex = 0;
            this.chkLabels.Text = "Labels over the items";
            this.chkLabels.UseVisualStyleBackColor = true;
            //
            // chkFlowAnimation
            //
            this.chkFlowAnimation.AutoSize = true;
            this.chkFlowAnimation.Location = new System.Drawing.Point(281, 3);
            this.chkFlowAnimation.Name = "chkFlowAnimation";
            this.chkFlowAnimation.Size = new System.Drawing.Size(97, 17);
            this.chkFlowAnimation.TabIndex = 1;
            this.chkFlowAnimation.Text = "Flow animation";
            this.chkFlowAnimation.UseVisualStyleBackColor = true;
            //
            // chkInsulation
            //
            this.chkInsulation.AutoSize = true;
            this.chkInsulation.Location = new System.Drawing.Point(3, 26);
            this.chkInsulation.Name = "chkInsulation";
            this.chkInsulation.Size = new System.Drawing.Size(72, 17);
            this.chkInsulation.TabIndex = 2;
            this.chkInsulation.Text = "Insulation";
            this.chkInsulation.UseVisualStyleBackColor = true;
            //
            // chkPlatforms
            //
            this.chkPlatforms.AutoSize = true;
            this.chkPlatforms.Location = new System.Drawing.Point(281, 26);
            this.chkPlatforms.Name = "chkPlatforms";
            this.chkPlatforms.Size = new System.Drawing.Size(140, 17);
            this.chkPlatforms.TabIndex = 3;
            this.chkPlatforms.Text = "Platforms and ladders";
            this.chkPlatforms.UseVisualStyleBackColor = true;
            //
            // chkInstruments
            //
            this.chkInstruments.AutoSize = true;
            this.chkInstruments.Location = new System.Drawing.Point(3, 49);
            this.chkInstruments.Name = "chkInstruments";
            this.chkInstruments.Size = new System.Drawing.Size(80, 17);
            this.chkInstruments.TabIndex = 4;
            this.chkInstruments.Text = "Instruments";
            this.chkInstruments.UseVisualStyleBackColor = true;
            //
            // chkDynamicReplay
            //
            this.chkDynamicReplay.AutoSize = true;
            this.chkDynamicReplay.Location = new System.Drawing.Point(281, 49);
            this.chkDynamicReplay.Name = "chkDynamicReplay";
            this.chkDynamicReplay.Size = new System.Drawing.Size(180, 17);
            this.chkDynamicReplay.TabIndex = 5;
            this.chkDynamicReplay.Text = "Replay of the last dynamic run";
            this.chkDynamicReplay.UseVisualStyleBackColor = true;
            //
            // lblRackHeight
            //
            this.lblRackHeight.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblRackHeight.AutoSize = true;
            this.lblRackHeight.Location = new System.Drawing.Point(3, 76);
            this.lblRackHeight.Name = "lblRackHeight";
            this.lblRackHeight.Size = new System.Drawing.Size(150, 13);
            this.lblRackHeight.TabIndex = 6;
            this.lblRackHeight.Text = "Top of the pipe rack (m)";
            //
            // tbRackHeight
            //
            this.tbRackHeight.Location = new System.Drawing.Point(281, 72);
            this.tbRackHeight.Name = "tbRackHeight";
            this.tbRackHeight.Size = new System.Drawing.Size(80, 20);
            this.tbRackHeight.TabIndex = 7;
            //
            // lblItemGap
            //
            this.lblItemGap.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblItemGap.AutoSize = true;
            this.lblItemGap.Location = new System.Drawing.Point(3, 102);
            this.lblItemGap.Name = "lblItemGap";
            this.lblItemGap.Size = new System.Drawing.Size(180, 13);
            this.lblItemGap.TabIndex = 8;
            this.lblItemGap.Text = "Smallest gap between items (m)";
            //
            // tbItemGap
            //
            this.tbItemGap.Location = new System.Drawing.Point(281, 98);
            this.tbItemGap.Name = "tbItemGap";
            this.tbItemGap.Size = new System.Drawing.Size(80, 20);
            this.tbItemGap.TabIndex = 9;
            //
            // gbRender
            //
            this.gbRender.AutoSize = true;
            this.gbRender.Controls.Add(this.tlpRender);
            this.gbRender.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbRender.Location = new System.Drawing.Point(11, 469);
            this.gbRender.Name = "gbRender";
            this.gbRender.Size = new System.Drawing.Size(562, 100);
            this.gbRender.TabIndex = 5;
            this.gbRender.TabStop = false;
            this.gbRender.Text = "Photorealistic picture";
            //
            // tlpRender
            //
            this.tlpRender.AutoSize = true;
            this.tlpRender.ColumnCount = 2;
            this.tlpRender.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpRender.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpRender.Controls.Add(this.chkRender, 0, 0);
            this.tlpRender.Controls.Add(this.lblResolution, 0, 1);
            this.tlpRender.Controls.Add(this.cbResolution, 1, 1);
            this.tlpRender.Controls.Add(this.lblSamples, 0, 2);
            this.tlpRender.Controls.Add(this.cbSamples, 1, 2);
            this.tlpRender.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRender.Location = new System.Drawing.Point(3, 16);
            this.tlpRender.Name = "tlpRender";
            this.tlpRender.RowCount = 3;
            this.tlpRender.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRender.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRender.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRender.Size = new System.Drawing.Size(556, 81);
            this.tlpRender.TabIndex = 0;
            //
            // chkRender
            //
            this.chkRender.AutoSize = true;
            this.tlpRender.SetColumnSpan(this.chkRender, 2);
            this.chkRender.Location = new System.Drawing.Point(3, 3);
            this.chkRender.Name = "chkRender";
            this.chkRender.Size = new System.Drawing.Size(300, 17);
            this.chkRender.TabIndex = 0;
            this.chkRender.Text = "Render a photorealistic picture of the model (path tracing)";
            this.chkRender.UseVisualStyleBackColor = true;
            this.chkRender.CheckedChanged += new System.EventHandler(this.chkRender_CheckedChanged);
            //
            // lblResolution
            //
            this.lblResolution.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblResolution.AutoSize = true;
            this.lblResolution.Location = new System.Drawing.Point(3, 30);
            this.lblResolution.Name = "lblResolution";
            this.lblResolution.Size = new System.Drawing.Size(120, 13);
            this.lblResolution.TabIndex = 1;
            this.lblResolution.Text = "Resolution (pixels)";
            //
            // cbResolution
            //
            this.cbResolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbResolution.FormattingEnabled = true;
            this.cbResolution.Location = new System.Drawing.Point(281, 26);
            this.cbResolution.Name = "cbResolution";
            this.cbResolution.Size = new System.Drawing.Size(180, 21);
            this.cbResolution.TabIndex = 2;
            //
            // lblSamples
            //
            this.lblSamples.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblSamples.AutoSize = true;
            this.lblSamples.Location = new System.Drawing.Point(3, 57);
            this.lblSamples.Name = "lblSamples";
            this.lblSamples.Size = new System.Drawing.Size(120, 13);
            this.lblSamples.TabIndex = 3;
            this.lblSamples.Text = "Samples per pixel";
            //
            // cbSamples
            //
            this.cbSamples.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbSamples.FormattingEnabled = true;
            this.cbSamples.Location = new System.Drawing.Point(281, 53);
            this.cbSamples.Name = "cbSamples";
            this.cbSamples.Size = new System.Drawing.Size(180, 21);
            this.cbSamples.TabIndex = 4;
            //
            // flpButtons
            //
            this.flpButtons.AutoSize = true;
            this.flpButtons.Controls.Add(this.btnRun);
            this.flpButtons.Controls.Add(this.btnCancel);
            this.flpButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flpButtons.Location = new System.Drawing.Point(11, 575);
            this.flpButtons.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this.flpButtons.Name = "flpButtons";
            this.flpButtons.Size = new System.Drawing.Size(562, 29);
            this.flpButtons.TabIndex = 6;
            //
            // btnRun
            //
            this.btnRun.Location = new System.Drawing.Point(484, 3);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(75, 23);
            this.btnRun.TabIndex = 0;
            this.btnRun.Text = "Export";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(403, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // CadToolDialog
            //
            this.AcceptButton = this.btnRun;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(584, 611);
            this.Controls.Add(this.tlpRoot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CadToolDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CAD Tools";
            this.Load += new System.EventHandler(this.CadToolDialog_Load);
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.gbProgram.ResumeLayout(false);
            this.gbProgram.PerformLayout();
            this.tlpProgram.ResumeLayout(false);
            this.tlpProgram.PerformLayout();
            this.gbOutput.ResumeLayout(false);
            this.gbOutput.PerformLayout();
            this.tlpOutput.ResumeLayout(false);
            this.tlpOutput.PerformLayout();
            this.gbDrawing.ResumeLayout(false);
            this.gbDrawing.PerformLayout();
            this.tlpDrawing.ResumeLayout(false);
            this.tlpDrawing.PerformLayout();
            this.gbModel.ResumeLayout(false);
            this.gbModel.PerformLayout();
            this.tlpModel.ResumeLayout(false);
            this.tlpModel.PerformLayout();
            this.gbRender.ResumeLayout(false);
            this.gbRender.PerformLayout();
            this.tlpRender.ResumeLayout(false);
            this.tlpRender.PerformLayout();
            this.flpButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.GroupBox gbProgram;
        private System.Windows.Forms.TableLayoutPanel tlpProgram;
        private System.Windows.Forms.Label lblProgramFolder;
        private System.Windows.Forms.TextBox tbProgramFolder;
        private System.Windows.Forms.Button btnProgramFolder;
        private System.Windows.Forms.Label lblProgramStatus;
        private System.Windows.Forms.GroupBox gbOutput;
        private System.Windows.Forms.TableLayoutPanel tlpOutput;
        private System.Windows.Forms.Label lblOutput;
        private System.Windows.Forms.TextBox tbOutput;
        private System.Windows.Forms.Button btnOutput;
        private System.Windows.Forms.CheckBox chkOpenInApp;
        private System.Windows.Forms.GroupBox gbDrawing;
        private System.Windows.Forms.TableLayoutPanel tlpDrawing;
        private System.Windows.Forms.Label lblStandard;
        private System.Windows.Forms.ComboBox cbStandard;
        private System.Windows.Forms.Label lblOwner;
        private System.Windows.Forms.TextBox tbOwner;
        private System.Windows.Forms.Label lblClearance;
        private System.Windows.Forms.TextBox tbClearance;
        private System.Windows.Forms.GroupBox gbModel;
        private System.Windows.Forms.TableLayoutPanel tlpModel;
        private System.Windows.Forms.CheckBox chkLabels;
        private System.Windows.Forms.CheckBox chkFlowAnimation;
        private System.Windows.Forms.CheckBox chkInsulation;
        private System.Windows.Forms.CheckBox chkPlatforms;
        private System.Windows.Forms.CheckBox chkInstruments;
        private System.Windows.Forms.CheckBox chkDynamicReplay;
        private System.Windows.Forms.Label lblRackHeight;
        private System.Windows.Forms.TextBox tbRackHeight;
        private System.Windows.Forms.Label lblItemGap;
        private System.Windows.Forms.TextBox tbItemGap;
        private System.Windows.Forms.GroupBox gbRender;
        private System.Windows.Forms.TableLayoutPanel tlpRender;
        private System.Windows.Forms.CheckBox chkRender;
        private System.Windows.Forms.Label lblResolution;
        private System.Windows.Forms.ComboBox cbResolution;
        private System.Windows.Forms.Label lblSamples;
        private System.Windows.Forms.ComboBox cbSamples;
        private System.Windows.Forms.FlowLayoutPanel flpButtons;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnCancel;
    }
}
