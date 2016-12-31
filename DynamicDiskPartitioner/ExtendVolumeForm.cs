using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class ExtendVolumeForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private Volume m_volume;

        public ExtendVolumeForm(List<DynamicDisk> diskGroup, Volume volume)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_volume = volume;
        }

        private void ExtendVolumeForm_Load(object sender, EventArgs e)
        {
            long numberOfExtentBytesAvailable = ExtendHelper.GetMaximumSizeToExtendVolume(m_volume);
            long numberOfMBAvailable = numberOfExtentBytesAvailable / 1024 / 1024;
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
            long additionalNumberOfExtentSectors = requestedSizeInBytes / m_volume.BytesPerSector;
            DiskGroupLockResult result;
            if (m_volume is Partition)
            {
                result = ExtendVolumeHelper.ExtendPartition((Partition)m_volume, additionalNumberOfExtentSectors);
            }
            else
            {
                result = ExtendVolumeHelper.ExtendDynamicVolume(m_diskGroup, (DynamicVolume)m_volume, additionalNumberOfExtentSectors);
            }
            if (result == DiskGroupLockResult.Success)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else if (result == DiskGroupLockResult.CannotLockDisk)
            {
                MessageBox.Show("Unable to lock all disks!", "Error");
            }
            else if (result == DiskGroupLockResult.CannotLockVolume)
            {
                MessageBox.Show("Unable to lock all volumes!", "Error");
            }
            else if (result == DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly)
            {
                MessageBox.Show("One or more disks are offline or set to readonly.", "Error");
            }
            else if (result == DiskGroupLockResult.CannotTakeDiskOffline)
            {
                MessageBox.Show("Failed to take all dynamic disks offline!", "Error");
            }
        }
    }
}