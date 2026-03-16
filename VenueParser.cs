using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;
using static WebScraper.Objects;

namespace WebScraper;

/// <summary>
/// Parses HTML pages of venue profiles and converts them into <see cref="Venue"/> objects.
/// 
/// The parser extracts information from two sources:
/// 1. JSON-LD structured data embedded in the page
/// 2. The contact section within the HTML DOM
/// </summary>
public sealed class VenueParser
{
    #region === PUBLIC INTERFACE ===
    /// <summary>
    /// Parses a venue HTML page and extracts venue information.
    /// </summary>
    /// <param name="html">Raw HTML content of the venue page.</param>
    /// <returns>
    /// A populated <see cref="Venue"/> object if parsing succeeds,
    /// otherwise <c>null</c> if essential information (e.g. venue name) is missing.
    /// </returns>
    public static Venue? Parse(string html)
    {
        // Load the HTML into an HtmlAgilityPack document
        HtmlDocument doc = LoadDocument(html);

        // Extract structured JSON-LD venue data
        JsonVenueData jsonData = ParseJsonLd(doc);

        // Extract contact information from the contact section
        ContactData contactData = ParseContactInfo(doc);

        // If the venue name is missing, assume parsing failed
        if (string.IsNullOrWhiteSpace(jsonData.Name))
            return null;

        // Combine the extracted data into a final Venue object
        return CreateVenue(jsonData, contactData);
    }
    #endregion

    #region === HTML PARSING ===
    /// <summary>
    /// Creates and loads an <see cref="HtmlDocument"/> from raw HTML.
    /// </summary>
    /// <param name="html">Raw HTML content.</param>
    /// <returns>A parsed HTML document.</returns>
    private static HtmlDocument LoadDocument(string html)
    {
        HtmlDocument doc = new HtmlDocument
        {
            // Attempt to fix malformed HTML structures automatically
            OptionFixNestedTags = true,

            // Ensure all open tags are closed at the end of the document
            OptionAutoCloseOnEnd = true
        };

        // Parse the HTML content
        doc.LoadHtml(html);

        return doc;
    }
    #endregion

    #region === JSON-LD PARSING ===
    /// <summary>
    /// Searches the document for JSON-LD blocks and extracts venue data.
    /// </summary>
    /// <param name="doc">The parsed HTML document.</param>
    /// <returns>A <see cref="JsonVenueData"/> object containing extracted values.</returns>
    private static JsonVenueData ParseJsonLd(HtmlDocument doc)
    {
        // Select all JSON-LD script tags
        HtmlNodeCollection scripts =
            doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");

        // If no scripts were found, return an empty data object
        if (scripts == null)
            return new JsonVenueData();

        // Iterate through all JSON-LD blocks
        foreach (HtmlNode script in scripts)
        {
            try
            {
                // Parse the JSON content
                using JsonDocument jsonDoc = JsonDocument.Parse(script.InnerText);
                JsonElement root = jsonDoc.RootElement;

                // Ensure the JSON block contains a @type property
                if (!root.TryGetProperty("@type", out JsonElement typeProp))
                    continue;

                // Only process objects describing a MusicVenue
                if (typeProp.GetString() != "MusicVenue")
                    continue;

                // Extract venue information from the JSON object
                return ExtractVenueFromJson(root);
            }
            catch
            {
                // Ignore malformed JSON blocks and continue searching
            }
        }

        // Return an empty data object if no suitable JSON-LD block was found
        return new JsonVenueData();
    }

    /// <summary>
    /// Extracts venue data from a JSON-LD element.
    /// </summary>
    /// <param name="root">The root JSON element representing the venue.</param>
    /// <returns>A populated <see cref="JsonVenueData"/> object.</returns>
    private static JsonVenueData ExtractVenueFromJson(JsonElement root)
    {
        JsonVenueData data = new JsonVenueData
        {
            // Required fields
            Url = root.GetProperty("url").GetString(),
            Name = root.GetProperty("name").GetString(),
        };

        // Optional external website
        if (root.TryGetProperty("sameAs", out JsonElement sameAs))
            data.Website = sameAs.GetString();

        // Extract address information if available
        if (root.TryGetProperty("address", out JsonElement address))
        {
            data.Street = address.GetProperty("streetAddress").GetString();
            data.PostalCode = address.GetProperty("postalCode").GetString();
            data.City = address.GetProperty("addressLocality").GetString();
            data.Region = address.GetProperty("addressRegion").GetString();
            data.Country = address.GetProperty("addressCountry").GetString();
        }

        return data;
    }
    #endregion

    #region === CONTACT PARSING ===
    /// <summary>
    /// Extracts contact information from the contact section of the page.
    /// </summary>
    /// <param name="doc">The parsed HTML document.</param>
    /// <returns>A <see cref="ContactData"/> object containing email and website.</returns>
    private static ContactData ParseContactInfo(HtmlDocument doc)
    {
        // Locate the contact section within the page
        HtmlNode contactNode =
            doc.DocumentNode.SelectSingleNode("//section[contains(@class,'podium_contact')]");

        // If the contact section does not exist, return empty contact data
        if (contactNode == null)
            return new ContactData();

        // Extract email address
        string? email = ExtractContactEmail(contactNode);

        // Extract website link
        string? website = ExtractWebsite(contactNode);

        return new ContactData
        {
            Email = email,
            Website = website
        };
    }

    /// <summary>
    /// Extracts an email address from the contact section.
    /// </summary>
    /// <param name="contactNode">The HTML node containing contact information.</param>
    /// <returns>The decoded or detected email address, if available.</returns>
    private static string? ExtractContactEmail(HtmlNode contactNode)
    {
        // Cloudflare often protects email addresses using data-cfemail
        HtmlNode cfEmailNode = contactNode.SelectSingleNode(".//a[@data-cfemail]");

        if (cfEmailNode != null)
        {
            // Decode Cloudflare protected email
            string encoded = cfEmailNode.GetAttributeValue("data-cfemail", "");
            return DecodeCloudflareEmail(encoded);
        }

        // Fallback: attempt to extract email from visible text
        return ExtractEmail(contactNode.InnerText);
    }

    /// <summary>
    /// Extracts a website URL from the contact section.
    /// </summary>
    /// <param name="contactNode">The HTML node containing contact information.</param>
    /// <returns>The website URL if found.</returns>
    private static string? ExtractWebsite(HtmlNode contactNode)
    {
        // Look for the first anchor tag containing an HTTP link
        HtmlNode websiteNode =
            contactNode.SelectSingleNode(".//a[starts-with(@href,'http')]");

        return websiteNode?.GetAttributeValue("href", null!);
    }
    #endregion

    #region === OBJECT CREATION ===
    /// <summary>
    /// Combines JSON and contact data into a final <see cref="Venue"/> object.
    /// </summary>
    /// <param name="json">Structured JSON venue data.</param>
    /// <param name="contact">Contact information extracted from HTML.</param>
    /// <returns>A populated <see cref="Venue"/> instance.</returns>
    private static Venue CreateVenue(JsonVenueData json, ContactData contact)
    {
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
    #endregion

    #region === EMAIL EXTRACTION & DECODING ===
    /// <summary>
    /// Attempts to extract a plain email address from a text block using a regex pattern.
    /// </summary>
    /// <param name="text">Text containing a potential email address.</param>
    /// <returns>The detected email address if found; otherwise <c>null</c>.</returns>
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
    /// Decodes an email address protected by Cloudflare's email protection.
    /// </summary>
    /// <param name="encoded">Hexadecimal encoded email string.</param>
    /// <returns>The decoded email address.</returns>
    private static string DecodeCloudflareEmail(string encoded)
    {
        // Convert hexadecimal string to byte array
        List<byte> bytes = new();

        for (int i = 0; i < encoded.Length; i += 2)
            bytes.Add(Convert.ToByte(encoded.Substring(i, 2), 16));

        // The first byte represents the XOR key
        int key = bytes[0];

        // Prepare result buffer excluding the key byte
        char[] result = new char[bytes.Count - 1];

        // Decode each byte using XOR with the key
        for (int i = 1; i < bytes.Count; i++)
            result[i - 1] = (char)(bytes[i] ^ key);

        return new string(result);
    }
    #endregion

    #region === INTERNAL DATA STRUCTURES ===
    /// <summary>
    /// Internal container for venue data extracted from JSON-LD.
    /// </summary>
    private class JsonVenueData
    {
        public string? Url;
        public string? Name;
        public string? Website;
        public string? Street;
        public string? City;
        public string? Region;
        public string? PostalCode;
        public string? Country;
    }

    /// <summary>
    /// Internal container for contact information extracted from HTML.
    /// </summary>
    private class ContactData
    {
        public string? Email;
        public string? Website;
    }
#endregion
}