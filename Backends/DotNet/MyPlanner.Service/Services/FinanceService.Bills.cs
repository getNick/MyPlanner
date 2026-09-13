using MyPlanner.Data.Entities.Finance;

namespace MyPlanner.Service;

public partial class FinanceService
{
    // An omitted collection is a transitional header-only write; an empty collection deletes all
    // detail. Validate the entire replacement before mutating tracked entities.
    private void ReplaceBillLineItems(Transaction bill, IReadOnlyList<TransactionItem> items)
    {
        if (bill.DataOrigin is not (DataOrigin.Receipt or DataOrigin.Reconciled))
            throw new InvalidOperationException("Complete Line Item replacement is only supported for Bills.");

        var existing = bill.Items.ToDictionary(i => i.Id);
        var retainedIds = new HashSet<Guid>();
        foreach (var item in items)
        {
            ValidateBillLineItem(item);
            if (string.IsNullOrWhiteSpace(item.Name))
                throw new InvalidOperationException("Line Item Name is required.");
            if (item.Id != Guid.Empty && (!existing.ContainsKey(item.Id) || !retainedIds.Add(item.Id)))
                throw new InvalidOperationException("Line Item ID must be unique and belong to this Bill.");
        }

        foreach (var removed in bill.Items.Where(i => !retainedIds.Contains(i.Id)).ToArray())
        {
            _context.TransactionItems.Remove(removed);
            bill.Items.Remove(removed);
        }
        foreach (var item in items)
        {
            if (!existing.TryGetValue(item.Id, out var stored))
            {
                stored = new TransactionItem
                {
                    TransactionId = bill.Id, Name = item.Name, FullName = item.FullName
                };
                bill.Items.Add(stored);
                _context.TransactionItems.Add(stored);
            }
            stored.Name = item.Name;
            stored.FullName = item.FullName;
            stored.Category = item.Category;
            stored.Subcategory = item.Subcategory;
            stored.Quantity = item.Quantity;
            stored.PricePerUnit = item.PricePerUnit;
            stored.TotalPrice = item.TotalPrice;
            stored.Origin = ItemOrigin.ManualInput;
        }
    }
}
