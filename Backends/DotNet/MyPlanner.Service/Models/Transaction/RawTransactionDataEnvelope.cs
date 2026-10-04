using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyPlanner.Service.Models;

/// <summary>
/// The <strong>Raw Transaction Data Envelope</strong> — what one <c>Transaction.RawTransactionData</c>
/// column holds once a row has stored uploads behind it. It describes the source file and the relevant
/// part of it; the file itself is never in here (D4).
///
/// <para>The keys are domain words rather than numbered slots, so nothing has to remember what a bare
/// number meant — that is how today's column dies.</para>
/// </summary>
public sealed record RawTransactionDataEnvelope(BillRawData? Bill = null, BankRawData? Bank = null)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A row with no raw material at all — a hand-entered transaction.</summary>
    public static RawTransactionDataEnvelope Empty { get; } = new();

    /// <summary>
    /// Reads the column. Every shape it can meet is tolerated, because the column holds three
    /// generations of data and a reader must never guess: an envelope, a legacy bare statement line,
    /// or nothing at all. Malformed JSON reads as no provenance rather than throwing.
    /// </summary>
    public static RawTransactionDataEnvelope Read(string? rawTransactionData)
    {
        if (string.IsNullOrWhiteSpace(rawTransactionData))
            return Empty;

        var text = rawTransactionData.Trim();

        // Anything not in envelope form predates stored files: the column held the bare statement line.
        if (!text.StartsWith('{'))
            return new RawTransactionDataEnvelope(Bank: new BankRawData(LegacyLine: text));

        try
        {
            return JsonSerializer.Deserialize<RawTransactionDataEnvelope>(text, _json) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>The form written to the column. Only the sides actually present are written.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _json);

    public RawTransactionDataEnvelope WithBill(BillRawData? bill) => this with { Bill = bill };

    public RawTransactionDataEnvelope WithBank(BankRawData? bank) => this with { Bank = bank };
}

/// <summary>The Bill side: which image the Bill was confirmed from.</summary>
/// <param name="ImageKey">FileKey of the <strong>Bill Image</strong> in the Bucket.</param>
public sealed record BillRawData(string ImageKey);

/// <summary>The Bank side: which file the row came from, and where in it.</summary>
/// <param name="StatementFileKey">FileKey of the <strong>Statement File</strong>; null for a legacy row.</param>
/// <param name="RowNumber">1-based line in the statement file (header is line 1); absent for a receipt row.</param>
/// <param name="Profile">Which bank profile read the file — which columns the numbers were read with.</param>
/// <param name="LegacyLine">A row imported before Statement Files were kept: the bare statement line,
/// verbatim. Nothing new writes it; a merge carries it forward so nothing is lost.</param>
public sealed record BankRawData(
    string? StatementFileKey = null,
    int? RowNumber = null,
    string? Profile = null,
    string? LegacyLine = null);
