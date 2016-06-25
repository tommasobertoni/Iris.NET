using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisServer
    {
        private int _port;
        public int Port => _port;

        public Guid Id => Guid.NewGuid();

        public IrisServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            TcpListener serverSocket = new TcpListener(IPAddress.Any, _port);
            serverSocket.Start();
            Console.WriteLine($"[IrisServer] started {Id}");
            TcpClient clientSocket = serverSocket.AcceptTcpClient();
            NetworkStream networkStream = clientSocket.GetStream();
            Console.WriteLine("[IrisServer] client connected");

            while (true)
            {
                Console.WriteLine("[waiting for message]");
                try
                {
                    var memoryStream = Read(networkStream);
                    var data = memoryStream.DeserializeFromMemoryStream();
                    Console.WriteLine($"[IrisServer] data received: {data.GetType().Name}");
                    if (data is IrisMessage)
                    {
                        var message = data as IrisMessage;
                        Console.WriteLine($"[message] {message.Content};{message.TargetChannel};{message.PublicationDateTime};{message.PropagateThroughHierarchy};{message.PublisherId}");

                        IrisMessage response = null;

                        if (message.Content as string == "HELLO")
                            response = new IrisMessage(Id, "main") { Content = "WORLD" };
                        else if (message.Content as string == "PING")
                            response = new IrisMessage(Id, "main") { Content = "PONG" };
                        else if (message.Content as string != null)
                            response = new IrisMessage(Id, "main") { Content = $"echo: {message.Content}" };

                        if (response != null)
                        {
                            var stream = response.SerializeToMemoryStream();
                            var rowData = stream.ToArray();
                            networkStream.Write(rowData, 0, rowData.Length);
                            networkStream.Flush();
                        }
                    }
                    else if (data is IrisSubscribe)
                    {
                        var request = data as IrisSubscribe;
                        Console.WriteLine($"[subscribe] {request.Channel};{request.PublisherId}");
                    }
                    else if (data is IrisUnsubscribe)
                    {
                        var request = data as IrisUnsubscribe;
                        Console.WriteLine($"[UNsubscribe] {request.Channel};{request.PublisherId}");
                    }
                    else
                    {
                        Console.WriteLine($"[unknown] {data.GetType().FullName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Exception] {ex.GetFullException()}");
                    break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("Stopping");
            networkStream.Close();
            clientSocket.Close();
            serverSocket.Stop();
            Console.Write("Close...");
            Console.ReadLine();
        }

        private static MemoryStream Read(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            MemoryStream ms = new MemoryStream();
            int read = input.Read(buffer, 0, buffer.Length);
            ms.Write(buffer, 0, read);
            return ms;
        }
    }
}
