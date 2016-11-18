﻿using System;
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
                if (cmd.ToUpper() == "A")
                {
                    PerfAnalysis(receivedMessagesCount, start.Value, DateTime.Now);
                }
                else if (cmd.ToUpper() == "S")
                {
                    var task = actorNode.Publish(channel, "TEST");
                }
                else
                {
                    Console.WriteLine($"{nameof(receivedMessagesCount)}: {receivedMessagesCount}");
                }

                Console.WriteLine($"{receivedMessagesCount * 100.0 / messagesCount}% received");
            }
        }

        static string channel;
        static IrisClientNode actorNode;
        static volatile int messagesCount;
        static volatile int receivedMessagesCount;
        static volatile object sync = new object();
        static DateTime? start = null;

        private static async void PerfTest()
        {
            channel = "perf";

            await TaskEx.Delay(1000); // Delay for the server to spawn.
            
            IrisClientConfig config = new IrisClientConfig
            {
                Hostname = "127.0.0.1",
                Port = 22000,
            };

            actorNode = new IrisClientNode();
            actorNode.Connect(config);
            Console.WriteLine("Actor node connected");

            messagesCount = 1000;
            start = null;

            receivedMessagesCount = 0;
            Task<IDisposableSubscription> asyncSubscription = null;
            asyncSubscription = actorNode.Subscribe(channel, (c, h) =>
            {
                if (!h.Unsubscribing)
                {
                    lock (sync)
                    {
                        receivedMessagesCount++;
                    }

                    if (receivedMessagesCount % (messagesCount / 10) == 0)
                        Console.WriteLine($"Received: {c} ({receivedMessagesCount * 100.0 / messagesCount}%)");

                    if (receivedMessagesCount == messagesCount)
                    {
                        var end = DateTime.Now;
                        var sub = asyncSubscription.Result; // Brutal!
                        sub.Dispose();

                        Task.Factory.StartNew(async () =>
                        {
                            await actorNode.Publish(channel, new Test());
                            actorNode.Dispose();
                        });

                        PerfAnalysis(messagesCount, start.Value, end);
                    }
                }
            });

            IDisposableSubscription subscription = await asyncSubscription;

            Console.WriteLine("Starting perf test");
            Task[] publishTasks = new Task[messagesCount];
            start = DateTime.Now;
            object bigload = new byte[((int)(2 * 1024))];
            for (int i = 0; i < messagesCount; i++)
            {
                var message = $"message#{i + 1}";
                var task = actorNode.Publish(channel, new Test { Message = message, Data = bigload });
                publishTasks[i] = task;
            }

            await TaskEx.WhenAll(publishTasks);

            Console.WriteLine($"Sent {messagesCount} messages");
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
