using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using static WebScraper.Objects;

namespace WebScraper;

/// <summary>
/// Parses HTML pages of venue profiles from the Podiuminfo website
/// and converts them into strongly typed <see cref="Venue"/> objects.
///
/// The parser extracts data from two main sources:
/// 1. JSON-LD structured data embedded in the page
/// 2. DOM scraping of the contact section (email / website)
/// </summary>
public sealed class VenueParser
{
    /// <summary>
    /// Parses the HTML of a venue page and extracts venue information.
    /// </summary>
    /// <param name="html">Raw HTML content of the venue page.</param>
    /// <returns>
    /// A populated <see cref="Venue"/> object if parsing succeeds,
    /// otherwise <c>null</c> if required information is missing.
    /// </returns>
    public static Venue? Parse(string html)
    {
        // Load HTML into HtmlAgilityPack document
        HtmlDocument doc = new HtmlDocument();       // Create a new HTML document
        doc.OptionFixNestedTags = true;     // Enable auto-closing of tags to handle malformed HTML
        doc.OptionAutoCloseOnEnd = true;    // Ensure all tags are closed at the end of the document
        doc.LoadHtml(html);                 // Parse the HTML content into a DOM structure

        // Variables used to collect parsed values
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
        // Many modern websites embed structured data using JSON-LD.
        // Podiuminfo includes venue data under "@type": "MusicVenue".
        HtmlNodeCollection scripts = doc.DocumentNode
            .SelectNodes("//script[@type='application/ld+json']");
        
        // Loop through all JSON-LD blocks to find the one describing the MusicVenue.
        if (scripts != null)
        {
            foreach (HtmlNode script in scripts)
            {
                try
                {
                    using JsonDocument jsonDoc = JsonDocument.Parse(script.InnerText);
                    JsonElement root = jsonDoc.RootElement;

                    // Ensure the JSON block contains a @type field
                    if (!root.TryGetProperty("@type", out JsonElement typeProp))
                        continue;

                    // Only parse objects describing a MusicVenue
                    if (typeProp.GetString() != "MusicVenue")
                        continue;

                    // Extract primary venue metadata
                    url = root.GetProperty("url").GetString();
                    name = root.GetProperty("name").GetString();

                    // Optional external website
                    if (root.TryGetProperty("sameAs", out JsonElement sameAsProp))
                        sameAs = sameAsProp.GetString();

                    // Address block
                    if (root.TryGetProperty("address", out JsonElement address))
                    {
                        street = address.GetProperty("streetAddress").GetString();
                        postalCode = address.GetProperty("postalCode").GetString();
                        city = address.GetProperty("addressLocality").GetString();
                        region = address.GetProperty("addressRegion").GetString();
                        country = address.GetProperty("addressCountry").GetString();
                    }

                    // Stop once the correct JSON-LD block is found
                    break;
                }
                catch
                {
                    // Ignore malformed JSON blocks and continue scanning
                }
            }
        }

        #region === TEST ===
        /*
        ------------------------------------------------------------------
        Alternative JSON-LD approach (kept for reference)
        This version assumes there is only one JSON-LD block in the page.
        The loop above is safer because Podiuminfo often includes multiple
        structured data blocks.
        ------------------------------------------------------------------
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
        #endregion

        // -------- Contact block parsing --------
        // The contact section may contain:
        // - Email (often protected by Cloudflare)
        // - Website link
        var contactNode = doc.DocumentNode
            .SelectSingleNode("//section[contains(@class,'podium_contact')]");

        if (contactNode != null)
        {
            // Cloudflare email protection:
            // Emails are encoded inside the attribute "data-cfemail"
            // and decoded using JavaScript on the client side.
            HtmlNode cfEmailNode = contactNode.SelectSingleNode(".//a[@data-cfemail]");

            if (cfEmailNode != null)
            {
                string encoded = cfEmailNode.GetAttributeValue("data-cfemail", "");
                email = DecodeCloudflareEmail(encoded);
            }
            else
            {
                // Fallback: attempt to extract plain email from text
                email = ExtractEmail(contactNode.InnerText);
            }

            // Extract website link from contact section if available
            HtmlNode websiteNode = contactNode.SelectSingleNode(".//a[starts-with(@href,'http')]");

            if (websiteNode != null && string.IsNullOrWhiteSpace(sameAs))
                sameAs = websiteNode.GetAttributeValue("href", "");
        }

        // If no venue name was found, assume parsing failed
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // Create and return the Venue object
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
     Emails can be obfuscated in two ways:

     1. Cloudflare Email Protection
        The email is encoded in a data attribute and decoded via JavaScript.

     2. Plain text obfuscation
        The email may appear normally in page text.

     This method attempts to detect a standard email pattern using regex.
    */

    /// <summary>
    /// Extracts the first valid email address from a text block using regex.
    /// </summary>
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

    /// <summary>
    /// Decodes Cloudflare's email protection format.
    /// </summary>
    /// <param name="encoded">Hexadecimal encoded email string.</param>
    /// <returns>The decoded email address.</returns>
    private static string DecodeCloudflareEmail(string encoded)
    {
        List<byte> bytes = new List<byte>();

        // Convert hex string to byte array
        for (int i = 0; i < encoded.Length; i += 2)
            bytes.Add(Convert.ToByte(encoded.Substring(i, 2), 16));

        int key = bytes[0];                         // The first byte is the XOR key
        char[] result = new char[bytes.Count - 1];  // Prepare a char array to hold the decoded email (excluding the key byte)

        // Decode using XOR with the first byte as the key
        for (int i = 1; i < bytes.Count; i++)
            result[i - 1] = (char)(bytes[i] ^ key);

        return new string(result);
    }
}