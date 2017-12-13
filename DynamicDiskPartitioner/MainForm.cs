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
using DiskAccessLibrary.LogicalDiskManager;

namespace DynamicDiskPartitioner
{
    public partial class MainForm : Form
    {
        private const int Windows6WaitTimeBeforeRefresh = 200;

        private List<PhysicalDisk> m_disks;
        private List<DynamicDisk> m_dynamicDisks;

        public MainForm()
        {
            InitializeComponent();
            disksView.DiskRightClick += new EventHandler<DiskRightClickEventArgs>(DiskCollectionView_DiskRightClick);
            disksView.ExtentRightClick += new EventHandler<ExtentRightClickEventArgs>(DiskCollectionView_ExtentRightClick);
        }

        private void DiskCollectionView_DiskRightClick(object sender, DiskRightClickEventArgs e)
        {
            bool hasMBR = false;
            try
            {
                MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(e.Disk);
                hasMBR = (mbr != null);
            }
            catch (IOException)
            {
            }
            initializeDiskMenuItem.Visible = !hasMBR;
            diskContextMenu.Tag = e.Disk;
            diskContextMenu.Show((Control)sender, e.Location);
        }

        private void DiskCollectionView_ExtentRightClick(object sender, ExtentRightClickEventArgs e)
        {
            bool isDynamicDisk = false;
            bool hasResumeRecord = false;
            try
            {
                isDynamicDisk = DynamicDisk.IsDynamicDisk(e.Extent.Disk);
                if (e.Volume != null && (!(e.Volume is DynamicVolume) || ((DynamicVolume)e.Volume).IsOperational))
                {
                    hasResumeRecord = DynamicDiskPartitionerResumeRecord.HasValidSignature(e.Volume.ReadSector(0));
                }
            }
            catch (IOException)
            {
            }
            createVolumeMenuItem.Visible = (e.Volume == null && isDynamicDisk);
            moveExtentMenuItem.Visible = (e.Volume != null && e.Volume is DynamicVolume);
            resumeOperationMenuItem.Visible = hasResumeRecord;
            extendVolumeMenuItem.Visible = (e.Volume != null);
            volumePropertiesMenuItem.Visible = (e.Volume != null);
            fileSystemMenuItem.Visible = (e.Volume != null);
            addDiskToVolumeMenuItem.Visible = (e.Volume != null && e.Volume is Raid5Volume);

            bool isHealthy = (e.Volume != null && (!(e.Volume is DynamicVolume) || ((DynamicVolume)e.Volume).IsHealthy));
            bool isOperational = (e.Volume != null && (!(e.Volume is DynamicVolume) || ((DynamicVolume)e.Volume).IsOperational));
            bool isMirroredVolume = (e.Volume != null && e.Volume is MirroredVolume);
            extendVolumeMenuItem.Enabled = isHealthy && !isMirroredVolume;
            moveExtentMenuItem.Enabled = isOperational;
            fileSystemMenuItem.Enabled = isOperational;
            addDiskToVolumeMenuItem.Enabled = isHealthy;

            extentContextMenu.Tag = new KeyValuePair<Volume, DiskExtent>(e.Volume, e.Extent);
            extentContextMenu.Show((Control)sender, e.Location);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += " v" + version.ToString(3);
            UpdateView();
        }

        private void UpdateView()
        {
            m_disks = PhysicalDiskHelper.GetPhysicalDisks();
            m_dynamicDisks = null;
            disksView.PopulateView(m_disks);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            disksView.Width = this.Width - 27;
            disksView.Height = this.Height - 60;
            lblAuthor.Location = new Point(lblAuthor.Location.X, this.Height - 42);
        }

        private void extentPropertiesMenuItem_Click(object sender, EventArgs e)
        {
            Volume volume = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            DiskExtent extent = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Value;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Volume information:");
            builder.Append(VolumeInfo.GetVolumeInformation(volume));
            builder.AppendLine();

            MessageBox.Show(builder.ToString(), "Volume Properties");
        }

        private void diskPropertiesMenuItem_Click(object sender, EventArgs e)
        {
            Disk disk = (Disk)diskContextMenu.Tag;
            MessageBox.Show(DiskInfo.GetDiskInformation(disk), "Disk Properties");
        }

        private void cleanDiskMenuItem_Click(object sender, EventArgs e)
        {
            Disk disk = (Disk)diskContextMenu.Tag;
            DialogResult result = MessageBox.Show("Are you sure that you want to destroy the disk?", "Warning", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                if (disk is PhysicalDisk)
                {
                    bool success = ((PhysicalDisk)disk).ExclusiveLock();
                    if (!success)
                    {
                        Console.WriteLine("Failed to lock the disk.");
                        return;
                    }
                }
                for (int index = 0; index < 63; index++)
                {
                    byte[] bytes = new byte[disk.BytesPerSector];
                    disk.WriteSectors(index, bytes);
                }
                if (disk is PhysicalDisk)
                {
                    ((PhysicalDisk)disk).ReleaseLock();
                    ((PhysicalDisk)disk).UpdateProperties();
                }
                UpdateView();
            }
        }

        private void initializeDiskMenuItem_Click(object sender, EventArgs e)
        {
            Disk disk = (Disk)diskContextMenu.Tag;
            InitializeDiskForm initializeDisk = new InitializeDiskForm(disk);
            DialogResult result = initializeDisk.ShowDialog();
            if (result == DialogResult.OK)
            {
                UpdateView();
            }
        }

        private void createVolumeMenuItem_Click(object sender, EventArgs e)
        {
            DiskExtent extent = (DiskExtent)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Value;
            
            List<DynamicDisk> dynamicDisks = GetDynamicDisks();
            DynamicDisk disk = DynamicDisk.ReadFromDisk(extent.Disk);
            List<DynamicDisk> diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, disk.DiskGroupGuid);

            CreateVolumeForm createVolume = new CreateVolumeForm(diskGroup, extent);
            DialogResult result = createVolume.ShowDialog();
            if (result == DialogResult.OK)
            {
                UpdateView();
            }
        }

        private void moveExtentMenuItem_Click(object sender, EventArgs e)
        {
            DynamicVolume volume = (DynamicVolume)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            DynamicDiskExtent extent = (DynamicDiskExtent)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Value;

            List<DynamicDisk> dynamicDisks = GetDynamicDisks();
            List<DynamicDisk> diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, volume.DiskGroupGuid);

            bool isBootVolume;
            if (RetainHelper.IsVolumeRetained(volume, out isBootVolume))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("You're trying to move a retained volume (volume that has a partition");
                builder.AppendLine("associated with it).");
                builder.AppendLine("If an operating system is present on this volume, a reconfiguration");
                builder.AppendLine("might be necessary before you could boot it successfully.");
                builder.AppendLine("This operation is currently not supported.");
                MessageBox.Show(builder.ToString(), "Warning");
                return;
            }

            if (DynamicDiskPartitionerResumeRecord.HasValidSignature(volume.ReadSector(0)))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("There is already an operation in progress");
                builder.AppendLine("Use the RESUME command to resume the operation");
                MessageBox.Show(builder.ToString(), "Error");
                return;
            }

            MoveExtentForm moveExtent = new MoveExtentForm(diskGroup, volume, extent);
            DialogResult result = moveExtent.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    // Windows 7 / 2008 will likely make changes to the disk group, it will be marked as 'dirty' if we don't wait
                    Thread.Sleep(Windows6WaitTimeBeforeRefresh);
                }
                UpdateView();
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    //MessageBox.Show("Please go to Disk Management and reactivate the disk group", "Operation completed successfully");
                    MessageBox.Show("Click OK to Continue", "Operation completed successfully");
                }
                else
                {
                    string message = OperatingSystemHelper.GetUpdateMessage();
                    MessageBox.Show(message, "Operation completed successfully");
                }
            }
        }

        private void extendVolumeMenuItem_Click(object sender, EventArgs e)
        {
            Volume volume = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;

            List<DynamicDisk> diskGroup = null;
            if (volume is DynamicVolume)
            {
                List<DynamicDisk> dynamicDisks = GetDynamicDisks();
                diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, ((DynamicVolume)volume).DiskGroupGuid);
            }
            
            ExtendVolumeForm extendVolume = new ExtendVolumeForm(diskGroup, volume);
            DialogResult result = extendVolume.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    // Windows 7 / 2008 will likely make changes to the disk group, it will be marked as 'dirty' if we don't wait
                    Thread.Sleep(Windows6WaitTimeBeforeRefresh);
                }
                UpdateView();
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    //MessageBox.Show("Please go to Disk Management and reactivate the disk group", "Operation completed successfully");
                    MessageBox.Show("The volume has been extended successfully.\nyou can now proceed to extend the underlying file system.", "Operation completed successfully");
                }
                else
                {
                    string message = "The volume has been extended successfully.\nyou can now proceed to extend the underlying file system.";
                    if (volume is DynamicVolume)
                    {
                        message += "\n\n" + OperatingSystemHelper.GetUpdateMessage();
                    }
                    MessageBox.Show(message, "Operation completed successfully");
                }
            }
        }

        private void addDiskToVolumeMenuItem_Click(object sender, EventArgs e)
        {
            DynamicVolume volume = (DynamicVolume)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            DynamicDiskExtent extent = (DynamicDiskExtent)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Value;

            List<DynamicDisk> dynamicDisks = GetDynamicDisks();
            List<DynamicDisk> diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, volume.DiskGroupGuid);

            AddDiskForm addDisk = new AddDiskForm(diskGroup, volume);
            DialogResult result = addDisk.ShowDialog();
            if (result == DialogResult.OK)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    // Windows 7 / 2008 will likely make changes to the disk group, it will be marked as 'dirty' if we don't wait
                    Thread.Sleep(Windows6WaitTimeBeforeRefresh);
                }
                UpdateView();
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    //MessageBox.Show("Please go to Disk Management and reactivate the disk group", "Operation completed successfully");
                    MessageBox.Show("The volume has been extended successfully.\nyou can now proceed to extend the underlying file system.", "Operation completed successfully");
                }
                else
                {
                    string message = "The volume has been extended successfully.\nyou can now proceed to extend the underlying file system.";
                    message += "\n\n" + OperatingSystemHelper.GetUpdateMessage();
                    MessageBox.Show(message, "Operation completed successfully");
                }
            }
        }

        private void resumeOperationMenuItem_Click(object sender, EventArgs e)
        {
            DynamicVolume volume = (DynamicVolume)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            DynamicDiskExtent extent = (DynamicDiskExtent)((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Value;
            
            List<DynamicDisk> dynamicDisks = GetDynamicDisks();
            List<DynamicDisk> diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, volume.DiskGroupGuid);

            ResumeForm resumeForm = new ResumeForm(diskGroup, volume, extent);
            DialogResult result = resumeForm.ShowDialog();
            if (result == DialogResult.OK)
            {
                UpdateView();
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    MessageBox.Show("Click OK to Continue", "Operation completed successfully");
                }
                else
                {
                    string message = OperatingSystemHelper.GetUpdateMessage();
                    MessageBox.Show(message, "Operation completed successfully");
                }
            }
        }

        private void extendFileSystemMenuItem_Click(object sender, EventArgs e)
        {
            Volume volume = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;

            List<DynamicDisk> diskGroup = null;
            if (volume is DynamicVolume)
            {
                List<DynamicDisk> dynamicDisks = GetDynamicDisks();
                diskGroup = DynamicDiskHelper.FindDiskGroup(dynamicDisks, ((DynamicVolume)volume).DiskGroupGuid);
            }

            ExtendFileSystemForm extendFileSystem = new ExtendFileSystemForm(diskGroup, volume);
            DialogResult result = extendFileSystem.ShowDialog();
            if (result == DialogResult.OK)
            {
                UpdateView();
                if (Environment.OSVersion.Version.Major >= 6 && volume is DynamicVolume)
                {
                    //MessageBox.Show("Please go to Disk Management and reactivate the disk group", "Operation completed successfully");
                    MessageBox.Show("Click OK to Continue", "Operation completed successfully");
                }
            }
        }

        private void exportFileMenuItem_Click(object sender, EventArgs e)
        {
            Volume volume = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            ExportFileForm exportFile = new ExportFileForm(volume);
            exportFile.ShowDialog();
        }

        private void fileSystemPropertiesMenuItem_Click(object sender, EventArgs e)
        {
            Volume volume = ((KeyValuePair<Volume, DiskExtent>)extentContextMenu.Tag).Key;
            MessageBox.Show(VolumeInfo.GetFileSystemInformation(volume), "File system Properties");
        }

        private List<DynamicDisk> GetDynamicDisks()
        {
            if (m_dynamicDisks == null)
            {
                m_dynamicDisks = new List<DynamicDisk>();
                foreach (PhysicalDisk disk in m_disks)
                {
                    DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
                    if (dynamicDisk != null)
                    {
                        m_dynamicDisks.Add(dynamicDisk);
                    }
                }
            }
            return m_dynamicDisks;
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                UpdateView();
            }
        }
    }
}