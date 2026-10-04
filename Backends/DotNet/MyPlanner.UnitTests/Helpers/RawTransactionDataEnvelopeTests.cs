using System.Text.Json;
using MyPlanner.Service.Models;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Helpers;

/*
 * ═══════════════════════════════════════════════════════════
 *  RawTransactionDataEnvelopeTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the envelope reader: Read(string) and ToJson(). It is the only code in v1 that reads
 * RawTransactionData back, and it has to be strict enough that a future feature cannot guess its way
 * through a column holding three different generations of data.
 *
 * ────────────────────────────────────────────────────────────
 *  What it writes
 * ────────────────────────────────────────────────────────────
 *   [x] only the sides actually present, under exactly the keys the design names
 *   [x] a Bill side carries its Image FileKey; a Bank side carries file key, row number and profile
 *
 * ────────────────────────────────────────────────────────────
 *  What it tolerates
 * ────────────────────────────────────────────────────────────
 *   [x] null reads as empty (a hand-entered transaction has no raw material at all)
 *   [x] a legacy bare statement line reads as a Bank Transaction's line, not lost and not crashing
 *   [x] malformed JSON reads as empty — never an exception
 *   [x] keys it does not know are ignored
 *
 * ────────────────────────────────────────────────────────────
 *  Merging (what reconciliation needs)
 * ────────────────────────────────────────────────────────────
 *   [x] a Bill's own side survives while the deleted Bank row's side is taken over
 */

[TestFixture]
public class RawTransactionDataEnvelopeTests
{
    private const string LegacyLine = "14.09.2026 09:41;СILPO;5457;-234,50";

    [Test]
    public void Read_Null_ReadsAsEmpty()
    {
        var envelope = RawTransactionDataEnvelope.Read(null);

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bill, Is.Null, "a hand-entered transaction has no raw material");
            Assert.That(envelope.Bank, Is.Null);
        });
    }

    [Test]
    public void Read_LegacyBareStatementLine_SurvivesAsABankLine()
    {
        var envelope = RawTransactionDataEnvelope.Read(LegacyLine);

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bank, Is.Not.Null,
                "rows imported before files were kept still have raw material — the line itself");
            Assert.That(envelope.Bank!.LegacyLine, Is.EqualTo(LegacyLine), "kept verbatim, never parsed away");
            Assert.That(envelope.Bank.StatementFileKey, Is.Null, "there is no Statement File behind it");
            Assert.That(envelope.Bill, Is.Null);
        });
    }

    [Test]
    public void Read_MalformedJson_ReadsAsEmptyRatherThanThrowing()
    {
        var envelope = RawTransactionDataEnvelope.Read("{bill:{truncat");

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bill, Is.Null);
            Assert.That(envelope.Bank, Is.Null);
        });
    }

    [Test]
    public void Read_IgnoresKeysItDoesNotKnow()
    {
        var envelope = RawTransactionDataEnvelope.Read(
            """{"bill":{"imageKey":"aaa.png","futureThing":42},"crystalBall":{"confidence":0.9}}""");

        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bill!.ImageKey, Is.EqualTo("aaa.png"));
            Assert.That(envelope.Bank, Is.Null);
        });
    }

    [Test]
    public void ToJson_WritesExactlyTheSidesPresent()
    {
        var billOnly = new RawTransactionDataEnvelope(new BillRawData("aef71bb.png"), Bank: null).ToJson();
        var bothSides = new RawTransactionDataEnvelope(
            new BillRawData("aef71bb.png"),
            new BankRawData("9dc51c3.csv", RowNumber: 8, Profile: "monobank", LegacyLine: null)).ToJson();

        Assert.Multiple(() =>
        {
            Assert.That(Keys(billOnly), Is.EquivalentTo(new[] { "bill.imageKey" }),
                "a receipt row records its image and nothing else");
            Assert.That(Keys(bothSides), Is.EquivalentTo(new[]
            {
                "bill.imageKey", "bank.statementFileKey", "bank.rowNumber", "bank.profile"
            }), "the keys are domain words, so no code has to remember what a number means (D5)");
        });
    }

    [Test]
    public void WithBank_TakesTheDeletedRowsProvenanceAndKeepsTheBillImage()
    {
        var bill = RawTransactionDataEnvelope.Read("""{"bill":{"imageKey":"aef71bb.png"}}""");
        var bankRow = RawTransactionDataEnvelope.Read(
            """{"bank":{"statementFileKey":"9dc51c3.csv","rowNumber":8,"profile":"monobank"}}""");

        var merged = bill.WithBank(bankRow.Bank);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Bill!.ImageKey, Is.EqualTo("aef71bb.png"),
                "the Bill Image is the last trace of a deleted Bill (Q15/Q20)");
            Assert.That(merged.Bank!.StatementFileKey, Is.EqualTo("9dc51c3.csv"));
            Assert.That(merged.Bank.RowNumber, Is.EqualTo(8));
            Assert.That(merged.Bank.Profile, Is.EqualTo("monobank"));
        });
    }

    /// <summary>Dotted path of every leaf property the stored JSON actually contains.</summary>
    private static string[] Keys(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .SelectMany(side => side.Value.EnumerateObject().Select(field => $"{side.Name}.{field.Name}"))
            .ToArray();
    }
}
