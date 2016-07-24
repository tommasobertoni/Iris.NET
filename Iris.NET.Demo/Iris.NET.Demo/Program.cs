using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new LocalTestClass();

            Console.WriteLine("/Local (no network) demo of Iris.NET.Server/\n");

            test.RunFullTest();

            Console.Write("Demo terminated, press ENTER to exit...");
            Console.ReadLine();
        }
    }
}
