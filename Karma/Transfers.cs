using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;
using System.Collections;
using System.Threading;

namespace eventuali
{
    //Used to manage state of transfers in progress/completed.
    public class TransferDetail
    {
        public string fileHash;
        public string fileName;
        public string status;
        public float percentageComplete;
        public float transferRate;
        public long fileSize;
        public string peerIP;
        public string peerName;
        public int userStatus;
        public string systemStatus;
        public char ttype;
        public long transferred;
        public float timeRemaining;

        public TransferDetail(string t_fileHash, char t_type)
        {
            fileHash = t_fileHash;
            ttype = t_type;
            fileName = "Unknown";
            status = "Unknown";
            percentageComplete = 0;
            transferRate = 0;
            fileSize = 0;
            peerIP = "";
            peerName = "";
            userStatus = 1; //1-OK, 2-Canceled, 3-Paused
            transferred = 0;
            timeRemaining = 0;
        }

        public string percentageCompleteString
        {
            get { return percentageComplete.ToString("F1")+"%"; }
        }

        public string transferRateString
        {
            get { return transferRate.ToString("F1") + " kB/s"; }
        }

        public string fileSizeString
        {
            get { return Transfers.roundFileSize(fileSize); }
        }

        public string timeRemainingString
        {
            get { return timeRemaining.ToString("F1") + 's'; }
        }

        public void newTransfer(string t_fileName, long t_fileSize, string t_peerIP, string t_peerName)
        {

            fileName = t_fileName;
            fileSize = t_fileSize;
            peerIP = t_peerIP;
            peerName = t_peerName;
            
            if (ttype == 'd')
                status = "Beginning download";
            else
                status = "Beginning upload";
        }

        public void updateTransferProgress(long t_transferred, float t_transferRate, float t_timeRemaining)
        {
            if (ttype == 'd')
                status = "Downloading";
            else
                status = "Uploading";

            transferred = t_transferred;
            transferRate = t_transferRate;
            percentageComplete = ((float)100.0 * transferred) / fileSize;
            timeRemaining = t_timeRemaining;
        }

    }

    //Used to manage and return search results to UI.
    public class SearchResult
    {
        public string fileHash;
        public string fileName;
        public long fileSize;
        public string peerIP;
        public string peerName;
        public bool fileAvailable;

        //Attempts to parse results from server, and store them in the 
        //correct format. Returns false if unable to parse
        public bool parseSearchResult(string resultLine)
        {
            Configuration.writeLog("Parsing : " + resultLine);
            string[] results = resultLine.Split('|');
            if (results.Length == 6)
            {
                fileHash = results[0];
                fileName = results[1];
                fileSize = Convert.ToInt64(results[2]);
                peerIP = results[3];
                peerName = results[4];
                if (results[5] == "ONLINE")
                {
                    fileAvailable = true;
                }
                else
                {
                    fileAvailable = false;
                    peerIP = "Offline";
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }

    static class Transfers
    {
        public static BackgroundWorker passiveWorker;
        public static BackgroundWorker activeWorker;
        public static Hashtable userDownloadState;
        public static ASCIIEncoding encoder;

        public static void beginDownloadFromIP(string IPAddress, string fileHash, string peerName)
        {
            //Connects to peer, performs handshake, sends download instruction and then begins download.
            Configuration.writeLog("beginDownloadFromIP : Started.");
            TcpClient tcpclnt = new TcpClient();
            tcpclnt.NoDelay = true;
            NetworkStream clientStream;
            //TODO - Figure out port numbers
            try
            {
                tcpclnt.Connect(IPAddress, 8002);
            
                clientStream = tcpclnt.GetStream();
                Configuration.writeLog("beginDownloadFromIP : Sending download instruction to peer " + IPAddress + "...");
                //filehash | filename | filesize | peer ip| peernick
                //TODO - Figure out what IP to put here.
                sendMessage("FILE-REQUEST|" + fileHash + "|fname|0|127.0.0.1|"+Configuration.username, clientStream);
                Configuration.writeLog("beginDownloadFromIP : Download started.");
            }
            catch (Exception e)
            {
                Configuration.writeLog("beginDownloadFromIP : Could not establish connection.");
                return;
            }
            //Begin file transfer
            string filePath = download(tcpclnt, clientStream, fileHash, IPAddress, peerName);

            if (filePath == "ERROR")
            {
                Configuration.writeLog("beginDownloadFromIP : Download aborted.");
                return;
            }

            Configuration.writeLog("beginDownloadFromIP : Verifying file contents..");

            //TODO - Add more complex behaviours to handle corrupted files.
            if (Index.Md5SumByProcess(filePath) == fileHash)
            {
                Configuration.writeLog("beginDownloadFromIP : File contents verified sucessfully.");
            }
        }

        public static string download(TcpClient tcpClient, NetworkStream clientStream, string fileHash, string peerIP, string peerName)
        {
            //Downloads a file from peer. Returns the path of the downloaded file.

            FileStream strLocal = null;
            string fileName = "";
            bool successfulTransfer = false;
            long fileSize = 0;

            TransferDetail tdCurrent = new TransferDetail(fileHash, 'd');

            try
            {
                //Setting state of file to 'downloading'
                userDownloadState[fileHash] = 1;

                // For holding the number of bytes we are reading at one time from the stream
                int bytesSize = 0;
                byte[] downBuffer = new byte[4096];
                //byte[] downBuffer = new byte[1300];
                Configuration.writeLog("Download module : Getting file details..");

                string instruction_params = receiveMessage(clientStream);

                //TODO - If 404, tell user better instead of silently failing
                if (instruction_params == "404")
                {
                    Configuration.writeLog("Download module : Peer reported file not found.");
                    return "ERROR";
                }

                string[] instructions = instruction_params.Split('\n');
                if(instructions.Length<2)
                {
                    Configuration.writeLog("download : Incorrect params passed " + instruction_params);
                }
                fileName = instructions[0];
                fileSize = Convert.ToInt64(instructions[1]);
                Configuration.writeLog("Download module : Beginning download of " + fileName + " (" + fileSize + ") bytes");

                //Reporting addition to UI.

                tdCurrent.newTransfer(fileName, fileSize, peerIP, peerName);
                passiveWorker.ReportProgress(0, tdCurrent);

                long bytesUploaded = 0;
                int displayRefreshFactor = (int)fileSize/1000000; //Changes how fast the download progress in the UI changes.
                int currentCycle = 0;
                DateTime startTime = DateTime.Now;
                DateTime currentTime;
                TimeSpan duration = new TimeSpan();
                float tempTransferRate = 0;

                strLocal = new FileStream(Configuration.downloadFolder + "\\" + fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                
                //Performing file transfer.
                while ((bytesSize = clientStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    //In case user cancels download.
                    if ((int)userDownloadState[fileHash] == 0)
                    {
                        tdCurrent.systemStatus = "CANCELED";
                        tdCurrent.status = "Canceled.";
                        passiveWorker.ReportProgress(0, tdCurrent);
                        return "ERROR";
                    }

                    currentCycle = currentCycle + 1;
                    bytesUploaded = bytesUploaded + bytesSize;
                    strLocal.Write(downBuffer, 0, bytesSize);
                    //TODO: Make this fixed update - every half a second or so, and not dependent on file
                    //size.
                    if (currentCycle == displayRefreshFactor)
                    {
                        tempTransferRate =  (float)(1.0 * bytesUploaded / (1024 * duration.Seconds));
                        tdCurrent.updateTransferProgress(bytesUploaded, tempTransferRate, 0); 
                        passiveWorker.ReportProgress(0, tdCurrent);
                        currentCycle = 0;
                    }

                    currentTime = DateTime.Now;
                    duration = currentTime - startTime;

                }
                tdCurrent.updateTransferProgress(bytesUploaded, tempTransferRate, 0);
                tdCurrent.status=("Downloaded in " + duration.Seconds.ToString() + " secs");
                passiveWorker.ReportProgress(0, tdCurrent);

                successfulTransfer = true;
                strLocal.Close();
            }
            catch (Exception e)
            {
                successfulTransfer = false;
                Configuration.writeLog("Download module reported error : " + e.ToString());
                tdCurrent.transferRate = 0;
                tdCurrent.status = "Download failed.";
            }
            finally
            {
                clientStream.Close();
            }
            if (successfulTransfer)
            {
                Configuration.writeLog("Download module : Successfully downloaded " + fileName + "(" + fileSize + ")");
            }
            return Configuration.downloadFolder + "\\" + fileName;
        }

        public static bool upload(string fileHash, NetworkStream serverStream, string peerName)
        {
            Configuration.writeLog("Upload module started.");
            
            bool successfulTransfer = false;
            byte[] byteSend = new byte[8096];
            //byte[] byteSend = new byte[1450];
            string filePath = "";
            string fileName = "";
            long fileSize = 0;

            TransferDetail tdCurrent = new TransferDetail(fileHash, 'u');
            tdCurrent.peerName = peerName;

            //Get file path from hash.
            //TODO - Add error checking here.!!
            filePath = (string)Index.filehashes[fileHash];
            FileStream fstFile;
            string[] temp;
            try
            {
                if (filePath == null)
                {
                    Configuration.writeLog("upload : Incorrect args passed.");
                
                    return false;
                }

                temp = filePath.Split('\\');
                fileName = temp[temp.Length - 1];

                //Start reading the file as binary
                fstFile = null;
            
                fstFile = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            }
            //TODO - Put better exception handling
            catch (Exception e)
            {
                Configuration.writeLog("Upload module reported error : " + e.ToString());
                sendMessage("404", serverStream);
                serverStream.Close();
                return false;
            }

            BinaryReader binFile = new BinaryReader(fstFile);
            FileInfo fInfo = new FileInfo(filePath);
            Configuration.writeLog("Upload module : Started reading file from disk : " + filePath);
            Thread.Sleep(1000);
            fileName = fInfo.Name;
            fileSize = fInfo.Length;

            long bytesUploaded = 0;
            TimeSpan duration = new TimeSpan();
            float tempTransferRate = 0;

            try
            {
                Configuration.writeLog("Upload module : Sending file details..");


                sendMessage(fileName + "\n" + fileSize.ToString() + "\n", serverStream);

                tdCurrent.newTransfer(fileName, fileSize, "", "");
                passiveWorker.ReportProgress(0, tdCurrent);

                Configuration.writeLog("Upload module : Sending the file " + fileName + "(" + fileSize + ")");

                int bytesSize = 0;


                //Changes how fast the upload progress in the UI changes.
                int displayRefreshFactor = 10;
                int currentCycle = 0;
                DateTime startTime = DateTime.Now;
                DateTime currentTime;
                

                //Send the file.
                while ((bytesSize = fstFile.Read(byteSend, 0, byteSend.Length)) > 0)
                {
                    //TODO - Remove the next line, it's for simulation only.
                    //Thread.Sleep(30);

                    currentCycle = currentCycle + 1;
                    bytesUploaded = bytesUploaded + bytesSize;
                    serverStream.Write(byteSend, 0, bytesSize);
                    if (currentCycle == displayRefreshFactor)
                    {
                        tempTransferRate = (float)(1.0 * bytesUploaded / (1024 * duration.Seconds));
                        tdCurrent.updateTransferProgress(bytesUploaded, tempTransferRate, 0);
                        passiveWorker.ReportProgress(0, tdCurrent);
                        currentCycle = 0;
                    }
                    currentTime = DateTime.Now;
                    duration = currentTime - startTime;
                    
                }
                tdCurrent.updateTransferProgress(bytesUploaded, tempTransferRate, 0);
                tdCurrent.status = ("Uploaded in " + duration.Seconds.ToString() + " secs");
                passiveWorker.ReportProgress(0, tdCurrent);

                Configuration.writeLog("Upload module : Sent file " + fileName);
                successfulTransfer = true;
            }
            catch (Exception e)
            {
                Configuration.writeLog("Upload module reported error : " + e.ToString());
                tdCurrent.updateTransferProgress(bytesUploaded, tempTransferRate, 0);
                tdCurrent.status = ("Upload error.");
                passiveWorker.ReportProgress(0, tdCurrent);
                successfulTransfer = false;
            }
            finally
            {
                serverStream.Close();
                fstFile.Close();
            }
            return successfulTransfer;
        }

        public static string roundFileSize(long bytes)
        {
            string rounded = bytes.ToString() + "B";
            if (bytes > 1024)
            {
                rounded = (bytes / 1024.0).ToString("F2") + "KB";
            }
            if (bytes > (1024 * 1024))
            {
                rounded = (bytes / (1024 * 1024.0)).ToString("F2") + "MB";
            }
            if (bytes > (1024 * 1024 * 1024))
            {
                rounded = (bytes / (1024 * 1024 * 1024.0)).ToString("F2") + "GB";
            }
            return rounded;
        }

        public static void sendMessage(string message, NetworkStream clientStream)
        {
            if (encoder == null)
                encoder = new ASCIIEncoding();
            byte[] buffer = encoder.GetBytes(message);

            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();
        }

        public static string receiveMessage(NetworkStream clientStream)
        {
            byte[] message = new byte[4096];
            int bytesRead;
            bytesRead = 0;
            try
            {
                //Blocks until a client sends a message
                bytesRead = clientStream.Read(message, 0, 4096);
            }
            catch
            {
                MessageBox.Show("SOCKET_ERROR");
                return "SOCKET_ERROR";
            }

            if (bytesRead == 0)
            {
                MessageBox.Show("CLIENT_DISCON");
                throw new IOException("Client disconnected");
            }
            //If code gets here, message was successfully received.
            return encoder.GetString(message, 0, bytesRead);
        }

        public static bool uploadIndexFile(NetworkStream serverStream)
        {
            Configuration.writeLog("uploadIndexFile : Started.");

            bool successfulTransfer = false;
            byte[] byteSend = new byte[4096];

            string filePath = Application.StartupPath + "\\index.dat";

            //Start reading the file as binary
            FileStream fstFile = null;
            try
            {
                fstFile = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            }
            
            //TODO - Put better exception handling
            catch (Exception e)
            {
                Configuration.writeLog("uploadIndexFile module reported error : " + e.ToString());
                return false;
            }

            BinaryReader binFile = new BinaryReader(fstFile);
            FileInfo fInfo = new FileInfo(filePath);
            Configuration.writeLog("Upload module : Started reading file from disk : " + filePath);
            long bytesUploaded = 0;
            try
            {
                int bytesSize = 0;

                //Send the file.
                while ((bytesSize = fstFile.Read(byteSend, 0, byteSend.Length)) > 0)
                {
                    bytesUploaded = bytesUploaded + bytesSize;
                    serverStream.Write(byteSend, 0, bytesSize);

                }
                Configuration.writeLog("uploadIndexFile module : Sent index.");
                successfulTransfer = true;
            }
            catch (Exception e)
            {
                Configuration.writeLog("uploadIndexFile module reported error : " + e.ToString());
                successfulTransfer = false;
            }
            return successfulTransfer;
        }

        public static SearchResult[] searchServerFileList(string query, NetworkStream serverStream)
        {
            activeWorker.ReportProgress(11, query);
            sendMessage("SEARCH|" + query, serverStream);
            Configuration.writeLog("searchServerFileList : Query sent.");
            string resultsMessage = receiveMessage(serverStream);
            //MessageBox.Show(resultsMessage);
            string[] resultsArray = resultsMessage.Split('\n');
            SearchResult[] returnedResults = new SearchResult[resultsArray.Length-1];
            string currentParsePoint = resultsArray[0];
            int i=0;
            while (currentParsePoint != "END OF RESULTS")
            {
                SearchResult temporaryResult = new SearchResult();
                temporaryResult.parseSearchResult(currentParsePoint);
                returnedResults[i]=temporaryResult;
                i = i + 1;
                currentParsePoint = resultsArray[i];
            }
            return returnedResults;
        }
    
    }


}
