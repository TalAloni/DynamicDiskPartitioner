using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void SetCommand(string[] args)
        {
            if (args.Length == 1)
            {
                HelpSet();
                return;
            }
            else if (args.Length > 2)
            {
                Console.WriteLine("Too many arguments.");
                HelpSet();
                return;
            }

            KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
            if (!VerifyParameters(parameters, "buffer", "debug"))
            {
                Console.WriteLine("Invalid parameter.");
                return;
            }

            if (parameters.ContainsKey("buffer"))
            {
                int bufferSize = (int)FormattingHelper.ParseStandardSizeString(parameters.ValueOf("buffer"));
                if (bufferSize > 0)
                {
                    int transferSizeLBA = bufferSize / 512; // we assume 512 bytes per sector
                    Settings.MaximumTransferSizeLBA = transferSizeLBA;
                    Console.WriteLine("Buffer size has been set to {0} Blocks", transferSizeLBA);
                }
            }

            if (parameters.ContainsKey("debug"))
            {
                m_debug = true;
            }
        }
        
        public static void HelpSet()
        {
            Console.WriteLine();
            Console.WriteLine("Syntax: SET BUFFER=<N>");
            Console.WriteLine();
            Console.WriteLine("    BUFFER=<N>  The size of the copy buffer in bytes.");
            Console.WriteLine("                Default: 131072 sectors.");
            Console.WriteLine();
            Console.WriteLine("    Example:");
            Console.WriteLine("    --------");
            Console.WriteLine("    SET BUFFER=128MB");
        }
    }
}
