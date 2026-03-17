//using Polly;
//using Polly.Extensions.Http;
using static WebScraper.Objects;

namespace WebScraper
{
    partial class Program
    {
        /// <summary>
        /// Main asynchronous entry point. Runs a limited crawl for testing,
        /// saves results to JSON and logs progress.
        /// </summary>
        static async Task Main()
        {
            Logs.Log("Running program Webscraper...");

            // Create crawler instance and perform a short crawl (5 venues)
            VenueCrawler crawler = new VenueCrawler();

            List<Venue> venues = await crawler.Crawl5Venues();

            // Persist the collected venues to a JSON file
            VenueCrawler.SaveJson(venues, "venues.json");

            Logs.Log($"\n[Program.Main] > Saved {venues.Count} venues");
        }
    }
}
