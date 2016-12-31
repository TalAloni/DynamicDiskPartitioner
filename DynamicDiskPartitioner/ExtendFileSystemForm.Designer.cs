namespace DynamicDiskPartitioner
{
    partial class ExtendFileSystemForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExtendFileSystemForm));
            this.lblMiB = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.numericAdditionalSize = new System.Windows.Forms.NumericUpDown();
            this.lblAddition = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numericAdditionalSize)).BeginInit();
            this.SuspendLayout();
            // 
            // lblMiB
            // 
            this.lblMiB.AutoSize = true;
            this.lblMiB.Location = new System.Drawing.Point(221, 17);
            this.lblMiB.Name = "lblMiB";
            this.lblMiB.Size = new System.Drawing.Size(25, 13);
            this.lblMiB.TabIndex = 19;
            this.lblMiB.Text = "MiB";
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 96);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(368, 23);
            this.progressBar.TabIndex = 18;
            this.progressBar.Visible = false;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(224, 135);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 17;
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
            this.btnCancel.TabIndex = 16;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // numericAdditionalSize
            // 
            this.numericAdditionalSize.Location = new System.Drawing.Point(94, 15);
            this.numericAdditionalSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericAdditionalSize.Name = "numericAdditionalSize";
            this.numericAdditionalSize.Size = new System.Drawing.Size(121, 20);
            this.numericAdditionalSize.TabIndex = 15;
            this.numericAdditionalSize.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // lblAddition
            // 
            this.lblAddition.AutoSize = true;
            this.lblAddition.Location = new System.Drawing.Point(12, 17);
            this.lblAddition.Name = "lblAddition";
            this.lblAddition.Size = new System.Drawing.Size(48, 13);
            this.lblAddition.TabIndex = 14;
            this.lblAddition.Text = "Addition:";
            // 
            // ExtendFileSystemForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(394, 175);
            this.Controls.Add(this.lblMiB);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.numericAdditionalSize);
            this.Controls.Add(this.lblAddition);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(400, 200);
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "ExtendFileSystemForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Extend File System";
            this.Load += new System.EventHandler(this.ExtendFileSystemForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.numericAdditionalSize)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblMiB;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.NumericUpDown numericAdditionalSize;
        private System.Windows.Forms.Label lblAddition;
    }
}