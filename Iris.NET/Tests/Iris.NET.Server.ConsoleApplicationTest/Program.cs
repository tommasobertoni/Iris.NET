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
#if FULL
            ServerFull();
#else
            if (args != null && args.Length > 0 && args[0].ToLower() == "full")
                ServerFull();
            else
                ServerBase(args);
#endif
        }

        static void ServerFull()
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

        static void ServerBase(string[] args)
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
            var local = new IrisServerLocalNode();
            local.Connect(config);
            var subscriptionToBroadcast = local.SubscribeToBroadcast((c, h) => Console.WriteLine($"Content: {c} [received from broadcast]"));

            string root = "tch"; // test channel
            string leaf = $"{root}/deep";

            var otherNode = new IrisServerLocalNode();
            otherNode.Connect(config);
            var otherSubscriptionToBroadcast = otherNode.SubscribeToBroadcast((c, h) => Console.WriteLine($"{nameof(otherNode)} received from broadcast: {c}"));
            var otherSubscription = otherNode.Subscribe(root, (c, h) => Console.WriteLine($"{nameof(otherNode)} received @{root} for {h.TargetChannel}: {c}"));

            var deepNode = new IrisServerLocalNode();
            deepNode.Connect(config);
            var deepSubscriptionToBroadcast = deepNode.SubscribeToBroadcast((c, h) => Console.WriteLine($"{nameof(deepNode)} received from broadcast: {c}"));
            var deepSubscription = deepNode.Subscribe(leaf, (c, h) => Console.WriteLine($"{nameof(deepNode)} received @{leaf} for {h.TargetChannel}: {c}"));

            var superNode = new IrisServerLocalNode();
            superNode.Connect(config);
            var superSubscriptionToBroadcast = superNode.SubscribeToBroadcast((c, h) => Console.WriteLine($"{nameof(superNode)} received from broadcast: {c}"));
            var superSubscription1 = superNode.Subscribe(root, (c, h) => Console.WriteLine($"{nameof(superNode)} received @{root} for {h.TargetChannel}: {c}"));
            var superSubscription2 = superNode.Subscribe(leaf, (c, h) => Console.WriteLine($"{nameof(superNode)} received @{leaf} for {h.TargetChannel}: {c}"));

            Console.WriteLine($"Current id: {local.Id}");
            Console.WriteLine($"{nameof(otherNode)} id: {otherNode.Id}");
            Console.WriteLine($"{nameof(deepNode)} id: {deepNode.Id}");
            Console.WriteLine($"{nameof(superNode)} id: {superNode.Id}");
            Console.WriteLine();
            Console.WriteLine("- Write \"SUB {channel}\" to subscribe to a channel");
            Console.WriteLine("- Write \"UNSUB {channel}\" to unsubscribe from a channel");
            Console.WriteLine("- Write \"SEND {message} {channel}\" to send a message to a channel");
            Console.WriteLine("- Use \"SEND-F\" to send a message to the whole hierarchy");
            Console.WriteLine("- Write \"SEND {message}\" to send a message in broadcast");
            Console.WriteLine("- Write \"QUIT\" or \"Q\" to quit and dispose the server");
            Console.WriteLine();

            string input;
            do
            {
                input = Console.ReadLine();
                string[] command = input.ToUpper().Split(' ');
                bool handled = false;

                if (command.Length > 0)
                {
                    switch (command[0])
                    {
                        case "SUB":
                            if (handled = command.Length == 2 && local.Subscribe(command[1], GenericContentHandler) != null)
                                Console.WriteLine("- Subscribed");
                            break;

                        case "UNSUB":
                            if (handled = command.Length == 2 && local.Unsubscribe(command[1], GenericContentHandler))
                                Console.WriteLine("- Unsubscribed");
                            break;

                        case "SEND":
                        case "SEND-F":
                            if (handled = (command.Length == 3 && local.Send(command[2], command[1], command[0] == "SEND-F")) ||
                                          (command.Length == 2 && local.SendToBroadcast(command[1])))
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

            superSubscription2?.Dispose();
            superSubscription1?.Dispose();
            superSubscriptionToBroadcast?.Dispose();
            deepSubscription?.Dispose();
            deepSubscriptionToBroadcast?.Dispose();
            otherSubscription?.Dispose();
            otherSubscriptionToBroadcast?.Dispose();

            superNode.Dispose();
            deepNode.Dispose();
            otherNode.Dispose();
            local.Dispose();
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
