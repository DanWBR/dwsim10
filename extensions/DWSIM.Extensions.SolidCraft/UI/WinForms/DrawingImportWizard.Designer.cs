namespace DWSIM.Extensions.SolidCraft
{
    partial class DrawingImportWizard
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
            this.lstSteps = new System.Windows.Forms.ListBox();
            this.tlpHeader = new System.Windows.Forms.TableLayoutPanel();
            this.lblStepTitle = new System.Windows.Forms.Label();
            this.lblStepText = new System.Windows.Forms.Label();
            this.pnlPages = new System.Windows.Forms.Panel();
            this.tlpDrawing = new System.Windows.Forms.TableLayoutPanel();
            this.flpVision = new System.Windows.Forms.FlowLayoutPanel();
            this.lblVision = new System.Windows.Forms.Label();
            this.lnkAssistantSettings = new System.Windows.Forms.LinkLabel();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtFile = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblKind = new System.Windows.Forms.Label();
            this.cbKind = new System.Windows.Forms.ComboBox();
            this.flpRead = new System.Windows.Forms.FlowLayoutPanel();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnCancelRead = new System.Windows.Forms.Button();
            this.btnOpenReading = new System.Windows.Forms.Button();
            this.btnSaveReading = new System.Windows.Forms.Button();
            this.lblReadStatus = new System.Windows.Forms.Label();
            this.picDrawing = new System.Windows.Forms.PictureBox();
            this.splitItems = new System.Windows.Forms.SplitContainer();
            this.dgvItems = new System.Windows.Forms.DataGridView();
            this.colItemInclude = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colItemTag = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colItemCategory = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colItemType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.picItems = new System.Windows.Forms.PictureBox();
            this.splitLines = new System.Windows.Forms.SplitContainer();
            this.dgvLines = new System.Windows.Forms.DataGridView();
            this.colLineInclude = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colLineName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLineFrom = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLineTo = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLineT = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLineP = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLineW = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLineVF = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLineComp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.picLines = new System.Windows.Forms.PictureBox();
            this.tlpProcess = new System.Windows.Forms.TableLayoutPanel();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.lblClass = new System.Windows.Forms.Label();
            this.cbClass = new System.Windows.Forms.ComboBox();
            this.lblPackage = new System.Windows.Forms.Label();
            this.cbPackage = new System.Windows.Forms.ComboBox();
            this.gbUnits = new System.Windows.Forms.GroupBox();
            this.tlpUnits = new System.Windows.Forms.TableLayoutPanel();
            this.lblUnitT = new System.Windows.Forms.Label();
            this.cbUnitT = new System.Windows.Forms.ComboBox();
            this.lblUnitP = new System.Windows.Forms.Label();
            this.cbUnitP = new System.Windows.Forms.ComboBox();
            this.lblUnitW = new System.Windows.Forms.Label();
            this.cbUnitW = new System.Windows.Forms.ComboBox();
            this.lblUnitE = new System.Windows.Forms.Label();
            this.cbUnitE = new System.Windows.Forms.ComboBox();
            this.gbCompounds = new System.Windows.Forms.GroupBox();
            this.tlpCompounds = new System.Windows.Forms.TableLayoutPanel();
            this.lstCompounds = new System.Windows.Forms.ListBox();
            this.flpCompoundButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAddCompound = new System.Windows.Forms.Button();
            this.btnRemoveCompound = new System.Windows.Forms.Button();
            this.btnSimWizard = new System.Windows.Forms.Button();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lstFound = new System.Windows.Forms.ListBox();
            this.tlpBuild = new System.Windows.Forms.TableLayoutPanel();
            this.dgvIssues = new System.Windows.Forms.DataGridView();
            this.colIssueLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIssueSubject = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIssueText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.flpBuild = new System.Windows.Forms.FlowLayoutPanel();
            this.chkSpecs = new System.Windows.Forms.CheckBox();
            this.chkUnits = new System.Windows.Forms.CheckBox();
            this.chkSolve = new System.Windows.Forms.CheckBox();
            this.btnBuild = new System.Windows.Forms.Button();
            this.lblSummary = new System.Windows.Forms.Label();
            this.flpFit = new System.Windows.Forms.FlowLayoutPanel();
            this.lblFit = new System.Windows.Forms.Label();
            this.clbFit = new System.Windows.Forms.CheckedListBox();
            this.btnFit = new System.Windows.Forms.Button();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.colResLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colResSubject = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colResText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.flpButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.tlpRoot.SuspendLayout();
            this.tlpHeader.SuspendLayout();
            this.pnlPages.SuspendLayout();
            this.tlpDrawing.SuspendLayout();
            this.flpVision.SuspendLayout();
            this.flpRead.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitItems)).BeginInit();
            this.splitItems.Panel1.SuspendLayout();
            this.splitItems.Panel2.SuspendLayout();
            this.splitItems.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvItems)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picItems)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitLines)).BeginInit();
            this.splitLines.Panel1.SuspendLayout();
            this.splitLines.Panel2.SuspendLayout();
            this.splitLines.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLines)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picLines)).BeginInit();
            this.tlpProcess.SuspendLayout();
            this.gbUnits.SuspendLayout();
            this.tlpUnits.SuspendLayout();
            this.gbCompounds.SuspendLayout();
            this.tlpCompounds.SuspendLayout();
            this.flpCompoundButtons.SuspendLayout();
            this.tlpBuild.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvIssues)).BeginInit();
            this.flpBuild.SuspendLayout();
            this.flpFit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.flpButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.ColumnCount = 2;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 170F));
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.lstSteps, 0, 0);
            this.tlpRoot.Controls.Add(this.tlpHeader, 1, 0);
            this.tlpRoot.Controls.Add(this.pnlPages, 1, 1);
            this.tlpRoot.Controls.Add(this.flpButtons, 0, 2);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(6);
            this.tlpRoot.RowCount = 3;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.Size = new System.Drawing.Size(1184, 761);
            this.tlpRoot.TabIndex = 0;
            //
            // lstSteps
            //
            this.lstSteps.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstSteps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstSteps.FormattingEnabled = true;
            this.lstSteps.IntegralHeight = false;
            this.lstSteps.ItemHeight = 20;
            this.lstSteps.Location = new System.Drawing.Point(9, 9);
            this.lstSteps.Name = "lstSteps";
            this.tlpRoot.SetRowSpan(this.lstSteps, 2);
            this.lstSteps.Size = new System.Drawing.Size(164, 702);
            this.lstSteps.TabIndex = 0;
            this.lstSteps.SelectedIndexChanged += new System.EventHandler(this.lstSteps_SelectedIndexChanged);
            //
            // tlpHeader
            //
            this.tlpHeader.AutoSize = true;
            this.tlpHeader.ColumnCount = 1;
            this.tlpHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpHeader.Controls.Add(this.lblStepTitle, 0, 0);
            this.tlpHeader.Controls.Add(this.lblStepText, 0, 1);
            this.tlpHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpHeader.Location = new System.Drawing.Point(179, 9);
            this.tlpHeader.Name = "tlpHeader";
            this.tlpHeader.RowCount = 2;
            this.tlpHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpHeader.Size = new System.Drawing.Size(996, 46);
            this.tlpHeader.TabIndex = 1;
            //
            // lblStepTitle
            //
            this.lblStepTitle.AutoSize = true;
            this.lblStepTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblStepTitle.Location = new System.Drawing.Point(3, 0);
            this.lblStepTitle.Name = "lblStepTitle";
            this.lblStepTitle.Size = new System.Drawing.Size(60, 20);
            this.lblStepTitle.TabIndex = 0;
            this.lblStepTitle.Text = "Drawing";
            //
            // lblStepText
            //
            this.lblStepText.AutoSize = true;
            this.lblStepText.Location = new System.Drawing.Point(3, 23);
            this.lblStepText.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.lblStepText.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblStepText.Name = "lblStepText";
            this.lblStepText.Size = new System.Drawing.Size(40, 17);
            this.lblStepText.TabIndex = 1;
            this.lblStepText.Text = "Step";
            //
            // pnlPages
            //
            this.pnlPages.Controls.Add(this.tlpDrawing);
            this.pnlPages.Controls.Add(this.splitItems);
            this.pnlPages.Controls.Add(this.splitLines);
            this.pnlPages.Controls.Add(this.tlpProcess);
            this.pnlPages.Controls.Add(this.tlpBuild);
            this.pnlPages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPages.Location = new System.Drawing.Point(179, 61);
            this.pnlPages.Name = "pnlPages";
            this.pnlPages.Size = new System.Drawing.Size(996, 650);
            this.pnlPages.TabIndex = 2;
            //
            // flpVision
            //
            this.flpVision.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.flpVision, 3);
            this.flpVision.Controls.Add(this.lblVision);
            this.flpVision.Controls.Add(this.lnkAssistantSettings);
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
            this.tlpDrawing.Controls.Add(this.flpVision, 0, 2);
            this.tlpDrawing.Controls.Add(this.lblFile, 0, 0);
            this.tlpDrawing.Controls.Add(this.txtFile, 1, 0);
            this.tlpDrawing.Controls.Add(this.btnBrowse, 2, 0);
            this.tlpDrawing.Controls.Add(this.lblKind, 0, 1);
            this.tlpDrawing.Controls.Add(this.cbKind, 1, 1);
            this.tlpDrawing.Controls.Add(this.flpRead, 0, 3);
            this.tlpDrawing.Controls.Add(this.lblReadStatus, 0, 4);
            this.tlpDrawing.Controls.Add(this.picDrawing, 0, 5);
            this.tlpDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpDrawing.Location = new System.Drawing.Point(0, 0);
            this.tlpDrawing.Name = "tlpDrawing";
            this.tlpDrawing.RowCount = 6;
            this.tlpDrawing.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
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
            this.lblFile.Size = new System.Drawing.Size(56, 17);
            this.lblFile.TabIndex = 0;
            this.lblFile.Text = "Drawing";
            //
            // txtFile
            //
            this.txtFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFile.Location = new System.Drawing.Point(65, 5);
            this.txtFile.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.txtFile.Name = "txtFile";
            this.txtFile.Size = new System.Drawing.Size(840, 25);
            this.txtFile.TabIndex = 1;
            this.txtFile.TextChanged += new System.EventHandler(this.txtFile_TextChanged);
            //
            // btnBrowse
            //
            this.btnBrowse.AutoSize = true;
            this.btnBrowse.Location = new System.Drawing.Point(911, 3);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(82, 27);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            //
            // lblKind
            //
            this.lblKind.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblKind.AutoSize = true;
            this.lblKind.Location = new System.Drawing.Point(3, 40);
            this.lblKind.Name = "lblKind";
            this.lblKind.Size = new System.Drawing.Size(36, 17);
            this.lblKind.TabIndex = 3;
            this.lblKind.Text = "Type";
            //
            // cbKind
            //
            this.cbKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbKind.FormattingEnabled = true;
            this.cbKind.Items.AddRange(new object[] {
            "Detect",
            "Process flow diagram (PFD)",
            "Piping and instrumentation diagram (P&ID)"});
            this.cbKind.Location = new System.Drawing.Point(65, 36);
            this.cbKind.Name = "cbKind";
            this.cbKind.Size = new System.Drawing.Size(320, 25);
            this.cbKind.TabIndex = 4;
            //
            // flpRead
            //
            this.flpRead.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.flpRead, 3);
            this.flpRead.Controls.Add(this.btnRead);
            this.flpRead.Controls.Add(this.btnCancelRead);
            this.flpRead.Controls.Add(this.btnOpenReading);
            this.flpRead.Controls.Add(this.btnSaveReading);
            this.flpRead.Location = new System.Drawing.Point(3, 67);
            this.flpRead.Name = "flpRead";
            this.flpRead.Size = new System.Drawing.Size(600, 33);
            this.flpRead.TabIndex = 5;
            //
            // btnRead
            //
            this.btnRead.AutoSize = true;
            this.btnRead.Location = new System.Drawing.Point(3, 3);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(120, 27);
            this.btnRead.TabIndex = 0;
            this.btnRead.Text = "Read the drawing";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);
            //
            // btnCancelRead
            //
            this.btnCancelRead.AutoSize = true;
            this.btnCancelRead.Enabled = false;
            this.btnCancelRead.Location = new System.Drawing.Point(129, 3);
            this.btnCancelRead.Name = "btnCancelRead";
            this.btnCancelRead.Size = new System.Drawing.Size(75, 27);
            this.btnCancelRead.TabIndex = 1;
            this.btnCancelRead.Text = "Cancel";
            this.btnCancelRead.UseVisualStyleBackColor = true;
            this.btnCancelRead.Click += new System.EventHandler(this.btnCancelRead_Click);
            //
            // btnOpenReading
            //
            this.btnOpenReading.AutoSize = true;
            this.btnOpenReading.Location = new System.Drawing.Point(210, 3);
            this.btnOpenReading.Name = "btnOpenReading";
            this.btnOpenReading.Size = new System.Drawing.Size(140, 27);
            this.btnOpenReading.TabIndex = 2;
            this.btnOpenReading.Text = "Open a saved reading...";
            this.btnOpenReading.UseVisualStyleBackColor = true;
            this.btnOpenReading.Click += new System.EventHandler(this.btnOpenReading_Click);
            //
            // btnSaveReading
            //
            this.btnSaveReading.AutoSize = true;
            this.btnSaveReading.Enabled = false;
            this.btnSaveReading.Location = new System.Drawing.Point(356, 3);
            this.btnSaveReading.Name = "btnSaveReading";
            this.btnSaveReading.Size = new System.Drawing.Size(120, 27);
            this.btnSaveReading.TabIndex = 3;
            this.btnSaveReading.Text = "Save the reading...";
            this.btnSaveReading.UseVisualStyleBackColor = true;
            this.btnSaveReading.Click += new System.EventHandler(this.btnSaveReading_Click);
            //
            // lblReadStatus
            //
            this.lblReadStatus.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.lblReadStatus, 3);
            this.lblReadStatus.Location = new System.Drawing.Point(3, 103);
            this.lblReadStatus.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.lblReadStatus.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblReadStatus.Name = "lblReadStatus";
            this.lblReadStatus.Size = new System.Drawing.Size(0, 17);
            this.lblReadStatus.TabIndex = 6;
            //
            // picDrawing
            //
            this.picDrawing.BackColor = System.Drawing.Color.White;
            this.picDrawing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tlpDrawing.SetColumnSpan(this.picDrawing, 3);
            this.picDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picDrawing.Location = new System.Drawing.Point(3, 129);
            this.picDrawing.Name = "picDrawing";
            this.picDrawing.Size = new System.Drawing.Size(990, 518);
            this.picDrawing.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picDrawing.TabIndex = 7;
            this.picDrawing.TabStop = false;
            //
            // splitItems
            //
            this.splitItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitItems.Location = new System.Drawing.Point(0, 0);
            this.splitItems.Name = "splitItems";
            this.splitItems.Panel1.Controls.Add(this.dgvItems);
            this.splitItems.Panel2.Controls.Add(this.picItems);
            this.splitItems.Size = new System.Drawing.Size(996, 650);
            this.splitItems.SplitterDistance = 430;
            this.splitItems.TabIndex = 1;
            //
            // dgvItems
            //
            this.dgvItems.AllowUserToAddRows = false;
            this.dgvItems.AllowUserToResizeRows = false;
            this.dgvItems.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvItems.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvItems.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvItems.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvItems.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colItemInclude,
            this.colItemTag,
            this.colItemCategory,
            this.colItemType});
            this.dgvItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvItems.Location = new System.Drawing.Point(0, 0);
            this.dgvItems.Name = "dgvItems";
            this.dgvItems.RowHeadersVisible = false;
            this.dgvItems.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvItems.Size = new System.Drawing.Size(430, 650);
            this.dgvItems.TabIndex = 0;
            this.dgvItems.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvItems_CellValueChanged);
            this.dgvItems.CurrentCellDirtyStateChanged += new System.EventHandler(this.Grid_CurrentCellDirtyStateChanged);
            this.dgvItems.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.Grid_DataError);
            this.dgvItems.SelectionChanged += new System.EventHandler(this.dgvItems_SelectionChanged);
            //
            // colItemInclude
            //
            this.colItemInclude.FillWeight = 12F;
            this.colItemInclude.HeaderText = "Build";
            this.colItemInclude.Name = "colItemInclude";
            //
            // colItemTag
            //
            this.colItemTag.FillWeight = 34F;
            this.colItemTag.HeaderText = "Tag";
            this.colItemTag.Name = "colItemTag";
            //
            // colItemCategory
            //
            this.colItemCategory.FillWeight = 27F;
            this.colItemCategory.HeaderText = "Drawn as";
            this.colItemCategory.Name = "colItemCategory";
            //
            // colItemType
            //
            this.colItemType.FillWeight = 27F;
            this.colItemType.HeaderText = "DWSIM object";
            this.colItemType.Name = "colItemType";
            //
            // picItems
            //
            this.picItems.BackColor = System.Drawing.Color.White;
            this.picItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picItems.Location = new System.Drawing.Point(0, 0);
            this.picItems.Name = "picItems";
            this.picItems.Size = new System.Drawing.Size(562, 650);
            this.picItems.TabIndex = 0;
            this.picItems.TabStop = false;
            this.picItems.Paint += new System.Windows.Forms.PaintEventHandler(this.picItems_Paint);
            this.picItems.Resize += new System.EventHandler(this.Picture_Resize);
            //
            // splitLines
            //
            this.splitLines.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitLines.Location = new System.Drawing.Point(0, 0);
            this.splitLines.Name = "splitLines";
            this.splitLines.Panel1.Controls.Add(this.dgvLines);
            this.splitLines.Panel2.Controls.Add(this.picLines);
            this.splitLines.Size = new System.Drawing.Size(996, 650);
            this.splitLines.SplitterDistance = 620;
            this.splitLines.TabIndex = 2;
            //
            // dgvLines
            //
            this.dgvLines.AllowUserToAddRows = false;
            this.dgvLines.AllowUserToResizeRows = false;
            this.dgvLines.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvLines.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvLines.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvLines.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLines.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLineInclude,
            this.colLineName,
            this.colLineFrom,
            this.colLineTo,
            this.colLineT,
            this.colLineP,
            this.colLineW,
            this.colLineVF,
            this.colLineComp});
            this.dgvLines.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLines.Location = new System.Drawing.Point(0, 0);
            this.dgvLines.Name = "dgvLines";
            this.dgvLines.RowHeadersVisible = false;
            this.dgvLines.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvLines.Size = new System.Drawing.Size(620, 650);
            this.dgvLines.TabIndex = 0;
            this.dgvLines.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvLines_CellValueChanged);
            this.dgvLines.CurrentCellDirtyStateChanged += new System.EventHandler(this.Grid_CurrentCellDirtyStateChanged);
            this.dgvLines.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.Grid_DataError);
            this.dgvLines.SelectionChanged += new System.EventHandler(this.dgvLines_SelectionChanged);
            //
            // colLineInclude
            //
            this.colLineInclude.FillWeight = 8F;
            this.colLineInclude.HeaderText = "Build";
            this.colLineInclude.Name = "colLineInclude";
            //
            // colLineName
            //
            this.colLineName.FillWeight = 18F;
            this.colLineName.HeaderText = "Stream / line";
            this.colLineName.Name = "colLineName";
            //
            // colLineFrom
            //
            this.colLineFrom.FillWeight = 16F;
            this.colLineFrom.HeaderText = "From";
            this.colLineFrom.Name = "colLineFrom";
            //
            // colLineTo
            //
            this.colLineTo.FillWeight = 16F;
            this.colLineTo.HeaderText = "To";
            this.colLineTo.Name = "colLineTo";
            //
            // colLineT
            //
            this.colLineT.FillWeight = 9F;
            this.colLineT.HeaderText = "T";
            this.colLineT.Name = "colLineT";
            //
            // colLineP
            //
            this.colLineP.FillWeight = 9F;
            this.colLineP.HeaderText = "P";
            this.colLineP.Name = "colLineP";
            //
            // colLineW
            //
            this.colLineW.FillWeight = 9F;
            this.colLineW.HeaderText = "Mass flow";
            this.colLineW.Name = "colLineW";
            //
            // colLineVF
            //
            this.colLineVF.FillWeight = 7F;
            this.colLineVF.HeaderText = "Vapor fraction";
            this.colLineVF.Name = "colLineVF";
            //
            // colLineComp
            //
            this.colLineComp.FillWeight = 8F;
            this.colLineComp.HeaderText = "Compounds";
            this.colLineComp.Name = "colLineComp";
            this.colLineComp.ReadOnly = true;
            //
            // picLines
            //
            this.picLines.BackColor = System.Drawing.Color.White;
            this.picLines.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picLines.Location = new System.Drawing.Point(0, 0);
            this.picLines.Name = "picLines";
            this.picLines.Size = new System.Drawing.Size(372, 650);
            this.picLines.TabIndex = 0;
            this.picLines.TabStop = false;
            this.picLines.Paint += new System.Windows.Forms.PaintEventHandler(this.picLines_Paint);
            this.picLines.Resize += new System.EventHandler(this.Picture_Resize);
            //
            // tlpProcess
            //
            this.tlpProcess.ColumnCount = 2;
            this.tlpProcess.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProcess.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpProcess.Controls.Add(this.txtDescription, 0, 0);
            this.tlpProcess.Controls.Add(this.lblClass, 0, 1);
            this.tlpProcess.Controls.Add(this.cbClass, 1, 1);
            this.tlpProcess.Controls.Add(this.lblPackage, 0, 2);
            this.tlpProcess.Controls.Add(this.cbPackage, 1, 2);
            this.tlpProcess.Controls.Add(this.gbUnits, 0, 3);
            this.tlpProcess.Controls.Add(this.gbCompounds, 0, 4);
            this.tlpProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpProcess.Location = new System.Drawing.Point(0, 0);
            this.tlpProcess.Name = "tlpProcess";
            this.tlpProcess.RowCount = 5;
            this.tlpProcess.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProcess.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProcess.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProcess.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpProcess.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpProcess.Size = new System.Drawing.Size(996, 650);
            this.tlpProcess.TabIndex = 3;
            //
            // txtDescription
            //
            this.tlpProcess.SetColumnSpan(this.txtDescription, 2);
            this.txtDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDescription.Location = new System.Drawing.Point(3, 3);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ReadOnly = true;
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.Size = new System.Drawing.Size(990, 70);
            this.txtDescription.TabIndex = 0;
            //
            // lblClass
            //
            this.lblClass.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblClass.AutoSize = true;
            this.lblClass.Location = new System.Drawing.Point(3, 83);
            this.lblClass.Name = "lblClass";
            this.lblClass.Size = new System.Drawing.Size(120, 17);
            this.lblClass.TabIndex = 1;
            this.lblClass.Text = "Kind of process";
            //
            // cbClass
            //
            this.cbClass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbClass.FormattingEnabled = true;
            this.cbClass.Location = new System.Drawing.Point(129, 79);
            this.cbClass.Name = "cbClass";
            this.cbClass.Size = new System.Drawing.Size(420, 25);
            this.cbClass.TabIndex = 2;
            this.cbClass.SelectedIndexChanged += new System.EventHandler(this.cbClass_SelectedIndexChanged);
            //
            // lblPackage
            //
            this.lblPackage.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPackage.AutoSize = true;
            this.lblPackage.Location = new System.Drawing.Point(3, 114);
            this.lblPackage.Name = "lblPackage";
            this.lblPackage.Size = new System.Drawing.Size(120, 17);
            this.lblPackage.TabIndex = 3;
            this.lblPackage.Text = "Property package";
            //
            // cbPackage
            //
            this.cbPackage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbPackage.FormattingEnabled = true;
            this.cbPackage.Location = new System.Drawing.Point(129, 110);
            this.cbPackage.Name = "cbPackage";
            this.cbPackage.Size = new System.Drawing.Size(420, 25);
            this.cbPackage.TabIndex = 4;
            this.cbPackage.SelectedIndexChanged += new System.EventHandler(this.cbPackage_SelectedIndexChanged);
            //
            // gbUnits
            //
            this.gbUnits.AutoSize = true;
            this.tlpProcess.SetColumnSpan(this.gbUnits, 2);
            this.gbUnits.Controls.Add(this.tlpUnits);
            this.gbUnits.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbUnits.Location = new System.Drawing.Point(3, 141);
            this.gbUnits.Name = "gbUnits";
            this.gbUnits.Size = new System.Drawing.Size(990, 56);
            this.gbUnits.TabIndex = 5;
            this.gbUnits.TabStop = false;
            this.gbUnits.Text = "Units of the drawing's values";
            //
            // tlpUnits
            //
            this.tlpUnits.AutoSize = true;
            this.tlpUnits.ColumnCount = 8;
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpUnits.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpUnits.Controls.Add(this.lblUnitT, 0, 0);
            this.tlpUnits.Controls.Add(this.cbUnitT, 1, 0);
            this.tlpUnits.Controls.Add(this.lblUnitP, 2, 0);
            this.tlpUnits.Controls.Add(this.cbUnitP, 3, 0);
            this.tlpUnits.Controls.Add(this.lblUnitW, 4, 0);
            this.tlpUnits.Controls.Add(this.cbUnitW, 5, 0);
            this.tlpUnits.Controls.Add(this.lblUnitE, 6, 0);
            this.tlpUnits.Controls.Add(this.cbUnitE, 7, 0);
            this.tlpUnits.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpUnits.Location = new System.Drawing.Point(3, 21);
            this.tlpUnits.Name = "tlpUnits";
            this.tlpUnits.RowCount = 1;
            this.tlpUnits.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpUnits.Size = new System.Drawing.Size(984, 32);
            this.tlpUnits.TabIndex = 0;
            //
            // lblUnitT
            //
            this.lblUnitT.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUnitT.AutoSize = true;
            this.lblUnitT.Location = new System.Drawing.Point(3, 7);
            this.lblUnitT.Name = "lblUnitT";
            this.lblUnitT.Size = new System.Drawing.Size(85, 17);
            this.lblUnitT.TabIndex = 0;
            this.lblUnitT.Text = "Temperature";
            //
            // cbUnitT
            //
            this.cbUnitT.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbUnitT.FormattingEnabled = true;
            this.cbUnitT.Location = new System.Drawing.Point(94, 3);
            this.cbUnitT.Name = "cbUnitT";
            this.cbUnitT.Size = new System.Drawing.Size(140, 25);
            this.cbUnitT.TabIndex = 1;
            //
            // lblUnitP
            //
            this.lblUnitP.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUnitP.AutoSize = true;
            this.lblUnitP.Location = new System.Drawing.Point(240, 7);
            this.lblUnitP.Name = "lblUnitP";
            this.lblUnitP.Size = new System.Drawing.Size(60, 17);
            this.lblUnitP.TabIndex = 2;
            this.lblUnitP.Text = "Pressure";
            //
            // cbUnitP
            //
            this.cbUnitP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbUnitP.FormattingEnabled = true;
            this.cbUnitP.Location = new System.Drawing.Point(306, 3);
            this.cbUnitP.Name = "cbUnitP";
            this.cbUnitP.Size = new System.Drawing.Size(140, 25);
            this.cbUnitP.TabIndex = 3;
            //
            // lblUnitW
            //
            this.lblUnitW.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUnitW.AutoSize = true;
            this.lblUnitW.Location = new System.Drawing.Point(452, 7);
            this.lblUnitW.Name = "lblUnitW";
            this.lblUnitW.Size = new System.Drawing.Size(68, 17);
            this.lblUnitW.TabIndex = 4;
            this.lblUnitW.Text = "Mass flow";
            //
            // cbUnitW
            //
            this.cbUnitW.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbUnitW.FormattingEnabled = true;
            this.cbUnitW.Location = new System.Drawing.Point(526, 3);
            this.cbUnitW.Name = "cbUnitW";
            this.cbUnitW.Size = new System.Drawing.Size(140, 25);
            this.cbUnitW.TabIndex = 5;
            //
            // lblUnitE
            //
            this.lblUnitE.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUnitE.AutoSize = true;
            this.lblUnitE.Location = new System.Drawing.Point(672, 7);
            this.lblUnitE.Name = "lblUnitE";
            this.lblUnitE.Size = new System.Drawing.Size(50, 17);
            this.lblUnitE.TabIndex = 6;
            this.lblUnitE.Text = "Energy";
            //
            // cbUnitE
            //
            this.cbUnitE.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbUnitE.FormattingEnabled = true;
            this.cbUnitE.Location = new System.Drawing.Point(728, 3);
            this.cbUnitE.Name = "cbUnitE";
            this.cbUnitE.Size = new System.Drawing.Size(140, 25);
            this.cbUnitE.TabIndex = 7;
            //
            // gbCompounds
            //
            this.tlpProcess.SetColumnSpan(this.gbCompounds, 2);
            this.gbCompounds.Controls.Add(this.tlpCompounds);
            this.gbCompounds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gbCompounds.Location = new System.Drawing.Point(3, 203);
            this.gbCompounds.Name = "gbCompounds";
            this.gbCompounds.Size = new System.Drawing.Size(990, 444);
            this.gbCompounds.TabIndex = 6;
            this.gbCompounds.TabStop = false;
            this.gbCompounds.Text = "Compounds";
            //
            // tlpCompounds
            //
            this.tlpCompounds.ColumnCount = 3;
            this.tlpCompounds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpCompounds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpCompounds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpCompounds.Controls.Add(this.lstCompounds, 0, 0);
            this.tlpCompounds.Controls.Add(this.flpCompoundButtons, 1, 0);
            this.tlpCompounds.Controls.Add(this.txtSearch, 2, 0);
            this.tlpCompounds.Controls.Add(this.lstFound, 2, 1);
            this.tlpCompounds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpCompounds.Location = new System.Drawing.Point(3, 21);
            this.tlpCompounds.Name = "tlpCompounds";
            this.tlpCompounds.RowCount = 2;
            this.tlpCompounds.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpCompounds.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpCompounds.Size = new System.Drawing.Size(984, 420);
            this.tlpCompounds.TabIndex = 0;
            //
            // lstCompounds
            //
            this.lstCompounds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstCompounds.FormattingEnabled = true;
            this.lstCompounds.IntegralHeight = false;
            this.lstCompounds.ItemHeight = 17;
            this.lstCompounds.Location = new System.Drawing.Point(3, 3);
            this.lstCompounds.Name = "lstCompounds";
            this.tlpCompounds.SetRowSpan(this.lstCompounds, 2);
            this.lstCompounds.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lstCompounds.Size = new System.Drawing.Size(400, 414);
            this.lstCompounds.TabIndex = 0;
            //
            // flpCompoundButtons
            //
            this.flpCompoundButtons.AutoSize = true;
            this.flpCompoundButtons.Controls.Add(this.btnAddCompound);
            this.flpCompoundButtons.Controls.Add(this.btnRemoveCompound);
            this.flpCompoundButtons.Controls.Add(this.btnSimWizard);
            this.flpCompoundButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpCompoundButtons.Location = new System.Drawing.Point(409, 3);
            this.flpCompoundButtons.Name = "flpCompoundButtons";
            this.tlpCompounds.SetRowSpan(this.flpCompoundButtons, 2);
            this.flpCompoundButtons.Size = new System.Drawing.Size(160, 99);
            this.flpCompoundButtons.TabIndex = 1;
            //
            // btnAddCompound
            //
            this.btnAddCompound.AutoSize = true;
            this.btnAddCompound.Location = new System.Drawing.Point(3, 3);
            this.btnAddCompound.Name = "btnAddCompound";
            this.btnAddCompound.Size = new System.Drawing.Size(154, 27);
            this.btnAddCompound.TabIndex = 0;
            this.btnAddCompound.Text = "< Add";
            this.btnAddCompound.UseVisualStyleBackColor = true;
            this.btnAddCompound.Click += new System.EventHandler(this.btnAddCompound_Click);
            //
            // btnRemoveCompound
            //
            this.btnRemoveCompound.AutoSize = true;
            this.btnRemoveCompound.Location = new System.Drawing.Point(3, 36);
            this.btnRemoveCompound.Name = "btnRemoveCompound";
            this.btnRemoveCompound.Size = new System.Drawing.Size(154, 27);
            this.btnRemoveCompound.TabIndex = 1;
            this.btnRemoveCompound.Text = "Remove";
            this.btnRemoveCompound.UseVisualStyleBackColor = true;
            this.btnRemoveCompound.Click += new System.EventHandler(this.btnRemoveCompound_Click);
            //
            // btnSimWizard
            //
            this.btnSimWizard.AutoSize = true;
            this.btnSimWizard.Location = new System.Drawing.Point(3, 69);
            this.btnSimWizard.Name = "btnSimWizard";
            this.btnSimWizard.Size = new System.Drawing.Size(154, 27);
            this.btnSimWizard.TabIndex = 2;
            this.btnSimWizard.Text = "Simulation Wizard...";
            this.btnSimWizard.UseVisualStyleBackColor = true;
            this.btnSimWizard.Click += new System.EventHandler(this.btnSimWizard_Click);
            //
            // txtSearch
            //
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.Location = new System.Drawing.Point(575, 3);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(406, 25);
            this.txtSearch.TabIndex = 2;
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);
            //
            // lstFound
            //
            this.lstFound.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstFound.FormattingEnabled = true;
            this.lstFound.IntegralHeight = false;
            this.lstFound.ItemHeight = 17;
            this.lstFound.Location = new System.Drawing.Point(575, 34);
            this.lstFound.Name = "lstFound";
            this.lstFound.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lstFound.Size = new System.Drawing.Size(406, 383);
            this.lstFound.TabIndex = 3;
            this.lstFound.DoubleClick += new System.EventHandler(this.btnAddCompound_Click);
            //
            // tlpBuild
            //
            this.tlpBuild.ColumnCount = 1;
            this.tlpBuild.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpBuild.Controls.Add(this.dgvIssues, 0, 0);
            this.tlpBuild.Controls.Add(this.flpBuild, 0, 1);
            this.tlpBuild.Controls.Add(this.lblSummary, 0, 2);
            this.tlpBuild.Controls.Add(this.flpFit, 0, 3);
            this.tlpBuild.Controls.Add(this.dgvResults, 0, 4);
            this.tlpBuild.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpBuild.Location = new System.Drawing.Point(0, 0);
            this.tlpBuild.Name = "tlpBuild";
            this.tlpBuild.RowCount = 5;
            this.tlpBuild.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBuild.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpBuild.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpBuild.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpBuild.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBuild.Size = new System.Drawing.Size(996, 650);
            this.tlpBuild.TabIndex = 4;
            //
            // dgvIssues
            //
            this.dgvIssues.AllowUserToAddRows = false;
            this.dgvIssues.AllowUserToDeleteRows = false;
            this.dgvIssues.AllowUserToResizeRows = false;
            this.dgvIssues.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvIssues.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvIssues.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvIssues.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvIssues.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvIssues.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colIssueLevel,
            this.colIssueSubject,
            this.colIssueText});
            this.dgvIssues.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvIssues.Location = new System.Drawing.Point(3, 3);
            this.dgvIssues.Name = "dgvIssues";
            this.dgvIssues.ReadOnly = true;
            this.dgvIssues.RowHeadersVisible = false;
            this.dgvIssues.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvIssues.Size = new System.Drawing.Size(990, 280);
            this.dgvIssues.TabIndex = 0;
            //
            // colIssueLevel
            //
            this.colIssueLevel.FillWeight = 12F;
            this.colIssueLevel.HeaderText = "Check";
            this.colIssueLevel.Name = "colIssueLevel";
            this.colIssueLevel.ReadOnly = true;
            //
            // colIssueSubject
            //
            this.colIssueSubject.FillWeight = 20F;
            this.colIssueSubject.HeaderText = "Item or line";
            this.colIssueSubject.Name = "colIssueSubject";
            this.colIssueSubject.ReadOnly = true;
            //
            // colIssueText
            //
            this.colIssueText.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.colIssueText.FillWeight = 68F;
            this.colIssueText.HeaderText = "What";
            this.colIssueText.Name = "colIssueText";
            this.colIssueText.ReadOnly = true;
            //
            // flpBuild
            //
            this.flpBuild.AutoSize = true;
            this.flpBuild.Controls.Add(this.chkSpecs);
            this.flpBuild.Controls.Add(this.chkUnits);
            this.flpBuild.Controls.Add(this.chkSolve);
            this.flpBuild.Controls.Add(this.btnBuild);
            this.flpBuild.Location = new System.Drawing.Point(3, 289);
            this.flpBuild.Name = "flpBuild";
            this.flpBuild.Size = new System.Drawing.Size(900, 33);
            this.flpBuild.TabIndex = 1;
            //
            // chkSpecs
            //
            this.chkSpecs.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkSpecs.AutoSize = true;
            this.chkSpecs.Checked = true;
            this.chkSpecs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSpecs.Location = new System.Drawing.Point(3, 8);
            this.chkSpecs.Name = "chkSpecs";
            this.chkSpecs.Size = new System.Drawing.Size(250, 21);
            this.chkSpecs.TabIndex = 0;
            this.chkSpecs.Text = "Set the items up from the stream table";
            this.chkSpecs.UseVisualStyleBackColor = true;
            //
            // chkUnits
            //
            this.chkUnits.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkUnits.AutoSize = true;
            this.chkUnits.Checked = true;
            this.chkUnits.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUnits.Location = new System.Drawing.Point(259, 8);
            this.chkUnits.Name = "chkUnits";
            this.chkUnits.Size = new System.Drawing.Size(200, 21);
            this.chkUnits.TabIndex = 1;
            this.chkUnits.Text = "Use the drawing's units";
            this.chkUnits.UseVisualStyleBackColor = true;
            //
            // chkSolve
            //
            this.chkSolve.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkSolve.AutoSize = true;
            this.chkSolve.Checked = true;
            this.chkSolve.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSolve.Location = new System.Drawing.Point(465, 8);
            this.chkSolve.Name = "chkSolve";
            this.chkSolve.Size = new System.Drawing.Size(220, 21);
            this.chkSolve.TabIndex = 2;
            this.chkSolve.Text = "Solve and compare with the table";
            this.chkSolve.UseVisualStyleBackColor = true;
            //
            // btnBuild
            //
            this.btnBuild.AutoSize = true;
            this.btnBuild.Location = new System.Drawing.Point(691, 3);
            this.btnBuild.Name = "btnBuild";
            this.btnBuild.Size = new System.Drawing.Size(140, 27);
            this.btnBuild.TabIndex = 3;
            this.btnBuild.Text = "Build the flowsheet";
            this.btnBuild.UseVisualStyleBackColor = true;
            this.btnBuild.Click += new System.EventHandler(this.btnBuild_Click);
            //
            // lblSummary
            //
            this.lblSummary.AutoSize = true;
            this.lblSummary.Location = new System.Drawing.Point(3, 325);
            this.lblSummary.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.lblSummary.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(0, 17);
            this.lblSummary.TabIndex = 2;
            //
            // flpFit
            //
            this.flpFit.AutoSize = true;
            this.flpFit.Controls.Add(this.lblFit);
            this.flpFit.Controls.Add(this.clbFit);
            this.flpFit.Controls.Add(this.btnFit);
            this.flpFit.Location = new System.Drawing.Point(3, 348);
            this.flpFit.Name = "flpFit";
            this.flpFit.Size = new System.Drawing.Size(900, 72);
            this.flpFit.TabIndex = 4;
            this.flpFit.Visible = false;
            //
            // lblFit
            //
            this.lblFit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblFit.AutoSize = true;
            this.lblFit.Location = new System.Drawing.Point(3, 27);
            this.lblFit.MaximumSize = new System.Drawing.Size(260, 0);
            this.lblFit.Name = "lblFit";
            this.lblFit.Size = new System.Drawing.Size(250, 17);
            this.lblFit.TabIndex = 0;
            this.lblFit.Text = "Fit what the drawing does not give to the stream table:";
            //
            // clbFit
            //
            this.clbFit.CheckOnClick = true;
            this.clbFit.FormattingEnabled = true;
            this.clbFit.IntegralHeight = false;
            this.clbFit.Location = new System.Drawing.Point(262, 3);
            this.clbFit.Name = "clbFit";
            this.clbFit.Size = new System.Drawing.Size(460, 66);
            this.clbFit.TabIndex = 1;
            //
            // btnFit
            //
            this.btnFit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.btnFit.AutoSize = true;
            this.btnFit.Location = new System.Drawing.Point(728, 22);
            this.btnFit.Name = "btnFit";
            this.btnFit.Size = new System.Drawing.Size(140, 27);
            this.btnFit.TabIndex = 2;
            this.btnFit.Text = "Fit to the table";
            this.btnFit.UseVisualStyleBackColor = true;
            this.btnFit.Click += new System.EventHandler(this.btnFit_Click);
            //
            // dgvResults
            //
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeRows = false;
            this.dgvResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvResults.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvResults.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvResults.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colResLevel,
            this.colResSubject,
            this.colResText});
            this.dgvResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvResults.Location = new System.Drawing.Point(3, 348);
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersVisible = false;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvResults.Size = new System.Drawing.Size(990, 299);
            this.dgvResults.TabIndex = 3;
            //
            // colResLevel
            //
            this.colResLevel.FillWeight = 12F;
            this.colResLevel.HeaderText = "Result";
            this.colResLevel.Name = "colResLevel";
            this.colResLevel.ReadOnly = true;
            //
            // colResSubject
            //
            this.colResSubject.FillWeight = 20F;
            this.colResSubject.HeaderText = "Item or stream";
            this.colResSubject.Name = "colResSubject";
            this.colResSubject.ReadOnly = true;
            //
            // colResText
            //
            this.colResText.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.colResText.FillWeight = 68F;
            this.colResText.HeaderText = "What";
            this.colResText.Name = "colResText";
            this.colResText.ReadOnly = true;
            //
            // flpButtons
            //
            this.flpButtons.AutoSize = true;
            this.tlpRoot.SetColumnSpan(this.flpButtons, 2);
            this.flpButtons.Controls.Add(this.btnNext);
            this.flpButtons.Controls.Add(this.btnBack);
            this.flpButtons.Controls.Add(this.btnClose);
            this.flpButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flpButtons.Location = new System.Drawing.Point(9, 717);
            this.flpButtons.Name = "flpButtons";
            this.flpButtons.Size = new System.Drawing.Size(1166, 35);
            this.flpButtons.TabIndex = 3;
            //
            // btnNext
            //
            this.btnNext.AutoSize = true;
            this.btnNext.Location = new System.Drawing.Point(1063, 3);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(100, 29);
            this.btnNext.TabIndex = 0;
            this.btnNext.Text = "Next >";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.btnNext_Click);
            //
            // btnBack
            //
            this.btnBack.AutoSize = true;
            this.btnBack.Location = new System.Drawing.Point(957, 3);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(100, 29);
            this.btnBack.TabIndex = 1;
            this.btnBack.Text = "< Back";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            //
            // btnClose
            //
            this.btnClose.AutoSize = true;
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(851, 3);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 29);
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            //
            // DrawingImportWizard
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(1184, 761);
            this.Controls.Add(this.tlpRoot);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.MinimizeBox = false;
            this.Name = "DrawingImportWizard";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import a Flowsheet from a Drawing";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DrawingImportWizard_FormClosing);
            this.Load += new System.EventHandler(this.DrawingImportWizard_Load);
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.tlpHeader.ResumeLayout(false);
            this.tlpHeader.PerformLayout();
            this.pnlPages.ResumeLayout(false);
            this.tlpDrawing.ResumeLayout(false);
            this.flpVision.ResumeLayout(false);
            this.flpVision.PerformLayout();
            this.tlpDrawing.PerformLayout();
            this.flpRead.ResumeLayout(false);
            this.flpRead.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).EndInit();
            this.splitItems.Panel1.ResumeLayout(false);
            this.splitItems.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitItems)).EndInit();
            this.splitItems.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvItems)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picItems)).EndInit();
            this.splitLines.Panel1.ResumeLayout(false);
            this.splitLines.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitLines)).EndInit();
            this.splitLines.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLines)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picLines)).EndInit();
            this.tlpProcess.ResumeLayout(false);
            this.tlpProcess.PerformLayout();
            this.gbUnits.ResumeLayout(false);
            this.gbUnits.PerformLayout();
            this.tlpUnits.ResumeLayout(false);
            this.tlpUnits.PerformLayout();
            this.gbCompounds.ResumeLayout(false);
            this.tlpCompounds.ResumeLayout(false);
            this.tlpCompounds.PerformLayout();
            this.flpCompoundButtons.ResumeLayout(false);
            this.flpCompoundButtons.PerformLayout();
            this.tlpBuild.ResumeLayout(false);
            this.tlpBuild.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvIssues)).EndInit();
            this.flpBuild.ResumeLayout(false);
            this.flpBuild.PerformLayout();
            this.flpFit.ResumeLayout(false);
            this.flpFit.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.flpButtons.ResumeLayout(false);
            this.flpButtons.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.ListBox lstSteps;
        private System.Windows.Forms.TableLayoutPanel tlpHeader;
        private System.Windows.Forms.Label lblStepTitle;
        private System.Windows.Forms.Label lblStepText;
        private System.Windows.Forms.Panel pnlPages;
        private System.Windows.Forms.TableLayoutPanel tlpDrawing;
        private System.Windows.Forms.FlowLayoutPanel flpVision;
        private System.Windows.Forms.Label lblVision;
        private System.Windows.Forms.LinkLabel lnkAssistantSettings;
        private System.Windows.Forms.Label lblFile;
        private System.Windows.Forms.TextBox txtFile;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblKind;
        private System.Windows.Forms.ComboBox cbKind;
        private System.Windows.Forms.FlowLayoutPanel flpRead;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnCancelRead;
        private System.Windows.Forms.Button btnOpenReading;
        private System.Windows.Forms.Button btnSaveReading;
        private System.Windows.Forms.Label lblReadStatus;
        private System.Windows.Forms.PictureBox picDrawing;
        private System.Windows.Forms.SplitContainer splitItems;
        private System.Windows.Forms.DataGridView dgvItems;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colItemInclude;
        private System.Windows.Forms.DataGridViewTextBoxColumn colItemTag;
        private System.Windows.Forms.DataGridViewComboBoxColumn colItemCategory;
        private System.Windows.Forms.DataGridViewComboBoxColumn colItemType;
        private System.Windows.Forms.PictureBox picItems;
        private System.Windows.Forms.SplitContainer splitLines;
        private System.Windows.Forms.DataGridView dgvLines;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colLineInclude;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineName;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLineFrom;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLineTo;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineT;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineP;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineW;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineVF;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineComp;
        private System.Windows.Forms.PictureBox picLines;
        private System.Windows.Forms.TableLayoutPanel tlpProcess;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Label lblClass;
        private System.Windows.Forms.ComboBox cbClass;
        private System.Windows.Forms.Label lblPackage;
        private System.Windows.Forms.ComboBox cbPackage;
        private System.Windows.Forms.GroupBox gbUnits;
        private System.Windows.Forms.TableLayoutPanel tlpUnits;
        private System.Windows.Forms.Label lblUnitT;
        private System.Windows.Forms.ComboBox cbUnitT;
        private System.Windows.Forms.Label lblUnitP;
        private System.Windows.Forms.ComboBox cbUnitP;
        private System.Windows.Forms.Label lblUnitW;
        private System.Windows.Forms.ComboBox cbUnitW;
        private System.Windows.Forms.Label lblUnitE;
        private System.Windows.Forms.ComboBox cbUnitE;
        private System.Windows.Forms.GroupBox gbCompounds;
        private System.Windows.Forms.TableLayoutPanel tlpCompounds;
        private System.Windows.Forms.ListBox lstCompounds;
        private System.Windows.Forms.FlowLayoutPanel flpCompoundButtons;
        private System.Windows.Forms.Button btnAddCompound;
        private System.Windows.Forms.Button btnRemoveCompound;
        private System.Windows.Forms.Button btnSimWizard;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ListBox lstFound;
        private System.Windows.Forms.TableLayoutPanel tlpBuild;
        private System.Windows.Forms.DataGridView dgvIssues;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIssueLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIssueSubject;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIssueText;
        private System.Windows.Forms.FlowLayoutPanel flpBuild;
        private System.Windows.Forms.CheckBox chkSpecs;
        private System.Windows.Forms.CheckBox chkUnits;
        private System.Windows.Forms.CheckBox chkSolve;
        private System.Windows.Forms.Button btnBuild;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.FlowLayoutPanel flpFit;
        private System.Windows.Forms.Label lblFit;
        private System.Windows.Forms.CheckedListBox clbFit;
        private System.Windows.Forms.Button btnFit;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResSubject;
        private System.Windows.Forms.DataGridViewTextBoxColumn colResText;
        private System.Windows.Forms.FlowLayoutPanel flpButtons;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnClose;
    }
}
