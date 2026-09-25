import {
  compareForListOrder,
  formatImportTargetLabel,
  isImportTarget,
  pickDefaultImportTarget,
  OTHER_PROVIDER_VALUE,
  providerFromChoice,
  providerOptions,
  supportsBankStatement,
} from "./paymentMethods";

/**
 * The payment-method rules the two bank screens share. Eligibility (which methods may own a
 * statement) lives here because ticket 02 will import into whatever the combobox offers and must
 * agree with it row for row; list order lives here because `/finance/payment-methods` shows one ordering.
 */

const method = (
  name: string,
  type: "BankCard" | "Cash" | "SavingsAccount" | "Other",
  bankProvider: string | null = null,
  currency: "UAH" | "USD" | "EURO" = "UAH"
) => ({ id: name, userId: "u1", name, type, currency, bankProvider });

describe("supportsBankStatement / isImportTarget", () => {
  it("accepts cards and savings accounts that carry a known provider", () => {
    expect(isImportTarget(method("Monobank black", "BankCard", "Monobank"))).toBe(true);
    expect(isImportTarget(method("Mono save", "SavingsAccount", "Monobank"))).toBe(true);
  });

  it("rejects Cash and Other even with a provider written on them", () => {
    // No bank issues a cash statement, and an `Other` method cannot be trusted to have a format.
    expect(isImportTarget(method("Cash Wallet", "Cash"))).toBe(false);
    expect(isImportTarget(method("Odd purse", "Cash", "Monobank"))).toBe(false);
    expect(isImportTarget(method("Voucher stack", "Other", "Monobank"))).toBe(false);
    expect(supportsBankStatement("Cash")).toBe(false);
    expect(supportsBankStatement("Other")).toBe(false);
  });

  it("rejects a card or savings account whose provider is unset", () => {
    // No statement format means ticket 02 would have nothing to parse the file with.
    expect(isImportTarget(method("PUMB card", "BankCard", null))).toBe(false);
  });
});

describe("compareForListOrder", () => {
  it("soft-groups cards, savings accounts, cash wallets, then other, alphabetically inside each", () => {
    const rows = [
      method("Zeta cash", "Cash"),
      method("Beta card", "BankCard"),
      method("Alpha savings", "SavingsAccount"),
      method("Aardvark card", "BankCard"),
      method("Odd thing", "Other"),
      method("Alpha cash", "Cash"),
      method("Zeta savings", "SavingsAccount"),
    ];

    expect([...rows].sort(compareForListOrder).map((m) => m.name)).toEqual([
      "Aardvark card",
      "Beta card",
      "Alpha savings",
      "Zeta savings",
      "Alpha cash",
      "Zeta cash",
      "Odd thing",
    ]);
  });
});

describe("providerOptions", () => {
  it("offers the banks that parse today plus an explicit Other, and starts unanswered", () => {
    expect(providerOptions()).toEqual([
      { value: "Monobank", label: "Monobank" },
      { value: OTHER_PROVIDER_VALUE, label: "Other / not listed" },
    ]);
  });

  it("keeps a stored provider that is no longer offered, so editing does not rewrite it", () => {
    const options = providerOptions("PUMB");
    expect(options.map((o) => o.value)).toEqual(["PUMB", "Monobank", OTHER_PROVIDER_VALUE]);
    expect(options[0].label).toContain("PUMB");
  });

  it("stores the Other choice as null and passes a real bank through untouched", () => {
    expect(providerFromChoice(OTHER_PROVIDER_VALUE)).toBeNull();
    expect(providerFromChoice("Monobank")).toBe("Monobank");
  });
});

describe("formatImportTargetLabel", () => {
  it("reads name — currency, with the display spelling of the currency", () => {
    expect(formatImportTargetLabel(method("Monobank black", "BankCard", "Monobank"))).toBe(
      "Monobank black — UAH"
    );
    expect(formatImportTargetLabel(method("Euro savings", "SavingsAccount", "Monobank", "EURO"))).toBe(
      "Euro savings — EUR"
    );
  });
});

describe("pickDefaultImportTarget", () => {
  it("takes the newest selectable method when nothing is remembered", () => {
    // The API returns methods oldest-first, and there is no created-at column on a payment
    // method, so the last entry in the response is the newest thing we can point at.
    const rows = [
      method("Old card", "BankCard", "Monobank"),
      method("Cash Wallet", "Cash"),
      method("New card", "BankCard", "Monobank"),
    ];

    expect(pickDefaultImportTarget(rows, null)?.id).toBe("New card");
  });

  it("skips methods that cannot hold a statement when falling back", () => {
    const rows = [
      method("Old card", "BankCard", "Monobank"),
      method("Provider-less card", "BankCard", null),
    ];

    expect(pickDefaultImportTarget(rows, null)?.id).toBe("Old card");
  });

  it("keeps a remembered method that is still selectable", () => {
    const rows = [
      method("Old card", "BankCard", "Monobank"),
      method("New card", "BankCard", "Monobank"),
    ];

    expect(pickDefaultImportTarget(rows, "Old card")?.id).toBe("Old card");
  });

  it("falls back to the newest selectable method when the remembered one is gone or no longer eligible", () => {
    const rows = [
      method("Kept card", "BankCard", "Monobank"),
      method("New card", "BankCard", "Monobank"),
    ];

    // A remembered id that no longer resolves is not a reason to show an empty picker.
    expect(pickDefaultImportTarget(rows, "deleted-card")?.id).toBe("New card");
    // Neither is a remembered method that stopped being importable (provider cleared, type changed).
    expect(pickDefaultImportTarget([...rows, method("Cash Wallet", "Cash")], "Cash Wallet")?.id).toBe(
      "New card"
    );
  });

  it("returns null when nothing at all is selectable", () => {
    expect(pickDefaultImportTarget([method("Cash Wallet", "Cash")], null)).toBeNull();
  });
});
