using Iris.NET.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Iris.NET.Demo.Local
{
    class Program
    {
        static string channel = "chatroom";

        static void Main(string[] args)
        {
            Console.WriteLine("/Local (no network) demo of Iris.NET.Server/\n");

            IrisServerConfig config = new IrisServerConfig(new IrisPubSubRouter());

            IrisServerLocalNode localNode1 = new IrisServerLocalNode();
            IrisServerLocalNode localNode2 = new IrisServerLocalNode();
            IrisServerLocalNode localNode3 = new IrisServerLocalNode();

            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();
            List<IDisposableSubscription> result;

            Console.WriteLine($"- {nameof(Setup)}");
            Setup(config, localNode1, localNode2, localNode3);
            Console.WriteLine("\n");

            Console.WriteLine($"- {nameof(UseCase1_BroadcastCommunication).Replace("_", ": ")}");
            result = UseCase1_BroadcastCommunication(localNode1, localNode2);
            disposableSubscriptions.AddRange(result);
            WaitALittle();
            Console.WriteLine("\n");

            Console.WriteLine($"- {nameof(UseCase2_ChatRoom).Replace("_", ": ")}");
            result = UseCase2_ChatRoom(localNode1, localNode2, localNode3);
            disposableSubscriptions.AddRange(result);
            WaitALittle();
            Console.WriteLine($"// Notice that {nameof(localNode3)} didn't receive the message because it isn't subscribed to the broadcast communication.");
            Console.WriteLine("\n");

            Console.WriteLine($"- {nameof(Unsubscribe)}");
            Unsubscribe(disposableSubscriptions, localNode1);
            Console.WriteLine("\n");
            
            Console.Write("Demo terminated, press ENTER to exit...");
            Console.ReadLine();
        }

        private static void Setup(IrisServerConfig config, params IrisServerLocalNode[] localNodes)
        {
            bool[] results = new bool[localNodes.Length];

            for (int i = 0; i < localNodes.Length; i++)
                results[i] = localNodes[i].Connect(config);

            CheckThatEverythingIsOk(results);
            Console.WriteLine($"All the nodes are connected.");
        }

        private static List<IDisposableSubscription> UseCase1_BroadcastCommunication(IrisServerLocalNode localNode1, IrisServerLocalNode localNode2)
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();
            
            var subscription1 = localNode1.SubscribeToBroadcast((content, hook) => Console.WriteLine($"{nameof(localNode1)} received \"{content.ToString()}\" from broadcast."));
            bool result1 = subscription1 != null;
            var subscription2 = localNode2.SubscribeToBroadcast((content, hook) => Console.WriteLine($"{nameof(localNode2)} received \"{content.ToString()}\" from broadcast."));
            bool result2 = subscription2 != null;

            CheckThatEverythingIsOk(result1, result2);
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription2);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode2)} are subscribed to the broadcast.");

            string message = "Hello!";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" in broadcast.");
            result1 = localNode1.SendToBroadcast(message);

            CheckThatEverythingIsOk(result1);
            return disposableSubscriptions;
        }

        private static List<IDisposableSubscription> UseCase2_ChatRoom(IrisServerLocalNode localNode1, IrisServerLocalNode localNode2, IrisServerLocalNode localNode3)
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();

            var subscription1 = localNode1.Subscribe(channel,
                (content, hook) => Console.WriteLine($"{nameof(localNode1)} received \"{content.ToString()}\" from \"{channel}\""));
            bool result1 = subscription1 != null;

            var subscription2 = localNode2.Subscribe(channel,
                (content, hook) => Console.WriteLine($"{nameof(localNode2)} received \"{content.ToString()}\" from \"{channel}\""));
            bool result2 = subscription2 != null;

            var subscription3 = localNode3.Subscribe(channel,
                (content, hook) => Console.WriteLine($"{nameof(localNode3)} received \"{content.ToString()}\" from \"{channel}\""));
            bool result3 = subscription3 != null;

            CheckThatEverythingIsOk(result1, result2, result3);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode2)}, {nameof(localNode3)} are subscribed to the \"{channel}\" channel.");
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription2);
            disposableSubscriptions.Add(subscription3);

            string message = $"Hi! my name is {nameof(localNode1)}";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" to the \"{channel}\" channel.");
            result1 = localNode1.Send(channel, message);
            CheckThatEverythingIsOk(result1);

            WaitALittle();
            Console.WriteLine();

            message = $"{message} [in broadcast]";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" in broadcast.");
            result1 = localNode1.SendToBroadcast(message);
            CheckThatEverythingIsOk(result1);

            return disposableSubscriptions;
        }

        private static void Unsubscribe(List<IDisposableSubscription> disposableSubscriptions, IrisServerLocalNode localNode1 = null)
        {
            disposableSubscriptions.ForEach(ds => ds.Dispose());
            Console.WriteLine("All the subscriptions have been disposed.");
            bool IDontBelieveIt = localNode1 != null;
            if (IDontBelieveIt)
            {
                Console.WriteLine("You don't believe it? Let's send a couple of messages...");
                var message = "Super-important-communication-that-cannot-be-ignored";
                localNode1.SendToBroadcast(message);
                localNode1.Send(channel, message);
                WaitALittle();
                Console.WriteLine("Nobody should have received any message.");
            }
        }

        private static void CheckThatEverythingIsOk(params bool[] results)
        {
            if (results.Any(r => !r))
            {
                Console.Write("Quit");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        private static void WaitALittle()
        {
            Thread.Sleep(300); // Gives some time to the asyncronous operations to complete
        }
    }
}
