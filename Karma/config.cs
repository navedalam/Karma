/*Config Class
 * -----------
 * This class stores global variables and manages client settings and configuration - including 
 * storing and retrieving them from disk. Also manages the log.
 * -----------
 * Raghav Sethi | raghav09035@iiitd.ac.in
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;

static class Configuration
    {
        private static string m_username = "";
        private static long m_numFilesShared = 0;
        private static List<string> m_sharedFolders = null;
        private static List<string> m_servers = null;
        private static string m_recursiveCheck = "RECURSIVE";
        private static int m_GBShared = 5;
        private static string m_downloadFolder = "";
        private static System.Object logLock = new System.Object();
        private static string m_indexHash = "";

        public static string username
        {
            get { return m_username; }
            set { m_username = value; }
        }

        public static string downloadFolder
        {
            get { return m_downloadFolder; }
            set { m_downloadFolder = value; }
        }

        public static int GBShared
        {
            get { return m_GBShared; }
            set { m_GBShared = value; }
        }

        public static string recursiveCheck
        {
            get { return m_recursiveCheck; }
            set { m_recursiveCheck = value; }
        }

        public static long numFilesShared
        {
            get { return m_numFilesShared; }
            set { m_numFilesShared = value; }
        }

        public static List<string> sharedFolders
        {
            get { return m_sharedFolders; }
            set { m_sharedFolders = value; }
        }

        public static List<string> servers
        {
            get { return m_servers; }
            set { m_servers = value; }
        }

        public static string indexHash
        {
            get { return m_indexHash; }
            set { m_indexHash = value; }
        }

        public static bool loadConfiguration()
        {
            TextReader tr = new StreamReader("config.dat");
            m_sharedFolders = new List<string>();
            m_servers = new List<string>();
            string currentLine;

            if ((m_username = tr.ReadLine()) == null)
            {
                MessageBox.Show("Welcome to Karma.\n\nUsing Karma is completely legal. However, it may be used for illegal purposes. Downloading copyrighted music, videos, or programs without the permission of the author is illegal. This is called copyright infringement or piracy. Karma (and its devs) do not condone the use of Karma for the purposes of copyright infringement.\n\nEnd of legal mumbo-jumbo.\nGo to the settings tab and perform basic setup to start enjoying Karma!", "Welcome to Karma", MessageBoxButtons.OK, MessageBoxIcon.Information);
                tr.Close();
                return false;
            }
            
            m_numFilesShared=Convert.ToInt64(tr.ReadLine());
            m_recursiveCheck = tr.ReadLine();
            m_GBShared = Convert.ToInt32(tr.ReadLine());
            m_downloadFolder = tr.ReadLine();
            m_indexHash = tr.ReadLine();

            //Reading list of shared folders.
            while ((currentLine = tr.ReadLine()) != "servers:")
            {
                m_sharedFolders.Add(currentLine);
            }
            while ((currentLine = tr.ReadLine()) != null)
            {
                m_servers.Add(currentLine);
            }
            tr.Close();
            return true;
        }

        public static void saveConfiguration()
        {
            TextWriter tw = new StreamWriter("config.dat", false);
            tw.WriteLine(m_username);
            tw.WriteLine(m_numFilesShared);
            tw.WriteLine(m_recursiveCheck);
            tw.WriteLine(m_GBShared);
            tw.WriteLine(m_downloadFolder);
            tw.WriteLine(m_indexHash);
            
            foreach (string sharedFolder in m_sharedFolders)
            {
                tw.WriteLine(sharedFolder);
            }
            tw.WriteLine("servers:");
            foreach (string server in m_servers)
            {
                tw.WriteLine(server);
            }
            tw.Close();
        }

        public static void writeLog(string message)
        {
            lock(logLock)
            {
                TextWriter tw;
                try
                {
                    tw = new StreamWriter("log.txt", true);
                }
                catch (IOException e)
                {
                    MessageBox.Show("Unhandleable IOException occurred.\nCould not write event details to log file.\nYou should be able to continue normally.");
                    return;
                }
                //tw.WriteLine("{0} : {1}", DateTime.Now.ToString("dd/MM/yyyy h:MM:ss tt"), message);
                tw.WriteLine("{0} : {1}", DateTime.Now.ToLongTimeString(), message);
                tw.Close();
            }
        }

        public static void clearLog()
        {
            TextWriter tw;
            tw = new StreamWriter("log.txt", false);
            tw.Write("");
            tw.Close();
        }

 }
