using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris.NET.Server.ConsoleApplicationTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            PerfTest();
            Console.ReadLine();
        }

        private static void PerfTest()
        {
            string channel = "perf";
            
            IrisServer server = new IrisServer();
            server.Start(22000);
            Console.WriteLine("Server started");

            IrisLocalNode echoNode = new IrisLocalNode();
            echoNode.Connect(server.GetServerConfig());
            Console.WriteLine("Echo node connected");

            echoNode.OnDisposed += () =>
            {
                server.Stop();
            };

            System.Threading.Tasks.Task<IDisposableSubscription> asyncSubscription = null;
            asyncSubscription = echoNode.Subscribe(channel, (c, h) =>
            {
                if (!h.Unsubscribing)
                {
                    var echoMessage = $"ECHO: {c}";
                    echoNode.Publish(channel, echoMessage);

                    if ((c as Test)?.Data == null && (c as Test)?.Message == null)
                    {
                        var sub = asyncSubscription.Result;
                        sub.Dispose();
                    }
                }
                else
                {
                    echoNode.Dispose();
                }
            });
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
