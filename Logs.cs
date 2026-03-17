using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace WebScraper
{
    public class Logs
    {
        /// <summary>
        /// Simple logging helper used across the scraper.
        /// Writes the provided message to the console and the debugger output.
        /// Keep this lightweight: production code should use a proper logging framework.
        /// </summary>
        /// <param name="message">Message to be logged.</param>
        public static void Log(string message)
        {
            // Print to console for user visibility
            Console.WriteLine($"{message}");

            // Also write to Debug output for development inspection
            Debug.WriteLine($"{message}");
        }
    }
}
