using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace WebScraper;

/// <summary>
/// Crawls venue pages from the Podiuminfo website and parses them into Venue objects.
/// The crawler first collects all venue URLs and then visits each venue page
/// to extract structured data.
/// </summary>
/// <summary>
/// Crawl Podiuminfo venue pages and parse them into <see cref="Objects.Venue"/> instances.
/// The crawler collects venue links from alphabetical index pages and visits each
/// venue page to extract structured data using <see cref="VenueParser"/>.
/// </summary>
public class VenueCrawler
{
    /// <summary>
    /// Shared HttpClient used for all HTTP requests.
    /// </summary>
    private readonly HttpClient _http = new HttpClient();

    /// <summary>
    /// Base URL for the Podiuminfo website.
    /// </summary>
    private const string BaseUrl = "https://www.podiuminfo.nl";

    /// <summary>
    /// Limits the number of concurrent requests.
    /// Prevents triggering rate limiting by the server.
    /// </summary>
    private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(2);

    /// <summary>
    /// Initializes the crawler and configures a user-agent header
    /// so requests resemble a normal browser request.
    /// </summary>
    public VenueCrawler()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; VenueCrawler/1.0)");
    }

    /// <summary>
    /// Crawl up to 5 venue pages and return parsed venues.
    /// This method is intentionally limited for testing and demonstrates
    /// the crawling and parsing workflow.
    /// </summary>
    /// <returns>List of parsed <see cref="Objects.Venue"/> objects.</returns>
    public async Task<List<Objects.Venue>> Crawl5Venues()
    {
        // Collect all venue URLs first
        HashSet<string> venueLinks = await CollectVenueLinks();

        Logs.Log($"Total venue links found: {venueLinks.Count}");

        // List to store parsed venues
        List<Objects.Venue> venues = new List<Objects.Venue>();

        int countParsed = 0;
        int countSkipped = 0;

        // Visit every discovered venue link (stopping at 5 parsed venues)
        foreach (string url in venueLinks)
        {
            // Temporary limit of 5 venues for testing
            if (countParsed >= 5)
            {
                Logs.Log($"[VenueCrawler.Crawl5Venues]  Reached 5 venues. Skipped {countSkipped} venues. Stop Program...");
                break;
            }

            try
            {
                // Download the venue page with retry logic
                string html = await GetPageWithRetry(url);

                // Parse the downloaded HTML into a Venue object
                Objects.Venue? venue = VenueParser.Parse(html);

                if (venue == null)
                {
                    Logs.Log($"[VenueCrawler.Crawl5Venues]  Parse returned NULL:    {url}");
                }
                else if (string.IsNullOrWhiteSpace(venue.Email))
                {
                    // Skip venues without email addresses
                    countSkipped++;
                    Logs.Log($"[VenueCrawler.Crawl5Venues] [SKIPPED]    No Email    {venue.Name}, {venue.City}: {url}");
                }
                else
                {
                    // Keep parsed venue and update counter
                    venues.Add(venue);
                    countParsed++;

                    Logs.Log($"[VenueCrawler.Crawl5Venues]  Parsed venue #{countParsed}:    {url}");
                }
            }
            catch (Exception ex)
            {
                // Log individual page failures and continue
                Logs.Log($"[VenueCrawler.Crawl5Venues]  Failed  {url}:   {ex.Message}");
            }

            // Random delay between requests to avoid server rate limiting
            Random rnd = new Random();
            await Task.Delay(rnd.Next(4000, 8000));
        }

        return venues;
    }

    #region === TEST ===
    /*
    -----------------------------------------------------------------------
    Parallel crawler version (currently disabled)

    This version performs parallel crawling using SemaphoreSlim to limit
    concurrency. It significantly improves performance but increases the
    chance of server rate limiting.
    -----------------------------------------------------------------------
    */

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
                await Task.Delay(rnd.Next(5000, 8000));

                _rateLimiter.Release();
            }
        });

        await Task.WhenAll(tasks);

        return venues.ToList();
    }
    */
    #endregion

    /// <summary>
    /// Collects all venue page links from the alphabetical listing pages.
    /// 
    /// Example pages:
    /// https://www.podiuminfo.nl/podium/letter/a/
    /// https://www.podiuminfo.nl/podium/letter/b/
    /// </summary>
    private async Task<HashSet<string>> CollectVenueLinks()
    {
        // Letters used by the Podiuminfo venue index
        List<string> letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray()
            .Select(c => c.ToString())
            .ToList();

        // Extra category found "overig", used by the site
        letters.Add("overig");

        HashSet<string> links = new HashSet<string>();

        foreach (string letter in letters)
        {
            string url = $"{BaseUrl}/podium/letter/{letter}/";

            Logs.Log($"[VenuwCrawler.CollectVenueLinks] Scanning    {url}");

            // Download index page and extract links to individual venues
            string html = await GetPageWithRetry(url);
            List<string> extracted = ExtractVenueLinks(html);

            foreach (string link in extracted)
                links.Add(link);

            // Be kind to the server: small delay between requests
            await Task.Delay(1500);
        }

        Logs.Log($"\n[VenuwCrawler.CollectVenueLinks] Returned [{links.Count}] unique venue links\n");

        return links;
    }

    /// <summary>
    /// Extracts venue URLs from an index page.
    /// 
    /// The method scans all anchor tags and selects those matching
    /// the pattern /podium/{id}/
    /// </summary>
    private static List<string> ExtractVenueLinks(string html)
    {
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(html);

        HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//a[@href]");

        List<string> results = new List<string>();

        if (nodes == null)
            return results;

        foreach (HtmlNode node in nodes)
        {
            string href = node.GetAttributeValue("href", "");

            // Only accept venue links of the form /podium/{id}/
            if (!Regex.IsMatch(href, @"\/podium\/\d+\/"))
                continue;

            // Convert relative links to absolute URLs when necessary
            if (!href.StartsWith("http"))
                href = BaseUrl + href;

            results.Add(href);
        }

        // Remove duplicates and return
        return results.Distinct().ToList();
    }

    /// <summary>
    /// Downloads a webpage with retry logic.
    /// 
    /// If the request fails, it will retry up to 3 times before throwing
    /// an exception.
    /// </summary>
    private async Task<string> GetPageWithRetry(string url)
    {
        // Retry up to 3 times with a delay between attempts
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Logs.Log($"[VanueCrawler.GetPageWithRetry]  Requesting  {url}");

                return await _http.GetStringAsync(url);
            }
            catch
            {
                Logs.Log($"[VanueCrawler.GetPageWithRetry]  Retry [{i + 1}] {url}");
                // Wait before retrying 3000 ms
                await Task.Delay(3000);
            }
        }

        throw new Exception($"[VanueCrawler.GetPageWithRetry]   Failed after 3 retries  {url}");
    }

    /// <summary>
    /// Saves the parsed venues to a JSON file.
    /// </summary>
    public static void SaveJson(List<Objects.Venue> venues, string path)
    {
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            // Format the JSON with indentation for readability
            WriteIndented = true 
        };

        string json = JsonSerializer.Serialize(venues, options);

        File.WriteAllText(path, json);
    }
}