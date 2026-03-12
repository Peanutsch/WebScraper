using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WebScraper
{
    public class Logs
    {
        public static void Log(string message)
        {
            Console.WriteLine($"{message}");
            Debug.WriteLine($"{message}");
        }
    }
}
