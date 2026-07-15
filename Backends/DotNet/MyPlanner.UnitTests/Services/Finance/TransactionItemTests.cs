using System;
using System.Linq;
using System.Threading.Tasks;
using MyPlanner.Data.Entities.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

[TestFixture]
public class TransactionItemTests : FinanceServiceTests_Base
{
    [Test]
    public async Task GetTransactionItemsAsync_ShouldReturnEmptyList_WhenNoItems()
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
            Description = "Transaction with no items"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionItemsAsync(transaction.Id, _testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTransactionItemsAsync_ShouldReturnEmptyList_WhenTransactionNotOwned()
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
            Description = "Other user's transaction"
        };

        _testContext.Transactions.Add(otherUserTransaction);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionItemsAsync(otherUserTransaction.Id, _testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTransactionItemsAsync_ShouldReturnItems_ForValidTransactionId()
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
            Description = "Transaction"
        };

        var item1 = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Name = "Item 1",
            FullName = "Full Item 1",
            Quantity = 2,
            PricePerUnit = 10m,
            TotalPrice = 20m
        };

        var item2 = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Name = "Item 2",
            FullName = "Full Item 2",
            Quantity = 1,
            PricePerUnit = 30m,
            TotalPrice = 30m
        };

        _testContext.Transactions.AddRange(new[] { transaction });
        _testContext.TransactionItems.AddRange(new[] { item1, item2 });
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionItemsAsync(transaction.Id, _testUserId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(ti => ti.Id), Contains.Item(item1.Id));
        Assert.That(result.Select(ti => ti.Id), Contains.Item(item2.Id));
    }

    [Test]
    public async Task GetTransactionItemAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetTransactionItemAsync(Guid.NewGuid(), _testUserId);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTransactionItemAsync_ShouldReturnNull_WhenNotOwned()
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
            Description = "Other user's transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = otherUserTransaction.Id,
            Name = "Test Item",
            FullName = "Full Test Item",
            Quantity = 1,
            PricePerUnit = 50m,
            TotalPrice = 50m
        };

        _testContext.Transactions.Add(otherUserTransaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionItemAsync(item.Id, _testUserId);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTransactionItemAsync_ShouldReturnItem_WhenFound()
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
            Description = "Transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Name = "Test Item",
            FullName = "Full Test Item",
            Quantity = 1,
            PricePerUnit = 50m,
            TotalPrice = 50m
        };

        _testContext.Transactions.Add(transaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var result = await _sut.GetTransactionItemAsync(item.Id, _testUserId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(item.Id));
        Assert.That(result.Name, Is.EqualTo("Test Item"));
    }

    [Test]
    public async Task CreateTransactionItemAsync_ShouldCreateAndReturnGuid()
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
            Description = "Transaction"
        };

        _testContext.Transactions.Add(transaction);
        await _testContext.SaveChangesAsync();

        var item = new TransactionItem
        {
            TransactionId = transaction.Id,
            Name = "New Item",
            FullName = "Full New Item",
            Quantity = 1,
            PricePerUnit = 25m,
            TotalPrice = 25m
        };

        var result = await _sut.CreateTransactionItemAsync(_testUserId, item);

        Assert.That(result, Is.Not.EqualTo(Guid.Empty));
        var created = await _testContext.TransactionItems.FindAsync(result);
        Assert.That(created, Is.Not.Null);
    }

    [Test]
    public async Task CreateTransactionItemAsync_ShouldThrow_WhenTransactionNotOwned()
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
            Description = "Other user's transaction"
        };

        _testContext.Transactions.Add(otherUserTransaction);
        await _testContext.SaveChangesAsync();

        var item = new TransactionItem
        {
            TransactionId = otherUserTransaction.Id,
            Name = "New Item",
            FullName = "Full New Item",
            Quantity = 1,
            PricePerUnit = 25m,
            TotalPrice = 25m
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _sut.CreateTransactionItemAsync(_testUserId, item));

        Assert.That(ex!.Message, Does.Contain("not found for user"));
    }

    [Test]
    public async Task UpdateTransactionItemAsync_ShouldUpdateAndReturnTrue_WhenFound()
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
            Description = "Transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Name = "Old Name",
            FullName = "Full Old Name",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        _testContext.Transactions.Add(transaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var updateModel = new TransactionItem
        {
            Id = item.Id,
            TransactionId = item.TransactionId,
            Name = "Updated Name",
            FullName = "Full Updated Name",
            Quantity = 2,
            PricePerUnit = 15m,
            TotalPrice = 30m
        };

        var result = await _sut.UpdateTransactionItemAsync(_testUserId, updateModel);

        Assert.That(result, Is.True);
        var updated = await _testContext.TransactionItems.FindAsync(item.Id);
        Assert.That(updated!.Name, Is.EqualTo("Updated Name"));
    }

    [Test]
    public async Task UpdateTransactionItemAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var updateModel = new TransactionItem
        {
            Id = nonExistentId,
            TransactionId = Guid.NewGuid(),
            Name = "Should Not Update",
            FullName = "Full Should Not Update",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        var result = await _sut.UpdateTransactionItemAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateTransactionItemAsync_ShouldReturnFalse_WhenNotOwned()
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
            Description = "Other user's transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = otherUserTransaction.Id,
            Name = "Old Name",
            FullName = "Full Old Name",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        _testContext.Transactions.Add(otherUserTransaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var updateModel = new TransactionItem
        {
            Id = item.Id,
            TransactionId = item.TransactionId,
            Name = "Should Not Update",
            FullName = "Full Should Not Update",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        var result = await _sut.UpdateTransactionItemAsync(_testUserId, updateModel);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeleteTransactionItemAsync_ShouldDeleteAndReturnTrue_WhenFound()
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
            Description = "Transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Name = "To Delete",
            FullName = "Full To Delete",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        _testContext.Transactions.Add(transaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeleteTransactionItemAsync(item.Id, _testUserId);

        Assert.That(result, Is.True);
        var exists = await _testContext.TransactionItems.FindAsync(item.Id);
        Assert.That(exists, Is.Null);
    }

    [Test]
    public async Task DeleteTransactionItemAsync_ShouldReturnFalse_WhenNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var result = await _sut.DeleteTransactionItemAsync(nonExistentId, _testUserId);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeleteTransactionItemAsync_ShouldReturnFalse_WhenNotOwned()
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
            Description = "Other user's transaction"
        };

        var item = new TransactionItem
        {
            Id = Guid.NewGuid(),
            TransactionId = otherUserTransaction.Id,
            Name = "To Delete",
            FullName = "Full To Delete",
            Quantity = 1,
            PricePerUnit = 10m,
            TotalPrice = 10m
        };

        _testContext.Transactions.Add(otherUserTransaction);
        _testContext.TransactionItems.Add(item);
        await _testContext.SaveChangesAsync();

        var result = await _sut.DeleteTransactionItemAsync(item.Id, _testUserId);

        Assert.That(result, Is.False);
        // Verify it still exists
        var exists = await _testContext.TransactionItems.FindAsync(item.Id);
        Assert.That(exists, Is.Not.Null);
    }
}
