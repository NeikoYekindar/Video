using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace Call
{
    public partial class Server : Form
    {
        private TcpListener server;
        private Thread listenThread;
        private List<TcpClient> clients = new List<TcpClient>();
        public Server()
        {
            InitializeComponent();
            StartServer();
        }

        private void StartServer()
        {
            server = new TcpListener(IPAddress.Any, 5000);
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        private void ListenForClients()
        {
            server.Start();
            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    lock (clients)
                    {
                        clients.Add(client);
                    }
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                    clientThread.IsBackground = true;
                    clientThread.Start(client);
                }
                catch (SocketException ex)
                {
                    MessageBox.Show("Socket exception: " + ex.Message);
                }
            }
        }

        private void HandleClientComm(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;
            NetworkStream clientStream = tcpClient.GetStream();

            while (true)
            {
                try
                {
                    byte[] lengthBuffer = new byte[4];
                    int lengthBytesRead = clientStream.Read(lengthBuffer, 0, 4);
                    if (lengthBytesRead == 0) break;

                    int frameLength = BitConverter.ToInt32(lengthBuffer, 0);
                    byte[] frameData = new byte[frameLength];
                    int bytesRead = 0;
                    while (bytesRead < frameLength)
                    {
                        int read = clientStream.Read(frameData, bytesRead, frameLength - bytesRead);
                        if (read == 0) break;
                        bytesRead += read;
                    }
                    lock (clients)
                    {
                        foreach (var client in clients)
                        {
                            if (client != tcpClient)
                            {
                                NetworkStream stream = client.GetStream();
                                stream.Write(lengthBuffer, 0, lengthBuffer.Length);
                                stream.Write(frameData, 0, frameData.Length);
                                stream.Flush();
                            }
                        }
                    }

                    using (MemoryStream ms = new MemoryStream(frameData))
                    {
                        Bitmap bitmap = new Bitmap(ms);
                        pictureBox1.Invoke(new Action(() =>
                        {
                            pictureBox1.Image?.Dispose();
                            pictureBox1.Image = bitmap;
                        }));
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show("IO exception: " + ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Exception: " + ex.Message);
                    break;
                }
            }

            tcpClient.Close();
        }
    }
}
