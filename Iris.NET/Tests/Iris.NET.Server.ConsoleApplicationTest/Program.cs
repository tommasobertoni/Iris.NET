using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server.ConsoleApplicationTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0].ToLower() == "full")
                FullTest(args);
            else
                ServerTest(args);
        }

        static void FullTest(string[] args)
        {
            IrisServer server = new IrisServer();
            server.Start(22000);
            Console.WriteLine("Server started");

            LocalClientTest(server.GetServerConfig());
            server.Stop();
            Console.WriteLine("Server stopped");
            Console.WriteLine();
            Console.Write("Press ENTER to terminate...");
            Console.ReadLine();
        }

        static void ServerTest(string[] args)
        {
            Console.WriteLine($"{typeof(Program).Namespace}");
            if (args?.Length == 0)
            {
                Console.Write("Press Enter to start");
                Console.ReadLine();
            }
            Console.WriteLine("Started\n");

            IrisServer server = new IrisServer();
            server.Start(22000);

            string input = null;
            do
            {
                if (input != null)
                {
                    if (args == null || !args.Contains(input))
                    {
                        Console.WriteLine($"-- Unrecognized command \"{input}\"\n");
                    }
                }

                if (args == null || !args.Contains(input))
                {
                    Console.Write("Command: ");
                    input = Console.ReadLine();
                }
            } while (input?.ToUpper() != "QUIT" && input?.ToUpper() != "Q");

            server.Stop();
            if (args?.Length == 0)
            {
                Console.Write("Terminate...");
                Console.ReadLine();
            }
        }

        static void LocalClientTest(IrisServerConfig config)
        {
            var node = new IrisServerLocalNode();
            node.Connect(config);

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
                            if (handled = command.Length == 2 && node.Subscribe(command[1], GenericContentHandler) != null)
                                Console.WriteLine("- Subscribed");
                            break;

                        case "UNSUB":
                            if (handled = command.Length == 2 && node.Unsubscribe(command[1], GenericContentHandler))
                                Console.WriteLine("- Unsubscribed");
                            break;

                        case "SEND":
                            if (handled = (command.Length == 3 && node.Send(command[1], command[2])) ||
                                          (command.Length == 2 && node.Send(null, command[1])))
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

            node.Dispose();
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
