namespace MyPlanner.Service.Models;

public record Category(string Name, IReadOnlyList<string> Subcategories);

/// <summary>
/// Single source of truth for receipt categorization. Defines all 11 categories and their subcategories.
/// </summary>
public static class ReceiptCategories
{
    public static readonly IReadOnlyList<Category> Categories = new List<Category>
    {
        new("Groceries", new[]
        {
            "Fruits & Veggies", "Meat", "Seafood", "Dairy & Eggs", "Cheese",
            "Pantry", "Bakery", "Snacks", "Sweets", "Beverages (non-alcoholic)",
            "Alcohol", "Prepared/Deli"
        }),
        new("Dining & Takeaway", new[]
        {
            "Restaurants", "Fast Food & Delivery", "Cafes & Coffee", "Bars & Nightlife"
        }),
        new("Housing & Utilities", new[]
        {
            "Rent/Mortgage", "Utilities", "Internet & TV"
        }),
        new("Transportation", new[]
        {
            "Fuel/Gas", "Public Transit & Rideshare", "Auto Maintenance & Repairs",
            "Car Insurance & Registration", "Parking & Tolls"
        }),
        new("Household & Supplies", new[]
        {
            "Cleaning & Consumables", "Furniture & Decor", "Home Repairs & Tools"
        }),
        new("Personal Care & Clothes", new[]
        {
            "Clothing & Shoes", "Haircuts & Grooming", "Cosmetics & Skincare"
        }),
        new("Entertainment & Leisure", new[]
        {
            "Digital Subscriptions", "Movies & Events", "Hobbies",
            "Education & Courses", "Travel & Vacations"
        }),
        new("Kids & Family", new[]
        {
            "Daycare & Babysitting", "Extracurriculars & Sports", "Kids' Clothes & Shoes",
            "Toys & Books", "School & Education", "Baby Consumables", "Baby Food & Formula"
        }),
        new("Pets", new[]
        {
            "Pet Food & Treats", "Veterinary & Medicine", "Grooming & Boarding",
            "Supplies & Accessories"
        }),
        new("Health & Wellness", new[]
        {
            "Medical & Dental", "Medicine & Supplements", "Fitness & Gym"
        }),
        new("Financial & Future", new[]
        {
            "Debt & Loan Payments", "Savings & Investments", "Taxes"
        })
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> _subcategoryCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a category name is valid (case-insensitive).
    /// </summary>
    public static bool IsValidCategory(string category) =>
        Categories.Any(c => c.Name.Equals(category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks if a subcategory belongs to the given category (case-insensitive).
    /// </summary>
    public static bool IsValidSubcategory(string subcategory, string category)
    {
        var cat = Categories.FirstOrDefault(c => c.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
        return cat?.Subcategories.Contains(subcategory, StringComparer.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Returns all valid categories for a given parent category name.
    /// </summary>
    public static IReadOnlyList<string> GetSubcategories(string category)
    {
        if (_subcategoryCache.TryGetValue(category, out var cached))
            return cached;

        var subcats = Categories.FirstOrDefault(c => c.Name.Equals(category, StringComparison.OrdinalIgnoreCase))?.Subcategories ?? Array.Empty<string>();
        _subcategoryCache[category] = subcats;
        return subcats;
    }

    /// <summary>
    /// Returns the parent category for a given subcategory (case-insensitive).
    /// </summary>
    public static string? GetCategoryForSubcategory(string subcategory)
    {
        foreach (var cat in Categories)
        {
            if (cat.Subcategories.Contains(subcategory, StringComparer.OrdinalIgnoreCase))
                return cat.Name;
        }
        return null;
    }

    /// <summary>
    /// Returns all valid categories and subcategories as a flat list for dropdowns.
    /// </summary>
    public static IReadOnlyList<string> GetAllSubcategories() =>
        Categories.SelectMany(c => c.Subcategories).ToArray().AsReadOnly();

    /// <summary>
    /// Maps MCC (Merchant Category Code) to budget category and subcategory.
    /// Based on standard ISO 18245 MCC codes. Only returns categories/subcategories defined in this file.
    /// </summary>
    public static (string Category, string Subcategory)? GetCategoryFromMcc(int mccCode)
    {
        return mccCode switch
        {
            // Specific single-value MCCs first
            5462 => ("Entertainment & Leisure", "Hobbies"),           // Bookstores
            7832 => ("Entertainment & Leisure", "Movies & Events"),   // Movie Theaters
            7833 => ("Entertainment & Leisure", "Movies & Events"),   // Movie Theaters
            7922 => ("Entertainment & Leisure", "Hobbies"),           // Sports/Recreation
            4131 => ("Transportation", "Public Transit & Rideshare"), // Taxi/Rideshare
            5533 => ("Transportation", "Fuel/Gas"),                   // Gas Station
            7372 => ("Housing & Utilities", "Internet & TV"),         // Internet/Cable TV
            7519 => ("Transportation", "Parking & Tolls"),            // Parking
            7523 => ("Transportation", "Parking & Tolls"),            // Parking

            // Donations — leave unclassified
            4829 => null,

            // Food & Restaurants
            >= 5811 and <= 5819 => ("Dining & Takeaway", "Restaurants"),
            >= 5411 and <= 5439 => ("Groceries", "Pantry"),           // Supermarkets
            >= 5441 and <= 5461 => ("Groceries", "Snacks"),           // Candy/Confectionery
            >= 5463 and <= 5499 => ("Groceries", "Meat"),             // Meat/Poultry/Fish

            // Shopping & Retail
            >= 5310 and <= 5399 => ("Household & Supplies", "Cleaning & Consumables"), // Department Stores
            >= 5611 and <= 5699 => ("Personal Care & Clothes", "Clothing & Shoes"),    // Clothing
            >= 5912 and <= 5949 => ("Health & Wellness", "Medicine & Supplements"),    // Drugstore/Pharmacy
            >= 5990 and <= 5998 => ("Household & Supplies", "Cleaning & Consumables"), // Misc Retail

            // Entertainment & Leisure (broad range — must come after specific codes)
            >= 7011 and <= 7209 => ("Entertainment & Leisure", "Travel & Vacations"),
            >= 7300 and <= 7371 => ("Financial & Future", "Debt & Loan Payments"),     // Business Services
            >= 7373 and <= 7831 => ("Entertainment & Leisure", "Travel & Vacations"),
            >= 7834 and <= 7921 => ("Entertainment & Leisure", "Travel & Vacations"),
            >= 7923 and <= 7999 => ("Entertainment & Leisure", "Travel & Vacations"),

            // Services & Healthcare
            >= 7210 and <= 7299 => ("Personal Care & Clothes", "Haircuts & Grooming"), // Personal Services
            >= 8011 and <= 8042 => ("Health & Wellness", "Medical & Dental"),          // Doctors/Dentists
            >= 8044 and <= 8099 => ("Health & Wellness", "Medical & Dental"),          // Hospitals/Services
            >= 8211 and <= 8299 => ("Entertainment & Leisure", "Education & Courses"), // Education

            // Transportation
            >= 4111 and <= 4121 => ("Transportation", "Public Transit & Rideshare"),   // Public Transit
            >= 5531 and <= 5532 => ("Transportation", "Fuel/Gas"),                     // Gas Stations

            // Utilities & Communications
            >= 4900 and <= 4999 => ("Housing & Utilities", "Utilities"),               // Utilities

            _ => null
        };
    }
}
