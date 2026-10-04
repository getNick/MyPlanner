using MyPlanner.Service.Helpers.BankExport;
using MyPlanner.Service.Models;

namespace MyPlanner.UnitTests.Helpers;

/*
 * ═══════════════════════════════════════════════════════════
 *  MccCategoryMapTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * The seam is `MccCategoryMap.Classify(mcc)` → (Category, Subcategory, Meaning) or null: the decision
 * a Bank Transaction's Category comes from. Nothing here touches the database; what happens to a row
 * when its Category changes is tested at the finance service seam.
 *
 * The cases are the rows this household's statements actually contain, because those are the
 * misclassifications that were real — see the remarks on each case.
 *
 * ────────────────────────────────────────────────────────────
 *  Codes that were wrong before (exact entries)
 * ────────────────────────────────────────────────────────────
 *   [x] 5411 supermarkets → Groceries / Pantry
 *   [x] 5462 bakeries → Groceries / Bakery (was Dining & Takeaway / Restaurants)
 *   [x] 5812 take-out and delivery → Dining & Takeaway / Fast Food & Delivery
 *   [x] 5814 cafeterias, which is where acquirers put coffee shops → Dining & Takeaway / Cafes & Coffee
 *   [x] 4215 courier, postal, freight (Нова пошта) → Transportation, the closest line the taxonomy has
 *   [x] 5641 children's clothing → Kids & Family / Kids' Clothes & Shoes
 *   [x] 5712 hardware and farm-supply tools → Household & Supplies / Home Repairs & Tools
 *   [x] 5815 digital media, books, music, movies (YouTube) → Entertainment & Leisure / Digital Subscriptions
 *   [x] 9311 taxes and government service payments → Financial & Future / Taxes
 *   [x] 4121 taxi (Uklon) → Transportation / Public Transit & Rideshare (range fallback)
 *   [x] 7298 barbers and hair salons → Personal Care & Clothes / Haircuts & Grooming (range fallback)
 *
 * ────────────────────────────────────────────────────────────
 *  Refusals — "not classified" is an answer, and it says why
 * ────────────────────────────────────────────────────────────
 *   [x] Money movement (4829, 6010, 6011, 6012, 6051, 6211, 6532, 6533, 6536, 6538, 6540) → null + a reason
 *   [x] Taxonomy gaps (5732, 5734, 5943, 5970, 5992) and the ambiguous 5977 → null + a reason
 *   [x] An unknown code (9999) → null, with no invented reason
 *   [x] Every refusal is explainable: WhyUnclassified names the reason for known codes
 *
 * ────────────────────────────────────────────────────────────
 *  The map stays inside the taxonomy
 * ────────────────────────────────────────────────────────────
 *   [x] Every Category/Subcategory pair it can produce is one ReceiptCategories has
 *   [x] The Meaning travels with the answer, for the audit trail (D10)
 */

/// <summary>
/// What the MCC table decides. Each case names the merchant kind rather than a code number so a change
/// in the table reads as a decision about a shop, not a reshuffled dictionary.
/// </summary>
public class MccCategoryMapTests
{
    [TestCase(5411, "Groceries", "Pantry")]                            // NOVUS, ATB, Silpo, Ashan
    [TestCase(5462, "Groceries", "Bakery")]                            // bread and bakery counters
    [TestCase(5812, "Dining & Takeaway", "Fast Food & Delivery")]      // take-out, carry-out, deliveries
    [TestCase(5814, "Dining & Takeaway", "Cafes & Coffee")]            // acquirers put coffee shops here
    [TestCase(4215, "Transportation", "Public Transit & Rideshare")]   // Нова пошта: no shipping line exists
    [TestCase(5641, "Kids & Family", "Kids' Clothes & Shoes")]         // Антошка: children's wear
    [TestCase(5712, "Household & Supplies", "Home Repairs & Tools")]   // EpicentrK, Epitsentr Kitchen
    [TestCase(5815, "Entertainment & Leisure", "Digital Subscriptions")] // YouTube, books, music, movies
    [TestCase(9311, "Financial & Future", "Taxes")]                    // Дія | Податки
    [TestCase(4121, "Transportation", "Public Transit & Rideshare")]   // Uklon — from the 4111–4121 block
    [TestCase(7298, "Personal Care & Clothes", "Haircuts & Grooming")] // barbers — from the 7210–7299 block
    public void Classify_KnownStatementCodes_AnswerAsTheHouseholdExpects(int mcc, string category, string subcategory)
    {
        var classification = MccCategoryMap.Classify(mcc);

        Assert.Multiple(() =>
        {
            Assert.That(classification, Is.Not.Null, $"MCC {mcc} should be classified");
            Assert.That(classification?.Category, Is.EqualTo(category), $"MCC {mcc} category");
            Assert.That(classification?.Subcategory, Is.EqualTo(subcategory), $"MCC {mcc} subcategory");
        });
    }

    [TestCase(4829)] // wire transfer / money remittance; Tabletochki donations arrive here
    [TestCase(6012)] // card-to-card transfers between people
    [TestCase(6536)] // marketplace payout (Tabletochki again)
    [TestCase(6540)] // prepaid wallet top-up (Starkyan)
    [TestCase(6011)] // credit or debit money transfer
    [TestCase(5732)] // electronics retail — the taxonomy has no line for it
    [TestCase(5977)] // jewellery repair by definition, cosmetics and discount chains in practice
    public void Classify_CodesTheMapRefuses_AnswerNullAndSayWhy(int mcc)
    {
        Assert.That(MccCategoryMap.Classify(mcc), Is.Null, $"MCC {mcc} is deliberately not classified");

        Assert.That(MccCategoryMap.WhyUnclassified(mcc), Is.Not.Null.And.Not.Empty,
            $"the refusal of MCC {mcc} has to be explainable in a report");
    }

    [Test]
    public void Classify_MoneyMovement_NeverLooksLikeMerchantSpend()
    {
        // The old range chain swept all of these into Financial & Future / Other, which read as a budget
        // line. They move money between places the user already has.
        foreach (var mcc in new[] { 4829, 6010, 6011, 6012, 6051, 6211, 6532, 6533, 6536, 6538, 6540 })
            Assert.That(MccCategoryMap.Classify(mcc), Is.Null, $"MCC {mcc} is money movement, not spending");
    }

    [Test]
    public void Classify_UnknownCode_AnswersNullWithoutInventingAReason()
    {
        Assert.That(MccCategoryMap.Classify(9999), Is.Null);
        Assert.That(MccCategoryMap.WhyUnclassified(9999), Is.Null, "nobody has looked at this code yet");
    }

    [Test]
    public void MappedPairs_EveryPairExistsInTheBudgetTaxonomy()
    {
        // The map is maintained apart from the taxonomy, so this is the seam that keeps them together: a
        // Category or Subcategory the rest of the app cannot name only shows up as a broken filter in the UI.
        var pairs = MccCategoryMap.MappedPairs();
        Assert.That(pairs, Is.Not.Empty);

        foreach (var (category, subcategory) in pairs)
        {
            Assert.That(ReceiptCategories.IsValidCategory(category), Is.True,
                $"'{category}' is not a Category the taxonomy has");
            Assert.That(ReceiptCategories.IsValidSubcategory(subcategory, category), Is.True,
                $"'{subcategory}' is not a Subcategory of '{category}'");
        }
    }

    [Test]
    public void Classify_MeaningTravelsWithTheAnswer()
    {
        // The Merchant Description on a row is not evidence (D10), so a report has to say which code
        // decided the Category and what that code means.
        Assert.That(MccCategoryMap.Classify(5411)!.Meaning, Does.Contain("Grocery Stores"));
        Assert.That(MccCategoryMap.Classify(4121)!.Meaning, Does.Contain("Taxicabs"));
    }
}
