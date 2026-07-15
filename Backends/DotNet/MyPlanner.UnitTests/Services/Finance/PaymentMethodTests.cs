using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Models;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ═══════════════════════════════════════════════════════════
 *  PaymentMethodTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [!] = stub / tests DB state only (not real pipeline)
 *   [ ] = not yet implemented
 *
 * ────────────────────────────────────────────────────────────
 *  PaymentMethod read
 * ────────────────────────────────────────────────────────────
 *   [x] GetPaymentMethodsAsync — empty list, returns all, user isolation
 *   [x] GetPaymentMethodAsync — not found, found by id + userId
 *
 * ────────────────────────────────────────────────────────────
 *  PaymentMethod create / update
 * ────────────────────────────────────────────────────────────
 *   [x] CreatePaymentMethodAsync — returns id, sets Id, forces ownership
 *   [x] CreatePaymentMethodAsync — persists BankProvider
 *   [x] CreatePaymentMethodAsync — blank BankProvider stored as null
 *   [x] UpdatePaymentMethodAsync — updates fields, not found, other user refused
 *   [x] UpdatePaymentMethodAsync — updates BankProvider
 *
 * ────────────────────────────────────────────────────────────
 *  PaymentMethod delete
 * ────────────────────────────────────────────────────────────
 *   [x] DeletePaymentMethodAsync — deletes when unused, not found, other user refused
 *   [x] DeletePaymentMethodAsync — refused when a transaction was paid by the method
 *   [x] DeletePaymentMethodAsync — refused when a transaction transfers into the method
 *
 * ────────────────────────────────────────────────────────────
 *  Cash Wallet (an ordinary Payment Method — nothing mints it)
 * ────────────────────────────────────────────────────────────
 *   [x] CRUD accepts a `Cash` method (hand-made wallets are legal)
 *   [x] Update can convert a card into a Cash Wallet
 *   [x] Cash Wallets are deleted under the same in-use refusal as anything else
 *   [ ] Cash Wallet is chain-verified like a card (ticket 04)
 *
 * ────────────────────────────────────────────────────────────
 *  Bank Provider (statement format; bank-backed types only)
 * ────────────────────────────────────────────────────────────
 *   [x] Create persists a provider, and blank arrives as null
 *   [x] Update sets a provider
 *   [x] A provider sent with a non-bank type is stored as null (write not refused)
 *
 * ────────────────────────────────────────────────────────────
 *  Should be implemented
 * ────────────────────────────────────────────────────────────
 *   [ ] Owner filtering on transaction queries (ticket 02/07)
 */
[TestFixture]
public class PaymentMethodTests : FinanceServiceTests_Base
{
    [Test]
    public async Task GetPaymentMethodsAsync_ShouldReturnEmptyList_WhenNoPaymentMethodsExist()
    {
        var result = await _sut.GetPaymentMethodsAsync(_testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPaymentMethodsAsync_ShouldReturnAllPaymentMethods()
    {
        var paymentMethod1 = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Visa Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        var paymentMethod2 = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Savings Account",
            Type = PaymentMethodType.SavingsAccount,
            Currency = Currency.UAH
        };

        _testContext.PaymentMethods.AddRange(new[] { paymentMethod1, paymentMethod2 });
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetPaymentMethodsAsync(_testUserId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(pm => pm.Id), Contains.Item(paymentMethod1.Id));
        Assert.That(result.Select(pm => pm.Id), Contains.Item(paymentMethod2.Id));
    }

    [Test]
    public async Task GetPaymentMethodAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetPaymentMethodAsync(Guid.NewGuid(), _testUserId);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetPaymentMethodAsync_ShouldReturnPaymentMethod_WhenFound()
    {
        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Mastercard",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.EURO
        };

        _testContext.PaymentMethods.Add(paymentMethod);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetPaymentMethodAsync(paymentMethod.Id, _testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(paymentMethod.Id));
        Assert.That(result.Name, Is.EqualTo("Mastercard"));
    }

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldCreateAndReturnGuid()
    {
        var paymentMethod = new PaymentMethod
        {
            Name = "New Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD,
            UserId = _testUserId
        };

        var result = await _sut.CreatePaymentMethodAsync(_testUserId, paymentMethod);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        var created = await _testContext.PaymentMethods.FindAsync(result);
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Name, Is.EqualTo("New Card"));
        Assert.That(created.UserId, Is.EqualTo(_testUserId));
    }

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldSetIdOnEntity()
    {
        // Not Cash: CRUD cannot mint a Cash Wallet (see the guard tests below).
        var paymentMethod = new PaymentMethod
        {
            Name = "Test",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            UserId = _testUserId
        };

        var id = await _sut.CreatePaymentMethodAsync(_testUserId, paymentMethod);

        Assert.That(id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(paymentMethod.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldUpdateAndReturnTrue_WhenFound()
    {
        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Old Name",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        _testContext.PaymentMethods.Add(paymentMethod);
        await _testContext.SaveChangesAsync();

        var updateModel = new PaymentMethod
        {
            Id = paymentMethod.Id,
            UserId = _testUserId,
            Name = "Updated Name",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.EURO
        };

        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, updateModel);

        Assert.That(result, Is.True);
        var updated = await _testContext.PaymentMethods.FindAsync(paymentMethod.Id);
        Assert.That(updated!.Name, Is.EqualTo("Updated Name"));
        Assert.That(updated.Currency, Is.EqualTo(Currency.EURO));
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var updateModel = new PaymentMethod
        {
            Id = nonExistentId,
            UserId = _testUserId,
            Name = "Should Not Update",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldDeleteAndReturnTrue_WhenFound()
    {
        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "To Delete",
            Type = PaymentMethodType.Cash,
            Currency = Currency.UAH
        };

        _testContext.PaymentMethods.Add(paymentMethod);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeletePaymentMethodAsync(paymentMethod.Id, _testUserId);

        Assert.That(result.Deleted, Is.True);
        var exists = await _testContext.PaymentMethods.FindAsync(paymentMethod.Id);
        Assert.That(exists, Is.Null);
    }

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var result = await _sut.DeletePaymentMethodAsync(nonExistentId, _testUserId);

        Assert.That(result.Deleted, Is.False);
        Assert.That(result.Status, Is.EqualTo(PaymentMethodDeletionStatus.NotFound));
    }

    // ── BankProvider (the CSV profile a statement is parsed with) ───

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldPersistBankProvider()
    {
        var paymentMethod = new PaymentMethod
        {
            Name = "Monobank card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "Monobank",
            UserId = _testUserId
        };

        var id = await _sut.CreatePaymentMethodAsync(_testUserId, paymentMethod);

        var created = await _testContext.PaymentMethods.FindAsync(id);
        Assert.That(created!.BankProvider, Is.EqualTo("Monobank"));
    }

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldStoreNullBankProvider_WhenBankProviderIsBlank()
    {
        var paymentMethod = new PaymentMethod
        {
            Name = "Unlabelled card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "   ",
            UserId = _testUserId
        };

        var id = await _sut.CreatePaymentMethodAsync(_testUserId, paymentMethod);

        var created = await _testContext.PaymentMethods.FindAsync(id);
        Assert.That(created!.BankProvider, Is.Null);
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldUpdateBankProvider()
    {
        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = null
        };

        _testContext.PaymentMethods.Add(paymentMethod);
        await _testContext.SaveChangesAsync();

        var updateModel = new PaymentMethod
        {
            Id = paymentMethod.Id,
            UserId = _testUserId,
            Name = "Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "monobank "
        };

        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, updateModel);

        Assert.That(result, Is.True);
        var updated = await _testContext.PaymentMethods.FindAsync(paymentMethod.Id);
        Assert.That(updated!.BankProvider, Is.EqualTo("monobank"));
    }

    // ── Refusal to delete money in use (ticket 01) ─────────────────

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldRefuse_WhenTransactionsExistOnTheMethod()
    {
        var paymentMethod = await SeedPaymentMethodAsync("Spent card", PaymentMethodType.BankCard);
        await SeedTransactionAsync(paymentMethod.Id, null, 128.50m);

        var result = await _sut.DeletePaymentMethodAsync(paymentMethod.Id, _testUserId);

        Assert.That(result.Deleted, Is.False);
        Assert.That(result.Status, Is.EqualTo(PaymentMethodDeletionStatus.InUse));
        Assert.That(result.TransactionCount, Is.EqualTo(1));
        Assert.That(result.Explanation, Does.Contain("1"));

        // Nothing is silently orphaned: the method and its transaction both survive.
        Assert.That(await _testContext.PaymentMethods.FindAsync(paymentMethod.Id), Is.Not.Null);
        Assert.That(await _testContext.Transactions.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldRefuse_WhenTransactionTransfersIntoTheMethod()
    {
        var cashWallet = await SeedPaymentMethodAsync("Cash Wallet", PaymentMethodType.Cash);
        var sourceCard = await SeedPaymentMethodAsync("Source card", PaymentMethodType.BankCard);
        await SeedTransactionAsync(sourceCard.Id, cashWallet.Id, 500m);

        var result = await _sut.DeletePaymentMethodAsync(cashWallet.Id, _testUserId);

        Assert.That(result.Deleted, Is.False);
        Assert.That(result.Status, Is.EqualTo(PaymentMethodDeletionStatus.InUse));
        Assert.That(result.TransactionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldRefuse_WhenAnotherUsersTransactionReferencesTheMethod()
    {
        // The database restricts the FK for *every* transaction, not only this household's,
        // so the rule counts every reference rather than letting SaveChanges throw.
        var paymentMethod = await SeedPaymentMethodAsync("Other user card", PaymentMethodType.BankCard);
        _testContext.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Type = TransactionType.Expense,
            PaymentMethodId = paymentMethod.Id,
            Amount = 10m,
            Currency = Currency.UAH,
            Description = "Other user purchase"
        });
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeletePaymentMethodAsync(paymentMethod.Id, _testUserId);

        Assert.That(result.Deleted, Is.False);
        Assert.That(result.Status, Is.EqualTo(PaymentMethodDeletionStatus.InUse));
    }

    private async Task<PaymentMethod> SeedPaymentMethodAsync(string name, PaymentMethodType type)
    {
        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = name,
            Type = type,
            Currency = Currency.UAH
        };

        _testContext.PaymentMethods.Add(paymentMethod);
        await _testContext.SaveChangesAsync();
        return paymentMethod;
    }

    private async Task SeedTransactionAsync(Guid? fromPaymentMethodId, Guid? toPaymentMethodId, decimal amount)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = toPaymentMethodId.HasValue ? TransactionType.Transfer : TransactionType.Expense,
            PaymentMethodId = fromPaymentMethodId,
            ToPaymentMethodId = toPaymentMethodId,
            Amount = amount,
            Currency = Currency.UAH,
            Description = "Seeded transaction"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetPaymentMethodsAsync_ShouldNotReturnOtherUserPayments()
    {
        var user1Method = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "User 1 Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        var user2Method = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            Name = "User 2 Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.EURO
        };

        _testContext.PaymentMethods.AddRange(new[] { user1Method, user2Method });
        await _testContext.SaveChangesAsync();

        var resultForUser1 = await _sut.GetPaymentMethodsAsync("user-1");
        Assert.That(resultForUser1, Has.Count.EqualTo(1));
        Assert.That(resultForUser1.First().Id, Is.EqualTo(user1Method.Id));
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldFail_WhenUpdatingOtherUsersPayment()
    {
        var otherUserPayment = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Name = "Old Name",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        _testContext.PaymentMethods.Add(otherUserPayment);
        await _testContext.SaveChangesAsync();

        var updateModel = new PaymentMethod
        {
            Id = otherUserPayment.Id,
            UserId = _testUserId,
            Name = "Updated Name",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.EURO
        };

        // Try to update with different user ID - should fail because not owned
        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeletePaymentMethodAsync_ShouldFail_WhenDeletingOtherUsersPayment()
    {
        var otherUserPayment = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Name = "To Delete",
            Type = PaymentMethodType.Cash,
            Currency = Currency.UAH
        };

        _testContext.PaymentMethods.Add(otherUserPayment);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeletePaymentMethodAsync(otherUserPayment.Id, _testUserId);

        // Another household's payment method is simply not there as far as this one is concerned.
        Assert.That(result.Deleted, Is.False);
        Assert.That(result.Status, Is.EqualTo(PaymentMethodDeletionStatus.NotFound));

        // Verify it still exists
        var exists = await _testContext.PaymentMethods.FindAsync(otherUserPayment.Id);
        Assert.That(exists, Is.Not.Null);
    }

    // ── Cash Wallets are ordinary methods: nothing mints them, CRUD accepts them ───

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldAcceptACashMethod()
    {
        // A Cash Wallet exists because someone made one. There is no auto-mint and no
        // single-wallet reservation, so CRUD accepts `Cash` like any other type.
        var handmade = new PaymentMethod
        {
            Name = "Cash jar",
            Type = PaymentMethodType.Cash,
            Currency = Currency.UAH,
            UserId = _testUserId
        };

        var id = await _sut.CreatePaymentMethodAsync(_testUserId, handmade);

        var created = await _testContext.PaymentMethods.FindAsync(id);
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Type, Is.EqualTo(PaymentMethodType.Cash));
        Assert.That(created.Name, Is.EqualTo("Cash jar"));
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldConvertACardIntoACashWallet()
    {
        var card = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Visa Card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.USD
        };

        _testContext.PaymentMethods.Add(card);
        await _testContext.SaveChangesAsync();

        var asCash = new PaymentMethod
        {
            Id = card.Id,
            UserId = _testUserId,
            Name = "Visa cash",
            Type = PaymentMethodType.Cash,
            Currency = Currency.USD
        };

        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, asCash);

        Assert.That(result, Is.True);
        var stored = await _testContext.PaymentMethods.FindAsync(card.Id);
        Assert.That(stored!.Type, Is.EqualTo(PaymentMethodType.Cash));
        Assert.That(stored.Name, Is.EqualTo("Visa cash"));
    }

    // ── Bank Provider applies to bank-backed types only ───

    [Test]
    public async Task CreatePaymentMethodAsync_ShouldStoreNullProvider_WhenTypeIsNotABankAccount()
    {
        // A Cash or Other method has no statement format, so a provider sent with one is
        // dropped rather than failing the write: the field does not apply to that shape.
        var cashWithProvider = new PaymentMethod
        {
            Name = "Cash jar",
            Type = PaymentMethodType.Cash,
            Currency = Currency.UAH,
            BankProvider = "Monobank",
            UserId = _testUserId
        };

        var id = await _sut.CreatePaymentMethodAsync(_testUserId, cashWithProvider);

        var created = await _testContext.PaymentMethods.FindAsync(id);
        Assert.That(created!.BankProvider, Is.Null);
    }

    [Test]
    public async Task UpdatePaymentMethodAsync_ShouldStoreNullProvider_WhenTypeIsNotABankAccount()
    {
        var card = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Monobank black",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "Monobank"
        };

        _testContext.PaymentMethods.Add(card);
        await _testContext.SaveChangesAsync();

        var asOther = new PaymentMethod
        {
            Id = card.Id,
            UserId = _testUserId,
            Name = "Something else",
            Type = PaymentMethodType.Other,
            Currency = Currency.UAH,
            BankProvider = "Monobank"
        };

        var result = await _sut.UpdatePaymentMethodAsync(_testUserId, asOther);

        Assert.That(result, Is.True);
        var stored = await _testContext.PaymentMethods.FindAsync(card.Id);
        Assert.That(stored!.BankProvider, Is.Null);
    }
}
