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
            Console.WriteLine($"{typeof(Program).Namespace}");
            if (args?.Length == 0)
            {
                Console.Write("Press Enter to start");
                Console.ReadLine();
            }
            Console.WriteLine("Started\n");

            IrisServer server = new IrisServer(new IrisPubSubRouter());
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
    }
}
