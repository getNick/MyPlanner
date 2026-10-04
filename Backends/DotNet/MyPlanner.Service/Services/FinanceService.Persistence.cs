using Microsoft.EntityFrameworkCore;

namespace MyPlanner.Service;

public partial class FinanceService
{
    /// <summary>Ingestion/edit and Matching share a commit. InMemory tests cannot prove rollback;
    /// relational integration tests exercise the transaction and failure path.</summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> write)
    {
        // Legacy single-item writes reuse the same guarded complete-detail save.
        if (_context.Database.CurrentTransaction != null) return await write();
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
            : null;
        try
        {
            var result = await write();
            if (transaction != null) await transaction.CommitAsync();
            return result;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            // Rolled-back inserts/updates must not be accidentally persisted by a later request
            // using this context. Read again rather than retaining pre-rollback tracked state.
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
