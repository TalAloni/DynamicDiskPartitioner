using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Reflection;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    partial class Program
    {
        public static bool m_debug = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Raid5Manager v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Author: Tal Aloni (tal.aloni.il@gmail.com)");

            WindowsIdentity windowsIdentity = null;
            try
            {
                windowsIdentity = WindowsIdentity.GetCurrent();
            }
            catch
            {
                Console.WriteLine("This program requires execution privileges, press any key to exit");
                Console.ReadKey();
                return;
            }
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
            bool isAdministrator = windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdministrator)
            {
                Console.WriteLine("This program requires administrator privileges, press any key to exit");
                Console.ReadKey();
                return;
            }

            MainLoop();
        }

        public static void MainLoop()
        {
            bool exit = false;
            while (true)
            {
                if (m_debug)
                {
                    exit = ProcessCommand();
                }
                else
                {
                    try
                    {
                        exit = ProcessCommand();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unhandled exception: " + ex.ToString());
                    }
                }

                if (exit)
                {
                    break;
                }
            }
        }

        /// <returns>true to exit</returns>
        public static bool ProcessCommand()
        {
            Console.WriteLine();
            Console.Write("RAID5MGR> ");
            string command = Console.ReadLine();
            string[] args = GetCommandArgsIgnoreEscape(command);
            bool exit = false;
            if (args.Length > 0)
            {
                string commandName = args[0];
                switch (commandName.ToLower())
                {
                    /* Disk / Volume management: */
                    case "add":
                        AddCommand(args);
                        break;
                    case "clean":
                        CleanCommand(args);
                        break;
                    case "create":
                        CreateCommand(args);
                        break;
                    case "detail":
                        DetailCommand(args);
                        break;
                    case "exit":
                        exit = true;
                        break;
                    case "extend":
                        ExtendCommand(args);
                        break;
                    case "help":
                        {
                            HelpCommand(args);
                            break;
                        }
                    case "initialize":
                        InitializeCommand(args);
                        break;
                    case "list":
                        ListCommand(args);
                        break;
                    case "move":
                        MoveCommand(args);
                        break;
                    case "offline":
                        OfflineCommand(args);
                        break;
                    case "online":
                        OnlineCommand(args);
                        break;
                    case "rebase":
                        RebaseCommand(args);
                        break;
                    case "resume":
                        ResumeCommand(args);
                        break;
                    case "select":
                        SelectCommand(args);
                        break;
                    case "test":
                        TestCommand(args);
                        break;
                    /* File System: */
                    case "cd":
                        CDCommand(args);
                        break;
                    case "cd..":
                        CDCommand(new string[] { "cd", ".." });
                        break;
                    case "dir":
                        DirCommand(args);
                        break;
                    case "copy":
                        CopyCommand(args);
                        break;
                    /* Program: */
                    case "set":
                        SetCommand(args);
                        break;
                    /* NTFS: */
                    case "type":
                        TypeCommand(args);
                        break;
                    case "append":
                        AppendCommand(args);
                        break;
                    /* temporary */
                    case "lock":
                        LockCommand(args);
                        break;
                    case "unlock":
                        UnlockCommand(args);
                        break;
                    case "info":
                        InfoCommand(args);
                        break;
                    case "klog":
                        {
                            List<DynamicDisk> disks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
                            DiskGroupDatabase db = DiskGroupDatabase.ReadFromDisks(disks)[0];
                            foreach (KernelUpdateLogPage page in db.KernelUpdateLog.Pages)
                            {
                                foreach (KernelUpdateLogEntry entry in page.LogEntries)
                                {
                                    Console.WriteLine("{0}: Committed TID: {1}, Pending TID: {2}", entry.Status, entry.CommittedTransactionID, entry.PendingTransactionID);
                                }
                            }
                            break;
                        }
                    case "dismount":
                        {
                            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                            if (windowsVolumeGuid.HasValue)
                            {
                                bool success = WindowsVolumeManager.DismountVolume(windowsVolumeGuid.Value);
                                Console.WriteLine("Success: " + success);
                            }
                            break;
                        }
                    case "bench":
                        for (int index = 0; index < 10; index++)
                        {
                            int buffer = 2 * (int)Math.Pow(2, index);
                            DateTime dt1 = DateTime.Now;
                            SetCommand(new string[] { "", "buffer=" + buffer + "mb" });
                            MoveCommand(new string[] { "", "offset=10mb", "extent=0" });
                            MoveCommand(new string[] { "", "offset=10gb", "extent=0" });
                            DateTime dt2 = DateTime.Now;
                            TimeSpan ts = dt2 - dt1;
                            Console.WriteLine("Buffer = {0}MB, Time (ms): {1}", buffer, ts.TotalMilliseconds);
                        }
                        break;
                    case "ldm":
                        {
                            LDMCommand(args);
                            break;
                        }
                    default:
                        Console.WriteLine("Invalid command. type HELP to see the list of commands.");
                        break;
                }
            }
            return exit;
        }

        public static KeyValuePairList<string, string> ParseParameters(string[] args, int start)
        {
            KeyValuePairList<string, string> result = new KeyValuePairList<string, string>();
            for (int index = start; index < args.Length; index++)
            {
                string[] pair = args[index].Split('=');
                if (pair.Length >= 2)
                {
                    string key = pair[0].ToLower(); // we search by the key, so it should be set to lowercase
                    string value = pair[1];
                    value = Unquote(value);
                    result.Add(key, value);
                }
                else
                {
                    result.Add(pair[0].ToLower(), String.Empty);
                }
            }
            return result;
        }

        /// <summary>
        /// Make sure all given parameters are allowed
        /// </summary>
        public static bool VerifyParameters(KeyValuePairList<string, string> parameters, params string[] allowedKeys)
        {
            List<string> allowedList = new List<string>(allowedKeys);
            List<string> keys = parameters.Keys;
            foreach(string key in keys)
            {
                if (!allowedList.Contains(key))
                {
                    return false;
                }
            }
            return true;
        }

        private static int IndexOfUnquotedSpace(string str)
        {
            return IndexOfUnquotedSpace(str, 0);
        }

        private static int IndexOfUnquotedSpace(string str, int startIndex)
        {
            return QuotedStringUtils.IndexOfUnquotedChar(str, ' ', startIndex);
        }

        public static string Unquote(string str)
        {
            string quote = '"'.ToString();
            if (str.StartsWith(quote) && str.EndsWith(quote))
            {
                return str.Substring(1, str.Length - 2);
            }
            else
            {
                return str;
            }
        }

        private static string[] GetCommandArgsIgnoreEscape(string commandLine)
        {
            List<string> argsList = new List<string>();
            int endIndex = IndexOfUnquotedSpace(commandLine);
            int startIndex = 0;
            while (endIndex != -1)
            {
                int length = endIndex - startIndex;
                string nextArg = commandLine.Substring(startIndex, length);
                nextArg = Unquote(nextArg);
                argsList.Add(nextArg);
                startIndex = endIndex + 1;
                endIndex = IndexOfUnquotedSpace(commandLine, startIndex);
            }

            string lastArg = commandLine.Substring(startIndex);
            lastArg = Unquote(lastArg);
            if (lastArg != String.Empty)
            {
                argsList.Add(lastArg);
            }

            return argsList.ToArray();
        }
    }
}
