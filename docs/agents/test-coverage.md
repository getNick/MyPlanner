# Test Coverage Checklists

Every service unit test file should have an inline coverage checklist at the top (after `using` / `namespace`, before `[TestFixture]`). It gives agents a quick overview of what's tested, stubbed, and missing.

## Format

Place a block comment right after the namespace declaration:

```csharp
namespace MyPlanner.UnitTests.Services;

/*
 * ═══════════════════════════════════════════════════════════
 *  FinanceServiceTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [!] = stub / tests DB state only (not real pipeline)
 *   [ ] = not yet implemented
 *
 * ────────────────────────────────────────────────────────────
 *  PaymentMethod CRUD
 * ────────────────────────────────────────────────────────────
 *   [x] GetPaymentMethodsAsync — empty list, returns all, user isolation
 *   [x] GetPaymentMethodAsync — not found, found by id + userId
 *   ...
 */
```

## Legend

- `[x]` = implemented & passing
- `[!]` = stub / tests DB state only (not real pipeline)
- `[ ]` = not yet implemented

## Guidelines

- Group items under feature headings separated by dashed lines.
- Use sub-bullets for related edge cases or field-level checks.
- Mark end-to-end gaps with a "Should be implemented" section at the bottom.
- **Don't obsess over accuracy** — `[x]` means "there's a test for this", not "this is fully verified". You don't need to update the checklist after every failing test run. It's a rough map, not a CI gate.
