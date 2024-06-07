using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace Call
{
    public partial class Form1 : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private TcpClient client;
        private NetworkStream clientStream;
        private bool isReconnecting = false;
        private Thread receiveThread;
        public Form1()
        {
            InitializeComponent();
            GetVideoDevices();
        }

        private void Call_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex >= 0)
            {
                try
                {
                    videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);
                    videoSource.NewFrame += videoSource_NewFrame;
                    videoSource.Start();
                    ConnectToServer();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error starting video source: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Please select a video device.");
            }
        }

        private void GetVideoDevices()
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    MessageBox.Show("No video devices found.");
                    return;
                }
                foreach (FilterInfo device in videoDevices)
                {
                    comboBox1.Items.Add(device.Name);
                }
                if (comboBox1.Items.Count > 0)
                {
                    comboBox1.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error getting video devices: " + ex.Message);
            }
        }

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient("127.0.0.1", 5000);
                clientStream = client.GetStream();
                isReconnecting = false;

                // Start receiving data from the server
                receiveThread = new Thread(new ThreadStart(ReceiveData));
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connecting to server: " + ex.Message);
            }
        }

        private void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
                pictureBox1.Invoke((Action)(() =>
                {
                    pictureBox2.Image?.Dispose();
                    pictureBox2.Image = (Bitmap)bitmap.Clone();
                }));

                SendFrame(bitmap);
                bitmap.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error processing new frame: " + ex.Message);
            }
        }

        private void SendFrame(Bitmap bitmap)
        {
            try
            {
                byte[] frameData;
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    frameData = stream.ToArray();
                }
                if (clientStream != null && clientStream.CanWrite)
                {
                    byte[] frameLength = BitConverter.GetBytes(frameData.Length);
                    clientStream.Write(frameLength, 0, frameLength.Length);
                    clientStream.Write(frameData, 0, frameData.Length);
                    clientStream.Flush();
                }
            }
            catch (IOException ioEx)
            {
                MessageBox.Show("Error sending frame: " + ioEx.Message);
                if (!isReconnecting)
                {
                    isReconnecting = true;
                    ReconnectToServer();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending frame: " + ex.Message);
            }
        }
        private void ReceiveData()
        {
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

                    using (MemoryStream ms = new MemoryStream(frameData))
                    {
                        Bitmap bitmap = new Bitmap(ms);
                        pictureBox2.Invoke(new Action(() =>
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
        }
        private void ReconnectToServer()
        {
            try
            {
                if (client != null)
                {
                    client.Close();
                }
                ConnectToServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reconnecting to server: " + ex.Message);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
            }
            if (clientStream != null)
            {
                clientStream.Close();
            }
            if (client != null)
            {
                client.Close();
            }
        }
    }
}
