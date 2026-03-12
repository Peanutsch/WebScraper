using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using static WebScraper.Objects;

namespace WebScraper;

public sealed class VenueParser
{
    public static Venue? Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        string? url = null;
        string? name = null;
        string? sameAs = null;
        string? street = null;
        string? city = null;
        string? region = null;
        string? postalCode = null;
        string? country = null;
        string? email = null;

        // -------- JSON-LD parsing --------
        var scripts = doc.DocumentNode
            .SelectNodes("//script[@type='application/ld+json']");

        if (scripts != null)
        {
            foreach (var script in scripts)
            {
                try
                {
                    using JsonDocument jsonDoc = JsonDocument.Parse(script.InnerText);
                    JsonElement root = jsonDoc.RootElement;

                    if (!root.TryGetProperty("@type", out JsonElement typeProp))
                        continue;

                    if (typeProp.GetString() != "MusicVenue")
                        continue;

                    url = root.GetProperty("url").GetString();
                    name = root.GetProperty("name").GetString();

                    if (root.TryGetProperty("sameAs", out var sameAsProp))
                        sameAs = sameAsProp.GetString();

                    if (root.TryGetProperty("address", out var address))
                    {
                        street = address.GetProperty("streetAddress").GetString();
                        city = address.GetProperty("addressLocality").GetString();
                        region = address.GetProperty("addressRegion").GetString();
                        postalCode = address.GetProperty("postalCode").GetString();
                        country = address.GetProperty("addressCountry").GetString();
                    }

                    break; // stop zodra MusicVenue gevonden is
                }
                catch { }
            }
        }
        /*
        var script = doc.DocumentNode
            .SelectSingleNode("//script[@type='application/ld+json']");

        if (script != null)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(script.InnerText);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("@type", out var typeProp) &&
                    typeProp.GetString() == "MusicVenue")
                {
                    url = root.GetProperty("url").GetString();
                    name = root.GetProperty("name").GetString();

                    if (root.TryGetProperty("sameAs", out var sameAsProp))
                        sameAs = sameAsProp.GetString();

                    if (root.TryGetProperty("address", out var address))
                    {
                        street = address.GetProperty("streetAddress").GetString();
                        city = address.GetProperty("addressLocality").GetString();
                        region = address.GetProperty("addressRegion").GetString();
                        postalCode = address.GetProperty("postalCode").GetString();
                        country = address.GetProperty("addressCountry").GetString();
                    }
                }
            }
            catch { }
        }
        */

        // -------- Contact block parsing --------

        var contactNode = doc.DocumentNode
            .SelectSingleNode("//section[contains(@class,'podium_contact')]");

        if (contactNode != null)
        {
            // Cloudflare email protection
            var cfEmailNode = contactNode.SelectSingleNode(".//a[@data-cfemail]");

            if (cfEmailNode != null)
            {
                var encoded = cfEmailNode.GetAttributeValue("data-cfemail", "");
                email = DecodeCloudflareEmail(encoded);
            }
            else
            {
                email = ExtractEmail(contactNode.InnerText);
            }

            var websiteNode = contactNode.SelectSingleNode(".//a[starts-with(@href,'http')]");

            if (websiteNode != null && string.IsNullOrWhiteSpace(sameAs))
                sameAs = websiteNode.GetAttributeValue("href", "");
        }

        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new Venue
        {
            Url = url!,
            Name = name!,
            SameAs = sameAs!,
            Street = street!,
            City = city!,
            Region = region!,
            PostalCode = postalCode!,
            Country = country!,
            Email = email!
        };
    }

    // -------- Email helpers --------
    /*
     * Emails can be obfuscated in two ways:
     * 1. Cloudflare's email protection, which encodes the email in a data attribute and decodes it with JavaScript.
     * 2. Plain text obfuscation, where the email is written in a way to avoid detection (e.g., "user [at] domain [dot] com").
     */
    private static string? ExtractEmail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        Match match = Regex.Match(
            text,
            @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Value : null;
    }

    private static string DecodeCloudflareEmail(string encoded)
    {
        var bytes = new List<byte>();

        for (int i = 0; i < encoded.Length; i += 2)
            bytes.Add(Convert.ToByte(encoded.Substring(i, 2), 16));

        int key = bytes[0];
        var result = new char[bytes.Count - 1];

        for (int i = 1; i < bytes.Count; i++)
            result[i - 1] = (char)(bytes[i] ^ key);

        return new string(result);
    }
}