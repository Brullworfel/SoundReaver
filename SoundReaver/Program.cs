using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SoundReaver
{
    class Program
    {
        static string inputFileName;
        static byte[] inputBytes;
        static int soundsCount;

        private static string ComputeHash(byte[] bytes)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            try
            {
                byte[] result = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < result.Length; i++)
                {
                    sb.Append(result[i].ToString("X2"));
                }
                return sb.ToString();
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("Hash has not been generated.");
                return null;
            }
        }


        static bool ByteArrayToFile(string fileName, byte[] byteArray)
        {
            return ByteArrayToFile(fileName, byteArray, 0, byteArray.Length);
        }

        static bool ByteArrayToFile(string fileName, byte[] byteArray, int offset, int length)
        {
            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(byteArray, offset, length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                return false;
            }
        }

        static void loadSMF(string fileName)
        {
            Console.WriteLine("\nLoading file: {0}...", Path.GetFileNameWithoutExtension(fileName));

            inputBytes = File.ReadAllBytes(fileName);

            inputFileName = fileName;
            soundsCount = BitConverter.ToInt16(inputBytes, 8);

            Console.WriteLine(" {0} sounds:", soundsCount);
        }

        static bool consoleQuestion(string question)
        {
            ConsoleKey response;
            do
            {
                Console.Write(question + " [y/n]: ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                    Console.WriteLine();
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);
            return response == ConsoleKey.Y;
        }

        static void Main(string[] args)
        {

            if (!File.Exists("sox.exe"))
            {
                Console.WriteLine("SoundReaver uses SoX - Sound eXchange to convert RAW data to WAV. "+
                    "Please, download latest release for Windows here:\nhttps://sourceforge.net/projects/sox/files/sox/\n"+
                    "and place sox.exe and all *.dll files which come with it to SoundReaver folder, then run it again." +
                    "\n\nPress any key to exit now..");
                Console.ReadKey();
                Environment.Exit(0);
            }

            string dir = AppDomain.CurrentDomain.BaseDirectory + "\\input";
            DirectoryInfo d = new DirectoryInfo(dir);
            FileInfo[] Files = d.GetFiles("*.smf");

            if (Files.Length < 1)
            {
                Console.WriteLine("SoundReaver is tool for extracting sound effects from PC-version of Soul Reaver 2 game. " +
                    "\nPlace *.smf files to \"input\" folder and run it again."+
                    "\n*.smf files are located in \"pcenglish\" folder wich you can extract from bigfile.dat with tool \"Soul Spiral\":" +
                    "\nhttps://www.thelostworlds.net/Software/Soul_Spiral.html" +
                    "\n\nPress any key to exit now..");
                Console.ReadKey();
                Environment.Exit(0);
            }


            Console.WriteLine("Most of the sounds are repeatedly duplicated in different files of one game.");
            bool onlyUnique = consoleQuestion("Would you like to skip the duplicates?");

            bool groupIntoFolders = consoleQuestion("\nGroup the sounds into folders according to the original file name?");

            System.IO.Directory.CreateDirectory("input");
            System.IO.Directory.CreateDirectory("temp");
            System.IO.Directory.CreateDirectory("output");

            Dictionary<string, string> Hashes = new Dictionary<string, string>();

            int processedCount = 0;

            foreach (FileInfo file in Files)
            {
                processedCount++;
                loadSMF(file.FullName);
                string smfName = Path.GetFileNameWithoutExtension(file.Name);

                if(groupIntoFolders)
                    System.IO.Directory.CreateDirectory("output\\" + smfName);

                int pos = 0x10,
                    soundID,
                    soundSampleRate,
                    soundLength,
                    soundStart;

                string soundFileName;

                for(int i = 0; i < soundsCount; i++)
                {
                    soundID = BitConverter.ToInt16(inputBytes, pos);
                    soundLength = BitConverter.ToInt32(inputBytes, pos + 0x10);
                    soundSampleRate = BitConverter.ToInt32(inputBytes, pos + 0x14);
                    soundFileName = smfName + "_" + soundID;

                    soundStart = pos + 0x16;
                    pos = soundStart + soundLength - 2; //2 bytes is used for SampleRate

                    byte[] soundBytes = new byte[soundLength - 2];
                    Array.Copy(inputBytes, soundStart, soundBytes, 0, soundLength - 2);

                    if (onlyUnique) //save only unique sounds
                    {
                        string hash = ComputeHash(soundBytes);
                        if (Hashes.ContainsKey(hash))
                        {
                            Console.WriteLine("ID: {0}\tDUPLICATE OF {1} - skipped",
                            soundID, Hashes[hash]);
                            continue;
                        }
                        Hashes.Add(hash, soundFileName);
                    }

                    string outputFileName = groupIntoFolders
                        ? String.Format("output\\{0}\\{1}.wav", smfName, soundFileName)
                        : String.Format("output\\{0}.wav", soundFileName);

                    ByteArrayToFile("temp\\" + soundFileName + ".raw", soundBytes);

                    Console.WriteLine("ID: {0}\tRate: {1}\tLength: {2}\tStart: {3}\tEnd: {4}",
                        soundID, soundSampleRate, soundLength, soundStart, pos);

                    string cmd = String.Format("-r {0} -e signed -b 16 -c 1 -L -t raw temp\\{1}.raw {2}",
                        soundSampleRate, soundFileName, outputFileName);
                    Process.Start("sox.exe", cmd).WaitForExit();
                }

            }

            Directory.Delete("temp", true);

            Console.WriteLine("\nFiles processed: {0}.\nPress any key to exit..", processedCount);
            Console.ReadKey();

        }
    }
}
