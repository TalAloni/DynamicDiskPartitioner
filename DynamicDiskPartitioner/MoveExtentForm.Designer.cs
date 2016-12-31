namespace DynamicDiskPartitioner
{
    partial class MoveExtentForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MoveExtentForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.listDisks = new System.Windows.Forms.ComboBox();
            this.numericDiskOffset = new System.Windows.Forms.NumericUpDown();
            this.listSuffixes = new System.Windows.Forms.ComboBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            ((System.ComponentModel.ISupportInitialize)(this.numericDiskOffset)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Select Disk:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 47);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Disk offset:";
            // 
            // listDisks
            // 
            this.listDisks.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.listDisks.FormattingEnabled = true;
            this.listDisks.Location = new System.Drawing.Point(82, 16);
            this.listDisks.Name = "listDisks";
            this.listDisks.Size = new System.Drawing.Size(121, 21);
            this.listDisks.TabIndex = 2;
            this.listDisks.SelectedIndexChanged += new System.EventHandler(this.listDisks_SelectedIndexChanged);
            // 
            // numericDiskOffset
            // 
            this.numericDiskOffset.Location = new System.Drawing.Point(82, 45);
            this.numericDiskOffset.Name = "numericDiskOffset";
            this.numericDiskOffset.Size = new System.Drawing.Size(121, 20);
            this.numericDiskOffset.TabIndex = 3;
            // 
            // listSuffixes
            // 
            this.listSuffixes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.listSuffixes.FormattingEnabled = true;
            this.listSuffixes.Items.AddRange(new object[] {
            "Bytes",
            "KiB",
            "MiB",
            "GiB"});
            this.listSuffixes.Location = new System.Drawing.Point(208, 44);
            this.listSuffixes.Name = "listSuffixes";
            this.listSuffixes.Size = new System.Drawing.Size(53, 21);
            this.listSuffixes.TabIndex = 4;
            this.listSuffixes.SelectedIndexChanged += new System.EventHandler(this.listSuffixes_SelectedIndexChanged);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(305, 138);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(224, 138);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 99);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(368, 23);
            this.progressBar.TabIndex = 7;
            this.progressBar.Visible = false;
            // 
            // MoveExtentForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(394, 175);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.listSuffixes);
            this.Controls.Add(this.numericDiskOffset);
            this.Controls.Add(this.listDisks);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(400, 200);
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "MoveExtentForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Move Extent";
            this.Load += new System.EventHandler(this.MoveExtentForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MoveExtentForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.numericDiskOffset)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox listDisks;
        private System.Windows.Forms.NumericUpDown numericDiskOffset;
        private System.Windows.Forms.ComboBox listSuffixes;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.ProgressBar progressBar;
    }
}