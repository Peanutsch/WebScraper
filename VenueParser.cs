using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using static WebScraper.Objects;

namespace WebScraper;

/// <summary>
/// Parse HTML venue pages and return a <see cref="Venue"/> instance.
/// This class extracts structured JSON-LD (schema.org) data and falls back
/// to the contact section in the page for email/website information.
/// The implementation is intentionally compact and uses tuples for internal
/// transfer of extracted values.
/// </summary>
public sealed class VenueParser
{
    /// <summary>
    /// Parse the provided HTML and produce a <see cref="Venue"/> object.
    /// Returns null when required data (name) cannot be found.
    /// </summary>
    /// <param name="html">Raw HTML content of a venue page.</param>
    /// <returns>Populated <see cref="Venue"/> or null when parsing fails.</returns>
    public static Venue? Parse(string html)
    {
        // Build and load the HTML document with tolerant parsing options
        var doc = new HtmlDocument
        {
            OptionFixNestedTags = true, // help with malformed HTML
            OptionAutoCloseOnEnd = true // ensure document is well-formed
        };
        doc.LoadHtml(html);

        // Try to extract structured data first (preferred source)
        var json = ParseJsonLd(doc);
        if (string.IsNullOrWhiteSpace(json.Name))
            return null; // name is required

        // Extract contact details from page as a fallback for email/website
        var contact = ParseContactInfo(doc);

        // Compose final Venue object using JSON-LD values first, contact fallbacks
        return new Venue
        {
            Name = json.Name!,
            PodiumInfoURL = json.Url!,
            Email = contact.Email,
            VenueURL = json.Website ?? contact.Website,
            Street = json.Street,
            City = json.City,
            Region = json.Region,
            PostalCode = json.PostalCode,
            Country = json.Country
        };
    }

    /// <summary>
    /// Search for JSON-LD script blocks and extract venue data.
    /// Returns a tuple with common venue fields (Url, Name, Website, Street, City, Region, PostalCode, Country).
    /// </summary>
    /// <param name="doc">Parsed HTML document.</param>
    /// <returns>Tuple with extracted values or nulls when not present.</returns>
    // Returns (Url, Name, Website, Street, City, Region, PostalCode, Country)
    private static (string? Url, string? Name, string? Website, string? Street, string? City, string? Region, string? PostalCode, string? Country) ParseJsonLd(HtmlDocument doc)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts == null)
            return (null, null, null, null, null, null, null, null);

        foreach (var script in scripts)
        {
            try
            {
                // Parse the JSON content of the script block
                using var jsonDoc = JsonDocument.Parse(script.InnerText);
                var root = jsonDoc.RootElement;

                // Only consider JSON nodes that declare a schema.org type
                if (!root.TryGetProperty("@type", out var typeProp))
                    continue;
                if (typeProp.GetString() != "MusicVenue")
                    continue; // skip unrelated structured data

                string? url = root.TryGetProperty("url", out var p) ? p.GetString() : null;
                string? name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? website = root.TryGetProperty("sameAs", out var s) ? s.GetString() : null;

                string? street = null; string? postal = null; string? city = null; string? region = null; string? country = null;
                if (root.TryGetProperty("address", out var address))
                {
                    // Extract common address fields when present
                    street = address.TryGetProperty("streetAddress", out var sa) ? sa.GetString() : null;
                    postal = address.TryGetProperty("postalCode", out var pc) ? pc.GetString() : null;
                    city = address.TryGetProperty("addressLocality", out var al) ? al.GetString() : null;
                    region = address.TryGetProperty("addressRegion", out var ar) ? ar.GetString() : null;
                    country = address.TryGetProperty("addressCountry", out var ac) ? ac.GetString() : null;
                }

                return (url, name, website, street, city, region, postal, country);
            }
            catch
            {
                // ignore malformed JSON-LD blocks and continue searching
            }
        }

        return (null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// Extract email and website from the page contact section.
    /// Prefer Cloudflare-protected email if present, otherwise search visible text.
    /// </summary>
    /// <param name="doc">Parsed HTML document.</param>
    /// <returns>Tuple containing Email and Website or nulls when not found.</returns>
    // Returns (Email, Website)
    private static (string? Email, string? Website) ParseContactInfo(HtmlDocument doc)
    {
        var contactNode = doc.DocumentNode.SelectSingleNode("//section[contains(@class,'podium_contact')]");
        if (contactNode == null)
            return (null, null);

        // Cloudflare protected email
        var cf = contactNode.SelectSingleNode(".//a[@data-cfemail]");
        string? email = null;
        if (cf != null)
        {
            var encoded = cf.GetAttributeValue("data-cfemail", "");
            if (!string.IsNullOrEmpty(encoded))
                email = DecodeCloudflareEmail(encoded);
        }
        else
        {
            // Fallback: try to find a plain email address in the contact text
            email = ExtractEmail(contactNode.InnerText);
        }

        var websiteNode = contactNode.SelectSingleNode(".//a[starts-with(@href,'http')]");
        string? website = websiteNode?.GetAttributeValue("href", null!);

        return (email, website);
    }

    /// <summary>
    /// Attempt to locate a plain email address inside a text block using a regex.
    /// </summary>
    private static string? ExtractEmail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var m = Regex.Match(text, "[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Decode Cloudflare's email protection hex string.
    /// The first byte is the XOR key for the remaining bytes.
    /// </summary>
    private static string DecodeCloudflareEmail(string encoded)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < encoded.Length; i += 2)
            bytes.Add(Convert.ToByte(encoded.Substring(i, 2), 16));
        int key = bytes[0];
        char[] result = new char[bytes.Count - 1];
        for (int i = 1; i < bytes.Count; i++)
            result[i - 1] = (char)(bytes[i] ^ key);
        return new string(result);
    }
}
