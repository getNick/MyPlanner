using System;
using System.Linq;
using System.Threading.Tasks;
using MyPlanner.Data.Entities.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

[TestFixture]
public class TransactionTests : FinanceServiceTests_Base
{
    [Test]
    public async Task GetTransactionsAsync_ShouldReturnEmptyList_WhenNoTransactions()
    {
        var result = await _sut.GetTransactionsAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTransactionsAsync_ShouldReturnAllTransactions_WhenNoFilters()
    {
        var transaction1 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-1),
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Transaction 1"
        };

        var transaction2 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Income,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-2),
            Amount = 200m,
            Currency = Currency.UAH,
            Description = "Transaction 2"
        };

        _testContext.Transactions.AddRange(new[] { transaction1, transaction2 });
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionsAsync(_testUserId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetTransactionsAsync_ShouldFilterByUserId()
    {
        var transaction1 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-1),
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Transaction for user 123"
        };

        var transaction2 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "user-456",
            Type = TransactionType.Income,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-2),
            Amount = 200m,
            Currency = Currency.UAH,
            Description = "Transaction for user 456"
        };

        _testContext.Transactions.AddRange(new[] { transaction1, transaction2 });
        await _testContext.SaveChangesAsync();

        var resultForUser123 = await _sut.GetTransactionsAsync("user-123");
        Assert.That(resultForUser123, Has.Count.EqualTo(1));
        Assert.That(resultForUser123.First().Id, Is.EqualTo(transaction1.Id));

        var resultForUser456 = await _sut.GetTransactionsAsync("user-456");
        Assert.That(resultForUser456, Has.Count.EqualTo(1));
        Assert.That(resultForUser456.First().Id, Is.EqualTo(transaction2.Id));
    }

    [Test]
    public async Task GetTransactionsAsync_ShouldFilterByDateRange()
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        var transaction1 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = yesterday,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Yesterday"
        };

        var transaction2 = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Income,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = twoDaysAgo,
            Amount = 200m,
            Currency = Currency.UAH,
            Description = "Two days ago"
        };

        _testContext.Transactions.AddRange(new[] { transaction1, transaction2 });
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionsAsync(_testUserId, startDate: yesterday, endDate: now);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Id, Is.EqualTo(transaction1.Id));
    }

    [Test]
    public async Task GetTransactionAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetTransactionAsync(Guid.NewGuid(), _testUserId);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTransactionAsync_ShouldReturnTransaction_WhenFound()
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Transfer,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 50m,
            Currency = Currency.EURO,
            Description = "Test transaction"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionAsync(transaction.Id, _testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(transaction.Id));
    }

    [Test]
    public async Task CreateTransactionAsync_ShouldCreateAndReturnGuid()
    {
        var transaction = new Transaction
        {
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "New transaction",
            UserId = _testUserId
        };

        var result = await _sut.CreateTransactionAsync(_testUserId, transaction);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        var created = await _testContext.Transactions.FindAsync(result);
        Assert.That(created, Is.Not.Null);
        Assert.That(created.UserId, Is.EqualTo(_testUserId));
    }

    [Test]
    public async Task UpdateTransactionAsync_ShouldUpdateAndReturnTrue_WhenFound()
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Old description"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();

        var updateModel = new Transaction
        {
            Id = transaction.Id,
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = transaction.PaymentMethodId,
            Timestamp = DateTime.UtcNow,
            Amount = 150m,
            Currency = Currency.USD,
            Description = "Updated description"
        };

        var result = await _sut.UpdateTransactionAsync(_testUserId, updateModel);

        Assert.That(result, Is.True);
        var updated = await _testContext.Transactions.FindAsync(transaction.Id);
        Assert.That(updated!.Amount, Is.EqualTo(150m));
    }

    [Test]
    public async Task UpdateTransactionAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var updateModel = new Transaction
        {
            Id = nonExistentId,
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Should not update"
        };

        var result = await _sut.UpdateTransactionAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeleteTransactionAsync_ShouldDeleteAndReturnTrue_WhenFound()
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "To delete"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeleteTransactionAsync(transaction.Id, _testUserId);

        Assert.That(result, Is.True);
        var exists = await _testContext.Transactions.FindAsync(transaction.Id);
        Assert.That(exists, Is.Null);
    }

    [Test]
    public async Task DeleteTransactionAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var result = await _sut.DeleteTransactionAsync(nonExistentId, _testUserId);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task GetTransactionsAsync_ShouldNotReturnOtherUserTransactions()
    {
        var user1Transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-1),
            Amount = 100m,
            Currency = Currency.USD,
            Description = "User 1 transaction"
        };

        var user2Transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            Type = TransactionType.Income,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddDays(-2),
            Amount = 200m,
            Currency = Currency.UAH,
            Description = "User 2 transaction"
        };

        _testContext.Transactions.AddRange(new[] { user1Transaction, user2Transaction });
        await _testContext.SaveChangesAsync();

        var resultForUser1 = await _sut.GetTransactionsAsync("user-1");
        Assert.That(resultForUser1, Has.Count.EqualTo(1));
        Assert.That(resultForUser1.First().Id, Is.EqualTo(user1Transaction.Id));
    }

    [Test]
    public async Task UpdateTransactionAsync_ShouldFail_WhenUpdatingOtherUsersTransaction()
    {
        var otherUserTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "Old description"
        };

        _testContext.Transactions.Add(otherUserTransaction);
        await _testContext.SaveChangesAsync();

        var updateModel = new Transaction
        {
            Id = otherUserTransaction.Id,
            UserId = _testUserId,
            Type = TransactionType.Expense,
            PaymentMethodId = otherUserTransaction.PaymentMethodId,
            Timestamp = DateTime.UtcNow,
            Amount = 150m,
            Currency = Currency.USD,
            Description = "Updated description"
        };

        var result = await _sut.UpdateTransactionAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeleteTransactionAsync_ShouldFail_WhenDeletingOtherUsersTransaction()
    {
        var otherUserTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Type = TransactionType.Expense,
            PaymentMethodId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Amount = 100m,
            Currency = Currency.USD,
            Description = "To delete"
        };

        _testContext.Transactions.Add(otherUserTransaction);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeleteTransactionAsync(otherUserTransaction.Id, _testUserId);

        Assert.That(result, Is.False);
        // Verify it still exists
        var exists = await _testContext.Transactions.FindAsync(otherUserTransaction.Id);
        Assert.That(exists, Is.Not.Null);
    }
}
