using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace eventuali
{
    class Server
    {
        private TcpListener tcpListener;
        private Thread listenThread;
        private List<string> activeClients;
        private ASCIIEncoding encoder;

        public Server()
        {
            activeClients = new List<string>();
            this.tcpListener = new TcpListener(IPAddress.Any, 8001);
            this.listenThread = new Thread(new ThreadStart(ListenForClients));
            this.listenThread.Start();
            encoder = new ASCIIEncoding();
        }
    
        private void ListenForClients()
        {
            this.tcpListener.Start();

            while (true)
            {
            //Waits until a client has connected to the server.
            TcpClient client = this.tcpListener.AcceptTcpClient();

            //Creates a thread to handle the client.
            Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
            clientThread.Start(client);
            }
        }

        private void HandleClientComm(object client)
        {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();
            string lastMessageRecd = "";
            string clientID = "";
            try
            {
                //First item received is MAC
                lastMessageRecd = recieveMessage(clientStream);
                activeClients.Add(lastMessageRecd);
                clientID = lastMessageRecd;
                sendMessage("OK", clientStream);

                //Second item received is username
                lastMessageRecd = recieveMessage(clientStream);
                //Add code to check and comm with DB here.
                sendMessage("OK", clientStream);

                //Third item received is numFilesShared
                lastMessageRecd = recieveMessage(clientStream);
                //Add code to check and comm with DB here.
                sendMessage("OK", clientStream);

                //Handshake completed.
            }
            catch (Exception e)
            {
                //Write to log here.
                activeClients.Remove(clientID);
            }
            
            //Waits to give client instructions or for client-request.
            while (true)
            {
                lastMessageRecd = recieveMessage(clientStream);

                if (lastMessageRecd == "SOCKET_ERROR" || lastMessageRecd == "CLIENT_DISCONNECT")
                {
                    activeClients.Remove(clientID);
                    break;
                }
                sendMessage("Hello client!", clientStream);
            }

            tcpClient.Close();
        }

        private void sendMessage(string message, NetworkStream clientStream)
        {
            byte[] buffer = encoder.GetBytes(message);
            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();
        }

        private string recieveMessage(NetworkStream clientStream)
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
                return "SOCKET_ERROR";
            }

            if (bytesRead == 0)
            {
                return "CLIENT_DISCONNECT";
            }

            //message has successfully been received
            return encoder.GetString(message, 0, bytesRead);
        }
    }
}
