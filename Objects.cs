using System;
using System.Collections.Generic;
using System.Text;

namespace WebScraper
{
    public class Objects
    {
        public class Venue
        {
            public required string Name { get; init; }
            public required string PodiumInfoURL { get; init; }
            public string? Email { get; init; }
            public string? VenueURL { get; init; }
            public string? Street { get; init; }
            public string? City { get; init; }
            public string? Region { get; init; }
            public string? PostalCode { get; init; }
            public string? Country { get; init; }
        }
    }
}
