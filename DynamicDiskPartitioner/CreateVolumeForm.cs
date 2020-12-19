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
using DiskAccessLibrary.Win32;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class CreateVolumeForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private DiskExtent m_extent;

        public CreateVolumeForm(List<DynamicDisk> diskGroup, DiskExtent extent)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_extent = extent;
        }

        private void CreateVolumeForm_Load(object sender, EventArgs e)
        {
            BindDiskList(m_diskGroup);
            int diskIndex = DiskHelper.IndexOfDisk(m_diskGroup, m_extent.Disk);
            if (diskIndex >= 0)
            {
                listDisks.SetItemChecked(diskIndex, true);
            }

            UpdateExtentSize();
        }

        private void BindDiskList(List<DynamicDisk> diskGroup)
        {
            KeyValuePairList<DynamicDisk, string> items = new KeyValuePairList<DynamicDisk, string>();
            foreach (DynamicDisk dynamicDisk in diskGroup)
            {
                if (dynamicDisk.Disk is PhysicalDisk)
                {
                    int diskNumber = ((PhysicalDisk)dynamicDisk.Disk).PhysicalDiskIndex;
                    string title = String.Format("Physical Disk {0}", diskNumber);
                    items.Add(dynamicDisk, title);
                }
            }
            listDisks.DataSource = items;
            listDisks.DisplayMember = "Value";
            listDisks.ValueMember = "Key";
        }

        private void UpdateExtentSize()
        {
            if (listDisks.CheckedIndices.Count > 0)
            {
                long minExtentSize = Int64.MaxValue;
                foreach (int checkedIndex in listDisks.CheckedIndices)
                {
                    long extentSize = DynamicDiskHelper.GetMaxNewExtentLength(m_diskGroup[checkedIndex]);
                    if (extentSize < minExtentSize)
                    {
                        minExtentSize = extentSize;
                    }
                }
                long minExtentSizeInMB = minExtentSize / 1024 / 1024;
                numericExtentSize.Maximum = minExtentSizeInMB;
                numericExtentSize.Value = minExtentSizeInMB;
                btnOK.Enabled = (minExtentSizeInMB > 0);
            }
            else
            {
                btnOK.Enabled = false;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            int diskCount = listDisks.CheckedIndices.Count;
            if (rbSimple.Checked && diskCount != 1)
            {
                MessageBox.Show("You must select a single disk in order to create a simple volume.", "Error");
                return;
            }

            if (rbRaid5.Checked)
            {
                if (chkDegraded.Checked && diskCount < 2)
                {
                    MessageBox.Show("You must select at least 2 disks in order to create a degraded RAID-5 volume.", "Error");
                    return;
                }

                if (!chkDegraded.Checked && diskCount < 3)
                {
                    MessageBox.Show("You must select at least 3 disks in order to create a RAID-5 volume.", "Error");
                    return;
                }
            }

            long extentSizeInBytes = (long)numericExtentSize.Value * 1024 * 1024;
            List<DiskExtent> extents = new List<DiskExtent>();
            foreach(int checkedIndex in listDisks.CheckedIndices)
            {
                DynamicDisk dynamicDisk = m_diskGroup[checkedIndex];
                DiskExtent extent = DynamicDiskHelper.FindExtentAllocation(dynamicDisk, extentSizeInBytes);
                if (extent == null)
                {
                    MessageBox.Show("One of the disks does not contain enough free space", "Error");
                    return;
                }
                extents.Add(extent);
            }

            DiskGroupLockResult result = DiskGroupHelper.LockDiskGroup(m_diskGroup);
            if (result == DiskGroupLockResult.CannotLockDisk)
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
            else if (result == DiskGroupLockResult.Success)
            {
                DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(m_diskGroup)[0];
                if (rbSimple.Checked)
                {
                    VolumeManagerDatabaseHelper.CreateSimpleVolume(database, extents[0]);
                }
                else
                {
                    VolumeManagerDatabaseHelper.CreateRAID5Volume(database, extents, chkDegraded.Checked);
                }

                DiskGroupHelper.UnlockDiskGroup(m_diskGroup);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void rbRaid5_CheckedChanged(object sender, EventArgs e)
        {
            chkDegraded.Enabled = rbRaid5.Checked;
        }

        private void listDisks_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExtentSize();
        }
    }
}