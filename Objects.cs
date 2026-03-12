using System;
using System.Collections.Generic;
using System.Text;

namespace WebScraper
{
    public class Objects
    {
        public class Venue
        {
            public string Name { get; init; } = default!;
            public string Url { get; init; } = default!;
            public string Email { get; init; } = default!;
            public string SameAs { get; init; } = default!;
            public string Street { get; init; } = default!;
            public string City { get; init; } = default!;            
            public string Region { get; init; } = default!;
            public string PostalCode { get; init; } = default!;
            public string Country { get; init; } = default!;
        }
    }
}
