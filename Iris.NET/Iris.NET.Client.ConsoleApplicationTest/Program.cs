using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Iris.NET.Client.ConsoleApplicationTest
{
    class Program
    {
        static string sep = "------------------------";
        static string logFileName = "iris.test.log";

        static void Main(string[] args)
        {
            string mainChannel = "main";

            File.Delete(logFileName);
            Console.WriteLine("Main started\n\n");

            IrisClientNode client = new IrisClientNode();
            IrisClientConfig config = new IrisClientConfig()
            {
                Hostname = "localhost",
                Port = 22000
            };
            Console.WriteLine($"Client and config created {client.ClientId}-{config.Hostname}:{config.Port}\n");

            try
            {
                client.Connect(config);
                Console.Write($"Is client connected? {client.IsConnected}!! (Press Enter)");
                Console.ReadLine();
                Console.WriteLine();

                client.OnException += ExceptionHandler;
                client.OnLog += LogHandler;
                Console.WriteLine("Exception/Log events hooked");

                if (client.Subscribe(mainChannel, ContentHandler))
                {
                    Console.WriteLine($"Client subscribed to \"{"main"}\" channel");
                }
                else
                {
                    Console.WriteLine($"Client FAILED TO subscribe to \"{"main"}\" channel");
                }

                string[] messages = { "HELLO", "PING" };
                foreach (var message in messages)
                {
                    client.SendAsync(mainChannel, message);
                    Console.WriteLine($"Sent {message} to {mainChannel}");
                    Thread.Sleep(1000);
                }

                Console.WriteLine("Write your message:");
                client.SendAsync(mainChannel, Console.ReadLine());
                Thread.Sleep(1000);
                Console.WriteLine("...everything ok?\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n{sep}\nException:\n{ex.GetFullException()}\n{sep}\n");
            }
            finally
            {
                Console.WriteLine("Closing");

                if (client.Unsubscribe(mainChannel, ContentHandler))
                {
                    Console.WriteLine($"Client unsubscribed from \"{mainChannel}\" channel");
                }
                else
                {
                    Console.WriteLine($"Client FAILED TO unsubscribe from \"{mainChannel}\" channel");
                }

                client.OnException -= ExceptionHandler;
                client.OnLog -= LogHandler;
                Console.WriteLine("Exception/Log events UNhooked");
            }

            Console.Write("\n\nTerminate...");
            Console.ReadLine();
        }

        private static void ContentHandler(object content)
        {
            Console.WriteLine($"Content received! {content.GetType().FullName}\n{content}\n");
        }

        private static void ExceptionHandler(Exception ex)
        {
            Console.WriteLine($"\n{sep}\nEXCEPTION: {ex.GetFullException()}\n{sep}\n");
        }

        private static void LogHandler(string log)
        {
            Console.WriteLine($"\n{sep}\nLog: {log}\n{sep}\n");
            File.AppendAllText(logFileName, log);
        }
    }
}
