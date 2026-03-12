using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace WebScraper;

public class VenueCrawler
{
    private readonly HttpClient _http = new HttpClient();
    private const string BaseUrl = "https://www.podiuminfo.nl";

    private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(2);
    // max 4 parallel requests

    public VenueCrawler()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; VenueCrawler/1.0)");
    }

    public async Task<List<Objects.Venue>> CrawlAllVenues()
    {
        var venueLinks = await CollectVenueLinks();

        Logs.Log($"Total venue links found: {venueLinks.Count}");

        var venues = new List<Objects.Venue>(); // Normal list to maintain order
        int count = 0;

        foreach (var url in venueLinks)
        {
            if (count >= 5)
            {
                Logs.Log("Reached 5 venues, stopping...");
                break;
            }

            try
            {
                var html = await GetPageWithRetry(url);

                var venue = VenueParser.Parse(html);

                if (venue == null)
                {
                    Logs.Log($"Parse returned NULL: {url}");
                }
                else
                {
                    venues.Add(venue);
                    count++;
                    Logs.Log($"Parsed {count}: {url}");
                }
            }
            catch (Exception ex)
            {
                Logs.Log($"Failed {url} : {ex.Message}");
            }

            // random delay to avoid rate limits
            Random rnd = new Random();
            await Task.Delay(rnd.Next(4000, 8000));
        }

        return venues;
    }

    /*
    public async Task<List<Objects.Venue>> CrawlAllVenues()
    {
        var venueLinks = await CollectVenueLinks();

        Logs.LogWriteLine($"Total venue links: {venueLinks.Count}");

        var venues = new ConcurrentBag<Objects.Venue>();

        var tasks = venueLinks.Select(async url =>
        {
            await _rateLimiter.WaitAsync();

            try
            {
                var html = await GetPageWithRetry(url);

                var venue = VenueParser.Parse(html);

                if (venue != null)
                    venues.Add(venue);

                Logs.LogWriteLine($"Parsed {url}");
            }
            catch (Exception ex)
            {
                Logs.LogWriteLine($"Failed {url} : {ex.Message}");
            }
            finally
            {
                Random rnd = new Random();
                await Task.Delay(rnd.Next(5000, 8000)); // Random delay between 5-8 seconds
                _rateLimiter.Release();
            }
        });

        await Task.WhenAll(tasks);

        return venues.ToList();
    }
    */

    private async Task<HashSet<string>> CollectVenueLinks()
    {
        var letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray()
            .Select(c => c.ToString())
            .ToList();

        letters.Add("overig");

        var links = new HashSet<string>();

        foreach (var letter in letters)
        {
            var url = $"{BaseUrl}/podium/letter/{letter}/";

            Logs.Log($"> Scanning {url}");

            var html = await GetPageWithRetry(url);

            var extracted = ExtractVenueLinks(html);

            foreach (var l in extracted)
                links.Add(l);

            await Task.Delay(1500);
        }

        Logs.Log($"Returned {links.Count} unique venue links");
        return links;
    }

    private List<string> ExtractVenueLinks(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodes = doc.DocumentNode.SelectNodes("//a[@href]");

        var results = new List<string>();

        if (nodes == null)
            return results;

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "");

            if (!Regex.IsMatch(href, @"\/podium\/\d+\/"))
                continue;

            if (!href.StartsWith("http"))
                href = BaseUrl + href;

            results.Add(href);
        }

        return results.Distinct().ToList();
    }

    private async Task<string> GetPageWithRetry(string url)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Logs.Log($"Requesting ({i}) {url}");

                return await _http.GetStringAsync(url);
            }
            catch
            {
                Logs.Log($"Retry {i + 1} for {url}");
                await Task.Delay(3000);
            }
        }

        throw new Exception($"Failed after retries: {url}");
    }

    public static void SaveJson(List<Objects.Venue> venues, string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(venues, options);

        File.WriteAllText(path, json);
    }
}