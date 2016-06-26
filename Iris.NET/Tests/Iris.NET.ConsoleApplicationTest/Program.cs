using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Iris.NET.ConsoleApplicationTest
{
    class Program
    {
        static string killWord = "KC";

        static void Main(string[] args)
        {
            var serverThread = new Thread(RunServer);
            
            Console.WriteLine("Press Enter to start (or insert clients count below)");
            var parametersString = Console.ReadLine();
            var clientsCount = 0;
            int.TryParse(parametersString, out clientsCount);
            if (clientsCount < 1) clientsCount = 1;

            Process[] clients = new Process[clientsCount];
            for (int i = 0; i < clientsCount; i++)
            {
                clients[i] = Process.Start(@"Iris.NET.Client.ConsoleApplicationTest.exe", "custom");
            }

            Console.WriteLine($"Write \"{killWord}\" and press Enter to terminate ALL clients and exit");
            Console.WriteLine("-- Note: the first input is handled by this main program, the second by the server thread\n");
            serverThread.Start();
            while (!serverThread.IsAlive);

            string input = null;
            do
            {
                if (input != null)
                    Console.Write("Command (for server): ");

                input = Console.ReadLine();
            } while (input?.ToUpper() != "KC");

            foreach (var client in clients)
                if (!client.HasExited)
                    client.Kill();

            Console.WriteLine("Waiting for server thread to finish");
            serverThread.Join();
        }

        private static void RunServer()
        {
            Server.ConsoleApplicationTest.Program.Main(new string[] { killWord });
        }
    }
}
