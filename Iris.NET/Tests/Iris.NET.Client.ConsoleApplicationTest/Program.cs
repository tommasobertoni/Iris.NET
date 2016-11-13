using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iris.NET.Client.ConsoleApplicationTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            PerfTest();
            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd == "A")
                {
                    PerfAnalysis(receivedMessagesCount, start.Value, DateTime.Now);
                }
                else
                {
                    Console.WriteLine($"{nameof(receivedMessagesCount)}: {receivedMessagesCount}");
                }
            }
        }

        static volatile int receivedMessagesCount;
        static DateTime? start = null;

        private static async void PerfTest()
        {
            string channel = "perf";

            await TaskEx.Delay(1000); // Delay for the server to spawn.
            

            IrisClientConfig config = new IrisClientConfig
            {
                Hostname = "127.0.0.1",
                Port = 22000,
            };

            IrisClientNode actorNode = new IrisClientNode();
            actorNode.Connect(config);
            Console.WriteLine("Actor node connected");

            int messagesCount = 100000;
            start = null;

            receivedMessagesCount = 0;
            Task<IDisposableSubscription> asyncSubscription = null;
            asyncSubscription = actorNode.Subscribe(channel, (c, h) =>
            {
                if (!h.Unsubscribing)
                {
                    receivedMessagesCount++;

                    //if (receivedMessagesCount % 1000 == 0)
                        //Console.WriteLine($"Received: {c}");

                    if (receivedMessagesCount == messagesCount)
                    {
                        var end = DateTime.Now;
                        var sub = asyncSubscription.Result; // Brutal!
                        sub.Dispose();
                        PerfAnalysis(messagesCount, start.Value, end);
                    }
                }
            });

            IDisposableSubscription subscription = await asyncSubscription;

            Console.WriteLine("Starting perf test");
            Task[] publishTasks = new Task[messagesCount];
            start = DateTime.Now;
            for (int i = 0; i < messagesCount; i++)
            {
                var task = actorNode.Publish(channel, $"message#{i + 1}");
                publishTasks[i] = task;
            }

            Console.WriteLine($"Sent {messagesCount} messages");

            await TaskEx.WhenAll(publishTasks);
        }

        private static void PerfAnalysis(int messagesCount, DateTime start, DateTime end)
        {
            var msLapsed = (end - start).TotalMilliseconds;
            var avg = msLapsed / messagesCount * 1.0;
            Console.WriteLine($"Results: {messagesCount} messages, started at {start}, finished at {end}");
            Console.WriteLine($"Average bidirectional time: {avg} milliseconds");
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
