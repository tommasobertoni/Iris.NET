using Iris.NET.Collections;
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
            // NetworkTest();
            DataStructuresTest();
        }

        private static void DataStructuresTest()
        {
            IChannelsSubscriptionsDictionary<string> csd = new IrisChannelsSubscriptionsDictionary<string>();
            Console.WriteLine("- Empty"); Console.WriteLine(csd); Console.WriteLine();

            string james = "James";
            csd.Add("main", james);
            Console.WriteLine("- James in main"); Console.WriteLine(csd); Console.WriteLine();

            csd.Add(new string[] { "main", "submain"}, james);
            var adam = "Adam";
            csd.Add(new string[] { "main", "submain" }, adam);
            Console.WriteLine("- James and Adam in submain"); Console.WriteLine(csd); Console.WriteLine();

            var anna = "Anna";
            csd.Add("parallel", anna);
            csd.Add("parallel", james);
            csd.Add("parallel", adam);
            Console.WriteLine("- James, Adam and Anna in parallel"); Console.WriteLine(csd); Console.WriteLine();

            var stuart = "Stuart";
            csd.Add(new string[] { "parallel", "subparallel" }, stuart);
            Console.WriteLine("- Stuart in subparallel"); Console.WriteLine(csd); Console.WriteLine();

            csd.Add(new string[] { "main", "submain", "extrasub" }, stuart);
            Console.WriteLine("- Stuart in extrasub"); Console.WriteLine(csd); Console.WriteLine();

            csd.RemoveChannel("main/submain");
            Console.WriteLine("- Removed main/submain"); Console.WriteLine(csd); Console.WriteLine();

            csd.Add(new string[] { "main", "submain", "extrasub" }, stuart);
            Console.WriteLine("- Stuart in extrasub"); Console.WriteLine(csd); Console.WriteLine();

            Console.Write("Press ENTER to terminate...");
            Console.ReadLine();
        }

        private static void NetworkTest()
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
            while (!serverThread.IsAlive) ;

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
