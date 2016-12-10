using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void HelpCommand(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine();
                Console.WriteLine("Available commands:");
                Console.WriteLine("-------------------");
                Console.WriteLine("ADD        - Expands the selected volume using an additional disk.");
                Console.WriteLine("CD         - Changes the current working directory.");
                Console.WriteLine("CLEAN      - Removes partitioning data from the selected disk.");
                Console.WriteLine("COPY       - Copies a file from the current directory.");
                Console.WriteLine("CREATE     - Creates a new volume.");
                Console.WriteLine("DETAIL     - Provides details about a selected object.");
                Console.WriteLine("DIR        - Lists all files and folders in the current directory.");
                Console.WriteLine("EXTEND     - Extend volume or filesystem.");
                Console.WriteLine("INITIALIZE - Initializes a new disk.");
                Console.WriteLine("LIST       - List disks, volumes, partitions or volume extents.");
                Console.WriteLine("MOVE       - Move extent within the disk or to another disk.");
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    Console.WriteLine("OFFLINE    - Take a disk offline.");
                    Console.WriteLine("ONLINE     - Brings an offline disk online.");
                }
                Console.WriteLine("RESUME     - Resume ADD or MOVE operation.");
                Console.WriteLine("SELECT     - Select disk, volume, partition or extent.");
                Console.WriteLine("SET        - Set program variables.");
                Console.WriteLine();
                Console.WriteLine("Type HELP XXX for help regarding command XXX.");
            }
            else
            {
                switch (args[1].ToLower())
                {
                    case "add":
                        HelpAdd();
                        break;
                    case "clean":
                        HelpClean();
                        break;
                    case "cd":
                        HelpCD();
                        break;
                    case "copy":
                        HelpCopy();
                        break;
                    case "create":
                        HelpCreate();
                        break;
                    case "detail":
                        HelpDetail();
                        break;
                    case "dir":
                        HelpDir();
                        break;
                    case "extend":
                        HelpExtend();
                        break;
                    case "initialize":
                        HelpInitialize();
                        break;
                    case "list":
                        HelpList();
                        break;
                    case "move":
                       HelpMove();
                        break;
                    case "offline":
                        HelpOffline();
                        break;
                    case "online":
                        HelpOnline();
                        break;
                    case "rebase":
                        HelpRebase();
                        break;
                    case "resume":
                        HelpResume();
                        break;
                    case "select":
                        HelpSelect();
                        break;
                    case "set":
                        HelpSet();
                        break;
                    case "test":
                        HelpTest();
                        break;
                    default:
                        Console.WriteLine("No such command: {0}", args[1]);
                        break;
                }
            }
        }
    }
}
