/*Index Class
 * ----------
 * This class creates and stores the file index of the client.
 * ----------
 * Raghav Sethi | raghav09035@iiitd.ac.in
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections;

namespace eventuali
{
    static class Index
    {
        public static List<string[]> files;
        public static Hashtable filehashes;

        public static int getNumFiles()
        {
            return files.Count;
        }

        public static string Md5SumByProcess(string file)
        {
            var p = new Process();
            //TODO Find relative instead of absolute path for md5sum.exe
            p.StartInfo.FileName = Application.StartupPath + "\\md5sums.exe";
            p.StartInfo.Arguments = " -e -u \"" + file + "\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
            string output = p.StandardOutput.ReadToEnd();
            try
            {
                return output.Split(' ')[0].Substring(1).ToUpper();
            }
            catch (ArgumentOutOfRangeException e)
            {
                return "D5SUMS";
            }
        }

        public static string[] getFileInfo(string filePath)
        {
            //TODO Return metadata in the future.
            //Returns file type, file name, and folder name

            char[] delimiterChars = { '\\', '.' };
            string[] fileInfo = new string[7];
            string[] temp = filePath.Split(delimiterChars);

            fileInfo[0] = Md5SumByProcess(filePath);
            fileInfo[1] = temp[temp.Length - 2]; //File name
            fileInfo[2] = new FileInfo(filePath).Length.ToString();
            fileInfo[3] = temp[temp.Length - 3]; //Folder name
            fileInfo[4] = "EMPTY"; //Find a use later
            fileInfo[5] = "EMPTY"; //Find a use later
            fileInfo[6] = temp[temp.Length - 1]; //File extension

            return fileInfo;
        }

        //Updates index and data structures
        public static void updateSharedIndex(CheckedListBox.ObjectCollection sharedFoldersList, bool recursiveCheck)
        {
            files = new List<string[]>();
            //TODO Optimize Hashtable constructor for efficiency.
            filehashes = new Hashtable();
            Configuration.writeLog("updateSharedIndex : Started indexing...");
            int numFoldersIndexed = 0;
            //In case of flat indexing.
            if (recursiveCheck == false)
            {
                foreach (string sharedFolderPath in sharedFoldersList)
                {
                    DirectoryInfo di = new DirectoryInfo(sharedFolderPath);
                    FileInfo[] rgFiles = di.GetFiles();
                    foreach (FileInfo fi in rgFiles)
                    {
                        string[] temp = getFileInfo(fi.FullName);
                        if (temp[0].StartsWith("D5SUM"))
                            continue;
                        files.Add(temp);
                        filehashes.Add(temp[0], fi.FullName);
                    }
                }
            }
            //In case of recursive indexing.
            else
            {
                List<string> folders = new List<string>();
                foreach (string sharedFolderPath in sharedFoldersList)
                {
                    folders.Add(sharedFolderPath);
                }
                while (folders.Count > 0)
                {
                    DirectoryInfo di = new DirectoryInfo(folders[0]);
                    FileInfo[] rgFiles = null;
                    DirectoryInfo[] recDirectories = null;
                    try
                    {
                        rgFiles = di.GetFiles();
                        recDirectories = di.GetDirectories();
                    }
                    catch (UnauthorizedAccessException u)
                    {
                        Configuration.writeLog("ERROR! updateSharedIndex: UnauthorizedAccessException while indexing: " + di.FullName);
                    }

                    if (recDirectories != null)
                    {
                        foreach (DirectoryInfo recDirectory in recDirectories)
                        {
                            folders.Add(recDirectory.FullName);
                        }
                    }

                    if (rgFiles != null)
                    {
                        //Determines whether duplicate file hash errors are written to the log.
                        bool logDuplicateHashes = false;

                        foreach (FileInfo fi in rgFiles)
                        {
                            string[] temp = getFileInfo(fi.FullName);
                            if (temp[0].StartsWith("D5SUM"))
                                continue;
                            files.Add(temp);
                            try
                            {
                                filehashes.Add(temp[0], fi.FullName);
                            }
                            catch (ArgumentException e)
                            {
                                if(logDuplicateHashes)
                                    Configuration.writeLog("Files have the same MD5 hash, the second file has not been indexed. Files are:" +  filehashes[temp[4]] + " and " + fi.FullName + ". HASH: " + temp[4]);
                            }

                        }
                    }
                    folders.Remove(folders[0]);
                    numFoldersIndexed++;
                }
            }
            Configuration.numFilesShared = files.Count;
            saveHashes();
            Configuration.writeLog("updateSharedIndex : Completed indexing.");
        }

        //Saves hashes.dat and indexes.dat to disk.
        public static void saveHashes()
        {

            TextWriter tw = new StreamWriter("hashes.dat", false);
            foreach (string hash in filehashes.Keys)
            {
                tw.WriteLine("{0}|{1}", hash, filehashes[hash]);
            }
            tw.Close();

            tw = new StreamWriter("index.dat", false);
            foreach (string[] fileinfo in files)
            {
                tw.WriteLine("{0}|{1}|{2}|{3}|{4}|{5}|{6}", fileinfo[0], fileinfo[1], fileinfo[2], fileinfo[3], fileinfo[4], fileinfo[5], fileinfo[6]);
            }
            tw.Close();

            //Saves the hash of the index file to the configuration
            Configuration.indexHash = Md5SumByProcess("index.dat");
            //MessageBox.Show("Saved hash" + Configuration.indexHash);
        }

        //Loads hashes.dat and indexes.dat to memory.
        public static void loadHashes()
        {
            //Loading filehashes from hashes.dat
            
            filehashes = new Hashtable();
            string currentLine = " ";
            try
            {
                TextReader tr = new StreamReader("hashes.dat", true);
                int i = 0;
                currentLine = tr.ReadLine();
                while (currentLine != null)
                {
                    i++;
                    string[] hashdetails = currentLine.Split('|');
                    filehashes[hashdetails[0]] = hashdetails[1];
                    currentLine = tr.ReadLine();
                }
                tr.Close();
            }
            catch (Exception e)
            {
                Configuration.writeLog("ERROR! loadHashes : Unable to load hashes.dat : " + currentLine +  e.ToString());
                return;
            }

            //Loading 'files' from index.dat

            files = new List<string[]>();
            currentLine = " ";
            try
            {
                TextReader tr = new StreamReader("index.dat", true);
                int i = 0;
                currentLine = tr.ReadLine();
                while (currentLine != null)
                {
                    i++;
                    string[] filedetails = currentLine.Split('|');
                    files.Add(filedetails);
                    currentLine = tr.ReadLine();
                }
                tr.Close();
            }
            catch (Exception e)
            {
                Configuration.writeLog("ERROR! loadHashes : Unable to load undex.dat : " + currentLine + e.ToString());
                return;
            }
        }
    }
}
