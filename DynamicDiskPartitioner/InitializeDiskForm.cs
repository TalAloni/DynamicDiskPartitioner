using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DiskAccessLibrary;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class InitializeDiskForm : Form
    {
        private Disk m_disk;

        public InitializeDiskForm(Disk disk)
        {
            InitializeComponent();
            m_disk = disk;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            long firstUsableLBA = 64; // for alignment purposes
            long reservedPartitionSizeInMB = (long)numericMicrosoftReservedPartitionSize.Value;
            long bytesAvailable = (m_disk.TotalSectors - firstUsableLBA) * m_disk.BytesPerSector;
            if (reservedPartitionSizeInMB * 1024 * 1024 > bytesAvailable)
            {
                MessageBox.Show("Invalid Reserved Partition Size specified, not enough space on the disk.", "Error");
                return;
            }

            if (m_disk is PhysicalDisk)
            {
                bool success = ((PhysicalDisk)m_disk).ExclusiveLock();
                if (!success)
                {
                    MessageBox.Show("Failed to lock the disk.", "Error");
                    return;
                }
            }

            long reservedPartitionSizeLBA = reservedPartitionSizeInMB * 1024 * 1024 / m_disk.BytesPerSector;
            GuidPartitionTable.InitializeDisk(m_disk, firstUsableLBA, reservedPartitionSizeLBA);

            if (m_disk is PhysicalDisk)
            {
                ((PhysicalDisk)m_disk).ReleaseLock();
                ((PhysicalDisk)m_disk).UpdateProperties();
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}