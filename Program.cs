//using Polly;
//using Polly.Extensions.Http;
using static WebScraper.Objects;

namespace WebScraper
{
    partial class Program
    {
        static async Task Main()
        {
            Logs.Log("Running program Webscraper...");

            VenueCrawler crawler = new VenueCrawler();

            List<Venue> venues = await crawler.CrawlAllVenues();

            VenueCrawler.SaveJson(venues, "venues.json");

            Logs.Log($"Saved {venues.Count} venues");

            /*
            using HttpClient client = new HttpClient();

            VenueParser parser = new VenueParser();
            PodiumLinkProvider linkProvider = new PodiumLinkProvider(client);
            ScraperService scraper = new ScraperService(client);

            List<string> links = await linkProvider.GetAllVenueLinksAsync();
            List<Venue> venues = await scraper.ScrapeAsync(links);

            Logs.Log($"Scraped {venues.Count} venues:\n");

            foreach (var item in venues)
            {
                //Logs.Log("==========");
                Logs.Log($"Name: {item.Name}");
                Logs.Log($"URL: {item.Url}");
                Logs.Log($"Website: {item.SameAs}");
                Logs.Log($"Email: {item.Email}");
                Logs.Log($"Street: {item.Street}");
                Logs.Log($"City: {item.City}");
                Logs.Log($"Region: {item.Region}");
                Logs.Log($"Postal Code: {item.PostalCode}");
                Logs.Log($"Country: {item.Country}");
                //Logs.Log($"Latitude: {item.Latitude}");
                //Logs.Log($"Longitude: {item.Longitude}");
                Logs.Log(new string('-', 40));
            }
            */
        }
    }
}
