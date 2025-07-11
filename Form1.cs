using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MessengerServer
{
    public partial class Form1 : Form
    {
        private TcpListener listener;
        private List<TcpClient> clients = new List<TcpClient>();
        private bool isRunning = false;

        public Form1()
        {
            InitializeComponent();
	    this.Text = "Message Receive Server   [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]";

	    this.Load += Form1_Load;

            StartServer();
        }

	private void Form1_Load(object sender, EventArgs e)
	{
            int leftWidthResize = 50;

            this.Width = Screen.PrimaryScreen.WorkingArea.Width / 2 + leftWidthResize;
            this.Height = Screen.PrimaryScreen.WorkingArea.Height;

            this.Left = Screen.PrimaryScreen.WorkingArea.Width / 2 - leftWidthResize;
            this.Top = 0;
	}

	protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            const int WM_KEYDOWN = 0x0100;

            switch (message.Msg)
            {
                case WM_KEYDOWN:
                    if (keyData.ToString() == "Escape")
                    {
                        Application.Exit();
                    }
                    else if (keyData.ToString() == "Return")
                    {
		        //
		    }
		    break;
                default:
                    break;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void StartServer()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 9000);
                listener.Start();
                isRunning = true;

                AppendMessage("서버가 시작되었습니다. 클라이언트 연결을 기다리는 중...");

                Task.Run(async () =>
                {
                    while (isRunning)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        clients.Add(client);
                        AppendMessage($"클라이언트 연결됨: {client.Client.RemoteEndPoint}");

                        _ = Task.Run(() => HandleClient(client));
                    }
                });
            }
            catch (Exception ex)
            {
                AppendMessage("서버 오류: " + ex.Message);
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (isRunning)
                {
                    int byteCount = stream.Read(buffer, 0, buffer.Length);
                    if (byteCount == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    AppendMessage($"[{client.Client.RemoteEndPoint}] {message}");

                    // 전체 클라이언트에 메시지 브로드캐스트
                    BroadcastMessage(client, message);
                }
            }
            catch (Exception ex)
            {
                AppendMessage($"클라이언트 오류: {ex.Message}");
            }
            finally
            {
                AppendMessage($"클라이언트 종료: {client.Client.RemoteEndPoint}");
                clients.Remove(client);
                client.Close();
            }
        }

        private void BroadcastMessage(TcpClient sender, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var client in clients)
            {
                if (client != sender && client.Connected)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch
                    {
                        // 예외 무시 (연결 끊김 등)
                    }
                }
            }
        }

        private void AppendMessage(string message)
        {
            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new Action(() => {
                    richTextBox1.AppendText($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] - {message}\n");
                }));
            }
            else
            {
                richTextBox1.AppendText($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] - {message}\n");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isRunning = false;
            listener?.Stop();

            foreach (var client in clients)
            {
                client.Close();
            }

            base.OnFormClosing(e);
        }
    }
}
