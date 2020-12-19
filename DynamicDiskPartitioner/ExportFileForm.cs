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
using DiskAccessLibrary.FileSystems.Abstractions;
using Utilities;

namespace DynamicDiskPartitioner
{
    public partial class ExportFileForm : Form
    {
        private Volume m_volume;
        private FileSystem m_fileSystem;

        public ExportFileForm(Volume volume)
        {
            InitializeComponent();
            m_volume = volume;
        }

        private void ExportFileForm_Load(object sender, EventArgs e)
        {
            m_fileSystem = FileSystemHelper.ReadFileSystem(m_volume, true);
            if (m_fileSystem != null)
            {
                foreach (FileSystemEntry entry in m_fileSystem.ListEntriesInRootDirectory())
                {
                    TreeNode node = new TreeNode(entry.Name);
                    node.Name = entry.FullName;
                    tvFileSystem.Nodes.Add(node);
                    if (entry.IsDirectory)
                    {
                        node.Nodes.Add(String.Empty); // Add dummy entry
                    }
                }
            }
            else
            {
                MessageBox.Show("File system is not supported", "Error");
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void tvFileSystem_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            string path = e.Node.Name;
            List<FileSystemEntry> entries = m_fileSystem.ListEntriesInDirectory(path);
            e.Node.Nodes.Clear(); // Remove dummy entry
            foreach (FileSystemEntry entry in entries)
            {
                TreeNode node = new TreeNode(entry.Name);
                node.Name = entry.FullName;
                e.Node.Nodes.Add(node);
                if (entry.IsDirectory)
                {
                    node.Nodes.Add(String.Empty); // Add dummy entry
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (tvFileSystem.SelectedNode == null)
            {
                MessageBox.Show("No file / folder has been selected", "Error");
                return;
            }
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                tvFileSystem.Enabled = false;
                btnOK.Enabled = false;
                btnCancel.Enabled = false;
                string selectedEntryPath = tvFileSystem.SelectedNode.Name;
                string folderPath = folderBrowserDialog.SelectedPath;
                if (!folderPath.EndsWith(@"\"))
                {
                    folderPath += @"\";
                }
                new Thread(delegate()
                {
                    CopySelectedEntry(selectedEntryPath, folderPath);
                }).Start();
            }
        }

        private void CopySelectedEntry(string selectedEntryPath, string folderPath)
        {
            List<FileSystemEntry> fileList = BuildFileList(selectedEntryPath);
            foreach (FileSystemEntry entry in fileList)
            {
                string parentDirectory = FileSystem.GetParentDirectory(selectedEntryPath);
                string relativePath = entry.FullName.Substring(parentDirectory.Length);
                string destinationPath = folderPath + relativePath;
                if (entry.IsDirectory)
                {
                    try
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    catch (IOException ex)
                    {
                        this.Invoke((MethodInvoker)delegate()
                        {
                            tvFileSystem.Enabled = true;
                            btnOK.Enabled = true;
                            btnCancel.Enabled = true;
                            MessageBox.Show(ex.Message, "Error");
                        });
                        return;
                    }
                }
                else
                {
                    string destinationDirectory = Path.GetDirectoryName(destinationPath);
                    try
                    {
                        if (!Directory.Exists(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }
                        CopyFile(entry.FullName, destinationPath);
                    }
                    catch (IOException ex)
                    {
                        this.Invoke((MethodInvoker)delegate()
                        {
                            tvFileSystem.Enabled = true;
                            btnOK.Enabled = true;
                            btnCancel.Enabled = true;
                            MessageBox.Show(ex.Message, "Error");
                        });
                        return;
                    }
                }

                try
                {
                    // Restore entry metadata
                    // Note: File.SetCreationTime will not work on directories, but Directory.SetCreationTime is documented to work on files.
                    Directory.SetCreationTime(destinationPath, entry.CreationTime);
                    Directory.SetLastWriteTime(destinationPath, entry.LastWriteTime);
                    Directory.SetLastAccessTime(destinationPath, entry.LastAccessTime);
                    if (entry.IsHidden || entry.IsReadonly || entry.IsArchived)
                    {
                        FileAttributes attributes = (FileAttributes)0;
                        if (entry.IsDirectory)
                        {
                            attributes |= FileAttributes.Directory;
                        }
                        if (entry.IsHidden)
                        {
                            attributes |= FileAttributes.Hidden;
                        }
                        if (entry.IsReadonly)
                        {
                            attributes |= FileAttributes.ReadOnly;
                        }
                        if (entry.IsArchived)
                        {
                            attributes |= FileAttributes.Archive;
                        }
                        File.SetAttributes(destinationPath, attributes);
                    }
                }
                catch (IOException)
                {
                }
            }
            this.Invoke((MethodInvoker)delegate()
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            });
        }

        private void CopyFile(string fileSystemPath, string destinationPath)
        {
            Stream sourceStream = m_fileSystem.OpenFile(fileSystemPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            FileStream destinationStream = File.Open(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            ByteUtils.CopyStream(sourceStream, destinationStream);
            sourceStream.Close();
            destinationStream.Close();
        }

        private List<FileSystemEntry> BuildFileList(string directoryPath)
        {
            List<FileSystemEntry> result = new List<FileSystemEntry>();
            FileSystemEntry searchRootEntry = m_fileSystem.GetEntry(directoryPath);
            result.Add(searchRootEntry);
            if (!searchRootEntry.IsDirectory)
            {
                return result;
            }
            Queue<FileSystemEntry> directories = new Queue<FileSystemEntry>();
            directories.Enqueue(searchRootEntry);
            while (directories.Count > 0)
            {
                FileSystemEntry currentDirectory = directories.Dequeue();
                List<FileSystemEntry> entries = m_fileSystem.ListEntriesInDirectory(currentDirectory.FullName);
                foreach (FileSystemEntry entry in entries)
                {
                    // The result should include directories (to retain empty directories and directory metadata)
                    result.Add(entry);

                    if (entry.IsDirectory)
                    {
                        directories.Enqueue(entry);
                    }
                }
            }
            return result;
        }
    }
}