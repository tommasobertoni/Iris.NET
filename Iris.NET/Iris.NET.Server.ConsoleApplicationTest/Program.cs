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
            Console.WriteLine("Main started\n\n");

            IrisServer server = new IrisServer(22000);
            server.Start();
        }
    }
}
