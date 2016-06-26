using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server.ConsoleApplicationTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"{typeof(Program).Namespace}");
            Console.Write("Press Enter to start");
            Console.ReadLine();
            Console.WriteLine("Started");

            IrisServer server = new IrisServer(new IrisPubSubRouter());
            server.Start(22000);
            Console.Write("Terminate...");
            Console.ReadLine();
        }
    }
}
