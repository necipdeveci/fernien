namespace fernien
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            toolStripMain = new ToolStrip();
            lblDeviceCaption = new ToolStripLabel();
            cmbDevices = new ToolStripComboBox();
            btnRefreshDevices = new ToolStripButton();
            sepMain1 = new ToolStripSeparator();
            lblConflictCaption = new ToolStripLabel();
            cmbConflict = new ToolStripComboBox();
            btnHelp = new ToolStripButton();
            splitMain = new SplitContainer();
            splitPanes = new SplitContainer();
            lvLocal = new fernien.UI.BufferedListView();
            colLocalName = new ColumnHeader();
            colLocalSize = new ColumnHeader();
            colLocalType = new ColumnHeader();
            colLocalModified = new ColumnHeader();
            txtLocalPath = new TextBox();
            toolStripLocal = new ToolStrip();
            btnLocalUp = new ToolStripButton();
            btnLocalRefresh = new ToolStripButton();
            ddLocalPlaces = new ToolStripDropDownButton();
            btnLocalNewFolder = new ToolStripButton();
            btnSendToPhone = new ToolStripButton();
            lblLocalTitle = new Label();
            lvPhone = new fernien.UI.BufferedListView();
            colPhoneName = new ColumnHeader();
            colPhoneSize = new ColumnHeader();
            colPhoneType = new ColumnHeader();
            colPhoneModified = new ColumnHeader();
            lblPhoneOverlay = new Label();
            txtPhonePath = new TextBox();
            toolStripPhone = new ToolStrip();
            btnPhoneUp = new ToolStripButton();
            btnPhoneRefresh = new ToolStripButton();
            ddPhonePlaces = new ToolStripDropDownButton();
            btnPhoneNewFolder = new ToolStripButton();
            btnPhoneRename = new ToolStripButton();
            btnPhoneDelete = new ToolStripButton();
            btnReceiveToPc = new ToolStripButton();
            lblPhoneTitle = new Label();
            lvQueue = new fernien.UI.BufferedListView();
            colQueueName = new ColumnHeader();
            colQueueDirection = new ColumnHeader();
            colQueueStatus = new ColumnHeader();
            colQueueProgress = new ColumnHeader();
            colQueueSpeed = new ColumnHeader();
            colQueueRemaining = new ColumnHeader();
            colQueueSize = new ColumnHeader();
            toolStripQueue = new ToolStrip();
            lblQueueTitle = new ToolStripLabel();
            btnCancelTransfer = new ToolStripButton();
            btnClearFinished = new ToolStripButton();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            lblStorage = new ToolStripStatusLabel();
            lblSpeed = new ToolStripStatusLabel();
            timerUi = new System.Windows.Forms.Timer(components);
            toolStripMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitPanes).BeginInit();
            splitPanes.Panel1.SuspendLayout();
            splitPanes.Panel2.SuspendLayout();
            splitPanes.SuspendLayout();
            toolStripLocal.SuspendLayout();
            toolStripPhone.SuspendLayout();
            toolStripQueue.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            //
            // toolStripMain
            //
            toolStripMain.GripStyle = ToolStripGripStyle.Hidden;
            toolStripMain.Items.AddRange(new ToolStripItem[] { lblDeviceCaption, cmbDevices, btnRefreshDevices, sepMain1, lblConflictCaption, cmbConflict, btnHelp });
            toolStripMain.Location = new Point(0, 0);
            toolStripMain.Name = "toolStripMain";
            toolStripMain.Padding = new Padding(8, 3, 8, 3);
            toolStripMain.Size = new Size(1280, 31);
            toolStripMain.TabIndex = 0;
            //
            // lblDeviceCaption
            //
            lblDeviceCaption.Name = "lblDeviceCaption";
            lblDeviceCaption.Size = new Size(44, 22);
            lblDeviceCaption.Text = "Cihaz:";
            //
            // cmbDevices
            //
            cmbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDevices.DropDownWidth = 360;
            cmbDevices.Name = "cmbDevices";
            cmbDevices.Size = new Size(320, 25);
            cmbDevices.SelectedIndexChanged += cmbDevices_SelectedIndexChanged;
            //
            // btnRefreshDevices
            //
            btnRefreshDevices.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnRefreshDevices.Name = "btnRefreshDevices";
            btnRefreshDevices.Size = new Size(64, 22);
            btnRefreshDevices.Text = "↻ Yenile";
            btnRefreshDevices.ToolTipText = "Cihaz listesini yenile";
            btnRefreshDevices.Click += btnRefreshDevices_Click;
            //
            // sepMain1
            //
            sepMain1.Name = "sepMain1";
            sepMain1.Size = new Size(6, 25);
            //
            // lblConflictCaption
            //
            lblConflictCaption.Name = "lblConflictCaption";
            lblConflictCaption.Size = new Size(124, 22);
            lblConflictCaption.Text = "Aynı adda dosya varsa:";
            //
            // cmbConflict
            //
            cmbConflict.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbConflict.Name = "cmbConflict";
            cmbConflict.Size = new Size(150, 25);
            //
            // btnHelp
            //
            btnHelp.Alignment = ToolStripItemAlignment.Right;
            btnHelp.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(110, 22);
            btnHelp.Text = "? Bağlantı yardımı";
            btnHelp.Click += btnHelp_Click;
            //
            // splitMain
            //
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel2;
            splitMain.Location = new Point(0, 31);
            splitMain.Name = "splitMain";
            splitMain.Orientation = Orientation.Horizontal;
            //
            // splitMain.Panel1
            //
            splitMain.Panel1.Controls.Add(splitPanes);
            //
            // splitMain.Panel2
            //
            splitMain.Panel2.Controls.Add(lvQueue);
            splitMain.Panel2.Controls.Add(toolStripQueue);
            splitMain.Size = new Size(1280, 727);
            splitMain.SplitterDistance = 500;
            splitMain.SplitterWidth = 6;
            splitMain.TabIndex = 1;
            //
            // splitPanes
            //
            splitPanes.Dock = DockStyle.Fill;
            splitPanes.Location = new Point(0, 0);
            splitPanes.Name = "splitPanes";
            //
            // splitPanes.Panel1
            //
            splitPanes.Panel1.Controls.Add(lvLocal);
            splitPanes.Panel1.Controls.Add(txtLocalPath);
            splitPanes.Panel1.Controls.Add(toolStripLocal);
            splitPanes.Panel1.Controls.Add(lblLocalTitle);
            //
            // splitPanes.Panel2
            //
            splitPanes.Panel2.Controls.Add(lvPhone);
            splitPanes.Panel2.Controls.Add(lblPhoneOverlay);
            splitPanes.Panel2.Controls.Add(txtPhonePath);
            splitPanes.Panel2.Controls.Add(toolStripPhone);
            splitPanes.Panel2.Controls.Add(lblPhoneTitle);
            splitPanes.Size = new Size(1280, 500);
            splitPanes.SplitterDistance = 637;
            splitPanes.SplitterWidth = 6;
            splitPanes.TabIndex = 0;
            //
            // lvLocal
            //
            lvLocal.AllowDrop = true;
            lvLocal.BorderStyle = BorderStyle.FixedSingle;
            lvLocal.Columns.AddRange(new ColumnHeader[] { colLocalName, colLocalSize, colLocalType, colLocalModified });
            lvLocal.Dock = DockStyle.Fill;
            lvLocal.FullRowSelect = true;
            lvLocal.HideSelection = false;
            lvLocal.Location = new Point(0, 89);
            lvLocal.Name = "lvLocal";
            lvLocal.Size = new Size(637, 411);
            lvLocal.TabIndex = 3;
            lvLocal.UseCompatibleStateImageBehavior = false;
            lvLocal.View = View.Details;
            lvLocal.ColumnClick += lvLocal_ColumnClick;
            lvLocal.ItemActivate += lvLocal_ItemActivate;
            lvLocal.ItemDrag += lvLocal_ItemDrag;
            lvLocal.DragDrop += lvLocal_DragDrop;
            lvLocal.DragEnter += lvLocal_DragEnter;
            lvLocal.DragOver += lvLocal_DragEnter;
            lvLocal.KeyDown += lvLocal_KeyDown;
            //
            // colLocalName
            //
            colLocalName.Text = "Ad";
            colLocalName.Width = 260;
            //
            // colLocalSize
            //
            colLocalSize.Text = "Boyut";
            colLocalSize.TextAlign = HorizontalAlignment.Right;
            colLocalSize.Width = 90;
            //
            // colLocalType
            //
            colLocalType.Text = "Tür";
            colLocalType.Width = 130;
            //
            // colLocalModified
            //
            colLocalModified.Text = "Değiştirilme";
            colLocalModified.Width = 130;
            //
            // txtLocalPath
            //
            txtLocalPath.BorderStyle = BorderStyle.FixedSingle;
            txtLocalPath.Dock = DockStyle.Top;
            txtLocalPath.Location = new Point(0, 66);
            txtLocalPath.Name = "txtLocalPath";
            txtLocalPath.Size = new Size(637, 23);
            txtLocalPath.TabIndex = 2;
            txtLocalPath.KeyDown += txtLocalPath_KeyDown;
            //
            // toolStripLocal
            //
            toolStripLocal.GripStyle = ToolStripGripStyle.Hidden;
            toolStripLocal.Items.AddRange(new ToolStripItem[] { btnLocalUp, btnLocalRefresh, ddLocalPlaces, btnLocalNewFolder, btnSendToPhone });
            toolStripLocal.Location = new Point(0, 34);
            toolStripLocal.Name = "toolStripLocal";
            toolStripLocal.Padding = new Padding(4, 2, 4, 2);
            toolStripLocal.Size = new Size(637, 32);
            toolStripLocal.TabIndex = 1;
            //
            // btnLocalUp
            //
            btnLocalUp.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnLocalUp.Name = "btnLocalUp";
            btnLocalUp.Size = new Size(42, 25);
            btnLocalUp.Text = "↑ Üst";
            btnLocalUp.ToolTipText = "Üst klasör (Geri)";
            btnLocalUp.Click += btnLocalUp_Click;
            //
            // btnLocalRefresh
            //
            btnLocalRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnLocalRefresh.Name = "btnLocalRefresh";
            btnLocalRefresh.Size = new Size(24, 25);
            btnLocalRefresh.Text = "↻";
            btnLocalRefresh.ToolTipText = "Yenile (F5)";
            btnLocalRefresh.Click += btnLocalRefresh_Click;
            //
            // ddLocalPlaces
            //
            ddLocalPlaces.DisplayStyle = ToolStripItemDisplayStyle.Text;
            ddLocalPlaces.Name = "ddLocalPlaces";
            ddLocalPlaces.Size = new Size(74, 25);
            ddLocalPlaces.Text = "Konumlar";
            ddLocalPlaces.DropDownOpening += ddLocalPlaces_DropDownOpening;
            //
            // btnLocalNewFolder
            //
            btnLocalNewFolder.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnLocalNewFolder.Name = "btnLocalNewFolder";
            btnLocalNewFolder.Size = new Size(74, 25);
            btnLocalNewFolder.Text = "+ Klasör";
            btnLocalNewFolder.ToolTipText = "Yeni klasör";
            btnLocalNewFolder.Click += btnLocalNewFolder_Click;
            //
            // btnSendToPhone
            //
            btnSendToPhone.Alignment = ToolStripItemAlignment.Right;
            btnSendToPhone.BackColor = Color.FromArgb(33, 99, 173);
            btnSendToPhone.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnSendToPhone.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSendToPhone.ForeColor = Color.White;
            btnSendToPhone.Margin = new Padding(0, 1, 2, 2);
            btnSendToPhone.Name = "btnSendToPhone";
            btnSendToPhone.Padding = new Padding(8, 0, 8, 0);
            btnSendToPhone.Size = new Size(130, 25);
            btnSendToPhone.Text = "Telefona gönder →";
            btnSendToPhone.ToolTipText = "Seçili dosya ve klasörleri telefondaki açık klasöre gönder";
            btnSendToPhone.Click += btnSendToPhone_Click;
            //
            // lblLocalTitle
            //
            lblLocalTitle.BackColor = Color.FromArgb(33, 99, 173);
            lblLocalTitle.Dock = DockStyle.Top;
            lblLocalTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblLocalTitle.ForeColor = Color.White;
            lblLocalTitle.Location = new Point(0, 0);
            lblLocalTitle.Name = "lblLocalTitle";
            lblLocalTitle.Padding = new Padding(8, 0, 0, 0);
            lblLocalTitle.Size = new Size(637, 34);
            lblLocalTitle.TabIndex = 0;
            lblLocalTitle.Text = "BİLGİSAYAR";
            lblLocalTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lvPhone
            //
            lvPhone.AllowDrop = true;
            lvPhone.BorderStyle = BorderStyle.FixedSingle;
            lvPhone.Columns.AddRange(new ColumnHeader[] { colPhoneName, colPhoneSize, colPhoneType, colPhoneModified });
            lvPhone.Dock = DockStyle.Fill;
            lvPhone.FullRowSelect = true;
            lvPhone.HideSelection = false;
            lvPhone.Location = new Point(0, 89);
            lvPhone.Name = "lvPhone";
            lvPhone.Size = new Size(637, 411);
            lvPhone.TabIndex = 3;
            lvPhone.UseCompatibleStateImageBehavior = false;
            lvPhone.View = View.Details;
            lvPhone.ColumnClick += lvPhone_ColumnClick;
            lvPhone.ItemActivate += lvPhone_ItemActivate;
            lvPhone.ItemDrag += lvPhone_ItemDrag;
            lvPhone.DragDrop += lvPhone_DragDrop;
            lvPhone.DragEnter += lvPhone_DragEnter;
            lvPhone.DragOver += lvPhone_DragEnter;
            lvPhone.KeyDown += lvPhone_KeyDown;
            //
            // colPhoneName
            //
            colPhoneName.Text = "Ad";
            colPhoneName.Width = 260;
            //
            // colPhoneSize
            //
            colPhoneSize.Text = "Boyut";
            colPhoneSize.TextAlign = HorizontalAlignment.Right;
            colPhoneSize.Width = 90;
            //
            // colPhoneType
            //
            colPhoneType.Text = "Tür";
            colPhoneType.Width = 130;
            //
            // colPhoneModified
            //
            colPhoneModified.Text = "Değiştirilme";
            colPhoneModified.Width = 130;
            //
            // lblPhoneOverlay
            //
            lblPhoneOverlay.BackColor = SystemColors.Window;
            lblPhoneOverlay.BorderStyle = BorderStyle.FixedSingle;
            lblPhoneOverlay.Dock = DockStyle.Fill;
            lblPhoneOverlay.ForeColor = Color.FromArgb(70, 70, 70);
            lblPhoneOverlay.Location = new Point(0, 89);
            lblPhoneOverlay.Name = "lblPhoneOverlay";
            lblPhoneOverlay.Padding = new Padding(40);
            lblPhoneOverlay.Size = new Size(637, 411);
            lblPhoneOverlay.TabIndex = 4;
            lblPhoneOverlay.Text = "Telefon bekleniyor…";
            lblPhoneOverlay.TextAlign = ContentAlignment.MiddleCenter;
            lblPhoneOverlay.Visible = false;
            //
            // txtPhonePath
            //
            txtPhonePath.BorderStyle = BorderStyle.FixedSingle;
            txtPhonePath.Dock = DockStyle.Top;
            txtPhonePath.Location = new Point(0, 66);
            txtPhonePath.Name = "txtPhonePath";
            txtPhonePath.Size = new Size(637, 23);
            txtPhonePath.TabIndex = 2;
            txtPhonePath.KeyDown += txtPhonePath_KeyDown;
            //
            // toolStripPhone
            //
            toolStripPhone.GripStyle = ToolStripGripStyle.Hidden;
            toolStripPhone.Items.AddRange(new ToolStripItem[] { btnPhoneUp, btnPhoneRefresh, ddPhonePlaces, btnPhoneNewFolder, btnPhoneRename, btnPhoneDelete, btnReceiveToPc });
            toolStripPhone.Location = new Point(0, 34);
            toolStripPhone.Name = "toolStripPhone";
            toolStripPhone.Padding = new Padding(4, 2, 4, 2);
            toolStripPhone.Size = new Size(637, 32);
            toolStripPhone.TabIndex = 1;
            //
            // btnPhoneUp
            //
            btnPhoneUp.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPhoneUp.Name = "btnPhoneUp";
            btnPhoneUp.Size = new Size(42, 25);
            btnPhoneUp.Text = "↑ Üst";
            btnPhoneUp.ToolTipText = "Üst klasör (Geri)";
            btnPhoneUp.Click += btnPhoneUp_Click;
            //
            // btnPhoneRefresh
            //
            btnPhoneRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPhoneRefresh.Name = "btnPhoneRefresh";
            btnPhoneRefresh.Size = new Size(24, 25);
            btnPhoneRefresh.Text = "↻";
            btnPhoneRefresh.ToolTipText = "Yenile (F5)";
            btnPhoneRefresh.Click += btnPhoneRefresh_Click;
            //
            // ddPhonePlaces
            //
            ddPhonePlaces.DisplayStyle = ToolStripItemDisplayStyle.Text;
            ddPhonePlaces.Name = "ddPhonePlaces";
            ddPhonePlaces.Size = new Size(74, 25);
            ddPhonePlaces.Text = "Konumlar";
            //
            // btnPhoneNewFolder
            //
            btnPhoneNewFolder.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPhoneNewFolder.Name = "btnPhoneNewFolder";
            btnPhoneNewFolder.Size = new Size(58, 25);
            btnPhoneNewFolder.Text = "+ Klasör";
            btnPhoneNewFolder.ToolTipText = "Yeni klasör";
            btnPhoneNewFolder.Click += btnPhoneNewFolder_Click;
            //
            // btnPhoneRename
            //
            btnPhoneRename.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPhoneRename.Name = "btnPhoneRename";
            btnPhoneRename.Size = new Size(83, 25);
            btnPhoneRename.Text = "Yeniden adlandır";
            btnPhoneRename.ToolTipText = "Yeniden adlandır (F2)";
            btnPhoneRename.Click += btnPhoneRename_Click;
            //
            // btnPhoneDelete
            //
            btnPhoneDelete.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPhoneDelete.Name = "btnPhoneDelete";
            btnPhoneDelete.Size = new Size(28, 25);
            btnPhoneDelete.Text = "Sil";
            btnPhoneDelete.ToolTipText = "Sil (Del)";
            btnPhoneDelete.Click += btnPhoneDelete_Click;
            //
            // btnReceiveToPc
            //
            btnReceiveToPc.Alignment = ToolStripItemAlignment.Right;
            btnReceiveToPc.BackColor = Color.FromArgb(46, 125, 50);
            btnReceiveToPc.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnReceiveToPc.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnReceiveToPc.ForeColor = Color.White;
            btnReceiveToPc.Margin = new Padding(0, 1, 2, 2);
            btnReceiveToPc.Name = "btnReceiveToPc";
            btnReceiveToPc.Padding = new Padding(8, 0, 8, 0);
            btnReceiveToPc.Size = new Size(130, 25);
            btnReceiveToPc.Text = "← Bilgisayara al";
            btnReceiveToPc.ToolTipText = "Seçili dosya ve klasörleri bilgisayardaki açık klasöre al";
            btnReceiveToPc.Click += btnReceiveToPc_Click;
            //
            // lblPhoneTitle
            //
            lblPhoneTitle.BackColor = Color.FromArgb(46, 125, 50);
            lblPhoneTitle.Dock = DockStyle.Top;
            lblPhoneTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblPhoneTitle.ForeColor = Color.White;
            lblPhoneTitle.Location = new Point(0, 0);
            lblPhoneTitle.Name = "lblPhoneTitle";
            lblPhoneTitle.Padding = new Padding(8, 0, 0, 0);
            lblPhoneTitle.Size = new Size(637, 34);
            lblPhoneTitle.TabIndex = 0;
            lblPhoneTitle.Text = "TELEFON";
            lblPhoneTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lvQueue
            //
            lvQueue.BorderStyle = BorderStyle.FixedSingle;
            lvQueue.Columns.AddRange(new ColumnHeader[] { colQueueName, colQueueDirection, colQueueStatus, colQueueProgress, colQueueSpeed, colQueueRemaining, colQueueSize });
            lvQueue.Dock = DockStyle.Fill;
            lvQueue.FullRowSelect = true;
            lvQueue.HideSelection = false;
            lvQueue.Location = new Point(0, 28);
            lvQueue.Name = "lvQueue";
            lvQueue.OwnerDraw = true;
            lvQueue.ShowItemToolTips = true;
            lvQueue.Size = new Size(1280, 193);
            lvQueue.TabIndex = 1;
            lvQueue.UseCompatibleStateImageBehavior = false;
            lvQueue.View = View.Details;
            lvQueue.DrawColumnHeader += lvQueue_DrawColumnHeader;
            lvQueue.DrawItem += lvQueue_DrawItem;
            lvQueue.DrawSubItem += lvQueue_DrawSubItem;
            //
            // colQueueName
            //
            colQueueName.Text = "Öğe";
            colQueueName.Width = 280;
            //
            // colQueueDirection
            //
            colQueueDirection.Text = "Yön";
            colQueueDirection.Width = 110;
            //
            // colQueueStatus
            //
            colQueueStatus.Text = "Durum";
            colQueueStatus.Width = 200;
            //
            // colQueueProgress
            //
            colQueueProgress.Text = "İlerleme";
            colQueueProgress.Width = 180;
            //
            // colQueueSpeed
            //
            colQueueSpeed.Text = "Hız";
            colQueueSpeed.TextAlign = HorizontalAlignment.Right;
            colQueueSpeed.Width = 100;
            //
            // colQueueRemaining
            //
            colQueueRemaining.Text = "Kalan süre";
            colQueueRemaining.TextAlign = HorizontalAlignment.Right;
            colQueueRemaining.Width = 90;
            //
            // colQueueSize
            //
            colQueueSize.Text = "Boyut";
            colQueueSize.TextAlign = HorizontalAlignment.Right;
            colQueueSize.Width = 170;
            //
            // toolStripQueue
            //
            toolStripQueue.GripStyle = ToolStripGripStyle.Hidden;
            toolStripQueue.Items.AddRange(new ToolStripItem[] { lblQueueTitle, btnCancelTransfer, btnClearFinished });
            toolStripQueue.Location = new Point(0, 0);
            toolStripQueue.Name = "toolStripQueue";
            toolStripQueue.Padding = new Padding(4, 1, 4, 1);
            toolStripQueue.Size = new Size(1280, 28);
            toolStripQueue.TabIndex = 0;
            //
            // lblQueueTitle
            //
            lblQueueTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblQueueTitle.Margin = new Padding(0, 1, 12, 2);
            lblQueueTitle.Name = "lblQueueTitle";
            lblQueueTitle.Size = new Size(84, 23);
            lblQueueTitle.Text = "AKTARIMLAR";
            //
            // btnCancelTransfer
            //
            btnCancelTransfer.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnCancelTransfer.Name = "btnCancelTransfer";
            btnCancelTransfer.Size = new Size(97, 23);
            btnCancelTransfer.Text = "Seçileni iptal et";
            btnCancelTransfer.Click += btnCancelTransfer_Click;
            //
            // btnClearFinished
            //
            btnClearFinished.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnClearFinished.Name = "btnClearFinished";
            btnClearFinished.Size = new Size(125, 23);
            btnClearFinished.Text = "Bitenleri temizle";
            btnClearFinished.Click += btnClearFinished_Click;
            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, lblStorage, lblSpeed });
            statusStrip.Location = new Point(0, 758);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1280, 22);
            statusStrip.TabIndex = 2;
            //
            // lblStatus
            //
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(1203, 17);
            lblStatus.Spring = true;
            lblStatus.Text = "Hazır";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            //
            // lblStorage
            //
            lblStorage.Name = "lblStorage";
            lblStorage.Size = new Size(0, 17);
            //
            // lblSpeed
            //
            lblSpeed.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSpeed.Name = "lblSpeed";
            lblSpeed.Size = new Size(0, 17);
            //
            // timerUi
            //
            timerUi.Enabled = true;
            timerUi.Interval = 300;
            timerUi.Tick += timerUi_Tick;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1280, 780);
            Controls.Add(splitMain);
            Controls.Add(statusStrip);
            Controls.Add(toolStripMain);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(900, 600);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Fernien — Dosya Aktarımı";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            toolStripMain.ResumeLayout(false);
            toolStripMain.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            splitMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            splitPanes.Panel1.ResumeLayout(false);
            splitPanes.Panel1.PerformLayout();
            splitPanes.Panel2.ResumeLayout(false);
            splitPanes.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitPanes).EndInit();
            splitPanes.ResumeLayout(false);
            toolStripLocal.ResumeLayout(false);
            toolStripLocal.PerformLayout();
            toolStripPhone.ResumeLayout(false);
            toolStripPhone.PerformLayout();
            toolStripQueue.ResumeLayout(false);
            toolStripQueue.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStrip toolStripMain;
        private ToolStripLabel lblDeviceCaption;
        private ToolStripComboBox cmbDevices;
        private ToolStripButton btnRefreshDevices;
        private ToolStripSeparator sepMain1;
        private ToolStripLabel lblConflictCaption;
        private ToolStripComboBox cmbConflict;
        private ToolStripButton btnHelp;
        private SplitContainer splitMain;
        private SplitContainer splitPanes;
        private fernien.UI.BufferedListView lvLocal;
        private ColumnHeader colLocalName;
        private ColumnHeader colLocalSize;
        private ColumnHeader colLocalType;
        private ColumnHeader colLocalModified;
        private TextBox txtLocalPath;
        private ToolStrip toolStripLocal;
        private ToolStripButton btnLocalUp;
        private ToolStripButton btnLocalRefresh;
        private ToolStripDropDownButton ddLocalPlaces;
        private ToolStripButton btnLocalNewFolder;
        private ToolStripButton btnSendToPhone;
        private Label lblLocalTitle;
        private fernien.UI.BufferedListView lvPhone;
        private ColumnHeader colPhoneName;
        private ColumnHeader colPhoneSize;
        private ColumnHeader colPhoneType;
        private ColumnHeader colPhoneModified;
        private Label lblPhoneOverlay;
        private TextBox txtPhonePath;
        private ToolStrip toolStripPhone;
        private ToolStripButton btnPhoneUp;
        private ToolStripButton btnPhoneRefresh;
        private ToolStripDropDownButton ddPhonePlaces;
        private ToolStripButton btnPhoneNewFolder;
        private ToolStripButton btnPhoneRename;
        private ToolStripButton btnPhoneDelete;
        private ToolStripButton btnReceiveToPc;
        private Label lblPhoneTitle;
        private fernien.UI.BufferedListView lvQueue;
        private ColumnHeader colQueueName;
        private ColumnHeader colQueueDirection;
        private ColumnHeader colQueueStatus;
        private ColumnHeader colQueueProgress;
        private ColumnHeader colQueueSpeed;
        private ColumnHeader colQueueRemaining;
        private ColumnHeader colQueueSize;
        private ToolStrip toolStripQueue;
        private ToolStripLabel lblQueueTitle;
        private ToolStripButton btnCancelTransfer;
        private ToolStripButton btnClearFinished;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripStatusLabel lblStorage;
        private ToolStripStatusLabel lblSpeed;
        private System.Windows.Forms.Timer timerUi;
    }
}
