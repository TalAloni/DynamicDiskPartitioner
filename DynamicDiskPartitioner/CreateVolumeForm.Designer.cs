namespace DynamicDiskPartitioner
{
    partial class CreateVolumeForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateVolumeForm));
            this.listDisks = new System.Windows.Forms.CheckedListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.rbSimple = new System.Windows.Forms.RadioButton();
            this.rbRaid5 = new System.Windows.Forms.RadioButton();
            this.chkDegraded = new System.Windows.Forms.CheckBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.numericExtentSize = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numericExtentSize)).BeginInit();
            this.SuspendLayout();
            // 
            // listDisks
            // 
            this.listDisks.CheckOnClick = true;
            this.listDisks.FormattingEnabled = true;
            this.listDisks.Location = new System.Drawing.Point(101, 63);
            this.listDisks.Name = "listDisks";
            this.listDisks.Size = new System.Drawing.Size(198, 109);
            this.listDisks.TabIndex = 0;
            this.listDisks.SelectedIndexChanged += new System.EventHandler(this.listDisks_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(72, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Volume Type:";
            // 
            // rbSimple
            // 
            this.rbSimple.AutoSize = true;
            this.rbSimple.Checked = true;
            this.rbSimple.Location = new System.Drawing.Point(101, 7);
            this.rbSimple.Name = "rbSimple";
            this.rbSimple.Size = new System.Drawing.Size(56, 17);
            this.rbSimple.TabIndex = 2;
            this.rbSimple.TabStop = true;
            this.rbSimple.Text = "Simple";
            this.rbSimple.UseVisualStyleBackColor = true;
            // 
            // rbRaid5
            // 
            this.rbRaid5.AutoSize = true;
            this.rbRaid5.Location = new System.Drawing.Point(101, 30);
            this.rbRaid5.Name = "rbRaid5";
            this.rbRaid5.Size = new System.Drawing.Size(60, 17);
            this.rbRaid5.TabIndex = 3;
            this.rbRaid5.Text = "RAID-5";
            this.rbRaid5.UseVisualStyleBackColor = true;
            this.rbRaid5.CheckedChanged += new System.EventHandler(this.rbRaid5_CheckedChanged);
            // 
            // chkDegraded
            // 
            this.chkDegraded.AutoSize = true;
            this.chkDegraded.Enabled = false;
            this.chkDegraded.Location = new System.Drawing.Point(167, 30);
            this.chkDegraded.Name = "chkDegraded";
            this.chkDegraded.Size = new System.Drawing.Size(73, 17);
            this.chkDegraded.TabIndex = 4;
            this.chkDegraded.Text = "Degraded";
            this.chkDegraded.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(174, 218);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 19;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(255, 218);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 18;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 63);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 13);
            this.label2.TabIndex = 20;
            this.label2.Text = "Selected Disk(s):";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 180);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(63, 13);
            this.label3.TabIndex = 21;
            this.label3.Text = "Extent Size:";
            // 
            // numericExtentSize
            // 
            this.numericExtentSize.Location = new System.Drawing.Point(101, 178);
            this.numericExtentSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericExtentSize.Name = "numericExtentSize";
            this.numericExtentSize.Size = new System.Drawing.Size(120, 20);
            this.numericExtentSize.TabIndex = 22;
            this.numericExtentSize.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(225, 182);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(25, 13);
            this.label4.TabIndex = 23;
            this.label4.Text = "MiB";
            // 
            // CreateVolumeForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(344, 255);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.numericExtentSize);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.chkDegraded);
            this.Controls.Add(this.rbRaid5);
            this.Controls.Add(this.rbSimple);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.listDisks);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(350, 280);
            this.MinimumSize = new System.Drawing.Size(350, 280);
            this.Name = "CreateVolumeForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Create Dynamic Volume";
            this.Load += new System.EventHandler(this.CreateVolumeForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numericExtentSize)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckedListBox listDisks;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton rbSimple;
        private System.Windows.Forms.RadioButton rbRaid5;
        private System.Windows.Forms.CheckBox chkDegraded;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numericExtentSize;
        private System.Windows.Forms.Label label4;
    }
}