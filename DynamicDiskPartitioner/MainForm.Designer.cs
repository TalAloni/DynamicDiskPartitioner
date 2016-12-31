namespace DynamicDiskPartitioner
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.diskContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.initializeDiskMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cleanDiskMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.diskPropertiesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extentContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.fileSystemMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportFileMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extendFileSystemMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileSystemPropertiesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createVolumeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addDiskToVolumeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extendVolumeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveExtentMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resumeOperationMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.volumePropertiesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lblAuthor = new System.Windows.Forms.Label();
            this.disksView = new DynamicDiskPartitioner.DiskCollectionView();
            this.diskContextMenu.SuspendLayout();
            this.extentContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // diskContextMenu
            // 
            this.diskContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.initializeDiskMenuItem,
            this.cleanDiskMenuItem,
            this.diskPropertiesMenuItem});
            this.diskContextMenu.Name = "contextMenuStrip1";
            this.diskContextMenu.Size = new System.Drawing.Size(136, 70);
            // 
            // initializeDiskMenuItem
            // 
            this.initializeDiskMenuItem.Name = "initializeDiskMenuItem";
            this.initializeDiskMenuItem.Size = new System.Drawing.Size(135, 22);
            this.initializeDiskMenuItem.Text = "Initialize Disk";
            this.initializeDiskMenuItem.Click += new System.EventHandler(this.initializeDiskMenuItem_Click);
            // 
            // cleanDiskMenuItem
            // 
            this.cleanDiskMenuItem.Name = "cleanDiskMenuItem";
            this.cleanDiskMenuItem.Size = new System.Drawing.Size(135, 22);
            this.cleanDiskMenuItem.Text = "Clean Disk";
            this.cleanDiskMenuItem.Click += new System.EventHandler(this.cleanDiskMenuItem_Click);
            // 
            // diskPropertiesMenuItem
            // 
            this.diskPropertiesMenuItem.Name = "diskPropertiesMenuItem";
            this.diskPropertiesMenuItem.Size = new System.Drawing.Size(135, 22);
            this.diskPropertiesMenuItem.Text = "Properties";
            this.diskPropertiesMenuItem.Click += new System.EventHandler(this.diskPropertiesMenuItem_Click);
            // 
            // extentContextMenu
            // 
            this.extentContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileSystemMenuItem,
            this.createVolumeMenuItem,
            this.addDiskToVolumeMenuItem,
            this.extendVolumeMenuItem,
            this.moveExtentMenuItem,
            this.resumeOperationMenuItem,
            this.volumePropertiesMenuItem});
            this.extentContextMenu.Name = "contextMenuExtent";
            this.extentContextMenu.Size = new System.Drawing.Size(145, 158);
            // 
            // fileSystemMenuItem
            // 
            this.fileSystemMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportFileMenuItem,
            this.extendFileSystemMenuItem,
            this.fileSystemPropertiesMenuItem});
            this.fileSystemMenuItem.Name = "fileSystemMenuItem";
            this.fileSystemMenuItem.Size = new System.Drawing.Size(144, 22);
            this.fileSystemMenuItem.Text = "File system";
            // 
            // exportFileMenuItem
            // 
            this.exportFileMenuItem.Name = "exportFileMenuItem";
            this.exportFileMenuItem.Size = new System.Drawing.Size(159, 22);
            this.exportFileMenuItem.Text = "Export File/Folder";
            this.exportFileMenuItem.Click += new System.EventHandler(this.exportFileMenuItem_Click);
            // 
            // extendFileSystemMenuItem
            // 
            this.extendFileSystemMenuItem.Name = "extendFileSystemMenuItem";
            this.extendFileSystemMenuItem.Size = new System.Drawing.Size(159, 22);
            this.extendFileSystemMenuItem.Text = "Extend";
            this.extendFileSystemMenuItem.Click += new System.EventHandler(this.extendFileSystemMenuItem_Click);
            // 
            // fileSystemPropertiesMenuItem
            // 
            this.fileSystemPropertiesMenuItem.Name = "fileSystemPropertiesMenuItem";
            this.fileSystemPropertiesMenuItem.Size = new System.Drawing.Size(159, 22);
            this.fileSystemPropertiesMenuItem.Text = "Properties";
            this.fileSystemPropertiesMenuItem.Click += new System.EventHandler(this.fileSystemPropertiesMenuItem_Click);
            // 
            // createVolumeMenuItem
            // 
            this.createVolumeMenuItem.Name = "createVolumeMenuItem";
            this.createVolumeMenuItem.Size = new System.Drawing.Size(144, 22);
            this.createVolumeMenuItem.Text = "Create Volume";
            this.createVolumeMenuItem.Click += new System.EventHandler(this.createVolumeMenuItem_Click);
            // 
            // addDiskToVolumeMenuItem
            // 
            this.addDiskToVolumeMenuItem.Name = "addDiskToVolumeMenuItem";
            this.addDiskToVolumeMenuItem.Size = new System.Drawing.Size(144, 22);
            this.addDiskToVolumeMenuItem.Text = "Add Disk";
            this.addDiskToVolumeMenuItem.Click += new System.EventHandler(this.addDiskToVolumeMenuItem_Click);
            // 
            // extendVolumeMenuItem
            // 
            this.extendVolumeMenuItem.Name = "extendVolumeMenuItem";
            this.extendVolumeMenuItem.Size = new System.Drawing.Size(144, 22);
            this.extendVolumeMenuItem.Text = "Extend";
            this.extendVolumeMenuItem.Click += new System.EventHandler(this.extendVolumeMenuItem_Click);
            // 
            // moveExtentMenuItem
            // 
            this.moveExtentMenuItem.Name = "moveExtentMenuItem";
            this.moveExtentMenuItem.Size = new System.Drawing.Size(144, 22);
            this.moveExtentMenuItem.Text = "Move Extent";
            this.moveExtentMenuItem.Click += new System.EventHandler(this.moveExtentMenuItem_Click);
            // 
            // resumeOperationMenuItem
            // 
            this.resumeOperationMenuItem.Name = "resumeOperationMenuItem";
            this.resumeOperationMenuItem.Size = new System.Drawing.Size(144, 22);
            this.resumeOperationMenuItem.Text = "Resume";
            // 
            // volumePropertiesMenuItem
            // 
            this.volumePropertiesMenuItem.Name = "volumePropertiesMenuItem";
            this.volumePropertiesMenuItem.Size = new System.Drawing.Size(144, 22);
            this.volumePropertiesMenuItem.Text = "Properties";
            this.volumePropertiesMenuItem.Click += new System.EventHandler(this.extentPropertiesMenuItem_Click);
            // 
            // lblAuthor
            // 
            this.lblAuthor.AutoSize = true;
            this.lblAuthor.Location = new System.Drawing.Point(6, 358);
            this.lblAuthor.Name = "lblAuthor";
            this.lblAuthor.Size = new System.Drawing.Size(201, 13);
            this.lblAuthor.TabIndex = 2;
            this.lblAuthor.Text = "Author: Tal Aloni <tal.aloni.il@gmail.com>";
            // 
            // disksView
            // 
            this.disksView.AutoScroll = true;
            this.disksView.Location = new System.Drawing.Point(9, 12);
            this.disksView.Name = "disksView";
            this.disksView.Size = new System.Drawing.Size(591, 340);
            this.disksView.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(610, 373);
            this.Controls.Add(this.lblAuthor);
            this.Controls.Add(this.disksView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "MainForm";
            this.Text = "Dynamic Disk Partitioner";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyUp);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.diskContextMenu.ResumeLayout(false);
            this.extentContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DiskCollectionView disksView;
        private System.Windows.Forms.ContextMenuStrip diskContextMenu;
        private System.Windows.Forms.ToolStripMenuItem initializeDiskMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cleanDiskMenuItem;
        private System.Windows.Forms.ContextMenuStrip extentContextMenu;
        private System.Windows.Forms.ToolStripMenuItem moveExtentMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extendVolumeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createVolumeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addDiskToVolumeMenuItem;
        private System.Windows.Forms.ToolStripMenuItem diskPropertiesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem volumePropertiesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileSystemMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportFileMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileSystemPropertiesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extendFileSystemMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resumeOperationMenuItem;
        private System.Windows.Forms.Label lblAuthor;
    }
}