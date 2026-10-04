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
    /// Maps MCC (Merchant Category Code) to budget category and subcategory. The table itself lives in
    /// <see cref="Helpers.BankExport.MccCategoryMap"/>; this is the shorthand for callers that only need
    /// the pair. A null means "not classified", which is an answer rather than a failure — see the map's
    /// remarks on money movement codes and taxonomy gaps.
    /// </summary>
    public static (string Category, string Subcategory)? GetCategoryFromMcc(int mccCode)
    {
        var classification = Helpers.BankExport.MccCategoryMap.Classify(mccCode);
        return classification is null ? null : (classification.Category, classification.Subcategory);
    }
}
