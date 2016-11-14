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
            PerfTest();
            Console.ReadLine();
        }

        private static async void PerfTest()
        {
            string channel = "perf";
            
            IrisServer server = new IrisServer();
            server.Start(22000);
            Console.WriteLine("Server started");

            IrisLocalNode echoNode = new IrisLocalNode();
            echoNode.Connect(server.GetServerConfig());
            Console.WriteLine("Echo node connected");

            var asyncSubscription = echoNode.Subscribe(channel, (c, h) =>
            {
                if (!h.Unsubscribing)
                {
                    var echoMessage = $"ECHO: {c}";
                    //Console.WriteLine(echoMessage);
                    echoNode.Publish(channel, echoMessage);
                }
            });
        }

        static void GenericContentHandler(object content, IrisContextHook hook)
        {
            Console.WriteLine($"Content: {content} [for {hook.TargetChannel}, on {hook.PublicationDateTime}]");
        }
    }
}
