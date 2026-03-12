using HtmlAgilityPack;

namespace WebScraper
{
    public sealed class PodiumLinkProvider
    {
        /*
        private readonly HttpClient _http;

        public PodiumLinkProvider(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<string>> GetAllVenueLinksAsync()
        {
            //Logs.Log("Running [GetAllVenueLinksAsync]\n");

            string? html = await _http.GetStringAsync("https://www.podiuminfo.nl/podium/");
            HtmlDocument? doc = new HtmlDocument();
            doc.LoadHtml(html);

            List<string>? links = doc.DocumentNode
                .SelectNodes("//a[contains(@href,'/podium/')]")?
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(h => h.Count(c => c == '/') >= 4) // filter detail links
                .Distinct()
                .Select(h => h.StartsWith("http") ? h : $"https://www.podiuminfo.nl{h}")
                .ToList();

            return links ?? new List<string>();
        }
        */
    }
}
