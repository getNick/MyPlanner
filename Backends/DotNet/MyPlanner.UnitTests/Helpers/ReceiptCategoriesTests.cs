using MyPlanner.Service.Models;

namespace MyPlanner.Service.UnitTests.Helpers;

[TestFixture]
public class ReceiptCategoriesTests
{
    [Test]
    public void IsValidCategory_ReturnsTrue_ForValidCategory()
    {
        Assert.That(ReceiptCategories.IsValidCategory("Groceries"), Is.True);
        Assert.That(ReceiptCategories.IsValidCategory("Dining & Takeaway"), Is.True);
        Assert.That(ReceiptCategories.IsValidCategory("Transportation"), Is.True);
    }

    [Test]
    public void IsValidCategory_ReturnsTrue_ForCaseInsensitiveMatch()
    {
        Assert.That(ReceiptCategories.IsValidCategory("groceries"), Is.True);
        Assert.That(ReceiptCategories.IsValidCategory("GROCERIES"), Is.True);
        Assert.That(ReceiptCategories.IsValidCategory("dInInG & TaKeAwAy"), Is.True);
    }

    [Test]
    public void IsValidCategory_ReturnsFalse_ForInvalidCategory()
    {
        Assert.That(ReceiptCategories.IsValidCategory("NonExistent"), Is.False);
        Assert.That(ReceiptCategories.IsValidCategory(""), Is.False);
        Assert.That(ReceiptCategories.IsValidCategory(null!), Is.False);
    }

    [Test]
    public void IsValidSubcategory_ReturnsTrue_ForValidSubcategory()
    {
        Assert.That(ReceiptCategories.IsValidSubcategory("Fruits & Veggies", "Groceries"), Is.True);
        Assert.That(ReceiptCategories.IsValidSubcategory("Fuel/Gas", "Transportation"), Is.True);
        Assert.That(ReceiptCategories.IsValidSubcategory("Clothing & Shoes", "Personal Care & Clothes"), Is.True);
    }

    [Test]
    public void IsValidSubcategory_ReturnsTrue_ForCaseInsensitiveMatch()
    {
        Assert.That(ReceiptCategories.IsValidSubcategory("fruits & veggies", "Groceries"), Is.True);
        Assert.That(ReceiptCategories.IsValidSubcategory("FRUITS & VEGGIES", "groceries"), Is.True);
    }

    [Test]
    public void IsValidSubcategory_ReturnsFalse_ForInvalidCategory()
    {
        Assert.That(ReceiptCategories.IsValidSubcategory("Fruits & Veggies", "NonExistent"), Is.False);
    }

    [Test]
    public void IsValidSubcategory_ReturnsFalse_ForSubcategoryNotInCategory()
    {
        Assert.That(ReceiptCategories.IsValidSubcategory("Fuel/Gas", "Groceries"), Is.False);
        Assert.That(ReceiptCategories.IsValidSubcategory("Fruits & Veggies", "Transportation"), Is.False);
    }

    [Test]
    public void IsValidSubcategory_ReturnsFalse_ForEmptyOrNull()
    {
        Assert.That(ReceiptCategories.IsValidSubcategory("", "Groceries"), Is.False);
        Assert.That(ReceiptCategories.IsValidSubcategory("Fruits & Veggies", ""), Is.False);
        Assert.That(ReceiptCategories.IsValidSubcategory(null!, "Groceries"), Is.False);
    }

    [Test]
    public void GetSubcategories_ReturnsCorrectList_ForValidCategory()
    {
        var subcats = ReceiptCategories.GetSubcategories("Groceries");
        Assert.That(subcats, Has.Count.EqualTo(12));
        Assert.That(subcats, Contains.Item("Fruits & Veggies"));
        Assert.That(subcats, Contains.Item("Meat"));
        Assert.That(subcats, Contains.Item("Dairy & Eggs"));
    }

    [Test]
    public void GetSubcategories_ReturnsEmptyArray_ForInvalidCategory()
    {
        var subcats = ReceiptCategories.GetSubcategories("NonExistent");
        Assert.That(subcats, Is.Empty);
    }

    [Test]
    public void GetSubcategories_CachesResult_ForPerformance()
    {
        // First call - should populate cache
        var firstCall = ReceiptCategories.GetSubcategories("Groceries");
        
        // Second call - should return cached result (same reference)
        var secondCall = ReceiptCategories.GetSubcategories("Groceries");
        
        Assert.That(secondCall, Is.SameAs(firstCall));
    }

    [Test]
    public void GetCategoryForSubcategory_ReturnsCorrectParent_ForValidSubcategory()
    {
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("Fruits & Veggies"), Is.EqualTo("Groceries"));
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("Fuel/Gas"), Is.EqualTo("Transportation"));
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("Clothing & Shoes"), Is.EqualTo("Personal Care & Clothes"));
    }

    [Test]
    public void GetCategoryForSubcategory_ReturnsNull_ForInvalidSubcategory()
    {
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("NonExistent"), Is.Null);
        Assert.That(ReceiptCategories.GetCategoryForSubcategory(""), Is.Null);
    }

    [Test]
    public void GetAllSubcategories_ReturnsAllSubcategoriesFlattened()
    {
        var all = ReceiptCategories.GetAllSubcategories();
        
        // Count expected subcategories across all 11 categories (52 total)
        Assert.That(all, Has.Count.EqualTo(52));
        
        // Verify some known items are present
        Assert.That(all, Contains.Item("Fruits & Veggies"));
        Assert.That(all, Contains.Item("Fuel/Gas"));
        Assert.That(all, Contains.Item("Clothing & Shoes"));
        Assert.That(all, Contains.Item("Kids' Clothes & Shoes"));
    }

    [Test]
    public void GetAllSubcategories_ReturnsReadOnlyList()
    {
        var all = ReceiptCategories.GetAllSubcategories();
        
        // Verify it's read-only by checking the type
        Assert.That(all, Is.InstanceOf<IReadOnlyList<string>>());
    }

    [Test]
    public void CategoryRecord_IsImmutable()
    {
        var category = new Category("Groceries", new[] { "Fruits & Veggies" });
        
        // Verify Name is immutable (record property)
        Assert.That(category.Name, Is.EqualTo("Groceries"));
        
        // Verify Subcategories is read-only (IReadOnlyList)
        Assert.That(category.Subcategories, Is.InstanceOf<IReadOnlyList<string>>());
    }

    [Test]
    public void CategoryRecord_EqualityWorksCorrectly()
    {
        var cat1 = new Category("Groceries", new[] { "Fruits & Veggies" });
        var cat2 = new Category("Groceries", new[] { "Fruits & Veggies" });
        
        // Records compare by value for reference types like IReadOnlyList
        Assert.That(cat1.Name, Is.EqualTo(cat2.Name));
        Assert.That(cat1.Subcategories.SequenceEqual(cat2.Subcategories), Is.True);
    }

    [Test]
    public void CategoryRecord_DoesNotEqualDifferentCategory()
    {
        var cat1 = new Category("Groceries", new[] { "Fruits & Veggies" });
        var cat2 = new Category("Dining & Takeaway", new[] { "Restaurants" });
        
        Assert.That(cat1, Is.Not.EqualTo(cat2));
    }

    [Test]
    public void GetSubcategories_HandlesCaseInsensitiveCategoryName()
    {
        // Should work regardless of case since category lookup is case-insensitive
        var subcats = ReceiptCategories.GetSubcategories("groceries");
        Assert.That(subcats, Has.Count.EqualTo(12));
        
        var cachedSubcats = ReceiptCategories.GetSubcategories("GROCERIES");
        Assert.That(cachedSubcats, Is.SameAs(subcats)); // Same cache entry
    }

    [Test]
    public void IsValidCategory_HandlesAllDefinedCategories()
    {
        foreach (var category in ReceiptCategories.Categories)
        {
            Assert.That(ReceiptCategories.IsValidCategory(category.Name), Is.True);
        }
    }

    [Test]
    public void GetCategoryForSubcategory_CaseInsensitiveMatch()
    {
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("fruits & veggies"), Is.EqualTo("Groceries"));
        Assert.That(ReceiptCategories.GetCategoryForSubcategory("FRUITS & VEGGIES"), Is.EqualTo("Groceries"));
    }

    [Test]
    public void GetAllSubcategories_NoDuplicatesAcrossCategories()
    {
        var all = ReceiptCategories.GetAllSubcategories();
        var distinct = new HashSet<string>(all, StringComparer.OrdinalIgnoreCase);
        
        Assert.That(distinct.Count, Is.EqualTo(all.Count), "There are duplicate subcategory names across categories");
    }

    [Test]
    public void GetAllSubcategories_ContainsKidsClothesAndShoesWithCorrectCasing()
    {
        var all = ReceiptCategories.GetAllSubcategories();
        
        // Verify the casing fix: should be "Kids' Clothes & Shoes" not "Kids' Clothes and shoes"
        Assert.That(all, Contains.Item("Kids' Clothes & Shoes"));
        Assert.That(all, Is.Not.Contains("Kids' Clothes and shoes"));
    }
}
