using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ═══════════════════════════════════════════════════════════
 *  BillImageStorageTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the finance service: PreviewReceiptAsync / ConfirmReceiptAsync, with the model mocked and
 * a real Bucket in a throwaway folder. Assertions are on what landed on disk and on the row's
 * Raw Transaction Data Envelope — never on how the store works internally.
 *
 * ────────────────────────────────────────────────────────────
 *  Confirming writes the paper down
 * ────────────────────────────────────────────────────────────
 *   [x] the Bill Image is stored and its FileKey recorded in {bill:{imageKey}} (D1/D8)
 *   [x] the stored bytes are the upload verbatim, not a re-encoded copy (D3)
 *   [x] OCR preview stores neither file nor row — the draft is not evidence yet (Q2/Q5)
 *
 * ────────────────────────────────────────────────────────────
 *  The cap, enforced where it saves work
 * ────────────────────────────────────────────────────────────
 *   [x] an over-limit image is refused before the model is ever called (D11)
 *   [x] an over-limit confirm writes no row and leaves no file (D12)
 */

[TestFixture]
public class BillImageStorageTests : FinanceServiceTests_Base
{
    /// <summary>sha256 of "bill-image-bytes", computed independently (printf | sha256sum).</summary>
    private const string ImageHash = "a56c1cdcfbfe658f8e85f8ed3591ddb6377be44499ac7dcb64489658312c58d0";

    private static Stream Image() => new MemoryStream(Encoding.UTF8.GetBytes("bill-image-bytes"));

    private async Task<Transaction> ConfirmBillAsync(Stream image, string contentType = "image/png")
    {
        var bill = await _sut.ConfirmReceiptAsync(new ConfirmReceiptRequest
        {
            FileStream = image,
            ContentType = contentType,
            Receipt = new ReceiptDto
            {
                MerchantName = "Silpo",
                Timestamp = new DateTime(2026, 9, 14, 9, 41, 0),
                TotalAmount = 234.50m,
                Currency = "UAH",
            },
        }, _testUserId);

        return GetTransaction(bill.Id);
    }

    [Test]
    public async Task ConfirmingABill_StoresItsImageAndNamesTheFileOnTheRow()
    {
        var bill = await ConfirmBillAsync(Image());

        var envelope = EnvelopeOf(bill);
        Assert.Multiple(() =>
        {
            Assert.That(envelope.Bill!.ImageKey, Is.EqualTo($"{ImageHash}.png"),
                "the Bill keeps exactly one pointer to its image (D1)");
            Assert.That(FileKeysInBucket(), Is.EquivalentTo(new[] { $"{ImageHash}.png" }));
            Assert.That(envelope.Bank, Is.Null, "a Bill alone has no Bank side to describe");
        });
    }

    [Test]
    public async Task ConfirmingABill_StoredBytesAreTheUploadVerbatim()
    {
        var bill = await ConfirmBillAsync(Image());

        var stored = await StoredBytesAsync(EnvelopeOf(bill).Bill!.ImageKey);

        Assert.That(Encoding.UTF8.GetString(stored), Is.EqualTo("bill-image-bytes"),
            "the Bucket holds the original upload, never a re-encoded copy (D3)");
    }

    [Test]
    public async Task PreviewingAReceipt_StoresNeitherFileNorRow()
    {
        await _sut.PreviewReceiptAsync(new ProcessReceiptRequest { FileStream = Image(), ContentType = "image/png" });

        Assert.Multiple(() =>
        {
            Assert.That(FileKeysInBucket(), Is.Empty, "OCR is not evidence yet (Q2)");
            Assert.That(_testContext.Transactions.Local, Is.Empty);
        });
    }

    [Test]
    public async Task AnOverLimitBillImage_IsRefusedBeforeTheModelIsCalled()
    {
        var tooLarge = new MemoryStream(new byte[UploadLimit.MaxBytes + 1]);

        var ex = Assert.ThrowsAsync<UploadTooLargeException>(
            async () => await _sut.PreviewReceiptAsync(new ProcessReceiptRequest { FileStream = tooLarge, ContentType = "image/png" }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(UploadLimit.MaxMegabytes));
            _llmServiceMock.Verify(x => x.SendImageRequestAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()), Times.Never);
        });
    }

    [Test]
    public async Task AnOverLimitBillImage_WritesNoRowAndLeavesNoFile()
    {
        var tooLarge = new MemoryStream(new byte[UploadLimit.MaxBytes + 1]);

        Assert.ThrowsAsync<UploadTooLargeException>(() => ConfirmBillAsync(tooLarge));

        Assert.Multiple(() =>
        {
            Assert.That(_testContext.Transactions.Local, Is.Empty);
            Assert.That(FileKeysInBucket(), Is.Empty, "a refused upload never reaches the Bucket (D12)");
        });
    }
}
