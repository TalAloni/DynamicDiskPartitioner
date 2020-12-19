/* Copyright (C) 2016-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary.Win32;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class DiskCollectionView : UserControl
    {
        private static int LargestDiskWidth = 500;
        private static int DiskButtonWidth = 71;
        private static int DiskHeight = 70;
        private static int ExtentBannerHeight = 9;
        private static int ExtentPaddingTop = 1;

        private List<CheckBox> m_diskCheckboxes = new List<CheckBox>();
        private List<CheckBox> m_extentCheckboxes = new List<CheckBox>();
        public event EventHandler<DiskRightClickEventArgs> DiskRightClick;
        public event EventHandler<ExtentRightClickEventArgs> ExtentRightClick;

        public DiskCollectionView()
        {
            InitializeComponent();
        }

        public void PopulateView(List<PhysicalDisk> disks)
        {
            List<Disk> temp = new List<Disk>();
            foreach (PhysicalDisk disk in disks)
            {
                temp.Add(disk);
            }
            PopulateView(temp);
        }

        private void CleanView()
        {
            foreach (CheckBox checkbox in m_diskCheckboxes)
            {
                this.Controls.Remove(checkbox);
            }

            foreach (CheckBox checkbox in m_extentCheckboxes)
            {
                this.Controls.Remove(checkbox);
            }
        }

        public void PopulateView(List<Disk> disks)
        {
            CleanView();
            long largestDiskSize = 0;
            for(int diskIndex = 0; diskIndex < disks.Count; diskIndex++)
            {
                Disk disk = disks[diskIndex];
                CheckBox chkDisk = new CheckBox();
                chkDisk.Appearance = Appearance.Button;
                chkDisk.Text = DiskLabelHelper.GetDiskLabel(disk, diskIndex);
                int locationY = diskIndex * (DiskHeight + 2);
                chkDisk.Location = new Point(0, locationY);
                chkDisk.Width = DiskButtonWidth;
                chkDisk.Height = DiskHeight;
                chkDisk.Padding = new Padding(0, ExtentBannerHeight + ExtentPaddingTop, 0, 0);
                chkDisk.TextAlign = ContentAlignment.TopLeft;
                chkDisk.Tag = disk;
                chkDisk.Click += new EventHandler(Disk_Click);
                chkDisk.MouseUp += new MouseEventHandler(Disk_MouseUp);
                this.Controls.Add(chkDisk);
                m_diskCheckboxes.Add(chkDisk);

                if (disk.Size > largestDiskSize)
                {
                    largestDiskSize = disk.Size;
                }
            }

            List<VisualDiskExtent> extents = VisualDiskHelper.GetVisualExtents(disks);
            for (int diskIndex = 0; diskIndex < disks.Count; diskIndex++)
            {
                Disk disk = disks[diskIndex];
                int diskWidth = (int)VisualDiskHelper.Scale(disk.Size, largestDiskSize, LargestDiskWidth);
                List<VisualDiskExtent> diskExtents = VisualDiskExtentHelper.GetFiltered(extents, diskIndex);
                VisualDiskExtentHelper.SortExtentsByFirstSector(diskExtents);
                List<int> widthEntries = VisualDiskExtentHelper.DistributeWidth(diskExtents, diskWidth);
                int locationX = DiskButtonWidth + 2;
                for (int extentIndex = 0; extentIndex < diskExtents.Count; extentIndex++)
                {
                    VisualDiskExtent extent = diskExtents[extentIndex];
                    CheckBox chkExtent = new CheckBox();
                    chkExtent.Appearance = Appearance.Button;
                    int extentWidth = widthEntries[extentIndex];
                    if (extentWidth > 20)
                    {
                        chkExtent.Text = DiskLabelHelper.GetExtentLabel(extent.Volume, extent.Extent, extentWidth);
                    }
                    if (extentWidth < 50)
                    {
                        chkExtent.Font = new Font(chkExtent.Font.FontFamily, 6.5f);
                    }
                    int locationY = extent.VisualDiskIndex * (DiskHeight + 2);
                    Rectangle extentRect = new Rectangle(locationX, locationY, extentWidth, DiskHeight);
                    locationX += extentWidth;
                    if (extent.Volume == null)
                    {
                        //chkExtent.BackColor = Color.FromArgb(214, 211, 208);
                    }
                    chkExtent.Location = extentRect.Location;
                    chkExtent.Width = extentRect.Width;
                    chkExtent.Height = extentRect.Height;
                    chkExtent.Padding = new Padding(0, ExtentBannerHeight + ExtentPaddingTop, 0, 0);
                    chkExtent.TextAlign = ContentAlignment.TopLeft;
                    chkExtent.Tag = extent;
                    chkExtent.Click += new EventHandler(Extent_Click);
                    chkExtent.MouseUp += new MouseEventHandler(Extent_MouseUp);
                    chkExtent.Paint += new PaintEventHandler(Extent_Paint);
                    this.Controls.Add(chkExtent);
                    m_extentCheckboxes.Add(chkExtent);
                }
            }
        }

        private void Extent_Paint(object sender, PaintEventArgs e)
        {
            CheckBox chkExtent = (CheckBox)sender;
            VisualDiskExtent extent = (VisualDiskExtent)chkExtent.Tag;
            Brush brush = DiskStyling.GetVolumeBrush(extent.Volume);
            int bannerLocationX = 1;
            int bannerLocationY = 2;
            int bannerWidth = chkExtent.Width - bannerLocationX - 2;
            int bannerTextLocationX = 2;
            int bannerTextLocationY = 0;

            if (chkExtent.Checked)
            {
                bannerLocationX++;
                bannerLocationY++;
                bannerTextLocationX++;
                bannerTextLocationY++;
            }
            e.Graphics.FillRectangle(brush, bannerLocationX, bannerLocationY, bannerWidth, ExtentBannerHeight);
            string typeString = VolumeHelper.GetVolumeTypeString(extent.Volume);
            if (extent.Volume == null)
            {
                typeString = "Free";
            }
            Font font = new Font(chkExtent.Font.FontFamily, 7);
            Brush bannerTextBrush = Brushes.White;
            if (extent.Volume is Raid5Volume)
            {
                bannerTextBrush = Brushes.Black;
            }
            e.Graphics.DrawString(typeString, font, bannerTextBrush, bannerTextLocationX, bannerTextLocationY);
        }

        private void Disk_Click(object sender, EventArgs e)
        {
            Disk selectedDisk = (Disk)((CheckBox)sender).Tag;
            UncheckUnselectedDisks(selectedDisk);
            ApplyExtentSelection(null);
        }

        void Disk_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ((CheckBox)sender).Checked = true;
                PhysicalDisk selectedDisk = (PhysicalDisk)((CheckBox)sender).Tag;
                UncheckUnselectedDisks(selectedDisk);
                ApplyExtentSelection(null);
                if (DiskRightClick != null)
                {
                    Point checkBoxLocation = ((CheckBox)sender).Location;
                    Point location = new Point(checkBoxLocation.X + e.X, checkBoxLocation.Y + e.Y);
                    DiskRightClick(this, new DiskRightClickEventArgs(selectedDisk, location));
                }
            }
        }

        private void UncheckUnselectedDisks(Disk selectedDisk)
        {
            foreach (CheckBox diskCheckbox in m_diskCheckboxes)
            {
                Disk disk = (Disk)diskCheckbox.Tag;
                if (disk != selectedDisk)
                {
                    diskCheckbox.Checked = false;
                }
            }
        }

        private void Extent_Click(object sender, EventArgs e)
        {
            VisualDiskExtent selectedExtent = (VisualDiskExtent)((CheckBox)sender).Tag;
            ApplyExtentSelection(selectedExtent);
            UncheckUnselectedDisks(null);
        }

        private void Extent_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                VisualDiskExtent selectedExtent = (VisualDiskExtent)((CheckBox)sender).Tag;
                ApplyExtentSelection(selectedExtent);
                UncheckUnselectedDisks(null);
                if (ExtentRightClick != null)
                {
                    Point checkBoxLocation = ((CheckBox)sender).Location;
                    Point location = new Point(checkBoxLocation.X + e.X, checkBoxLocation.Y + e.Y);
                    ExtentRightClick(this, new ExtentRightClickEventArgs(selectedExtent.Extent, selectedExtent.Volume, location));
                }
            }
        }

        private void ApplyExtentSelection(VisualDiskExtent selectedExtent)
        {
            foreach (CheckBox extentCheckbox in m_extentCheckboxes)
            {
                VisualDiskExtent extent = (VisualDiskExtent)extentCheckbox.Tag;
                extentCheckbox.Checked = (selectedExtent != null) && (extent == selectedExtent || (extent.Volume != null && extent.Volume == selectedExtent.Volume));
            }
        }
    }
}
