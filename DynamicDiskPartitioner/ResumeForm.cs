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
    public partial class ResumeForm : Form
    {
        private List<DynamicDisk> m_diskGroup;
        private DynamicVolume m_volume;
        private DynamicDiskExtent m_extent;
        private bool m_isWorking;

        public ResumeForm(List<DynamicDisk> diskGroup, DynamicVolume volume, DynamicDiskExtent extent)
        {
            InitializeComponent();
            m_diskGroup = diskGroup;
            m_volume = volume;
            m_extent = extent;
        }

        private void ResumeForm_Load(object sender, EventArgs e)
        {
            byte[] resumeRecordBytes = m_volume.ReadSector(0);
            DynamicDiskPartitionerResumeRecord resumeRecord = DynamicDiskPartitionerResumeRecord.FromBytes(resumeRecordBytes);
            if (resumeRecord == null)
            {
                MessageBox.Show("Resume record version is not supported.", "Error");
                return;
            }

            if (resumeRecord is AddDiskOperationResumeRecord)
            {
                ResumeAdd((AddDiskOperationResumeRecord)resumeRecord);
            }
            else if (resumeRecord is MoveExtentOperationResumeRecord)
            {
                ResumeMove((MoveExtentOperationResumeRecord)resumeRecord);
            }
            else
            {
                MessageBox.Show("Resume record operation is not supported.", "Error");
                return;
            }
        }

        private void ResumeAdd(AddDiskOperationResumeRecord resumeRecord)
        {
            // the RAID-5 volume was temporarily converted to striped volume
            if (m_volume is StripedVolume)
            {
                StripedVolume stripedVolume = (StripedVolume)m_volume;

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
                    long bytesTotal = stripedVolume.Size / stripedVolume.NumberOfColumns * (stripedVolume.NumberOfColumns - 2);
                    long bytesCopied = 0;
                    Thread workerThread = new Thread(delegate()
                    {
                        m_isWorking = true;
                        List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(stripedVolume.DiskGroupGuid);
                        DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(diskGroup, stripedVolume.DiskGroupGuid);
                        AddDiskToArrayHelper.ResumeAddDiskToRaid5Volume(database, stripedVolume, resumeRecord, ref bytesCopied);
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
        }

        private void ResumeMove(MoveExtentOperationResumeRecord resumeRecord)
        {
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
                int extentIndex = DynamicDiskExtentHelper.GetIndexOfExtentID(m_volume.DynamicExtents, (resumeRecord).ExtentID);
                DynamicDiskExtent sourceExtent = m_volume.DynamicExtents[extentIndex];

                long bytesTotal = sourceExtent.Size;
                long bytesCopied = 0;
                Thread workerThread = new Thread(delegate()
                {
                    m_isWorking = true;
                    List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(m_volume.DiskGroupGuid);
                    DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(diskGroup, m_volume.DiskGroupGuid);
                    MoveExtentHelper.ResumeMoveExtent(database, m_volume, resumeRecord, ref bytesCopied);
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

        private void ResumeForm_FormClosing(object sender, FormClosingEventArgs e)
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