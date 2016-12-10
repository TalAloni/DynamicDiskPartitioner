using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        /// <summary>
        /// Selected directory identifier (base segment number / folderID) for each volume GUID
        /// </summary>
        private static Dictionary<Guid, string> m_selectedDirectory = new Dictionary<Guid, string>();

        private static string SelectedDirectory
        {
            get
            {
                if (m_selectedVolume != null)
                {
                    Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                    if (!windowsVolumeGuid.HasValue)
                    {
                        windowsVolumeGuid = Guid.Empty;
                    }
                    if (!m_selectedDirectory.ContainsKey(windowsVolumeGuid.Value))
                    {
                        return null;
                    }

                    return m_selectedDirectory[windowsVolumeGuid.Value];
                }
                return null;
            }
            set
            {
                if (m_selectedVolume != null)
                {
                    Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                    if (!windowsVolumeGuid.HasValue)
                    {
                        windowsVolumeGuid = Guid.Empty;
                    }
                    m_selectedDirectory[windowsVolumeGuid.Value] = value;
                }
            }
        }

        public static void CDCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    if (!((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Volume is not operational.");
                        return;
                    }
                }

                FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                if (fileSystem != null)
                {
                    if (args.Length >= 2)
                    {
                        string folder = Unquote(args[1]);
                        string currentFolder = (SelectedDirectory == null) ? @"\" : SelectedDirectory;
                        if (folder == "..")
                        {
                            SelectedDirectory = FileSystem.GetParentDirectory(currentFolder);
                        }
                        else
                        {
                            FileSystemEntry subDirectory = fileSystem.GetEntry(currentFolder + folder);

                            if (subDirectory != null && subDirectory.IsDirectory)
                            {
                                SelectedDirectory = subDirectory.FullName;
                            }
                            else
                            {
                                Console.WriteLine("Invalid directory name.");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid or unsupported file system.");
                }
            }
            else
            {
                Console.WriteLine("No volume has been selected.");
            }
        }

        public static void DirCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    if (!((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Volume is not operational.");
                        return;
                    }
                }

                FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                if (fileSystem != null)
                {
                    string currentDirectory = (SelectedDirectory == null) ? @"\" : SelectedDirectory;
                    List<FileSystemEntry> entries = fileSystem.ListEntriesInDirectory(currentDirectory);

                    foreach (FileSystemEntry entry in entries)
                    {
                        string sizeString = String.Empty;
                        if (!entry.IsDirectory)
                        {
                            sizeString = entry.Size.ToString("###,###,###,##0");
                        }
                        sizeString = sizeString.PadLeft(15);
                        string isDirectoryString;
                        if (entry.IsDirectory)
                        {
                            isDirectoryString = "<DIR>";
                        }
                        else
                        {
                            isDirectoryString = "     ";
                        }

                        Console.WriteLine("{0}  {1}  {2}  {3}", entry.CreationTime.ToString("yyyy-MM-dd"), isDirectoryString, sizeString, entry.Name);
                    }
                }
                else
                {
                    Console.WriteLine("Invalid or unsupported file system.");
                }
            }
            else
            {
                Console.WriteLine("No volume has been selected.");
            }
        }

        public static void CopyCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    if (!((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Volume is not operational.");
                        return;
                    }
                }

                FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                if (fileSystem != null)
                {
                    if (args.Length == 3)
                    {
                        string source = args[1];

                        string currentDirectory = (SelectedDirectory == null) ? @"\" : SelectedDirectory;
                        FileSystemEntry fileEntry = fileSystem.GetEntry(currentDirectory + source);
                        if (fileEntry != null)
                        {
                            string destination = args[2];
                            if (destination.EndsWith("\\"))
                            {
                                destination = destination + fileEntry.Name;
                            }
                            
                            try
                            {
                                fileSystem.CopyFile(currentDirectory + source, currentDirectory + destination);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Console.WriteLine("Access denied.");
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine("IO error: " + ex.ToString());
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid source file specified.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid number of arguments.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid or unsupported file system.");
                }
            }
            else
            {
                Console.WriteLine("No volume has been selected.");
            }
        }

        public static void TypeCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    if (!((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Volume is not operational.");
                        return;
                    }
                }

                FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                if (fileSystem != null)
                {
                    string currentDirectory = (SelectedDirectory == null) ? @"\" : SelectedDirectory;
                    string path = currentDirectory + args[1];
                    FileSystemEntry entry = fileSystem.GetEntry(path);
                    if (entry != null)
                    {
                        Stream fileStream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        byte[] bytes = new byte[fileStream.Length];
                        fileStream.Read(bytes, 0, bytes.Length);
                        Console.WriteLine(ASCIIEncoding.ASCII.GetString(bytes));
                    }
                }
                else
                {
                    Console.WriteLine("Invalid or unsupported file system.");
                }
            }
        }

        public static void AppendCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                if (fileSystem != null)
                {
                    string currentDirectory = (SelectedDirectory == null) ? @"\" : SelectedDirectory;
                    string path = currentDirectory + args[1];
                    FileSystemEntry entry = fileSystem.GetEntry(path);
                    if (entry != null)
                    {
                        Stream fileStream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                        fileStream.Seek(fileStream.Length, SeekOrigin.Begin);
                        byte[] bytes = ASCIIEncoding.ASCII.GetBytes(args[2]);
                        fileStream.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        Console.WriteLine("File not found.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid or unsupported file system.");
                }
            }
        }

        public static void HelpCD()
        {
            Console.WriteLine();
            Console.WriteLine("    Changes the current working directory.");
            Console.WriteLine();
            Console.WriteLine("Synatx: CD ..");
            Console.WriteLine("Synatx: CD <subdirectory>");
        }

        public static void HelpDir()
        {
            Console.WriteLine();
            Console.WriteLine("    Lists all files and folders in the current directory.");
            Console.WriteLine();
            Console.WriteLine("Synatx: DIR");
        }

        public static void HelpCopy()
        {
            Console.WriteLine();
            Console.WriteLine("    Copy a file from the current directory on the selected volume, to a volume");
            Console.WriteLine("    mounted by the oprating system.");
            Console.WriteLine();
            Console.WriteLine("Syntax: COPY <source-filename> <destination>");
            Console.WriteLine();
            
            Console.WriteLine("    Examples:");
            Console.WriteLine("    ---------");
            Console.WriteLine("    COPY movie.mkv C:\\");
            Console.WriteLine("    COPY movie.mkv C:\\me.mkv");
        }
    }
}
