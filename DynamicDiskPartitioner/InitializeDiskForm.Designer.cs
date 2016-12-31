namespace DynamicDiskPartitioner
{
    partial class InitializeDiskForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InitializeDiskForm));
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.numericMicrosoftReservedPartitionSize = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.lblMiB = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numericMicrosoftReservedPartitionSize)).BeginInit();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 96);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(368, 23);
            this.progressBar.TabIndex = 16;
            this.progressBar.Visible = false;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(224, 135);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 15;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(305, 135);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 14;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // numericMicrosoftReservedPartitionSize
            // 
            this.numericMicrosoftReservedPartitionSize.Location = new System.Drawing.Point(181, 15);
            this.numericMicrosoftReservedPartitionSize.Maximum = new decimal(new int[] {
            128,
            0,
            0,
            0});
            this.numericMicrosoftReservedPartitionSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericMicrosoftReservedPartitionSize.Name = "numericMicrosoftReservedPartitionSize";
            this.numericMicrosoftReservedPartitionSize.Size = new System.Drawing.Size(121, 20);
            this.numericMicrosoftReservedPartitionSize.TabIndex = 13;
            this.numericMicrosoftReservedPartitionSize.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(166, 13);
            this.label1.TabIndex = 17;
            this.label1.Text = "Microsoft Reserved Partition Size:";
            // 
            // lblMiB
            // 
            this.lblMiB.AutoSize = true;
            this.lblMiB.Location = new System.Drawing.Point(308, 17);
            this.lblMiB.Name = "lblMiB";
            this.lblMiB.Size = new System.Drawing.Size(25, 13);
            this.lblMiB.TabIndex = 18;
            this.lblMiB.Text = "MiB";
            // 
            // InitializeDiskForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(394, 175);
            this.Controls.Add(this.lblMiB);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.numericMicrosoftReservedPartitionSize);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(400, 200);
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "InitializeDiskForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Initialize Disk";
            ((System.ComponentModel.ISupportInitialize)(this.numericMicrosoftReservedPartitionSize)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.NumericUpDown numericMicrosoftReservedPartitionSize;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblMiB;
    }
}