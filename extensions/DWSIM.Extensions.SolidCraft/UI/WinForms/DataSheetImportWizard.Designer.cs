namespace DWSIM.Extensions.SolidCraft
{
    partial class DataSheetImportWizard
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
            this.wpValues = new AeroWizard.WizardPage();
            this.lblValuesHelp = new System.Windows.Forms.Label();
            this.wpEquipment = new AeroWizard.WizardPage();
            this.lblEquipmentHelp = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.wizard)).BeginInit();
            this.wpDrawing.SuspendLayout();
            this.wpValues.SuspendLayout();
            this.wpEquipment.SuspendLayout();
            this.tlpDrawing = new System.Windows.Forms.TableLayoutPanel();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtFile = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.flpVision = new System.Windows.Forms.FlowLayoutPanel();
            this.lblVision = new System.Windows.Forms.Label();
            this.lnkAssistantSettings = new System.Windows.Forms.LinkLabel();
            this.lblModel = new System.Windows.Forms.Label();
            this.flpRead = new System.Windows.Forms.FlowLayoutPanel();
            this.prgRead = new System.Windows.Forms.ProgressBar();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnCancelRead = new System.Windows.Forms.Button();
            this.btnOpenReading = new System.Windows.Forms.Button();
            this.btnSaveReading = new System.Windows.Forms.Button();
            this.lblReadStatus = new System.Windows.Forms.Label();
            this.picDrawing = new System.Windows.Forms.PictureBox();
            this.tlpValues = new System.Windows.Forms.TableLayoutPanel();
            this.flpSheet = new System.Windows.Forms.FlowLayoutPanel();
            this.lblType = new System.Windows.Forms.Label();
            this.cbType = new System.Windows.Forms.ComboBox();
            this.lblTag = new System.Windows.Forms.Label();
            this.txtTag = new System.Windows.Forms.TextBox();
            this.dgvFields = new System.Windows.Forms.DataGridView();
            this.colFieldUse = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colFieldLabel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldUnit = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldKey = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colFieldCase = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldSide = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFieldStream = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tlpEquipment = new System.Windows.Forms.TableLayoutPanel();
            this.flpTarget = new System.Windows.Forms.FlowLayoutPanel();
            this.lblTarget = new System.Windows.Forms.Label();
            this.cbTarget = new System.Windows.Forms.ComboBox();
            this.lblCase = new System.Windows.Forms.Label();
            this.cbCase = new System.Windows.Forms.ComboBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.dgvChanges = new System.Windows.Forms.DataGridView();
            this.colChangeUse = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colChangeWhat = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colChangeCurrent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colChangeNew = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblSummary = new System.Windows.Forms.Label();
            this.dgvChecks = new System.Windows.Forms.DataGridView();
            this.colCheckLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCheckSubject = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCheckText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tlpDrawing.SuspendLayout();
            this.flpVision.SuspendLayout();
            this.flpRead.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).BeginInit();
            this.tlpValues.SuspendLayout();
            this.flpSheet.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFields)).BeginInit();
            this.tlpEquipment.SuspendLayout();
            this.flpTarget.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChanges)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChecks)).BeginInit();
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
            this.wizard.Pages.Add(this.wpValues);
            this.wizard.Pages.Add(this.wpEquipment);
            this.wizard.Size = new System.Drawing.Size(1184, 761);
            this.wizard.TabIndex = 0;
            this.wizard.Title = "Set Equipment up from a Data Sheet";
            //
            // wpDrawing
            //
            this.wpDrawing.Controls.Add(this.tlpDrawing);
            this.wpDrawing.Controls.Add(this.lblDrawingHelp);
            this.wpDrawing.Name = "wpDrawing";
            this.wpDrawing.Size = new System.Drawing.Size(980, 606);
            this.wpDrawing.TabIndex = 0;
            this.wpDrawing.Text = "Data sheet";
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
            this.lblDrawingHelp.Text = "Choose the equipment data sheet: an image, a PDF or a DXF/DWG drawing. The AI Assistant reads its values with the model set up in its settings.";
            //
            // wpValues
            //
            this.wpValues.Controls.Add(this.tlpValues);
            this.wpValues.Controls.Add(this.lblValuesHelp);
            this.wpValues.Name = "wpValues";
            this.wpValues.Size = new System.Drawing.Size(980, 606);
            this.wpValues.TabIndex = 1;
            this.wpValues.Text = "Values";
            this.wpValues.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.wpValues_Initialize);
            this.wpValues.Commit += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.wpValues_Commit);
            this.wpValues.Rollback += new System.EventHandler<AeroWizard.WizardPageConfirmEventArgs>(this.wpValues_Commit);
            //
            // lblValuesHelp
            //
            this.lblValuesHelp.AutoSize = true;
            this.lblValuesHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblValuesHelp.Location = new System.Drawing.Point(0, 0);
            this.lblValuesHelp.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblValuesHelp.Name = "lblValuesHelp";
            this.lblValuesHelp.Padding = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblValuesHelp.Size = new System.Drawing.Size(960, 25);
            this.lblValuesHelp.TabIndex = 1;
            this.lblValuesHelp.Text = "The values read, with the standard key each one stands for. Correct a value, a unit or a key, or clear the ones not to use.";
            //
            // wpEquipment
            //
            this.wpEquipment.Controls.Add(this.tlpEquipment);
            this.wpEquipment.Controls.Add(this.lblEquipmentHelp);
            this.wpEquipment.IsFinishPage = true;
            this.wpEquipment.Name = "wpEquipment";
            this.wpEquipment.Size = new System.Drawing.Size(980, 606);
            this.wpEquipment.TabIndex = 2;
            this.wpEquipment.Text = "Equipment";
            this.wpEquipment.Initialize += new System.EventHandler<AeroWizard.WizardPageInitEventArgs>(this.wpEquipment_Initialize);
            //
            // lblEquipmentHelp
            //
            this.lblEquipmentHelp.AutoSize = true;
            this.lblEquipmentHelp.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblEquipmentHelp.Location = new System.Drawing.Point(0, 0);
            this.lblEquipmentHelp.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblEquipmentHelp.Name = "lblEquipmentHelp";
            this.lblEquipmentHelp.Padding = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblEquipmentHelp.Size = new System.Drawing.Size(960, 25);
            this.lblEquipmentHelp.TabIndex = 1;
            this.lblEquipmentHelp.Text = "The equipment the sheet goes into, the operating case, and the changes it makes; tick the ones to make. The checks compare the sheet with itself and with the simulation.";
            //
            // tlpDrawing
            //
            this.tlpDrawing.ColumnCount = 3;
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpDrawing.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpDrawing.Controls.Add(this.lblFile, 0, 0);
            this.tlpDrawing.Controls.Add(this.txtFile, 1, 0);
            this.tlpDrawing.Controls.Add(this.btnBrowse, 2, 0);
            this.tlpDrawing.Controls.Add(this.flpVision, 0, 1);
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
            this.lblFile.Size = new System.Drawing.Size(70, 17);
            this.lblFile.TabIndex = 0;
            this.lblFile.Text = "Data sheet";
            //
            // txtFile
            //
            this.txtFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFile.Location = new System.Drawing.Point(79, 4);
            this.txtFile.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.txtFile.Name = "txtFile";
            this.txtFile.Size = new System.Drawing.Size(808, 25);
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
            this.flpVision.TabIndex = 3;
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
            // flpRead
            //
            this.flpRead.AutoSize = true;
            this.tlpDrawing.SetColumnSpan(this.flpRead, 3);
            this.flpRead.Controls.Add(this.btnRead);
            this.flpRead.Controls.Add(this.btnCancelRead);
            this.flpRead.Controls.Add(this.btnOpenReading);
            this.flpRead.Controls.Add(this.btnSaveReading);
            this.flpRead.Controls.Add(this.prgRead);
            this.flpRead.Location = new System.Drawing.Point(3, 60);
            this.flpRead.Name = "flpRead";
            this.flpRead.Size = new System.Drawing.Size(600, 33);
            this.flpRead.TabIndex = 4;
            //
            // btnRead
            //
            this.btnRead.AutoSize = true;
            this.btnRead.Enabled = false;
            this.btnRead.Location = new System.Drawing.Point(3, 3);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(150, 27);
            this.btnRead.TabIndex = 0;
            this.btnRead.Text = "Read the data sheet";
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
            this.lblReadStatus.Location = new System.Drawing.Point(3, 99);
            this.lblReadStatus.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.lblReadStatus.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblReadStatus.Name = "lblReadStatus";
            this.lblReadStatus.Size = new System.Drawing.Size(0, 17);
            this.lblReadStatus.TabIndex = 5;
            //
            // picDrawing
            //
            this.picDrawing.BackColor = System.Drawing.Color.White;
            this.picDrawing.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tlpDrawing.SetColumnSpan(this.picDrawing, 3);
            this.picDrawing.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picDrawing.Location = new System.Drawing.Point(3, 125);
            this.picDrawing.Name = "picDrawing";
            this.picDrawing.Size = new System.Drawing.Size(990, 522);
            this.picDrawing.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picDrawing.TabIndex = 6;
            this.picDrawing.TabStop = false;
            //
            // tlpValues
            //
            this.tlpValues.ColumnCount = 1;
            this.tlpValues.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpValues.Controls.Add(this.flpSheet, 0, 0);
            this.tlpValues.Controls.Add(this.dgvFields, 0, 1);
            this.tlpValues.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpValues.Location = new System.Drawing.Point(0, 0);
            this.tlpValues.Name = "tlpValues";
            this.tlpValues.RowCount = 2;
            this.tlpValues.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpValues.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpValues.Size = new System.Drawing.Size(996, 650);
            this.tlpValues.TabIndex = 1;
            //
            // flpSheet
            //
            this.flpSheet.AutoSize = true;
            this.flpSheet.Controls.Add(this.lblType);
            this.flpSheet.Controls.Add(this.cbType);
            this.flpSheet.Controls.Add(this.lblTag);
            this.flpSheet.Controls.Add(this.txtTag);
            this.flpSheet.Location = new System.Drawing.Point(3, 3);
            this.flpSheet.Name = "flpSheet";
            this.flpSheet.Size = new System.Drawing.Size(700, 33);
            this.flpSheet.TabIndex = 0;
            //
            // lblType
            //
            this.lblType.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblType.AutoSize = true;
            this.lblType.Name = "lblType";
            this.lblType.TabIndex = 0;
            this.lblType.Text = "Equipment";
            //
            // cbType
            //
            this.cbType.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cbType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbType.FormattingEnabled = true;
            this.cbType.Items.AddRange(new object[] {
            "pump",
            "compressor",
            "expander",
            "heat_exchanger",
            "vessel",
            "control_valve",
            "air_cooler",
            "column",
            "relief_valve",
            "fired_heater",
            "other"});
            this.cbType.Name = "cbType";
            this.cbType.Size = new System.Drawing.Size(200, 25);
            this.cbType.TabIndex = 1;
            this.cbType.SelectedIndexChanged += new System.EventHandler(this.SheetField_Changed);
            //
            // lblTag
            //
            this.lblTag.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTag.AutoSize = true;
            this.lblTag.Margin = new System.Windows.Forms.Padding(20, 0, 3, 0);
            this.lblTag.Name = "lblTag";
            this.lblTag.TabIndex = 2;
            this.lblTag.Text = "Tag";
            //
            // txtTag
            //
            this.txtTag.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.txtTag.Name = "txtTag";
            this.txtTag.Size = new System.Drawing.Size(200, 25);
            this.txtTag.TabIndex = 3;
            this.txtTag.Leave += new System.EventHandler(this.SheetField_Changed);
            //
            // dgvFields
            //
            this.dgvFields.AllowUserToResizeRows = false;
            this.dgvFields.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFields.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvFields.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFields.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFieldUse,
            this.colFieldLabel,
            this.colFieldValue,
            this.colFieldNumber,
            this.colFieldUnit,
            this.colFieldKey,
            this.colFieldCase,
            this.colFieldSide,
            this.colFieldStream});
            this.dgvFields.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFields.Location = new System.Drawing.Point(3, 42);
            this.dgvFields.Name = "dgvFields";
            this.dgvFields.RowHeadersVisible = false;
            this.dgvFields.Size = new System.Drawing.Size(990, 605);
            this.dgvFields.TabIndex = 1;
            this.dgvFields.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvFields_CurrentCellDirtyStateChanged);
            this.dgvFields.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dgvFields_DataError);
            //
            // colFieldUse
            //
            this.colFieldUse.FillWeight = 5F;
            this.colFieldUse.HeaderText = "Use";
            this.colFieldUse.Name = "colFieldUse";
            //
            // colFieldLabel
            //
            this.colFieldLabel.FillWeight = 22F;
            this.colFieldLabel.HeaderText = "Label on the sheet";
            this.colFieldLabel.Name = "colFieldLabel";
            //
            // colFieldValue
            //
            this.colFieldValue.FillWeight = 16F;
            this.colFieldValue.HeaderText = "Value as written";
            this.colFieldValue.Name = "colFieldValue";
            //
            // colFieldNumber
            //
            this.colFieldNumber.FillWeight = 9F;
            this.colFieldNumber.HeaderText = "Number";
            this.colFieldNumber.Name = "colFieldNumber";
            //
            // colFieldUnit
            //
            this.colFieldUnit.FillWeight = 8F;
            this.colFieldUnit.HeaderText = "Unit";
            this.colFieldUnit.Name = "colFieldUnit";
            //
            // colFieldKey
            //
            this.colFieldKey.FillWeight = 16F;
            this.colFieldKey.HeaderText = "Stands for";
            this.colFieldKey.Name = "colFieldKey";
            //
            // colFieldCase
            //
            this.colFieldCase.FillWeight = 8F;
            this.colFieldCase.HeaderText = "Case";
            this.colFieldCase.Name = "colFieldCase";
            //
            // colFieldSide
            //
            this.colFieldSide.FillWeight = 6F;
            this.colFieldSide.HeaderText = "Side";
            this.colFieldSide.Name = "colFieldSide";
            //
            // colFieldStream
            //
            this.colFieldStream.FillWeight = 12F;
            this.colFieldStream.HeaderText = "Stream";
            this.colFieldStream.Name = "colFieldStream";
            //
            // tlpEquipment
            //
            this.tlpEquipment.ColumnCount = 1;
            this.tlpEquipment.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpEquipment.Controls.Add(this.flpTarget, 0, 0);
            this.tlpEquipment.Controls.Add(this.dgvChanges, 0, 1);
            this.tlpEquipment.Controls.Add(this.lblSummary, 0, 2);
            this.tlpEquipment.Controls.Add(this.dgvChecks, 0, 3);
            this.tlpEquipment.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpEquipment.Location = new System.Drawing.Point(0, 0);
            this.tlpEquipment.Name = "tlpEquipment";
            this.tlpEquipment.RowCount = 4;
            this.tlpEquipment.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpEquipment.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.tlpEquipment.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpEquipment.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tlpEquipment.Size = new System.Drawing.Size(996, 650);
            this.tlpEquipment.TabIndex = 2;
            //
            // flpTarget
            //
            this.flpTarget.AutoSize = true;
            this.flpTarget.Controls.Add(this.lblTarget);
            this.flpTarget.Controls.Add(this.cbTarget);
            this.flpTarget.Controls.Add(this.lblCase);
            this.flpTarget.Controls.Add(this.cbCase);
            this.flpTarget.Controls.Add(this.btnApply);
            this.flpTarget.Location = new System.Drawing.Point(3, 3);
            this.flpTarget.Name = "flpTarget";
            this.flpTarget.Size = new System.Drawing.Size(900, 33);
            this.flpTarget.TabIndex = 0;
            //
            // lblTarget
            //
            this.lblTarget.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTarget.AutoSize = true;
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.TabIndex = 0;
            this.lblTarget.Text = "Put the sheet in";
            //
            // cbTarget
            //
            this.cbTarget.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cbTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbTarget.FormattingEnabled = true;
            this.cbTarget.Name = "cbTarget";
            this.cbTarget.Size = new System.Drawing.Size(240, 25);
            this.cbTarget.TabIndex = 1;
            this.cbTarget.SelectedIndexChanged += new System.EventHandler(this.Target_Changed);
            //
            // lblCase
            //
            this.lblCase.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCase.AutoSize = true;
            this.lblCase.Margin = new System.Windows.Forms.Padding(20, 0, 3, 0);
            this.lblCase.Name = "lblCase";
            this.lblCase.TabIndex = 2;
            this.lblCase.Text = "Operating case";
            //
            // cbCase
            //
            this.cbCase.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.cbCase.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbCase.FormattingEnabled = true;
            this.cbCase.Name = "cbCase";
            this.cbCase.Size = new System.Drawing.Size(160, 25);
            this.cbCase.TabIndex = 3;
            this.cbCase.SelectedIndexChanged += new System.EventHandler(this.Target_Changed);
            //
            // btnApply
            //
            this.btnApply.AutoSize = true;
            this.btnApply.Margin = new System.Windows.Forms.Padding(20, 3, 3, 3);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(220, 27);
            this.btnApply.TabIndex = 4;
            this.btnApply.Text = "Make the ticked changes";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // dgvChanges
            //
            this.dgvChanges.AllowUserToAddRows = false;
            this.dgvChanges.AllowUserToDeleteRows = false;
            this.dgvChanges.AllowUserToResizeRows = false;
            this.dgvChanges.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvChanges.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvChanges.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvChanges.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colChangeUse,
            this.colChangeWhat,
            this.colChangeCurrent,
            this.colChangeNew});
            this.dgvChanges.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvChanges.Location = new System.Drawing.Point(3, 42);
            this.dgvChanges.Name = "dgvChanges";
            this.dgvChanges.RowHeadersVisible = false;
            this.dgvChanges.Size = new System.Drawing.Size(990, 320);
            this.dgvChanges.TabIndex = 1;
            this.dgvChanges.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvChanges_CellValueChanged);
            this.dgvChanges.CurrentCellDirtyStateChanged += new System.EventHandler(this.dgvChanges_CurrentCellDirtyStateChanged);
            //
            // colChangeUse
            //
            this.colChangeUse.FillWeight = 6F;
            this.colChangeUse.HeaderText = "Make";
            this.colChangeUse.Name = "colChangeUse";
            //
            // colChangeWhat
            //
            this.colChangeWhat.FillWeight = 34F;
            this.colChangeWhat.HeaderText = "What";
            this.colChangeWhat.Name = "colChangeWhat";
            this.colChangeWhat.ReadOnly = true;
            //
            // colChangeCurrent
            //
            this.colChangeCurrent.FillWeight = 25F;
            this.colChangeCurrent.HeaderText = "Now";
            this.colChangeCurrent.Name = "colChangeCurrent";
            this.colChangeCurrent.ReadOnly = true;
            //
            // colChangeNew
            //
            this.colChangeNew.FillWeight = 35F;
            this.colChangeNew.HeaderText = "From the sheet";
            this.colChangeNew.Name = "colChangeNew";
            this.colChangeNew.ReadOnly = true;
            //
            // lblSummary
            //
            this.lblSummary.AutoSize = true;
            this.lblSummary.Location = new System.Drawing.Point(3, 368);
            this.lblSummary.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.lblSummary.MaximumSize = new System.Drawing.Size(980, 0);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(0, 17);
            this.lblSummary.TabIndex = 2;
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
            this.dgvChecks.Location = new System.Drawing.Point(3, 391);
            this.dgvChecks.Name = "dgvChecks";
            this.dgvChecks.ReadOnly = true;
            this.dgvChecks.RowHeadersVisible = false;
            this.dgvChecks.Size = new System.Drawing.Size(990, 256);
            this.dgvChecks.TabIndex = 3;
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
            // DataSheetImportWizard
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1184, 761);
            this.Controls.Add(this.wizard);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.MinimizeBox = false;
            this.Name = "DataSheetImportWizard";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set Equipment up from a Data Sheet";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DataSheetImportWizard_FormClosing);
            this.Load += new System.EventHandler(this.DataSheetImportWizard_Load);
            this.tlpDrawing.ResumeLayout(false);
            this.tlpDrawing.PerformLayout();
            this.flpVision.ResumeLayout(false);
            this.flpVision.PerformLayout();
            this.flpRead.ResumeLayout(false);
            this.flpRead.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picDrawing)).EndInit();
            this.tlpValues.ResumeLayout(false);
            this.tlpValues.PerformLayout();
            this.flpSheet.ResumeLayout(false);
            this.flpSheet.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFields)).EndInit();
            this.tlpEquipment.ResumeLayout(false);
            this.tlpEquipment.PerformLayout();
            this.flpTarget.ResumeLayout(false);
            this.flpTarget.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChanges)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvChecks)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.wizard)).EndInit();
            this.wpDrawing.ResumeLayout(false);
            this.wpDrawing.PerformLayout();
            this.wpValues.ResumeLayout(false);
            this.wpValues.PerformLayout();
            this.wpEquipment.ResumeLayout(false);
            this.wpEquipment.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private AeroWizard.StepWizardControl wizard;
        private AeroWizard.WizardPage wpDrawing;
        private System.Windows.Forms.Label lblDrawingHelp;
        private AeroWizard.WizardPage wpValues;
        private System.Windows.Forms.Label lblValuesHelp;
        private AeroWizard.WizardPage wpEquipment;
        private System.Windows.Forms.Label lblEquipmentHelp;

        private System.Windows.Forms.TableLayoutPanel tlpDrawing;
        private System.Windows.Forms.Label lblFile;
        private System.Windows.Forms.TextBox txtFile;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.FlowLayoutPanel flpVision;
        private System.Windows.Forms.Label lblVision;
        private System.Windows.Forms.LinkLabel lnkAssistantSettings;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.FlowLayoutPanel flpRead;
        private System.Windows.Forms.ProgressBar prgRead;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnCancelRead;
        private System.Windows.Forms.Button btnOpenReading;
        private System.Windows.Forms.Button btnSaveReading;
        private System.Windows.Forms.Label lblReadStatus;
        private System.Windows.Forms.PictureBox picDrawing;
        private System.Windows.Forms.TableLayoutPanel tlpValues;
        private System.Windows.Forms.FlowLayoutPanel flpSheet;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.ComboBox cbType;
        private System.Windows.Forms.Label lblTag;
        private System.Windows.Forms.TextBox txtTag;
        private System.Windows.Forms.DataGridView dgvFields;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colFieldUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldUnit;
        private System.Windows.Forms.DataGridViewComboBoxColumn colFieldKey;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldCase;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldSide;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFieldStream;
        private System.Windows.Forms.TableLayoutPanel tlpEquipment;
        private System.Windows.Forms.FlowLayoutPanel flpTarget;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.ComboBox cbTarget;
        private System.Windows.Forms.Label lblCase;
        private System.Windows.Forms.ComboBox cbCase;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.DataGridView dgvChanges;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colChangeUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn colChangeWhat;
        private System.Windows.Forms.DataGridViewTextBoxColumn colChangeCurrent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colChangeNew;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.DataGridView dgvChecks;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckSubject;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCheckText;
    }
}
