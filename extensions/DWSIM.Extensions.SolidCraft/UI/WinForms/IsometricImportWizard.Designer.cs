namespace DWSIM.Extensions.SolidCraft
{
    partial class IsometricImportWizard
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
            this.wizard = new AeroWizard.StepWizardControl();
            this.wpDrawing = new AeroWizard.WizardPage();
            this.lblDrawingHelp = new System.Windows.Forms.Label();
            this.wpRoute = new AeroWizard.WizardPage();
            this.lblRouteHelp = new System.Windows.Forms.Label();
            this.wpPipe = new AeroWizard.WizardPage();
            this.lblPipeHelp = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.wizard)).BeginInit();
            this.wpDrawing.SuspendLayout();
            this.wpRoute.SuspendLayout();
            this.wpPipe.SuspendLayout();
            this.tlpDrawing = new System.Windows.Forms.TableLayoutPanel();
            this.flpVision = new System.Windows.Forms.FlowLayoutPanel();
            this.lblVision = new System.Windows.Forms.Label();
            this.lnkAssistantSettings = new System.Windows.Forms.LinkLabel();
            this.lblModel = new System.Windows.Forms.Label();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtFile = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.flpRead = new System.Windows.Forms.FlowLayoutPanel();
            this.prgRead = new System.Windows.Forms.ProgressBar();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnCancelRead = new System.Windows.Forms.Button();
            this.btnOpenReading = new System.Windows.Forms.Button();
            this.btnSaveReading = new System.Windows.Forms.Button();
            this.lblReadStatus = new System.Windows.Forms.Label();
            this.picDrawing = new System.Windows.Forms.PictureBox();
            this.splitRoute = new System.Windows.Forms.SplitContainer();
            this.tlpRouteLeft = new System.Windows.Forms.TableLayoutPanel();
            this.tlpLine = new System.Windows.Forms.TableLayoutPanel();
            this.lblLine = new System.Windows.Forms.Label();
            this.txtLine = new System.Windows.Forms.TextBox();
            this.lblSize = new System.Windows.Forms.Label();
            this.txtSize = new System.Windows.Forms.TextBox();
            this.lblSchedule = new System.Windows.Forms.Label();
            this.txtSchedule = new System.Windows.Forms.TextBox();
            this.lblOd = new System.Windows.Forms.Label();
            this.txtOd = new System.Windows.Forms.TextBox();
            this.lblWall = new System.Windows.Forms.Label();
            this.txtWall = new System.Windows.Forms.TextBox();
            this.lblMaterial = new System.Windows.Forms.Label();
            this.txtMaterial = new System.Windows.Forms.TextBox();
            this.lblEnds = new System.Windows.Forms.Label();
            this.lblEndsValue = new System.Windows.Forms.Label();
            this.dgvLegs = new System.Windows.Forms.DataGridView();
            this.colLegUse = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colLegNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLegDirection = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLegLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLegRise = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLegFittings = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.picRoute = new System.Windows.Forms.PictureBox();
            this.tlpPipe = new System.Windows.Forms.TableLayoutPanel();
            this.dgvChecks = new System.Windows.Forms.DataGridView();
            this.colCheckLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCheckSubject = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCheckText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.flpTarget = new System.Windows.Forms.FlowLayoutPanel();
            this.lblTarget = new System.Windows.Forms.Label();
            this.cbTarget = new System.Windows.Forms.ComboBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.lblSummary = new System.Windows.Forms.Label();
            this.dgvSections = new System.Windows.Forms.DataGridView();
            this.colSecType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSecQuantity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSecLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSecElevation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSecDiameters = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSecMaterial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tlpDrawing.SuspendLayout();
            this.flpVision.SuspendLayout();
            this.flpRead.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitRoute)).BeginInit();
            this.splitRoute.Panel1.SuspendLayout();
            this.splitRoute.Panel2.SuspendLayout();
            this.splitRoute.SuspendLayout();
            this.tlpRouteLeft.SuspendLayout();
            this.tlpLine.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLegs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picRoute)).BeginInit();
            this.tlpPipe.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChecks)).BeginInit();
            this.flpTarget.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSections)).BeginInit();
            this.SuspendLayout();
            //
            // wizard
            //
            this.wizard.BackButtonToolTipText = "Back to the previous page";
            this.wizard.CancelButtonText = "Close";
            this.wizard.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wizard.FinishButtonText = "Finish";
            this.wizard.Location = new System.Drawing.Point(0, 0);
            this.wizard.Name = "wizard";
            this.wizard.NextButtonText = "Next >";
            this.wizard.Pages.Add(this.wpDrawing);
            this.wizard.Pages.Add(this.wpRoute);
            this.wizard.Pages.Add(this.wpPipe);
            this.wizard.Size = new System.Drawing.Size(1184, 761);
            this.wizard.TabIndex = 0;
            this.wizard.Title = "Import a Pipe Segment from a Piping Isometric";
            //
            // wpDrawing
            //
            this.wpDrawing.Controls.Add(this.tlpDrawing);
            this.wpDrawing.Controls.Add(this.lblDrawingHelp);
            this.wpDrawing.Name = "wpDrawing";
            this.wpDrawing.Size = new System.Drawing.Size(980, 606);
            this.wpDrawing.TabIndex = 0;
            this.wpDrawing.Text = "Isometric";
            //
            // lblDrawingHelp
            //
            this.lblDrawingHelp.AutoSize = true;
            this.lblDrawingHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDrawingHelp.Location = new System.Drawing.Point(0, 0);
            this.lblDrawingHelp.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblDrawingHelp.Name = "lblDrawingHelp";
            this.lblDrawingHelp.Padding = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblDrawingHelp.Size = new System.Drawing.Size(960, 25);
            this.lblDrawingHelp.TabIndex = 1;
            this.lblDrawingHelp.Text = "Choose the piping isometric: an image, a PDF or a DXF/DWG drawing. The AI Assistant reads its route, size and material with the model set up in its settings.";
            //
            // wpRoute
            //
            this.wpRoute.Controls.Add(this.splitRoute);
            this.wpRoute.Controls.Add(this.lblRouteHelp);
            this.wpRoute.Name = "wpRoute";
            this.wpRoute.Size = new System.Drawing.Size(980, 606);
            this.wpRoute.TabIndex = 1;
            this.wpRoute.Text = "Route";
            this.wpRoute.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.wpRoute_Initialize);
            this.wpRoute.Commit += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.wpRoute_Commit);
            this.wpRoute.Rollback += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.wpRoute_Commit);
            //
            // lblRouteHelp
            //
            this.lblRouteHelp.AutoSize = true;
            this.lblRouteHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblRouteHelp.Location = new System.Drawing.Point(0, 0);
            this.lblRouteHelp.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblRouteHelp.Name = "lblRouteHelp";
            this.lblRouteHelp.Padding = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblRouteHelp.Size = new System.Drawing.Size(960, 25);
            this.lblRouteHelp.TabIndex = 1;
            this.lblRouteHelp.Text = "The line and its route leg by leg, from where it starts to where it ends. Correct the directions, lengths, rises and fittings; the sketch redraws the route as you go.";
            //
            // wpPipe
            //
            this.wpPipe.Controls.Add(this.tlpPipe);
            this.wpPipe.Controls.Add(this.lblPipeHelp);
            this.wpPipe.IsFinishPage = true;
            this.wpPipe.Name = "wpPipe";
            this.wpPipe.Size = new System.Drawing.Size(980, 606);
            this.wpPipe.TabIndex = 2;
            this.wpPipe.Text = "Pipe segment";
            this.wpPipe.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.wpPipe_Initialize);
            //
            // lblPipeHelp
            //
            this.lblPipeHelp.AutoSize = true;
            this.lblPipeHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblPipeHelp.Location = new System.Drawing.Point(0, 0);
            this.lblPipeHelp.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblPipeHelp.Name = "lblPipeHelp";
            this.lblPipeHelp.Padding = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblPipeHelp.Size = new System.Drawing.Size(960, 25);
            this.lblPipeHelp.TabIndex = 1;
            this.lblPipeHelp.Text = "The checks against the bill of materials and the elevations, and the sections the pipe segment gets. Choose the pipe segment and make its hydraulic profile.";
            //
            // flpVision
            //
            this.flpVision.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.flpVision, 3);
            this.flpVision.Controls.Add(this.lblVision);
            this.flpVision.Controls.Add(this.lnkAssistantSettings);
            this.flpVision.Controls.Add(this.lblModel);
            this.flpVision.SetFlowBreak(this.lnkAssistantSettings, true);
            this.flpVision.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpVision.Name = "flpVision";
            this.flpVision.TabIndex = 20;
            //
            // lblVision
            //
            this.lblVision.AutoSize = true;
            this.lblVision.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblVision.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.lblVision.MaximumSize = new System.Drawing.Size(720, 0);
            this.lblVision.Name = "lblVision";
            this.lblVision.TabIndex = 0;
            this.lblVision.Text = "The AI Assistant reads the drawing with the model chosen in its settings: choose one that reads images (a vision model, such as Claude, GPT-4o or Gemini).";
            //
            // lblModel
            //
            this.lblModel.AutoSize = true;
            this.lblModel.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.lblModel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.lblModel.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblModel.Name = "lblModel";
            this.lblModel.TabIndex = 2;
            //
            // lnkAssistantSettings
            //
            this.lnkAssistantSettings.AutoSize = true;
            this.lnkAssistantSettings.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.lnkAssistantSettings.Name = "lnkAssistantSettings";
            this.lnkAssistantSettings.TabIndex = 1;
            this.lnkAssistantSettings.TabStop = true;
            this.lnkAssistantSettings.Text = "Open the AI Assistant settings";
            this.lnkAssistantSettings.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkAssistantSettings_LinkClicked);
            //
            // tlpDrawing
            //
            this.tlpDrawing.ColumnCount = 3;
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.Controls.Add(this.flpVision, 0, 1);
            this.tlpDrawing.Controls.Add(this.lblFile, 0, 0);
            this.tlpDrawing.Controls.Add(this.txtFile, 1, 0);
            this.tlpDrawing.Controls.Add(this.btnBrowse, 2, 0);
            this.tlpDrawing.Controls.Add(this.flpRead, 0, 2);
            this.tlpDrawing.Controls.Add(this.lblReadStatus, 0, 3);
            this.tlpDrawing.Controls.Add(this.picDrawing, 0, 4);
            this.tlpDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpDrawing.Location = new System.Drawing.Point(0, 0);
            this.tlpDrawing.Name = "tlpDrawing";
            this.tlpDrawing.RowCount = 5;
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpDrawing.Size = new System.Drawing.Size(996, 650);
            this.tlpDrawing.TabIndex = 0;
            //
            // lblFile
            //
            this.lblFile.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblFile.AutoSize = true;
            this.lblFile.Location = new System.Drawing.Point(3, 8);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(60, 17);
            this.lblFile.TabIndex = 0;
            this.lblFile.Text = "Isometric";
            //
            // txtFile
            //
            this.txtFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFile.Location = new System.Drawing.Point(69, 4);
            this.txtFile.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.txtFile.Name = "txtFile";
            this.txtFile.Size = new System.Drawing.Size(818, 25);
            this.txtFile.TabIndex = 1;
            this.txtFile.TextChanged += new System.EventHandler(this.txtFile_TextChanged);
            //
            // btnBrowse
            //
            this.btnBrowse.AutoSize = true;
            this.btnBrowse.Location = new System.Drawing.Point(893, 3);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(100, 27);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            //
            // flpRead
            //
            this.flpRead.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.flpRead, 3);
            this.flpRead.Controls.Add(this.btnRead);
            this.flpRead.Controls.Add(this.btnCancelRead);
            this.flpRead.Controls.Add(this.btnOpenReading);
            this.flpRead.Controls.Add(this.btnSaveReading);
            this.flpRead.Controls.Add(this.prgRead);
            this.flpRead.Location = new System.Drawing.Point(3, 36);
            this.flpRead.Name = "flpRead";
            this.flpRead.Size = new System.Drawing.Size(600, 33);
            this.flpRead.TabIndex = 3;
            //
            // btnRead
            //
            this.btnRead.AutoSize = true;
            this.btnRead.Enabled = false;
            this.btnRead.Location = new System.Drawing.Point(3, 3);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(150, 27);
            this.btnRead.TabIndex = 0;
            this.btnRead.Text = "Read the isometric";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);
            //
            // btnCancelRead
            //
            this.btnCancelRead.AutoSize = true;
            this.btnCancelRead.Enabled = false;
            this.btnCancelRead.Location = new System.Drawing.Point(159, 3);
            this.btnCancelRead.Name = "btnCancelRead";
            this.btnCancelRead.Size = new System.Drawing.Size(80, 27);
            this.btnCancelRead.TabIndex = 1;
            this.btnCancelRead.Text = "Cancel";
            this.btnCancelRead.UseVisualStyleBackColor = true;
            this.btnCancelRead.Click += new System.EventHandler(this.btnCancelRead_Click);
            //
            // btnOpenReading
            //
            this.btnOpenReading.AutoSize = true;
            this.btnOpenReading.Location = new System.Drawing.Point(245, 3);
            this.btnOpenReading.Name = "btnOpenReading";
            this.btnOpenReading.Size = new System.Drawing.Size(150, 27);
            this.btnOpenReading.TabIndex = 2;
            this.btnOpenReading.Text = "Open a reading...";
            this.btnOpenReading.UseVisualStyleBackColor = true;
            this.btnOpenReading.Click += new System.EventHandler(this.btnOpenReading_Click);
            //
            // btnSaveReading
            //
            this.btnSaveReading.AutoSize = true;
            this.btnSaveReading.Enabled = false;
            this.btnSaveReading.Location = new System.Drawing.Point(401, 3);
            this.btnSaveReading.Name = "btnSaveReading";
            this.btnSaveReading.Size = new System.Drawing.Size(150, 27);
            this.btnSaveReading.TabIndex = 3;
            this.btnSaveReading.Text = "Save the reading...";
            this.btnSaveReading.UseVisualStyleBackColor = true;
            this.btnSaveReading.Click += new System.EventHandler(this.btnSaveReading_Click);
            //
            // prgRead
            //
            this.prgRead.Location = new System.Drawing.Point(566, 7);
            this.prgRead.Margin = new System.Windows.Forms.Padding(12, 7, 3, 3);
            this.prgRead.MarqueeAnimationSpeed = 30;
            this.prgRead.Name = "prgRead";
            this.prgRead.Size = new System.Drawing.Size(180, 20);
            this.prgRead.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.prgRead.TabIndex = 4;
            this.prgRead.Visible = false;
            //
            // lblReadStatus
            //
            this.lblReadStatus.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.lblReadStatus, 3);
            this.lblReadStatus.Location = new System.Drawing.Point(3, 75);
            this.lblReadStatus.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.lblReadStatus.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblReadStatus.Name = "lblReadStatus";
            this.lblReadStatus.Size = new System.Drawing.Size(0, 17);
            this.lblReadStatus.TabIndex = 4;
            //
            // picDrawing
            //
            this.picDrawing.BackColor = System.Drawing.Color.White;
            this.picDrawing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tlpDrawing.SetColumnSpan(this.picDrawing, 3);
            this.picDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picDrawing.Location = new System.Drawing.Point(3, 101);
            this.picDrawing.Name = "picDrawing";
            this.picDrawing.Size = new System.Drawing.Size(990, 546);
            this.picDrawing.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picDrawing.TabIndex = 5;
            this.picDrawing.TabStop = false;
            //
            // splitRoute
            //
            this.splitRoute.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitRoute.Location = new System.Drawing.Point(0, 0);
            this.splitRoute.Name = "splitRoute";
            //
            // splitRoute.Panel1
            //
            this.splitRoute.Panel1.Controls.Add(this.tlpRouteLeft);
            //
            // splitRoute.Panel2
            //
            this.splitRoute.Panel2.Controls.Add(this.picRoute);
            this.splitRoute.Size = new System.Drawing.Size(996, 650);
            this.splitRoute.SplitterDistance = 560;
            this.splitRoute.TabIndex = 1;
            //
            // tlpRouteLeft
            //
            this.tlpRouteLeft.ColumnCount = 1;
            this.tlpRouteLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRouteLeft.Controls.Add(this.tlpLine, 0, 0);
            this.tlpRouteLeft.Controls.Add(this.dgvLegs, 0, 1);
            this.tlpRouteLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRouteLeft.Location = new System.Drawing.Point(0, 0);
            this.tlpRouteLeft.Name = "tlpRouteLeft";
            this.tlpRouteLeft.RowCount = 2;
            this.tlpRouteLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRouteLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRouteLeft.Size = new System.Drawing.Size(560, 650);
            this.tlpRouteLeft.TabIndex = 0;
            //
            // tlpLine
            //
            this.tlpLine.AutoSize = true;
            this.tlpLine.ColumnCount = 4;
            this.tlpLine.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpLine.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpLine.Controls.Add(this.lblLine, 0, 0);
            this.tlpLine.Controls.Add(this.txtLine, 1, 0);
            this.tlpLine.Controls.Add(this.lblMaterial, 2, 0);
            this.tlpLine.Controls.Add(this.txtMaterial, 3, 0);
            this.tlpLine.Controls.Add(this.lblSize, 0, 1);
            this.tlpLine.Controls.Add(this.txtSize, 1, 1);
            this.tlpLine.Controls.Add(this.lblSchedule, 2, 1);
            this.tlpLine.Controls.Add(this.txtSchedule, 3, 1);
            this.tlpLine.Controls.Add(this.lblOd, 0, 2);
            this.tlpLine.Controls.Add(this.txtOd, 1, 2);
            this.tlpLine.Controls.Add(this.lblWall, 2, 2);
            this.tlpLine.Controls.Add(this.txtWall, 3, 2);
            this.tlpLine.Controls.Add(this.lblEnds, 0, 3);
            this.tlpLine.Controls.Add(this.lblEndsValue, 1, 3);
            this.tlpLine.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpLine.Location = new System.Drawing.Point(3, 3);
            this.tlpLine.Name = "tlpLine";
            this.tlpLine.RowCount = 4;
            this.tlpLine.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpLine.Size = new System.Drawing.Size(554, 120);
            this.tlpLine.TabIndex = 0;
            //
            // lblLine
            //
            this.lblLine.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLine.AutoSize = true;
            this.lblLine.Name = "lblLine";
            this.lblLine.TabIndex = 0;
            this.lblLine.Text = "Line";
            //
            // txtLine
            //
            this.txtLine.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLine.Name = "txtLine";
            this.txtLine.TabIndex = 1;
            this.txtLine.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblMaterial
            //
            this.lblMaterial.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMaterial.AutoSize = true;
            this.lblMaterial.Name = "lblMaterial";
            this.lblMaterial.TabIndex = 2;
            this.lblMaterial.Text = "Material";
            //
            // txtMaterial
            //
            this.txtMaterial.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtMaterial.Name = "txtMaterial";
            this.txtMaterial.TabIndex = 3;
            this.txtMaterial.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblSize
            //
            this.lblSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblSize.AutoSize = true;
            this.lblSize.Name = "lblSize";
            this.lblSize.TabIndex = 4;
            this.lblSize.Text = "Nominal size";
            //
            // txtSize
            //
            this.txtSize.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSize.Name = "txtSize";
            this.txtSize.TabIndex = 5;
            this.txtSize.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblSchedule
            //
            this.lblSchedule.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblSchedule.AutoSize = true;
            this.lblSchedule.Name = "lblSchedule";
            this.lblSchedule.TabIndex = 6;
            this.lblSchedule.Text = "Schedule";
            //
            // txtSchedule
            //
            this.txtSchedule.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSchedule.Name = "txtSchedule";
            this.txtSchedule.TabIndex = 7;
            this.txtSchedule.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblOd
            //
            this.lblOd.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblOd.AutoSize = true;
            this.lblOd.Name = "lblOd";
            this.lblOd.TabIndex = 8;
            this.lblOd.Text = "Outside diameter (mm)";
            //
            // txtOd
            //
            this.txtOd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOd.Name = "txtOd";
            this.txtOd.TabIndex = 9;
            this.txtOd.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblWall
            //
            this.lblWall.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblWall.AutoSize = true;
            this.lblWall.Name = "lblWall";
            this.lblWall.TabIndex = 10;
            this.lblWall.Text = "Wall (mm)";
            //
            // txtWall
            //
            this.txtWall.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtWall.Name = "txtWall";
            this.txtWall.TabIndex = 11;
            this.txtWall.Leave += new System.EventHandler(this.LineField_Leave);
            //
            // lblEnds
            //
            this.lblEnds.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEnds.AutoSize = true;
            this.lblEnds.Name = "lblEnds";
            this.lblEnds.TabIndex = 12;
            this.lblEnds.Text = "From / to";
            //
            // lblEndsValue
            //
            this.lblEndsValue.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEndsValue.AutoSize = true;
            this.tlpLine.SetColumnSpan(this.lblEndsValue, 3);
            this.lblEndsValue.Name = "lblEndsValue";
            this.lblEndsValue.TabIndex = 13;
            //
            // dgvLegs
            //
            this.dgvLegs.AllowUserToResizeRows = false;
            this.dgvLegs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvLegs.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvLegs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLegs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLegUse,
            this.colLegNumber,
            this.colLegDirection,
            this.colLegLength,
            this.colLegRise,
            this.colLegFittings});
            this.dgvLegs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLegs.Location = new System.Drawing.Point(3, 129);
            this.dgvLegs.Name = "dgvLegs";
            this.dgvLegs.RowHeadersVisible = false;
            this.dgvLegs.Size = new System.Drawing.Size(554, 518);
            this.dgvLegs.TabIndex = 1;
            this.dgvLegs.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvLegs_CellEndEdit);
            this.dgvLegs.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvLegs_CurrentCellDirtyStateChanged);
            this.dgvLegs.UserDeletedRow += new System.Windows.Forms.DataGridViewRowEventHandler(this.dgvLegs_UserDeletedRow);
            //
            // colLegUse
            //
            this.colLegUse.FillWeight = 8F;
            this.colLegUse.HeaderText = "Use";
            this.colLegUse.Name = "colLegUse";
            //
            // colLegNumber
            //
            this.colLegNumber.FillWeight = 8F;
            this.colLegNumber.HeaderText = "Leg";
            this.colLegNumber.Name = "colLegNumber";
            this.colLegNumber.ReadOnly = true;
            //
            // colLegDirection
            //
            this.colLegDirection.FillWeight = 14F;
            this.colLegDirection.HeaderText = "Direction";
            this.colLegDirection.Items.AddRange(new object[] {
            "N",
            "S",
            "E",
            "W",
            "NE",
            "NW",
            "SE",
            "SW",
            "up",
            "down"});
            this.colLegDirection.Name = "colLegDirection";
            //
            // colLegLength
            //
            this.colLegLength.FillWeight = 14F;
            this.colLegLength.HeaderText = "Length (mm)";
            this.colLegLength.Name = "colLegLength";
            //
            // colLegRise
            //
            this.colLegRise.FillWeight = 14F;
            this.colLegRise.HeaderText = "Rise (mm)";
            this.colLegRise.Name = "colLegRise";
            //
            // colLegFittings
            //
            this.colLegFittings.FillWeight = 42F;
            this.colLegFittings.HeaderText = "Fittings at the end (comma separated)";
            this.colLegFittings.Name = "colLegFittings";
            //
            // picRoute
            //
            this.picRoute.BackColor = System.Drawing.Color.White;
            this.picRoute.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picRoute.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picRoute.Location = new System.Drawing.Point(0, 0);
            this.picRoute.Name = "picRoute";
            this.picRoute.Size = new System.Drawing.Size(432, 650);
            this.picRoute.TabIndex = 0;
            this.picRoute.TabStop = false;
            this.picRoute.Paint += new System.Windows.Forms.PaintEventHandler(this.picRoute_Paint);
            this.picRoute.Resize += new System.EventHandler(this.picRoute_Resize);
            //
            // tlpPipe
            //
            this.tlpPipe.ColumnCount = 1;
            this.tlpPipe.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpPipe.Controls.Add(this.dgvChecks, 0, 0);
            this.tlpPipe.Controls.Add(this.flpTarget, 0, 1);
            this.tlpPipe.Controls.Add(this.lblSummary, 0, 2);
            this.tlpPipe.Controls.Add(this.dgvSections, 0, 3);
            this.tlpPipe.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpPipe.Location = new System.Drawing.Point(0, 0);
            this.tlpPipe.Name = "tlpPipe";
            this.tlpPipe.RowCount = 4;
            this.tlpPipe.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tlpPipe.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpPipe.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpPipe.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tlpPipe.Size = new System.Drawing.Size(996, 650);
            this.tlpPipe.TabIndex = 2;
            //
            // dgvChecks
            //
            this.dgvChecks.AllowUserToAddRows = false;
            this.dgvChecks.AllowUserToDeleteRows = false;
            this.dgvChecks.AllowUserToResizeRows = false;
            this.dgvChecks.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvChecks.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvChecks.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvChecks.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvChecks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvChecks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCheckLevel,
            this.colCheckSubject,
            this.colCheckText});
            this.dgvChecks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvChecks.Location = new System.Drawing.Point(3, 3);
            this.dgvChecks.Name = "dgvChecks";
            this.dgvChecks.ReadOnly = true;
            this.dgvChecks.RowHeadersVisible = false;
            this.dgvChecks.Size = new System.Drawing.Size(990, 230);
            this.dgvChecks.TabIndex = 0;
            //
            // colCheckLevel
            //
            this.colCheckLevel.FillWeight = 10F;
            this.colCheckLevel.HeaderText = "";
            this.colCheckLevel.Name = "colCheckLevel";
            this.colCheckLevel.ReadOnly = true;
            //
            // colCheckSubject
            //
            this.colCheckSubject.FillWeight = 14F;
            this.colCheckSubject.HeaderText = "Where";
            this.colCheckSubject.Name = "colCheckSubject";
            this.colCheckSubject.ReadOnly = true;
            //
            // colCheckText
            //
            this.colCheckText.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.colCheckText.FillWeight = 76F;
            this.colCheckText.HeaderText = "What";
            this.colCheckText.Name = "colCheckText";
            this.colCheckText.ReadOnly = true;
            //
            // flpTarget
            //
            this.flpTarget.AutoSize = true;
            this.flpTarget.Controls.Add(this.lblTarget);
            this.flpTarget.Controls.Add(this.cbTarget);
            this.flpTarget.Controls.Add(this.btnApply);
            this.flpTarget.Location = new System.Drawing.Point(3, 239);
            this.flpTarget.Name = "flpTarget";
            this.flpTarget.Size = new System.Drawing.Size(700, 33);
            this.flpTarget.TabIndex = 1;
            //
            // lblTarget
            //
            this.lblTarget.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new System.Drawing.Point(3, 8);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(150, 17);
            this.lblTarget.TabIndex = 0;
            this.lblTarget.Text = "Put the route in";
            //
            // cbTarget
            //
            this.cbTarget.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cbTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbTarget.FormattingEnabled = true;
            this.cbTarget.Location = new System.Drawing.Point(159, 4);
            this.cbTarget.Name = "cbTarget";
            this.cbTarget.Size = new System.Drawing.Size(300, 25);
            this.cbTarget.TabIndex = 1;
            //
            // btnApply
            //
            this.btnApply.AutoSize = true;
            this.btnApply.Location = new System.Drawing.Point(465, 3);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(200, 27);
            this.btnApply.TabIndex = 2;
            this.btnApply.Text = "Make the hydraulic profile";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // lblSummary
            //
            this.lblSummary.AutoSize = true;
            this.lblSummary.Location = new System.Drawing.Point(3, 278);
            this.lblSummary.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.lblSummary.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(0, 17);
            this.lblSummary.TabIndex = 2;
            //
            // dgvSections
            //
            this.dgvSections.AllowUserToAddRows = false;
            this.dgvSections.AllowUserToDeleteRows = false;
            this.dgvSections.AllowUserToResizeRows = false;
            this.dgvSections.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvSections.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvSections.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvSections.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSections.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSecType,
            this.colSecQuantity,
            this.colSecLength,
            this.colSecElevation,
            this.colSecDiameters,
            this.colSecMaterial});
            this.dgvSections.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSections.Location = new System.Drawing.Point(3, 301);
            this.dgvSections.Name = "dgvSections";
            this.dgvSections.ReadOnly = true;
            this.dgvSections.RowHeadersVisible = false;
            this.dgvSections.Size = new System.Drawing.Size(990, 346);
            this.dgvSections.TabIndex = 3;
            //
            // colSecType
            //
            this.colSecType.FillWeight = 34F;
            this.colSecType.HeaderText = "Section";
            this.colSecType.Name = "colSecType";
            this.colSecType.ReadOnly = true;
            //
            // colSecQuantity
            //
            this.colSecQuantity.FillWeight = 10F;
            this.colSecQuantity.HeaderText = "Quantity";
            this.colSecQuantity.Name = "colSecQuantity";
            this.colSecQuantity.ReadOnly = true;
            //
            // colSecLength
            //
            this.colSecLength.FillWeight = 12F;
            this.colSecLength.HeaderText = "Length (m)";
            this.colSecLength.Name = "colSecLength";
            this.colSecLength.ReadOnly = true;
            //
            // colSecElevation
            //
            this.colSecElevation.FillWeight = 12F;
            this.colSecElevation.HeaderText = "Rise (m)";
            this.colSecElevation.Name = "colSecElevation";
            this.colSecElevation.ReadOnly = true;
            //
            // colSecDiameters
            //
            this.colSecDiameters.FillWeight = 18F;
            this.colSecDiameters.HeaderText = "OD / ID (in)";
            this.colSecDiameters.Name = "colSecDiameters";
            this.colSecDiameters.ReadOnly = true;
            //
            // colSecMaterial
            //
            this.colSecMaterial.FillWeight = 14F;
            this.colSecMaterial.HeaderText = "Material";
            this.colSecMaterial.Name = "colSecMaterial";
            this.colSecMaterial.ReadOnly = true;
            //
            // IsometricImportWizard
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1184, 761);
            this.Controls.Add(this.wizard);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.MinimizeBox = false;
            this.Name = "IsometricImportWizard";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import a Pipe Segment from a Piping Isometric";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.IsometricImportWizard_FormClosing);
            this.Load += new System.EventHandler(this.IsometricImportWizard_Load);
            this.tlpDrawing.ResumeLayout(false);
            this.flpVision.ResumeLayout(false);
            this.flpVision.PerformLayout();
            this.tlpDrawing.PerformLayout();
            this.flpRead.ResumeLayout(false);
            this.flpRead.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).EndInit();
            this.splitRoute.Panel1.ResumeLayout(false);
            this.splitRoute.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitRoute)).EndInit();
            this.splitRoute.ResumeLayout(false);
            this.tlpRouteLeft.ResumeLayout(false);
            this.tlpRouteLeft.PerformLayout();
            this.tlpLine.ResumeLayout(false);
            this.tlpLine.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLegs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picRoute)).EndInit();
            this.tlpPipe.ResumeLayout(false);
            this.tlpPipe.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChecks)).EndInit();
            this.flpTarget.ResumeLayout(false);
            this.flpTarget.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSections)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.wizard)).EndInit();
            this.wpDrawing.ResumeLayout(false);
            this.wpDrawing.PerformLayout();
            this.wpRoute.ResumeLayout(false);
            this.wpRoute.PerformLayout();
            this.wpPipe.ResumeLayout(false);
            this.wpPipe.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private AeroWizard.StepWizardControl wizard;
        private AeroWizard.WizardPage wpDrawing;
        private System.Windows.Forms.Label lblDrawingHelp;
        private AeroWizard.WizardPage wpRoute;
        private System.Windows.Forms.Label lblRouteHelp;
        private AeroWizard.WizardPage wpPipe;
        private System.Windows.Forms.Label lblPipeHelp;

        private System.Windows.Forms.TableLayoutPanel tlpDrawing;
        private System.Windows.Forms.FlowLayoutPanel flpVision;
        private System.Windows.Forms.Label lblVision;
        private System.Windows.Forms.LinkLabel lnkAssistantSettings;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.Label lblFile;
        private System.Windows.Forms.TextBox txtFile;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.FlowLayoutPanel flpRead;
        private System.Windows.Forms.ProgressBar prgRead;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnCancelRead;
        private System.Windows.Forms.Button btnOpenReading;
        private System.Windows.Forms.Button btnSaveReading;
        private System.Windows.Forms.Label lblReadStatus;
        private System.Windows.Forms.PictureBox picDrawing;
        private System.Windows.Forms.SplitContainer splitRoute;
        private System.Windows.Forms.TableLayoutPanel tlpRouteLeft;
        private System.Windows.Forms.TableLayoutPanel tlpLine;
        private System.Windows.Forms.Label lblLine;
        private System.Windows.Forms.TextBox txtLine;
        private System.Windows.Forms.Label lblSize;
        private System.Windows.Forms.TextBox txtSize;
        private System.Windows.Forms.Label lblSchedule;
        private System.Windows.Forms.TextBox txtSchedule;
        private System.Windows.Forms.Label lblOd;
        private System.Windows.Forms.TextBox txtOd;
        private System.Windows.Forms.Label lblWall;
        private System.Windows.Forms.TextBox txtWall;
        private System.Windows.Forms.Label lblMaterial;
        private System.Windows.Forms.TextBox txtMaterial;
        private System.Windows.Forms.Label lblEnds;
        private System.Windows.Forms.Label lblEndsValue;
        private System.Windows.Forms.DataGridView dgvLegs;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colLegUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLegNumber;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLegDirection;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLegLength;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLegRise;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLegFittings;
        private System.Windows.Forms.PictureBox picRoute;
        private System.Windows.Forms.TableLayoutPanel tlpPipe;
        private System.Windows.Forms.DataGridView dgvChecks;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckSubject;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckText;
        private System.Windows.Forms.FlowLayoutPanel flpTarget;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.ComboBox cbTarget;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.DataGridView dgvSections;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecQuantity;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecLength;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecElevation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecDiameters;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSecMaterial;
    }
}
