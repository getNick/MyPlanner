namespace MyPlanner.Service.Helpers.BankExport;

/// <summary>
/// What one MCC decides: the Category/Subcategory pair, and what the code is defined to mean.
/// The meaning travels with the answer so a report can say why a row moved — the Merchant
/// Description on a Statement Row is too unreliable to carry that explanation (D10).
/// </summary>
public sealed record MccClassification(string Category, string Subcategory, string Meaning);

/// <summary>
/// The MCC → Category/Subcategory table a Bank Transaction is classified from. Every pair it can
/// produce is validated against <see cref="Models.ReceiptCategories"/> by unit test, so the table
/// cannot drift away from the taxonomy.
///
/// <para>
/// It is a table rather than a chain of range checks because MCC blocks are neighbours by number,
/// not by what is sold. Reading 5411–5999 as one block called a hardware store, a book shop, a
/// liquor store and a sports shop all "Medicine &amp; Supplements", and reading 5811–5819 as dining
/// called YouTube a restaurant. So an exact code is looked up first, and only the blocks that really
/// are homogeneous (supermarkets, clothing, medical practitioners, utilities) fall back to a range.
/// </para>
///
/// <para>
/// A code that is not here stays uncategorised. That covers two kinds of row, both deliberate: money
/// moving rather than being spent (wire transfers, cash withdrawals, card-to-card transfers — see
/// <see cref="WhyUnclassified"/>), and spending the taxonomy has no line for yet (electronics and
/// computer retail, vehicle purchase, car rental, tobacco, florists). Naming those gaps is more
/// useful than guessing over them.
/// </para>
/// </summary>
public static class MccCategoryMap
{
    private sealed record Block(int From, int To, string Category, string Subcategory, string Meaning);

    /// <summary>Exact codes win over <see cref="_blocks"/>.</summary>
    private static readonly Dictionary<int, MccClassification> _codes = new()
    {
        // --- Groceries: the supermarket block, split by what each code actually sells ----------
        [5411] = Grocery("Pantry", "Grocery Stores, Supermarkets"),
        [5422] = Grocery("Meat", "Meat Markets and Freezers"),
        [5441] = Grocery("Sweets", "Candy, Nut and Confectionery Stores"),
        [5451] = Grocery("Dairy & Eggs", "Dairy Products Stores"),
        // 5462 is a retail bakery. It sat next to the food block only by number and used to be read
        // as "bookstores", which are 5942.
        [5462] = Grocery("Bakery", "Bakeries — Retail"),
        [5499] = Grocery("Pantry", "Misc Food Retailers and Specialty Gourmet Stores"),
        [5944] = Grocery("Seafood", "Fish and Seafood Markets"),

        // --- Dining & Takeaway ---------------------------------------------------------------
        [5811] = Dining("Restaurants", "Eating Places — Restaurants, Cafeterias, Family Style"),
        [5812] = Dining("Fast Food & Delivery", "Eating Places — Take-Out, Carry-Out, Caterers, Deliveries"),
        [5813] = Dining("Bars & Nightlife", "Drink Establishments — Taverns, Bars, Cocktail Lounges"),
        // 5814 is nominally cafeteria/fast-food catering; Ukrainian acquirers put coffee shops and
        // bakery-cafés under it, which is what this household's statements show.
        [5814] = Dining("Cafes & Coffee", "Computer Food Operators — Cafeterias and Fast Food (coffee shops land here)"),

        // --- Entertainment & Leisure ---------------------------------------------------------
        // The digital goods block: subscriptions and downloads bought from a platform. Inside the old
        // dining range, they came out as Restaurants.
        [5815] = Leisure("Digital Subscriptions", "Digital Goods — Media, Books, Movies, Music"),
        [5816] = Leisure("Digital Subscriptions", "Digital Goods — Games (Computer)"),
        [5817] = Leisure("Digital Subscriptions", "Digital Goods — Games (Video)"),
        [5818] = Leisure("Digital Subscriptions", "Digital Goods — Multimedia and Large Digital Goods Merchants"),
        [5968] = Leisure("Digital Subscriptions", "Subscription Services"),
        [5942] = Leisure("Hobbies", "Book Stores"),
        [5946] = Leisure("Hobbies", "Cameras and Photographic Supplies"),
        [5947] = Leisure("Hobbies", "Gift, Card, Party, Stationery, Novelty and Souvenir Stores"),
        [5971] = Leisure("Hobbies", "Art Dealers and Galleries"),
        [5972] = Leisure("Hobbies", "Stamp and Coin Stores — Philatelic Supplies"),
        [5994] = Leisure("Hobbies", "News Dealers and Newsstands"),
        [7922] = Leisure("Hobbies", "Picture Taking Studios — Portraits and Commercial"),
        [7932] = Leisure("Hobbies", "Bowling Lanes"),
        [7933] = Leisure("Hobbies", "Pool Tables — Establishment and Equipment"),
        [7993] = Leisure("Hobbies", "Video Amusement Game Supplies"),
        [7994] = Leisure("Hobbies", "Video Game Arcades"),
        [7996] = Leisure("Hobbies", "Golf Courses — Public and Private"),
        [7998] = Leisure("Movies & Events", "Auditoriums, Concert Halls, Music Halls and Theaters"),
        [4411] = Leisure("Travel & Vacations", "Cruise Lines and Steamship Lines"),
        [4722] = Leisure("Travel & Vacations", "Travel Agencies and Tour Operations"),
        [7011] = Leisure("Travel & Vacations", "Accommodation — Hotels, Motels, Resorts"),
        [7012] = Leisure("Travel & Vacations", "Vacation Homes and Rental Properties"),
        [7032] = Leisure("Education & Courses", "Student Dormitories — Room and Board"),
        [7033] = Leisure("Education & Courses", "International Colleges"),
        [7991] = Leisure("Travel & Vacations", "Tourist Attractions, Museums and Landmarks"),

        // --- Housing & Utilities -------------------------------------------------------------
        [4812] = Housing("Utilities", "Telecommunications Equipment — Telephone Sales"),
        [4814] = Housing("Utilities", "Telecommunications Services — Monthly Charges and Top-Up"),
        [4899] = Housing("Internet & TV", "Cable and Other Pay Television Services"),
        // 7372 covers internet providers, hosting and data services; platform subscriptions such as
        // Google One are acquired under it too.
        [7372] = Housing("Internet & TV", "Computer and Internet Services — Data Preparation, Hosting, Providers"),
        [5983] = Housing("Utilities", "Fuel Dealers — Heating Oil, Coal, Lumber and Firewood"),

        // --- Transportation ------------------------------------------------------------------
        [4111] = Transport("Public Transit & Rideshare", "Local and Commuter Passenger Transportation"),
        [4112] = Transport("Public Transit & Rideshare", "Passenger Railways"),
        [4131] = Transport("Public Transit & Rideshare", "Bus Lines, including Charters and Tour Buses"),
        [4789] = Transport("Public Transit & Rideshare", "Transportation — Not Elsewhere Classified"),
        // A courier or parcel office (Нова пошта, Ukrposhta) is how a bought thing reaches the
        // household; Transportation is the closest existing line because the taxonomy has no shipping.
        [4215] = Transport("Public Transit & Rideshare", "Courier Services — Air or Ground, Freight Forwarding, Parcel Delivery"),
        [4784] = Transport("Parking & Tolls", "Toll and Highway Fees and Bridge Fees"),
        [7523] = Transport("Parking & Tolls", "Parking Lots and Garages — Payment"),
        [5533] = Transport("Fuel/Gas", "Service Stations — Full Service, Gasoline and Other Fuels"),
        [5541] = Transport("Fuel/Gas", "Service Stations — Self-Service"),
        // Parts shops sat inside the old 5531–5542 fuel range, which called them petrol stations.
        [5531] = Transport("Auto Maintenance & Repairs", "Auto and Parts Stores"),
        [5532] = Transport("Auto Maintenance & Repairs", "Automotive Parts and Accessories Stores"),
        // 7519 is a tyre shop, not parking (the old table read it as Parking & Tolls).
        [7519] = Transport("Auto Maintenance & Repairs", "Automotive Tire Shops"),

        // --- Household & Supplies ------------------------------------------------------------
        [5200] = Household("Home Repairs & Tools", "Home Supply Warehouse Stores"),
        [5211] = Household("Home Repairs & Tools", "Building Materials — Lumber and Wood Storage Facilities"),
        [5231] = Household("Home Repairs & Tools", "Glass and Paint Retail Stores"),
        [5251] = Household("Home Repairs & Tools", "Hardware Stores"),
        [5261] = Household("Home Repairs & Tools", "Lawn and Garden Supplies Stores"),
        // 5712 is hardware by definition; home furnishing warehouses are acquired under it in practice.
        [5712] = Household("Home Repairs & Tools", "Hardware Stores"),
        [5722] = Household("Home Repairs & Tools", "Household Appliance Stores"),
        [5950] = Household("Furniture & Decor", "Glassware and Pottery Retail Stores"),
        [5310] = Household("Cleaning & Consumables", "Discount, Warehouse and Closeout Stores"),
        [5311] = Household("Cleaning & Consumables", "Variety Stores"),
        [5399] = Household("Cleaning & Consumables", "Misc General Merchandise"),
        [5964] = Household("Cleaning & Consumables", "Direct Marketing — Catalog Merchant"),
        [5965] = Household("Cleaning & Consumables", "Direct Marketing — Combination Catalog and Retail Merchant"),

        // --- Personal Care & Clothes ---------------------------------------------------------
        [5641] = KidsFamily("Kids' Clothes & Shoes", "Children's and Infant's Wear Stores"),
        [5940] = PersonalCare("Clothing & Shoes", "Used Clothing — Resale Shops"),
        [5948] = PersonalCare("Clothing & Shoes", "Luggage and Leather Goods Stores"),

        // --- Kids & Family -------------------------------------------------------------------
        [5945] = KidsFamily("Toys & Books", "Hobby, Toy and Game Shops"),
        [8351] = KidsFamily("Daycare & Babysitting", "Child Care Services"),

        // --- Health & Wellness ---------------------------------------------------------------
        [5912] = Health("Medicine & Supplements", "Drug Stores and Pharmacies — Prescription Medicine"),
        [5976] = Health("Medicine & Supplements", "Drug Stores — Non-Prescription"),
        // Nutrition shops are supplements; pet suppliers are acquired under this code too, which MCC
        // alone cannot tell apart.
        [5995] = Health("Medicine & Supplements", "Nutrition Stores"),
        [5975] = Health("Medical & Dental", "Hearing Aids — Sales and Rental"),
        [5941] = Health("Fitness & Gym", "Sporting Goods Stores"),
        [7941] = Health("Fitness & Gym", "Commercial Sports — Athletic Clubs, Fields and Organizations"),
        [7997] = Health("Fitness & Gym", "Physical Fitness Facilities"),

        // --- Financial & Future --------------------------------------------------------------
        [9311] = Financial("Taxes", "Tax Payments — Government Owned"),
    };

    /// <summary>
    /// Blocks homogeneous enough to classify by range. Checked in declaration order, and only when no
    /// exact code matched. Anything outside these ranges and outside <see cref="_codes"/> is left
    /// uncategorised.
    /// </summary>
    private static readonly Block[] _blocks =
    [
        new(4111, 4121, "Transportation", "Public Transit & Rideshare",
            "Passenger Transport — Local and Commuter Transport, Passenger Railways, Taxicabs"),
        new(4900, 4999, "Housing & Utilities", "Utilities",
            "Electric, Gas, Water, Sewerage and Other Utility Suppliers"),
        // General merchandise: wholesale clubs, discount and variety stores, online marketplaces.
        new(5300, 5399, "Household & Supplies", "Cleaning & Consumables",
            "Wholesale Clubs, Discount, Variety and Misc General Merchandise Stores"),
        // The clothing block: men's, women's and family clothing, accessory and shoe shops.
        // Children's wear (5641) is an exact code above, so it lands in Kids & Family instead.
        new(5611, 5699, "Personal Care & Clothes", "Clothing & Shoes",
            "Clothing and Accessory Shops — Men's, Women's, Family Clothing and Shoe Stores"),
        // Personal services: barbers, beauty and nail salons, tailors, dry cleaning.
        new(7210, 7299, "Personal Care & Clothes", "Haircuts & Grooming",
            "Personal Services — Barbers, Beauty Salons, Tailoring, Cleaning and Pressing"),
        new(7531, 7546, "Transportation", "Auto Maintenance & Repairs",
            "Automotive Service — Body Repair, Glass, Service Shops, Car and Truck Washes"),
        new(7829, 7841, "Entertainment & Leisure", "Movies & Events",
            "Motion Picture Studios, Theaters and Video Rental"),
        // Education is not assumed to be a child's: an MCC cannot tell a university from a course.
        new(8211, 8299, "Entertainment & Leisure", "Education & Courses",
            "Educational Services — Schools, Colleges and Training"),
        // Doctors, dentists, opticians, hospitals and laboratories. The old table stopped at 8042 and
        // skipped 8043, so opticians were unclassified.
        new(8011, 8099, "Health & Wellness", "Medical & Dental",
            "Medical Practitioners, Dentists, Opticians, Hospitals and Medical Laboratories"),
        // The recreation block's catch-all: tourist attractions, auditoriums, misc recreation places.
        // The codes inside it that mean something specific are exact entries above.
        new(7923, 7999, "Entertainment & Leisure", "Travel & Vacations",
            "Recreation Places — Tourist Attractions and Misc Recreation"),
    ];

    /// <summary>
    /// Codes the map knows and refuses on purpose. Writing them down is what stops the next reader
    /// from "fixing" the gap by guessing a category.
    /// </summary>
    private static readonly Dictionary<int, string> _leftUnclassified = new()
    {
        [4829] = "Wire transfers and money remittance — money leaving, not spending. Donation platforms such as Tabletochki are acquired here, and the taxonomy has no Donation line.",
        [6010] = "Money transfer — this is how a cash withdrawal from the bank's own ATM network appears.",
        [6011] = "Credit or debit money transfer — moving money, not buying.",
        [6012] = "Financial institutions manual cash calls — card-to-card transfers between people.",
        [6051] = "Quasi cash and currency conversion — an exchange or withdrawal, not a merchant category.",
        [6211] = "Securities, brokers and dealers — investment activity belongs to Savings & Investments only when a person says so.",
        [6532] = "Payment transaction facilitation — a payment intermediary, not a merchant.",
        [6533] = "Money transfer (institutional) — moving money between accounts.",
        [6536] = "Money transfer (cross-border institutional) — marketplace payouts are acquired here.",
        [6538] = "Money transfer (intra-EEA) — moving money between accounts.",
        [6540] = "Stored value card purchase — loading a prepaid wallet, not spending from it.",
        // Merchant categories the taxonomy has no line for yet. Left uncategorised so the row stays
        // visible and fixable by hand rather than filed somewhere wrong.
        [5732] = "Electronics retail — there is no electronics or appliances line to put it on.",
        [5734] = "Computer sales stores — same gap, and platform subscriptions (Google Play) are acquired under this code too, which makes guessing worse.",
        [5943] = "Computer and peripheral retail — same gap as 5732.",
        [5970] = "Tobacco shops — no line in Groceries or Household for it.",
        [5977] = "Defined as watch, clock and jewellery repair, but cosmetics and home discount chains are acquired under this code in this household's statements. Needs merchant-level evidence before it can be mapped.",
        [5992] = "Florists — gifting has no line of its own.",
    };

    /// <summary>Classifies one MCC, or returns null when the code is not classified.</summary>
    public static MccClassification? Classify(int mcc)
    {
        if (_codes.TryGetValue(mcc, out var exact)) return exact;

        foreach (var block in _blocks)
        {
            if (mcc < block.From || mcc > block.To) continue;

            return new MccClassification(block.Category, block.Subcategory, $"{block.From}–{block.To} {block.Meaning}");
        }

        return null;
    }

    /// <summary>
    /// Every Category/Subcategory pair the map can produce, deduplicated. Exists for one check: that the
    /// map stays inside the taxonomy the rest of the app uses — the test asserts each pair is valid.
    /// </summary>
    public static IReadOnlyList<(string Category, string Subcategory)> MappedPairs()
        => _codes.Values.Select(c => (c.Category, c.Subcategory))
            .Concat(_blocks.Select(b => (b.Category, b.Subcategory)))
            .Distinct()
            .OrderBy(p => p.Category).ThenBy(p => p.Subcategory)
            .ToList();

    /// <summary>
    /// Why a known code is deliberately left uncategorised, or null when the code is simply unknown.
    /// Lets a report say "this row is a cash withdrawal" instead of showing nothing.
    /// </summary>
    public static string? WhyUnclassified(int mcc) => _leftUnclassified.GetValueOrDefault(mcc);

    private static MccClassification Grocery(string subcategory, string meaning) => new("Groceries", subcategory, meaning);
    private static MccClassification Dining(string subcategory, string meaning) => new("Dining & Takeaway", subcategory, meaning);
    private static MccClassification Housing(string subcategory, string meaning) => new("Housing & Utilities", subcategory, meaning);
    private static MccClassification Transport(string subcategory, string meaning) => new("Transportation", subcategory, meaning);
    private static MccClassification Household(string subcategory, string meaning) => new("Household & Supplies", subcategory, meaning);
    private static MccClassification PersonalCare(string subcategory, string meaning) => new("Personal Care & Clothes", subcategory, meaning);
    private static MccClassification Leisure(string subcategory, string meaning) => new("Entertainment & Leisure", subcategory, meaning);
    private static MccClassification KidsFamily(string subcategory, string meaning) => new("Kids & Family", subcategory, meaning);
    private static MccClassification Health(string subcategory, string meaning) => new("Health & Wellness", subcategory, meaning);
    private static MccClassification Financial(string subcategory, string meaning) => new("Financial & Future", subcategory, meaning);
}
