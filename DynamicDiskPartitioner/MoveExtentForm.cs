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
    public partial class MoveExtentForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private DynamicVolume m_volume;
        private DynamicDiskExtent m_extent;
        private int m_previousSuffixIndex = 0;
        private bool m_isWorking;

        public MoveExtentForm(List<DynamicDisk> diskGroup, DynamicVolume volume, DynamicDiskExtent extent)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_volume = volume;
            m_extent = extent;
        }

        private void MoveExtentForm_Load(object sender, EventArgs e)
        {
            listSuffixes.SelectedIndex = 0;
            BindDiskList(m_diskGroup);
            int diskIndex = DiskHelper.IndexOfDisk(m_diskGroup, m_extent.Disk);
            if (diskIndex >= 0)
            {
                listDisks.SelectedIndex = diskIndex;
            }
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
            listDisks.DisplayMember = "Value";
            listDisks.ValueMember = "Key";
            listDisks.DataSource = items;
        }

        private void CompactNumericOffset()
        {
            while ((numericDiskOffset.Value % 1024 == 0) && (listSuffixes.SelectedIndex < listSuffixes.Items.Count - 1))
            {
                listSuffixes.SelectedIndex++; // listSuffixes_SelectedIndexChanged will take care of the rest
            }
        }

        private long GetDiskOffset()
        {
            long offset = (long)numericDiskOffset.Value;
            for (int index = 0; index < listSuffixes.SelectedIndex; index++)
            {
                offset = offset * 1024;
            }
            return offset;
        }

        private void listSuffixes_SelectedIndexChanged(object sender, EventArgs e)
        {
            int currentSuffixIndex = listSuffixes.SelectedIndex;
            int delta = Math.Abs(currentSuffixIndex - m_previousSuffixIndex);
            for (int index = 0; index < delta; index++)
            {
                if (currentSuffixIndex > m_previousSuffixIndex)
                {
                    numericDiskOffset.Minimum = numericDiskOffset.Minimum / 1024;
                    numericDiskOffset.Value = numericDiskOffset.Value / 1024;
                    numericDiskOffset.Maximum = numericDiskOffset.Maximum / 1024;
                }
                else // currentSuffixIndex < previousSuffixIndex
                {
                    numericDiskOffset.Maximum = numericDiskOffset.Maximum * 1024;
                    numericDiskOffset.Value = numericDiskOffset.Value * 1024;
                    numericDiskOffset.Minimum = numericDiskOffset.Minimum * 1024;
                }
            }
            m_previousSuffixIndex = currentSuffixIndex;
        }

        private void listDisks_SelectedIndexChanged(object sender, EventArgs e)
        {
            DynamicDisk dynamicDisk = (DynamicDisk)listDisks.SelectedValue;
            PrivateHeader privateHeader = dynamicDisk.PrivateHeader;
            long publicRegionEndLBA = (long)(privateHeader.PublicRegionStartLBA + privateHeader.PublicRegionSizeLBA);
            numericDiskOffset.Minimum = (long)privateHeader.PublicRegionStartLBA * dynamicDisk.BytesPerSector;
            numericDiskOffset.Maximum = publicRegionEndLBA * dynamicDisk.BytesPerSector - m_extent.Size;
            if (dynamicDisk.Disk != m_extent.Disk)
            {
                DiskExtent allocation = DynamicDiskHelper.FindExtentAllocation(dynamicDisk, m_extent.Size);
                numericDiskOffset.Enabled = (allocation != null);
                btnOK.Enabled = (allocation != null);
                if (allocation != null)
                {
                    numericDiskOffset.Value = allocation.FirstSector * allocation.BytesPerSector;
                    m_previousSuffixIndex = 0;
                    listSuffixes.SelectedIndex = 0;
                    CompactNumericOffset();
                }
            }
            else
            {
                numericDiskOffset.Enabled = true;
                btnOK.Enabled = true;
                numericDiskOffset.Value = m_extent.FirstSector * m_extent.Disk.BytesPerSector;
                m_previousSuffixIndex = 0;
                listSuffixes.SelectedIndex = 0;
                CompactNumericOffset();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DynamicDisk targetDisk = (DynamicDisk)listDisks.SelectedValue;
            long offset = GetDiskOffset();
            bool isSameDisk = (targetDisk.Disk == m_extent.Disk);
            if (!DynamicDiskHelper.IsMoveLocationValid(m_extent, targetDisk, offset))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Invalid offset specified.");
                builder.AppendLine();
                builder.AppendLine("The following conditions must be met:");
                builder.AppendLine("1. The destination must reside inside the data portion of the disk.");
                builder.AppendLine("2. The destination must not be used by any other extents.");
                builder.AppendLine("3. The offset must be aligned to sector size.");
                builder.AppendLine("4. Source and destination disk must have the same sector size.");
                MessageBox.Show(builder.ToString(), "Error");
                return;
            }

            if (isSameDisk && offset == m_extent.FirstSector * m_extent.BytesPerSector)
            {
                MessageBox.Show("Source and destination are the same.", "Error");
                return;
            }

            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(m_diskGroup, targetDisk.DiskGroupGuid);
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
                numericDiskOffset.Enabled = false;
                listSuffixes.Enabled = false;
                btnCancel.Enabled = false;
                btnOK.Enabled = false;
                progressBar.Visible = true;

                long firstSector = offset / targetDisk.BytesPerSector;
                DiskExtent targetExtent = new DiskExtent(targetDisk.Disk, firstSector, m_extent.Size);
                long bytesTotal = m_extent.Size;
                long bytesCopied = 0;
                Thread workerThread = new Thread(delegate()
                {
                    m_isWorking = true;
                    if (isSameDisk)
                    {
                        MoveExtentHelper.MoveExtentWithinSameDisk(database, m_volume, m_extent, targetExtent, ref bytesCopied);
                    }
                    else
                    {
                        MoveExtentHelper.MoveExtentToAnotherDisk(database, m_volume, m_extent, targetExtent, ref bytesCopied);
                    }
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

        private void MoveExtentForm_FormClosing(object sender, FormClosingEventArgs e)
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