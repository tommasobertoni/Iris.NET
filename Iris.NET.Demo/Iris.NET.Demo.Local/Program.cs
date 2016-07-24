using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Demo.Local
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new TestClass();

            Console.WriteLine("/Local (no network) demo of Iris.NET.Server/\n");

            test.RunFullTest();

            Console.Write("Demo terminated, press ENTER to exit...");
            Console.ReadLine();
        }
    }
}
