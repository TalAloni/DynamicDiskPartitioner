using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DiskAccessLibrary;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class ExtendFileSystemForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private Volume m_volume;
        private IExtendableFileSystem m_fileSystem;

        public ExtendFileSystemForm(List<DynamicDisk> diskGroup, Volume volume)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_volume = volume;
        }

        private void ExtendFileSystemForm_Load(object sender, EventArgs e)
        {
            m_fileSystem = FileSystemHelper.ReadFileSystem(m_volume) as IExtendableFileSystem;
            if (m_fileSystem == null)
            {
                MessageBox.Show("Filsystem is not supported for this operation.", "Error");
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            long numberOfBytesAvailable = m_fileSystem.GetMaximumSizeToExtend();
            long numberOfMBAvailable = numberOfBytesAvailable / 1024 / 1024;
            numericAdditionalSize.Maximum = numberOfMBAvailable;
            numericAdditionalSize.Value = numberOfMBAvailable;
            btnOK.Enabled = (numberOfMBAvailable > 0);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            long requestedSizeInBytes = (long)(numericAdditionalSize.Value * 1024 * 1024);
            long additionalNumberOfSectors = requestedSizeInBytes / m_volume.BytesPerSector;

            ExtendFileSystemResult result = ExtendFileSystemHelper.ExtendFileSystem(m_diskGroup, m_volume, additionalNumberOfSectors);
            if (result == ExtendFileSystemResult.Success)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            if (result == ExtendFileSystemResult.NonOperationalVolume)
            {
                MessageBox.Show("Error: non-operational volume!", "Error");
            }
            else if (result == ExtendFileSystemResult.CannotLockDisk)
            {
                MessageBox.Show("Unable to lock the disk!", "Error");
            }
            else if (result == ExtendFileSystemResult.CannotLockVolume)
            {
                MessageBox.Show("Unable to lock the volume!", "Error");
            }
            else if (result == ExtendFileSystemResult.CannotDismountVolume)
            {
                MessageBox.Show("Unable to dismount the volume!", "Error");
            }
            else if (result == ExtendFileSystemResult.OneOrMoreDisksAreOfflineOrReadonly)
            {
                MessageBox.Show("One or more dynamic disks are offline or set to readonly.", "Error");
            }
            else if (result == ExtendFileSystemResult.CannotTakeDiskOffline)
            {
                MessageBox.Show("Failed to take all dynamic disks offline!", "Error");
            }
        }
    }
}