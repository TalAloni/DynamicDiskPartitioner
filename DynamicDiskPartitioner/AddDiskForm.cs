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
using DiskAccessLibrary.LogicalDiskManager.Win32;
using DiskAccessLibrary.Win32;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class AddDiskForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private DynamicVolume m_volume;
        private bool m_isWorking;

        public AddDiskForm(List<DynamicDisk> diskGroup, DynamicVolume volume)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_volume = volume;
        }

        private void AddDiskForm_Load(object sender, EventArgs e)
        {
            BindDiskList();
        }

        private void BindDiskList()
        {
            List<DynamicDisk> disks = new List<DynamicDisk>(m_diskGroup);
            foreach (DynamicDiskExtent extent in m_volume.DynamicExtents)
            {
                // Remove disks that already have extents of this volume on them.
                int diskIndex = DiskHelper.IndexOfDisk(disks, extent.Disk);
                if (diskIndex >= 0)
                {
                    disks.RemoveAt(diskIndex);
                }
            }
            BindDiskList(disks);
        }

        private void BindDiskList(List<DynamicDisk> disks)
        {
            KeyValuePairList<DynamicDisk, string> items = new KeyValuePairList<DynamicDisk, string>();
            foreach (DynamicDisk dynamicDisk in disks)
            {
                if (dynamicDisk.Disk is PhysicalDisk)
                {
                    int diskNumber = ((PhysicalDisk)dynamicDisk.Disk).PhysicalDiskIndex;
                    string title = String.Format("Physical Disk {0}", diskNumber);
                    items.Add(dynamicDisk, title);
                }
            }
            listDisks.DisplayMember = "Value";
            listDisks.ValueMember = "Key";
            listDisks.DataSource = items;
            btnOK.Enabled = (items.Count > 0);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DynamicDisk targetDynamicDisk = (DynamicDisk)listDisks.SelectedValue;
            Raid5Volume raid5Volume = (Raid5Volume)m_volume;
            DiskExtent newExtent = DynamicDiskHelper.FindExtentAllocation(targetDynamicDisk, raid5Volume.ColumnSize);
            if (newExtent == null)
            {
                MessageBox.Show("The disk specified does not contain enough free space.", "Error");
                return;
            }

            List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(raid5Volume.DiskGroupGuid);
            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(diskGroup, raid5Volume.DiskGroupGuid);
            if (database.AreDisksMissing)
            {
                DialogResult disksMissingResult = MessageBox.Show("Some of the disks in this disk group are missing, Continue anyway?", "Warning", MessageBoxButtons.YesNo);
                if (disksMissingResult != DialogResult.Yes)
                {
                    return;
                }
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
                listDisks.Enabled = false;
                btnCancel.Enabled = false;
                btnOK.Enabled = false;
                progressBar.Visible = true;

                long bytesTotal = raid5Volume.Size;
                long bytesCopied = 0;
                Thread workerThread = new Thread(delegate()
                {
                    m_isWorking = true;
                    AddDiskToArrayHelper.AddDiskToRaid5Volume(database, raid5Volume, newExtent, ref bytesCopied);
                    m_isWorking = false;
                });
                workerThread.Start();

                new Thread(delegate()
                {
                    while (workerThread.IsAlive)
                    {
                        Thread.Sleep(250);
                        int progress = (int)(100 * (double)bytesCopied / bytesTotal);
                        this.Invoke((MethodInvoker)delegate()
                        {
                            progressBar.Value = progress;
                        });
                    }

                    this.Invoke((MethodInvoker)delegate()
                    {
                        DiskGroupHelper.UnlockDiskGroup(m_diskGroup);
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    });
                }).Start();
            }
        }

        private void AddDiskForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_isWorking)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("This program was designed to recover from a power failure, but not from user intervention.");
                builder.AppendLine("Do not abort or terminate this program during operation!");
                MessageBox.Show(builder.ToString(), "Warning!");
                e.Cancel = m_isWorking; // if a user clicked OK after the operation was complete, we can close this form
            }
        }
    }
}