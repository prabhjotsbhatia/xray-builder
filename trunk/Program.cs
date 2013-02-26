/*   Builds an X-Ray file to be used on the Amazon Kindle
 *   Original xray builder by shinew, http://www.mobileread.com/forums/showthread.php?t=157770 , http://www.xunwang.me/xray/
 *
 *   Copyright (C) 2013 Ephemerality <Nick Niemi - ephemeral.vilification@gmail.com>
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.

 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.

 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class Program
    {
        static void Exit(string err, bool wait = true)
        {
            Console.Error.WriteLine(err);
            if (wait)
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
            Environment.Exit(1);
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: xraybuilder [-m path] [-o path] [-r] [-s shelfariURL] [--spoilers] [-u path] mobiPath\n" +
                "-m path (--mobi2mobi)\tPath must point to mobi2mobi.exe\n\t\t\tIf not specified, searches in the current directory\n" +
                "-o path (--outdir)\tPath defines the output directory\n\t\t\tIf not specified, uses ./out\n" +
                "-r (--saveraw)\t\tSave raw book markup to the output directory\n" +
                "-s (--shelfari)\t\tShelfari URL\n\t\t\tIf not specified, there will be a prompt asking for it\n" +
                "--spoilers\t\tUse descriptions that contain spoilers\n\t\t\tDefault behaviour is to use spoiler-free descriptions.\n" +
                "-u path (--unpack)\tPath must point to mobi_unpack.py\n\t\t\tIf not specified, searches in the current directory\n\n" +
                "After used once, mobi2mobi and mobi_unpack paths will be saved as default and are not necessary to include every time.\n" +
                "You can also drag and drop a number of mobi files onto the exe after those paths have been saved.");
            Exit("", true);
        }

        static void Main(string[] args)
        {
            string mobi_unpack = "";
            string mobi2mobi = "";
            string shelfariURL = "";
            string outDir = "";
            bool saveRaw = true;
            bool spoilers = false;
            List<string> fileList = new List<string>();

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-m" || args[i] == "--mobi2mobi")
                    {
                        mobi2mobi = args[++i];
                        if (!File.Exists(mobi2mobi)) Exit("Specified mobi2mobi.exe not found.");
                    }
                    else if (args[i] == "-o" || args[i] == "--outdir")
                        outDir = args[++i];
                    else if (args[i] == "-r" || args[i] == "--saveraw")
                        saveRaw = true;
                    else if (args[i] == "-s" || args[i] == "--shelfari")
                        shelfariURL = args[++i];
                    else if (args[i] == "--spoilers")
                        spoilers = true;
                    else if (args[i] == "-u" || args[i] == "--unpack")
                    {
                        mobi_unpack = args[++i];
                        if (!File.Exists(mobi_unpack)) Exit("Specified mobi_unpack.py script not found.");
                    }
                    else if (File.Exists(args[i]))
                        fileList.Add(args[i]);
                    else if (args[0] == "--usage" || args[0] == "--help" || args[0] == "-?")
                        ShowUsage();
                    else
                        Console.Error.WriteLine("Unknown arg {0}: {1}. Continuing anyway...", i, args[i]);
                }

                //if (fileList.Count == 0)
                //    Exit("No mobi files specified.");
                //else if (shelfariURL == "")
                //    Exit("Shelfari URL not found.");

                if (outDir != "" && !Directory.Exists(outDir))
                    Exit("Out directory does not exist. (" + outDir + ")");
                else if (outDir == "")
                {
                    outDir = Environment.CurrentDirectory + "\\out";
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                }
            }
            else
                ShowUsage();

            Console.WriteLine("Using {0} as an output directory.", outDir);

            if (mobi2mobi != "" && mobi2mobi != XRayBuilder.Properties.Settings.Default.mobi2mobi)
            {
                XRayBuilder.Properties.Settings.Default.mobi2mobi = mobi2mobi;
                XRayBuilder.Properties.Settings.Default.Save();
                Console.WriteLine("Saving mobi2mobi directory as default. If not specified in the future, this one will be used.");
            }
            else if (mobi2mobi == "" && XRayBuilder.Properties.Settings.Default.mobi2mobi != "")
            {
                mobi2mobi = XRayBuilder.Properties.Settings.Default.mobi2mobi;
                Console.WriteLine("Using saved mobi2mobi path ({0}).", mobi2mobi);
            }
            else if (mobi2mobi == "") mobi2mobi = "mobi2mobi.exe";
            if(!File.Exists(mobi2mobi))
                Exit("Mobi2mobi not found.");

            if (mobi_unpack != "" && mobi_unpack != XRayBuilder.Properties.Settings.Default.mobi_unpack)
            {
                XRayBuilder.Properties.Settings.Default.mobi_unpack = mobi_unpack;
                XRayBuilder.Properties.Settings.Default.Save();
                Console.WriteLine("Saving mobi_unpack directory as default. If not specified in the future, this one will be used.");
            }
            else if (mobi_unpack == "" && XRayBuilder.Properties.Settings.Default.mobi_unpack != "")
            {
                mobi_unpack = XRayBuilder.Properties.Settings.Default.mobi_unpack;
                Console.WriteLine("Using saved mobi_unpack path ({0}).", mobi_unpack);
            }
            else if (mobi_unpack == "") mobi_unpack = "mobi_unpack.py";
            if(!File.Exists(mobi_unpack))
                Exit("Mobi_unpack not found.");

            foreach (string mobiFile in fileList)
            {
                Console.WriteLine("Processing {0}...", Path.GetFileName(mobiFile));
                if (shelfariURL == "")
                {
                    Console.WriteLine("Enter Shelfari URL for {0}: ", Path.GetFileNameWithoutExtension(mobiFile));
                    shelfariURL = Console.ReadLine().Trim();
                    if (shelfariURL == "") continue;
                }
                Console.WriteLine("Running mobi_unpack to get book data...");
                //Create a temp folder and use mobi_unpack from command line to unpack mobi file to that folder
                ProcessStartInfo startInfo = new ProcessStartInfo();
                string randomFile = GetTempDirectory();
                startInfo.FileName = "python";
                startInfo.Arguments = mobi_unpack + " -r -d \"" + mobiFile + @""" """ + randomFile + @"""";
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.UseShellExecute = false;
                string unpackInfo = "";
                using (Process process = Process.Start(startInfo))
                {
                    process.BeginErrorReadLine();
                    using (StreamReader reader1 = process.StandardOutput)
                    {
                        unpackInfo = reader1.ReadToEnd();
                        //Console.WriteLine(unpackInfo);
                    }
                }
                //Was the unpack successful?
                if (!unpackInfo.Contains("Write opf\r\nCompleted"))
                    Exit("Error unpacking mobi file: " + unpackInfo);
                Console.WriteLine("Mobi unpacked. Searching for .rawml...");
                //Attempt to find the .rawml unpacked from the mobi
                string rawML = randomFile + @"\mobi7\" + Path.GetFileNameWithoutExtension(mobiFile) + ".rawml";
                if (!File.Exists(rawML))
                    Exit("Error finding unpacked rawml file. Path: " + rawML);
                Console.WriteLine("RawML found at {0}. Running mobi2mobi to grab metadata...", rawML);
                if (saveRaw)
                {
                    Console.WriteLine("Saving rawML to output directory.");
                    File.Copy(rawML, Path.Combine(outDir, Path.GetFileName(rawML)), true);
                }
                //Run mobi2mobi to get db name, uniqid, and asin
                startInfo.FileName = mobi2mobi;
                startInfo.Arguments = @"""" + mobiFile + @"""";
                string[] mobiInfo;
                using (Process process = Process.Start(startInfo))
                {
                    using (StreamReader reader1 = process.StandardOutput)
                    {
                        string temp = reader1.ReadToEnd();
                        mobiInfo = temp.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                        //Console.WriteLine(temp);
                    }
                }
                string database = "";
                string uniqid = "";
                string asin = "";
                foreach (string s in mobiInfo)
                {
                    if (s.Contains("Database Name"))
                        database = s.Substring(15);
                    else if (s.Contains("MOBIHEADER   uniqid"))
                        uniqid = s.Substring(21);
                    else if (s.Contains("EXTH    item: 113 - ASIN - "))
                    {
                        asin = s.Substring(32);
                        break;
                    }
                }
                if (database == "" || uniqid == "" || asin == "")
                    Exit("Error getting metadata from mobi2mobi. Output: " + mobiInfo);

                Console.WriteLine("Got metadata! Attempting to build X-Ray...");

                //Create X-Ray and attempt to create the base file (essentially the same as the site)
                XRay ss = new XRay(shelfariURL, database, uniqid, asin, spoilers);
                if (ss.createXRAY() > 0)
                {
                    Console.WriteLine("Error while processing. Skipping to next file.");
                    continue;
                }

                Console.WriteLine("Initial X-Ray built, adding locs and chapters...");
                //Expand the X-Ray file from the unpacked mobi
                ss.expandFromRawML(rawML);

                using (StreamWriter streamWriter = new StreamWriter(outDir + "\\" + ss.getXRayName(), false, Encoding.Default))
                {
                    streamWriter.Write(ss.ToString());
                }
                Console.Error.WriteLine("XRay file created successfully!\nSaved to " + outDir + "\\" + ss.getXRayName());
                Console.WriteLine("*****************************************************");
                Directory.Delete(randomFile, true);
                shelfariURL = "";
            }
            Console.WriteLine("All files processed! Press Enter to exit.");
            Console.ReadLine();
        }

        public static string GetTempDirectory()
        {
            string path;
            do
            {
                path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()); 
            } while (Directory.Exists(path));
            Directory.CreateDirectory(path); 
            return path; 
        }
    }
}
