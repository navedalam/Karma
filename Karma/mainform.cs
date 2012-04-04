using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Threading;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net;
using System.Diagnostics;

namespace eventuali
{
    public partial class filemanager : Form
    {
        public TcpClient tcpClient;
        public NetworkStream clientStream;
        public NetworkStream serverStream;
        public string clientIP;
        public string clientFileHash;
        private TcpListener tcpListener;
        private Thread listenThread;
        private Hashtable currentUploadsHashTable; //To manage indexes in uploadListView
        private Hashtable currentDownloadsHashTable; //To manage indexes in downloadListView
        private Hashtable currentDownloadsDetails; //To store information about current downloads
        private Hashtable currentSearchResultsHashTable;
        private int currentlyConnectedServer; //Index of currently connected/connecting server
        private int searchReady=1; //0 - ready, 1 - not connected, 2 - syncing, 3 - processing another query
        private Thread serverCommThread;
        private TcpClient tcpclnt;

        //private Hashtable Transfers.userDownloadState; //To control/cancel/pause downloads.

        //Removes the blinking effect on changing tabs.
        protected override CreateParams CreateParams
        {
            //Enables double buffering to reduce tab flicker.
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        public filemanager()
        {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(this.filemanager_FormClosing);
        }

        private void addFolderButton_Click(object sender, EventArgs e)
        {
            folderBrowser.ShowDialog();
            sharedFolders.Items.Add(folderBrowser.SelectedPath);
            Configuration.sharedFolders.Add(folderBrowser.SelectedPath);
        }

        private void updateListButton_Click(object sender, EventArgs e)
        {
            updateProgressBar.Value = 50;
            updateListButton.Enabled = false;
            updateStatus.Text = "Indexing...";
            sharingStatus.Text = "Recreating index..";
            Configuration.writeLog("Indexing started.");
            indexer.RunWorkerAsync();
        }

        private void deleteSelectedButton_Click(object sender, EventArgs e)
        {
            updateStatus.Text = "File list out of date. Click 'Update List' to refresh.";
            updateProgressBar.Value = 0;
            List<int> checkedItems = new List<int>();
            foreach (int deletedIndex in sharedFolders.SelectedIndices)
            {
                checkedItems.Add(deletedIndex);
            }
            foreach (int deletedIndex in checkedItems)
            {
                sharedFolders.Items.RemoveAt(deletedIndex);
                Configuration.sharedFolders.RemoveAt(deletedIndex);
            }
        }

        private void addURLButton_Click(object sender, EventArgs e)
        {
            if (serverIPTextBox.Text != "")
            {
                //TODO Can add more validation here.
                serversList.Items.Add(serverIPTextBox.Text);
                Configuration.servers.Add(serverIPTextBox.Text);
            }
        }

        private void deleteURLButton_Click(object sender, EventArgs e)
        {
            List<int> checkedItems = new List<int>();
            foreach (int deletedIndex in serversList.SelectedIndices)
            {
                checkedItems.Add(deletedIndex);
            }
            foreach (int deletedIndex in checkedItems)
            {
                serversList.Items.RemoveAt(deletedIndex);
                Configuration.servers.RemoveAt(deletedIndex);
            }
        }

        //Reads and loads configuration file, initializes data structures and starts threads.
        private void filemanager_Load(object sender, EventArgs e)
        {
            if (Configuration.loadConfiguration() == true)
            {
                usernameTextBox.Text = Configuration.username;
                sharingStatus.Text = "Sharing " + Configuration.numFilesShared + " files.";
                recursiveCheck.Checked = (Configuration.recursiveCheck == "RECURSIVE");
                GBShared.Value = Configuration.GBShared;
                downloadFolder.Text = Configuration.downloadFolder;

                foreach (string server in Configuration.servers)
                {
                    serversList.Items.Add(server);
                }
                foreach (string sharedFolder in Configuration.sharedFolders)
                {
                    sharedFolders.Items.Add(sharedFolder);
                }
            }
            updateStatus.Text = "Ready.";

            Configuration.clearLog();
            Configuration.writeLog("Application started.");
            
            //Loading file details into memory.
            Index.loadHashes();

            currentUploadsHashTable = new Hashtable();
            currentDownloadsHashTable = new Hashtable();
            Transfers.userDownloadState = new Hashtable();
            currentDownloadsDetails = new Hashtable();

            if (Configuration.servers.Count == 0)
            {
                connectionStatus.Text = "No servers specified.";
                return;
            }

            currentlyConnectedServer = 0;
            
            connectionStatus.Text = "Starting communication thread.";

            Transfers.passiveWorker = passiveWorker;
            Transfers.activeWorker = activeWorker;

            passiveWorker.RunWorkerAsync();
            Thread.Sleep(1000);
            activeWorker.RunWorkerAsync();

            //Starting the timeout till which sync must complete.
            //timeoutTimer.Start();
        }

        private void filemanager_FormClosing(object sender, EventArgs e)
        {
            Configuration.saveConfiguration();
            serverStream.Close();
            tcpclnt.Close();
            Application.Exit();
        }

        private void usernameTextBox_TextChanged(object sender, EventArgs e)
        {
            Configuration.username = usernameTextBox.Text;
        }

        private void recursiveCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (recursiveCheck.Checked)
            {
                Configuration.recursiveCheck = "RECURSIVE";
            }
            else
            {
                Configuration.recursiveCheck = "FLAT";
            }
            updateStatus.Text = "File list out of date. Click 'Update List' to refresh.";
        }

        private void serverIPTextBox_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                addURLButton_Click(sender, e);
            }
        }

        private void GBShared_ValueChanged(object sender, EventArgs e)
        {
            Configuration.GBShared = (int)GBShared.Value;
        }

        //TODO: Remove this function.
        public void temp()
        {
            //Transfers.beginDownloadFromIP("192.168.4.132", "85D4A92DB1F9542CDAD8F04501A0000", "debugger");
            while (true)
            {
                Thread.Sleep(10000);
            }
            //Transfers.beginDownloadFromIP("192.168.4.132", "57DFF70933D6F8EE37A4531634123B6","debugger");
            //Transfers.beginDownloadFromIP("192.168.5.238", "9A79D5F21F2222227DD66E2EEF0F274","debugger2");
        }

        public void talkToServer()
        {
            Thread.Sleep(1500);
            //Transfers.beginDownloadFromIP("192.168.5.238", "B114EBC4048F804CFFBF32DF62A4379");
            //Transfers.beginDownloadFromIP("127.0.0.1", "796B77219E951479C0388BA6EFDE2AF");
            
            if (Configuration.servers.Count == 0)
            {
                connectionStatus.Text = "No server specified.";
                return;
            }

            string currentserver = Configuration.servers[currentlyConnectedServer];
            
            string ident = GetMACAddress();
            string str;
            try
            {
                tcpclnt = new TcpClient();
                activeWorker.ReportProgress(1);

                //Thread.Sleep(1000);
                tcpclnt.Connect(currentserver, 8026);
                Configuration.writeLog("talkToServer : Connected to server " + currentserver);
                activeWorker.ReportProgress(2);
                serverStream = tcpclnt.GetStream();
                //Thread.Sleep(1000);
                activeWorker.ReportProgress(3);
                str = "OK|" + usernameTextBox.Text + "|" + ident + "|" + Configuration.indexHash;

                //MessageBox.Show(str);
                Transfers.sendMessage(str, serverStream);
                Configuration.writeLog("talkToServer : Sent user details.");
                /*
                string lastIndexFileHash = Transfers.receiveMessage(serverStream);

                timeoutTimer.Enabled = false;
                Configuration.writeLog("Timeout canceled.");
                */
                //if (lastIndexFileHash != Configuration.indexHash)
                //{
                    Transfers.uploadIndexFile(serverStream);
                //}
                /*
                while (true)
                {
                    MessageBox.Show(Transfers.receiveMessage(serverStream));
                }
                */
                //tcpclnt.Close();
                activeWorker.ReportProgress(4);
            }
            //If the thread is aborted due to timeout/UI
            //TODO - do more garbage collection here
            catch (ThreadAbortException abortException)
            {
                Configuration.writeLog("talkToServer : Thread aborted : " + (string)abortException.ExceptionState);
                serverStream.Close();
                return;
            }
            catch (Exception e)
            {
                activeWorker.ReportProgress(255);
                Configuration.writeLog("ERROR! talkToServer module : " + e.Message);
                Configuration.writeLog("talkToServer : Retrying...");
                Thread.Sleep(500);
                //Trying the next available server or retrying the current server (dependent on num. of servers)
                currentlyConnectedServer = (currentlyConnectedServer + 1) % Configuration.servers.Count();
                talkToServer();
            }
        }

        public string GetMACAddress()
        {
            string macAddresses = "";
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses += nic.GetPhysicalAddress().ToString();
                    //break;
                }
            }
            return macAddresses;
        }

        private void indexer_DoWork(object sender, DoWorkEventArgs e)
        {
            // Do not access the form's BackgroundWorker reference directly.
            // Instead, use the reference provided by the sender parameter.
            BackgroundWorker bw = sender as BackgroundWorker;

            // Extract the argument.
            //int arg = (int)e.Argument;
            // Start the time-consuming operation.
            Index.updateSharedIndex(sharedFolders.Items, recursiveCheck.Checked);
            
            // If the operation was canceled by the user, 
            // set the DoWorkEventArgs.Cancel property to true.
            
            if (bw.CancellationPending)
            {
                e.Cancel = true;
            }
            
        }

        private void indexer_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                // The user canceled the operation.
                MessageBox.Show("Operation was canceled");
            }
            else if (e.Error != null)
            {
                // There was an error during the operation.
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
            else
            {
                // The operation completed normally.
            }
            Configuration.numFilesShared = Index.getNumFiles();
            Configuration.writeLog("Indexing completed.");
            updateStatus.Text = "Indexing complete. Click on Sync List to sync your filelist to the server.";
            updateProgressBar.Value = 100;
            sharingStatus.Text = "Sharing " + Configuration.numFilesShared.ToString() + " files.";
            updateListButton.Enabled = true;
        }

        private void changeDownloadFolderButton_Click(object sender, EventArgs e)
        {
            folderBrowser.ShowDialog();
            downloadFolder.Text=folderBrowser.SelectedPath;
            Configuration.downloadFolder=folderBrowser.SelectedPath;
        }

        private void passiveWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            TransferDetail update = (TransferDetail)e.UserState;
            currentDownloadsDetails[update.fileHash] = update;

            if (update.status.StartsWith("Beginning"))
            {
                ListViewItem new_item = new ListViewItem(update.fileName, 0);
                new_item.SubItems.Add(update.fileSizeString);
                new_item.SubItems.Add(update.status);
                new_item.SubItems.Add(update.transferRateString);
                new_item.SubItems.Add(update.percentageCompleteString);
                new_item.SubItems.Add(update.timeRemainingString);
                new_item.SubItems.Add(update.peerName);
                if(update.ttype=='u')
                {
                    uploadListView.Items.Add(new_item);
                    currentUploadsHashTable[update.fileHash] = uploadListView.Items.Count - 1;
                }
                else if(update.ttype=='d')
                {
                    downloadListView.Items.Add(new_item);
                    currentDownloadsHashTable[update.fileHash] = downloadListView.Items.Count - 1;
                }
            }
            else
            {
                ListViewItem mod_item = null;
                int index;
                if(update.ttype=='u')
                {
                    index = (int)currentUploadsHashTable[update.fileHash];
                    if (index >= uploadListView.Items.Count)
                    {
                        Configuration.writeLog("passiveWorker_progressChanged : Index out of bounds error");
                        return;
                    }
                    mod_item = uploadListView.Items[index];
                }
                else
                {
                    index = (int)currentDownloadsHashTable[update.fileHash];
                    mod_item = downloadListView.Items[index];
                }

                if (downloadListView.SelectedIndices.Count > 0)
                {
                    if (downloadListView.SelectedIndices[0] == index)
                    {
                        downloadProgressBar.Value = (int)update.percentageComplete;
                    }
                }
                
                mod_item.SubItems[1].Text = update.fileSizeString;
                mod_item.SubItems[2].Text = update.status;
                mod_item.SubItems[3].Text = update.transferRateString;
                mod_item.SubItems[4].Text = update.percentageCompleteString;
                mod_item.SubItems[5].Text = update.timeRemainingString;
                mod_item.SubItems[6].Text = update.peerName;
            }
        }

        public void passiveWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.tcpListener = new TcpListener(IPAddress.Any, 8002);
            Transfers.encoder = new ASCIIEncoding();
            this.listenThread = new Thread(new ThreadStart(ListenForClients));
            this.listenThread.IsBackground = true;
            this.listenThread.Start();
            Configuration.writeLog("Started passive communication module.");
            this.listenThread.Join();
            //while (true) ;
        }

        public void passiveWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }
   
        private void ListenForClients()
        {
            //Handles incoming instructions from server or peers.
            this.tcpListener.Start();

            while (true)
            {
                //Waits until a client has connected to the server.
                TcpClient client = this.tcpListener.AcceptTcpClient();
                Configuration.writeLog("listenForClients: Peer connected to passive communication module.");
                //Creates a thread to handle the client.
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
        }

        private void HandleClientComm(object recClient)
        {
            TcpClient tcpClient = (TcpClient)recClient;
            tcpClient.NoDelay = true;
            clientStream = tcpClient.GetStream();
            string lastMessageRecd = "";
            Configuration.writeLog("HandleClientComm : Started.");
            try
            {
                //TODO - Add code to check for single transfer to/from a peer.

                //Acknowledge receipt
                lastMessageRecd = Transfers.receiveMessage(clientStream);
                
                Configuration.writeLog("HandleClientComm : Peer sending instructions.");
                //lastMessageRecd = Transfers.receiveMessage(clientStream);
                string[] parts = lastMessageRecd.Split('|');
                
                clientFileHash = parts[1];
                string peerNick = parts[5];
                clientIP = parts[4];
                //CODE|filehash | filename | filesize | peer ip| peernick
                lastMessageRecd = parts[0];

                if (lastMessageRecd == "FRIEND-TRANSFER")
                {
                    Configuration.writeLog("Received instruction to download a file from " + clientIP);
                    Transfers.beginDownloadFromIP(clientIP, clientFileHash, peerNick);
                }
                else if (lastMessageRecd == "DIRECT-TRANSFER")
                {
                    Configuration.writeLog("Received instruction to upload a file to " + tcpClient.Client.RemoteEndPoint.ToString());
                    Transfers.beginDownloadFromIP(clientIP, clientFileHash, peerNick);
                }
                else if (lastMessageRecd == "FILE-REQUEST")
                {
                    Configuration.writeLog("Received instruction to upload a file to " + tcpClient.Client.RemoteEndPoint.ToString());
                    Transfers.upload(clientFileHash, clientStream, peerNick);
                }
                else
                    throw new FormatException("Client did not follow protocol.");

                Configuration.writeLog("Instructions processed.");
                //Instruction receipt completed.
                    
                //TODO - Add flag checking here.
                //Transfers.sendMessage("TRANSFER OK", clientStream);

            }
            catch (ThreadAbortException abortException)
            {
                Configuration.writeLog("talkToServer : Thread aborted : " + (string)abortException.ExceptionState);
                serverStream.Close();
                return;
            }
            catch (Exception e)
            {
                Configuration.writeLog("ERROR! HandleClientComm : " + e.ToString() + " : " + e.Message);
            }
            finally
            {
                tcpClient.Close();
            }
        }

        private void openFileLocationButton_Click(object sender, EventArgs e)
        {
            //MessageBox.Show(Configuration.downloadFolder + "\\" + downloadListView.SelectedItems[0].Text

            if (downloadListView.SelectedItems.Count != 0)
            {
                if (downloadListView.SelectedItems[0].SubItems[2].Text.StartsWith("Downloaded"))
                {
                    Process.Start("explorer.exe", @"/select," + Configuration.downloadFolder + "\\" + downloadListView.SelectedItems[0].Text);
                }
            }
        }

        private void cancelDownloadButton_Click(object sender, EventArgs e)
        {
            foreach(string i in currentDownloadsHashTable.Keys)
            {
                if ((int)currentDownloadsHashTable[i] == downloadListView.SelectedItems[0].Index)
                {
                    //Set download state to canceled.
                    Transfers.userDownloadState[i] = 0;
                    return;
                }
            }
            return;
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Karma v0.6\nSome rights reserved.\n\nDeveloped at IIIT-D by:\nRaghav Sethi (raghav09035@iiitd.ac.in)\nVarun Gandhi (varun09053@iiitd.ac.in)\nNaved Alam (naved09028@iiitd.ac.in)\n\nDedicated to pinkgirl.", "About Karma", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            if (searchBox.Text == "")
            {
                searchHelpText.Visible = true;
            }
            else
            {
                searchHelpText.Visible = false;
            }
        }

        private void searchHelpText_Click(object sender, EventArgs e)
        {
            searchHelpText.Visible = false;
            searchBox.Focus();

        }

        private void searchTab_Enter(object sender, EventArgs e)
        {
            searchBox.Focus();
        }

        private void searchBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            
            if (e.KeyChar == (char)13 && searchBox.Text!="" && searchReady==0)
            {
                searchProgressBar.Value = 100;
                string searchTerm = searchBox.Text;
                searchBox.Text = "";
                activeWorker.ReportProgress(11, searchTerm);
                Configuration.writeLog("searchBox : Starting file search on server " + searchTerm);
                SearchResult[] currentResults = Transfers.searchServerFileList(searchTerm, serverStream);
                searchResultsGroupBox.Visible = true;
                addResultsToDisplay(currentResults);
                activeWorker.ReportProgress(12);
            }

            if (searchReady == 3)
            {
                searchStatusLabel.Text = "Currently processing your last query. Try again in a second or two.";
            }

            if (searchReady == 2 || searchReady == 1)
            {
                searchStatusLabel.Text = "Not ready.";
            }

        }

        private void activeWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Configuration.writeLog("Active Worker started.");
            serverCommThread = new Thread(new ThreadStart(this.talkToServer));
            serverCommThread.IsBackground = true;
            serverCommThread.Start();
            Thread mcommThread2 = new Thread(new ThreadStart(this.temp));
            mcommThread2.IsBackground = true;
            mcommThread2.Start();
            serverCommThread.Join();
            mcommThread2.Join();
        }

        private void activeWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int status = e.ProgressPercentage;

            if (status == 0)
            {
                string statusUpdate = e.UserState.ToString();
                connectionStatus.Text = statusUpdate;
                return;
            }

            switch (status)
            {
                case 1:
                    connectionStatus.Text = "Attempting to connect.";
                    break;
                case 2:
                    connectionStatus.Text = "Connected succesfully.";
                    waitingStatus.Text = "negotiating connection...";
                    searchReady = 1;
                    break;
                case 3:
                    connectionStatus.Text = "Syncing index with server.";
                    waitingStatus.Text = "syncing with server";
                    searchReady = 2;
                    break;
                case 4:
                    connectionStatus.Text = "Connection established successfully.";
                    waitingStatus.Text = "yippee!\nkarma connected successfully!!";
                    searchStatusLabel.Text = "Ready.";
                    searchResultsGroupBox.Visible = true;
                    searchReady = 0;
                    break;

                //10+ cases handle search state changes.
                
                //Set if another query is being processed.
                case 11:
                    searchReady = 3;
                    searchStatusLabel.Text = "Searching for '" + e.UserState.ToString() + "'";
                    break;

                case 12:
                    searchReady = 0;
                    searchStatusLabel.Text = "Ready.";
                    break;

                case 99:
                    searchReady = 1;
                    searchStatusLabel.Text = "Not ready";
                    connectionStatus.Text = "Connection lost.";
                    waitingStatus.Text = "oh fudge\nwe lost the connection to server! restart karma and try again";
                    searchResultsGroupBox.Visible = false;
                    break;
                
                default:
                    connectionStatus.Text = "Unknown error.";
                    waitingStatus.Text = "sorry, karma couldn't connect.\n\nwe're going to keep trying, try configuring your firewall and settings.";
                    break;
            }
        }

        private void downloadListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (downloadListView.SelectedIndices.Count == 0)
            {
                fileHashLabel.Text = "";
                peerIPLabel.Text = "";
                filePathLabel.Text = "";
                downloadProgressBar.Value = 0;
                cancelDownloadButton.Enabled = false;
                openFileLocationButton.Enabled = false;
                return;
            }
            cancelDownloadButton.Enabled = true;
            openFileLocationButton.Enabled = true;

            int selection = downloadListView.SelectedIndices[0];
            foreach (string i in currentDownloadsHashTable.Keys)
            {
                if ((int)currentDownloadsHashTable[i] == selection)
                {
                    TransferDetail selectedTransfer = (TransferDetail)currentDownloadsDetails[i];
                    fileHashLabel.Text = selectedTransfer.fileHash;
                    peerIPLabel.Text = selectedTransfer.peerIP;
                    filePathLabel.Text = Configuration.downloadFolder + "\\" + selectedTransfer.fileName;
                    downloadProgressBar.Value = (int)selectedTransfer.percentageComplete;
                    return;
                }
            }
            return;
        }

        private void timeoutTimer_Tick(object sender, EventArgs e)
        {
            /*
            connectionStatus.Text = "Timeout during sync.";
            waitingStatus.Text = "sync timeout\n\nthere was an error during sync, retrying...";
            Configuration.writeLog("Timeout occurred during connection establishment. Restarting thread.");
            serverCommThread.Abort();
            serverCommThread = new Thread(new ThreadStart(this.talkToServer));
            serverCommThread.Start();
            */
        }

        private void addResultsToDisplay(SearchResult[] currentResults)
        {
            if (currentResults == null)
            {
                return;
            }
            searchResultsView.Items.Clear();
            currentSearchResultsHashTable = new Hashtable();
            int currentListPosition = 0;
            foreach (Object searchResultObj in currentResults)
            {
                SearchResult currentSearchResult = (SearchResult)searchResultObj;
                ListViewItem new_item = new ListViewItem(currentSearchResult.fileName, 0);
                new_item.SubItems.Add(Transfers.roundFileSize(currentSearchResult.fileSize));
                new_item.SubItems.Add("");
                new_item.SubItems.Add(currentSearchResult.peerName);
                new_item.SubItems.Add(currentSearchResult.peerIP);
                searchResultsView.Items.Add(new_item);
                currentSearchResultsHashTable[currentListPosition] = currentSearchResult;
                currentListPosition = currentListPosition + 1;
            }
        }

        private void searchResultsView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (searchResultsView.SelectedIndices.Count == 0)
            {
                startDownloadButton.Enabled = false;
                return;
            }
            startDownloadButton.Enabled = true;

            return;
        }

        private void startDownloadButton_Click(object sender, EventArgs e)
        {
            int selection = searchResultsView.SelectedIndices[0];
            SearchResult currentlySelectedSearchResult = (SearchResult)currentSearchResultsHashTable[selection];
            //Handling requests for offline files
            if (currentlySelectedSearchResult.fileAvailable == false || currentlySelectedSearchResult.peerIP=="0.0.0.0")
            {
                Transfers.sendMessage("REQUEST|" + currentlySelectedSearchResult.fileHash + "|" + GetMACAddress(), serverStream);
                MessageBox.Show("The file you requested has been marked by the server.\nYou will receive the file automagically when it is available online.");
            }
            else
            {
                Thread newDownloadThread = new Thread(new ParameterizedThreadStart(searchThreadStarter));
                newDownloadThread.IsBackground = true;
                newDownloadThread.Start(currentlySelectedSearchResult);
            }
        }

        private void searchThreadStarter(object pendingSearchResult)
        {
            SearchResult currentlySelectedSearchResult = (SearchResult)pendingSearchResult;
            Configuration.writeLog("searchThreadStarted : Starting download of selected file");
            Transfers.beginDownloadFromIP(currentlySelectedSearchResult.peerIP, currentlySelectedSearchResult.fileHash, currentlySelectedSearchResult.peerName);
        }

        private void searchWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            
            /*
            Thread newDownloadThread = new Thread(new ParameterizedThreadStart(Transfers.beginDownloadFromIP));
            newDownloadThread.IsBackground = true;
            newDownloadThread.Start(client);*/
        }

        private void helpLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            helpLink.LinkVisited = true;
            System.Diagnostics.Process.Start("http://www.slideshare.net/raghavsethirs/karma-user-guide");
        }


    }
}
