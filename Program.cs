using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MessengerServer
{
    class Program
    {
        private static TcpListener listener;
        private static List<TcpClient> clients = new List<TcpClient>();
        private static bool isRunning = false;

        static async Task Main(string[] args)
        {
            Console.Title = "Message Receive Server";

            StartServer();

            Console.WriteLine("서버가 시작되었습니다. 클라이언트 연결을 기다리는 중...");
            Console.WriteLine("종료하려면 Ctrl+C를 누르세요.");

            // Ctrl+C 누르면 종료 이벤트 처리
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("서버 종료 중...");
                isRunning = false;
                listener.Stop();
                lock (clients)
                {
                    foreach (var client in clients)
                    {
                        client.Close();
                    }
                }
                Environment.Exit(0);
            };

            // 서버가 계속 실행되도록 Task 대기
            while (isRunning)
            {
                await Task.Delay(1000);
            }
        }

        private static void StartServer()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 9000);
                listener.Start();
                isRunning = true;

                Task.Run(async () =>
                {
                    while (isRunning)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        lock (clients)
                        {
                            clients.Add(client);
                        }
                        Console.WriteLine($"클라이언트 연결됨: {client.Client.RemoteEndPoint}");

                        _ = Task.Run(() => HandleClient(client));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버 오류: " + ex.Message);
            }
        }

        private static void HandleClient(TcpClient client)
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
                    Console.WriteLine($"[{client.Client.RemoteEndPoint}] {message}");

                    BroadcastMessage(client, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 오류: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"클라이언트 종료: {client.Client.RemoteEndPoint}");
                lock (clients)
                {
                    clients.Remove(client);
                }
                client.Close();
            }
        }

        private static void BroadcastMessage(TcpClient sender, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (clients)
            {
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
                            // 예외 무시
                        }
                    }
                }
            }
        }
    }
}
