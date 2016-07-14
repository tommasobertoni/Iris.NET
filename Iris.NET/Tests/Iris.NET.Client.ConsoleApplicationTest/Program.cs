using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Iris.NET.Client.ConsoleApplicationTest
{
    public class Program
    {
        static string sep = "------------------------";
        static string mainChannel = "main";

        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0].ToLower() == "full")
                Full();
            else
                Base(args);
        }

        private static void Base(string[] args)
        {
            Console.WriteLine($"{typeof(Program).Namespace}");
            Console.WriteLine("Press Enter to start (or insert parameters below)");

            var parametersString = Console.ReadLine();
            string[] @params = parametersString.Split('\t');
            Console.WriteLine("Started\n");

            IrisClientNode client = new IrisClientNode();
            IrisClientConfig config = new IrisClientConfig()
            {
                Hostname = "127.0.0.1",
                Port = 22000
            };
            Console.WriteLine($"Client and config created {client.NodeId} <=> {config.Hostname}:{config.Port}");

            try
            {
                client.Connect(config);
                Console.WriteLine($"Is client connected? {client.IsConnected} (Press Enter)");

                client.OnDisposed += Client_OnClientDisposed;
                client.OnException += ExceptionHandler;
                Console.WriteLine("Exception/Log events hooked");

                if (client.Subscribe(mainChannel, ContentHandler) != null)
                {
                    Console.WriteLine($"Client subscribed to \"{"main"}\" channel\n");
                }
                else
                {
                    Console.WriteLine($"Client FAILED TO subscribe to \"{"main"}\" channel");
                }

                if (args?.Length > 0 && args?.First()?.ToUpper() == "CUSTOM")
                {
                    Thread.Sleep(500);
                    string message;
                    do
                    {
                        Console.WriteLine("Write your message:");
                        message = Console.ReadLine();
                        client.Send(mainChannel, message);
                    } while (message.ToUpper() != "QUIT" && message.ToUpper() != "Q");
                }
                else
                {
                    Thread.Sleep(1000);
                    string[] messages = { "HELLO", "PING" };
                    foreach (var message in messages)
                    {
                        client.Send(mainChannel, message);
                        Console.WriteLine($"Sent {message} to {mainChannel}");
                        Thread.Sleep(1000);
                    }
                }

                Console.Write("Tests completed");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n{sep}\nException:\n{ex.GetFullException()}\n{sep}\n");
            }
            finally
            {
                Console.WriteLine("Closing");

                if (@params != null && !@params.Contains("NU")) // Not Unsubscribe
                {
                    if (client.Unsubscribe(mainChannel))
                    {
                        Console.WriteLine($"Client unsubscribed from \"{mainChannel}\" channel");
                    }
                    else
                    {
                        Console.WriteLine($"Client FAILED TO unsubscribe from \"{mainChannel}\" channel");
                    }
                }
                else
                    Console.WriteLine("Skip \"Unsubscribe\"");

                client.OnException -= ExceptionHandler;
                Console.WriteLine("Exception/Log events UNhooked");
            }

            client.Dispose();
            Console.Write("\n\nTerminate...");
            Console.ReadLine();
        }

        private static void Client_OnClientDisposed()
        {
            Process.GetCurrentProcess().CloseMainWindow();
        }

        private static void ContentHandler(object content, IrisContextHook hook)
        {
            if (content != null)
                Console.WriteLine($"Content received! {content}\n");
            else if (hook != null)
                Console.WriteLine($"{nameof(IrisContextHook)} => {nameof(hook.Unsubscribing)}: {hook.Unsubscribing}");
        }

        private static void ExceptionHandler(Exception ex)
        {
            Console.WriteLine($"\n{sep}\nEXCEPTION: {ex.GetFullException()}\n{sep}\n");
        }

        private static void Full()
        {
            IrisClientNode client = new IrisClientNode();
            IrisClientConfig config = new IrisClientConfig()
            {
                Hostname = "127.0.0.1",
                Port = 22000
            };

            client.OnDisposed += Client_OnClientDisposed;
            client.OnException += ExceptionHandler;
            client.Connect(config);
            Console.WriteLine("- Client connected");

            string input;
            do
            {
                Console.WriteLine("- Write \"SUB {channel}\" to subscribe to a channel");
                Console.WriteLine("- Write \"UNSUB {channel}\" to unsubscribe from a channel");
                Console.WriteLine("- Write \"SEND {message} {channel}\" to send a message to a channel");
                Console.WriteLine("- Write \"SEND {message}\" to send a message in broadcast");
                Console.WriteLine("- Write \"QUIT\" or \"Q\" to quit and dispose the server");
                Console.WriteLine();

                input = Console.ReadLine();
                string[] command = input.ToUpper().Split(' ');
                bool handled = false;

                if (command.Length > 1)
                {
                    switch (command[0])
                    {
                        case "SUB":
                            if (handled = command.Length == 2 && client.Subscribe(command[1], GenericContentHandler) != null)
                                Console.WriteLine("- Subscribed");
                            break;

                        case "UNSUB":
                            if (handled = command.Length == 2 && client.Unsubscribe(command[1], GenericContentHandler))
                                Console.WriteLine("- Unsubscribed");
                            break;

                        case "SEND":
                            if (handled = (command.Length == 3 && client.Send(command[1], command[2])) ||
                                          (command.Length == 2 && client.Send(null, command[1])))
                                Console.WriteLine("- Message sent");
                            break;

                        case "Q":
                        case "QUIT":
                            handled = command.Length == 1;
                            break;
                    }
                }

                if (!handled)
                    Console.WriteLine($"- Unrecognized command \"{input}\"");
                Console.WriteLine();

            } while (input.ToUpper() != "QUIT" && input.ToUpper() != "Q");

            client.Dispose();
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
