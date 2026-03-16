using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WebScraper
{
    public class Logs
    {
        /// <summary>
        /// Logs a message to both the console and the debug output.
        /// </summary>
        /// <param name="message">Message to be logged</param>
        public static void Log(string message)
        {
            Console.WriteLine($"{message}");
            Debug.WriteLine($"{message}");
        }
    }
}
