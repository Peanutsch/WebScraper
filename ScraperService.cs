using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using static WebScraper.Objects;

namespace WebScraper
{
    /// <summary>
    /// Service that scrapes venue pages with concurrency, rate-limiting and retry logic.
    /// </summary>
    public sealed class ScraperService
    {
        /*
        private readonly HttpClient _http;
        //private readonly VenueParser _parser;

        // Maximum number of concurrent requests
        private readonly SemaphoreSlim _concurrency = new(2);

        // Rate limiter: ensures minimum spacing between requests (used globally)
        private readonly SemaphoreSlim _rateLimiter = new(1, 1);

        // Maximum number of attempts for a single request (initial attempt + retries)
        private const int MaxAttempts = 5;

        // Base delay for retries
        private readonly int delayBaseSeconds = 10; 

        /// <summary>
        /// Constructor accepting an HttpClient and a parser.
        /// </summary>
        public ScraperService(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Scrape multiple URLs concurrently and return parsed Venue objects.
        /// </summary>
        public async Task<List<Venue>> ScrapeAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
        {
            //Logs.Log("Running [ScrapeAsync]\n");

            ConcurrentBag<Venue> results = new ConcurrentBag<Venue>();

            // Start a processing task for each URL (concurrency controlled inside ProcessUrlAsync)
            IEnumerable<Task> tasks = urls.Select(url =>
                ProcessUrlAsync(url, results, cancellationToken));

            await Task.WhenAll(tasks);

            return results.ToList();
        }

        /// <summary>
        /// Process a single URL: acquire concurrency slot, fetch the page with retries, parse and store result.
        /// </summary>
        private async Task ProcessUrlAsync(string url, ConcurrentBag<Venue> results, CancellationToken cancellationToken)
        {
            //Logs.Log("Running [ProcessUrlAsync]\n");

            // Wait for an available concurrency slot
            await _concurrency.WaitAsync(cancellationToken);

            try
            {
                // Execute the HTTP request with built-in retry logic
                HttpResponseMessage response = await ExecuteRequestAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logs.LogWriteLine($"Failed: {url} ({response.StatusCode})");
                    return;
                }

                // Read response content and parse into a Venue
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                var venue = VenueParser.Parse(html);

                if (venue != null)
                {
                    Logs.LogWriteLine($"Parsed: {venue.Name} from {url}");
                    results.Add(venue);
                }
                    
            }
            finally
            {
                // Release concurrency slot regardless of success/failure
                _concurrency.Release();
            }
        }

        /// <summary>
        /// Execute an HTTP GET with rate-limiting and a custom retry loop that honors Retry-After.
        /// Returns the final HttpResponseMessage (successful or final failed response).
        /// </summary>
        private async Task<HttpResponseMessage> ExecuteRequestAsync(string url, CancellationToken cancellationToken)
        {
            // Global slot to serialize spacing between requests
            await _rateLimiter.WaitAsync(cancellationToken);

            try
            {
                // Base minimum spacing before issuing the request

                await Task.Delay(TimeSpan.FromSeconds(delayBaseSeconds), cancellationToken);
                Logs.LogWriteLine($"Issuing request to {url} after base delay of {delayBaseSeconds}s");

                int attempt = 0;

                while (true)
                {
                    attempt++;

                    HttpResponseMessage? response = null;
                    Exception? requestException = null;

                    try
                    {
                        response = await _http.GetAsync(url, cancellationToken);
                    }
                    catch (HttpRequestException ex) when (attempt < MaxAttempts)
                    {
                        requestException = ex;
                    }

                    if (requestException != null)
                    {
                        // Exponential backoff for network errors with jitter
                        var baseSeconds = Math.Min(Math.Pow(2, attempt), 300); // cap 5 minutes
                        var jitter = 0.8 + Random.Shared.NextDouble() * 0.4; // 0.8 .. 1.2
                        var backoff = TimeSpan.FromSeconds(Math.Max(30, baseSeconds * jitter)); // at least 30s
                        Logs.LogWriteLine($"Request exception on attempt {attempt}: {requestException.Message}. Backing off {backoff.TotalSeconds:F0}s");
                        await Task.Delay(backoff, cancellationToken);
                        continue;
                    }

                    if (response == null)
                        throw new InvalidOperationException("Unexpected null response and no exception.");

                    if (response.IsSuccessStatusCode)
                        return response;

                    bool is429 = response.StatusCode == HttpStatusCode.TooManyRequests;
                    bool isServerError = (int)response.StatusCode >= 500;
                    bool shouldRetry = is429 || isServerError;

                    if (!shouldRetry || attempt >= MaxAttempts)
                        return response;

                    // Compute delay: prefer Retry-After header when present
                    TimeSpan delay;
                    if (response.Headers?.RetryAfter?.Delta != null)
                    {
                        delay = response.Headers.RetryAfter.Delta.Value;
                    }
                    else if (response.Headers?.RetryAfter?.Date != null)
                    {
                        var date = response.Headers.RetryAfter.Date.Value;
                        var now = DateTimeOffset.UtcNow;
                        delay = date > now ? date - now : TimeSpan.FromSeconds(30);
                    }
                    else
                    {
                        // Default: start at 30s for 429, exponential for 5xx, cap at 10 minutes
                        double baseSeconds = is429 ? 30 : Math.Pow(2, attempt);
                        baseSeconds = Math.Min(baseSeconds, 600);

                        // Add jitter ±20%
                        var jitterFactor = 0.8 + Random.Shared.NextDouble() * 0.4; // 0.8 .. 1.2
                        delay = TimeSpan.FromSeconds(Math.Max(30, baseSeconds * jitterFactor));
                    }

                    Logs.LogWriteLine($"Retry {attempt} after {delay.TotalSeconds:F0}s (Status: {response.StatusCode})");

                    // Dispose response before waiting to free sockets
                    response.Dispose();

                    // Wait computed delay (non-zero)
                    var wait = delay.TotalMilliseconds < 1 ? TimeSpan.FromSeconds(30) : delay;
                    await Task.Delay(wait, cancellationToken);

                    // After waiting, enforce the global minimum spacing again before next request
                    await Task.Delay(TimeSpan.FromSeconds(0), cancellationToken); // no-op placeholder: rate limiter already serializes
                }
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        */
    }
}
