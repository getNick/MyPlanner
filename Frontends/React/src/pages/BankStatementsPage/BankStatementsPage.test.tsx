import React from "react";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import BankStatementsPage from "./BankStatementsPage";

/**
 * `/finance/bank` after ticket 02: a working two-step import whose result is *visible on the same page*. The
 * page offers the methods a statement can be read into (cards and savings accounts with a known bank
 * only), remembers that choice, previews a picked file — rows, date span, rows needing review — and
 * stores nothing until the preview is confirmed. Refusals come back as their reason. Below the
 * import sits the account's Bank Transactions, newest-first, so "imported 41 rows" can be checked
 * against the ledger instead of taken on trust. Nothing is managed here, so the old management list
 * must not come back.
 */

const mockGetPaymentMethods = jest.fn();
const mockGetTransactions = jest.fn();
const mockPreviewBankingFile = jest.fn();
const mockImportBankingFile = jest.fn();
const mockNavigate = jest.fn();

jest.mock("@clerk/clerk-react", () => {
  const stableToken = async () => "token";
  return { useAuth: () => ({ getToken: stableToken }) };
});

jest.mock("react-router-dom", () => ({ useNavigate: () => mockNavigate }));

jest.mock("../../services/FinanceService", () => {
  class MockFinanceService {
    getPaymentMethods = mockGetPaymentMethods;
    getTransactions = mockGetTransactions;
    previewBankingFile = mockPreviewBankingFile;
    importBankingFile = mockImportBankingFile;
  }
  return { __esModule: true, default: MockFinanceService };
});

const MONO_CARD = {
  id: "pm-mono",
  userId: "u1",
  name: "Monobank black",
  type: "BankCard",
  currency: "UAH",
  bankProvider: "Monobank",
};

const SAVINGS = {
  id: "pm-save",
  userId: "u1",
  name: "Mono save",
  type: "SavingsAccount",
  currency: "EURO",
  bankProvider: "Monobank",
};

const CASH_WALLET = {
  id: "pm-cash",
  userId: "u1",
  name: "Cash Wallet",
  type: "Cash",
  currency: "UAH",
  bankProvider: null,
};

const BLANK_CARD = {
  id: "pm-blank",
  userId: "u1",
  name: "PUMB card",
  type: "BankCard",
  currency: "USD",
  bankProvider: null,
};

// Stored bank rows, deliberately oldest-first in the array: the page owns newest-first ordering.
const SILPO = {
  id: "tx-silpo",
  userId: "u1",
  type: "Expense",
  paymentMethodId: MONO_CARD.id,
  toPaymentMethodId: null,
  timestamp: "2026-07-01T09:09:25",
  amount: 59.99,
  currency: "UAH",
  baseAmount: 59.99,
  description: "Silpo",
  additionalNotes: null,
  balanceAfter: 10000,
  dataOrigin: "Bank",
  rawTransactionData: null,
  items: [],
};

// The foreign-currency case from the fixture: original figure and currency kept, card figure beside it.
const ALIEXPRESS = {
  id: "tx-aliexpress",
  userId: "u1",
  type: "Expense",
  paymentMethodId: MONO_CARD.id,
  toPaymentMethodId: null,
  timestamp: "2026-07-10T12:00:00",
  amount: 13.37,
  currency: "EURO",
  baseAmount: 598.99,
  description: "AliExpress",
  additionalNotes: null,
  balanceAfter: 9401.01,
  dataOrigin: "Bank",
  rawTransactionData: null,
  items: [],
};

const SALARY = {
  ...ALIEXPRESS,
  id: "tx-salary",
  type: "Income",
  timestamp: "2026-07-11T17:30:53",
  amount: 1200,
  currency: "UAH",
  baseAmount: 1200,
  description: "Salary",
};

const SAVINGS_ROW = { ...SILPO, id: "tx-save-row", paymentMethodId: SAVINGS.id, description: "Deposit" };

// A Bill lives on /shopping-bills; a bank list must not show it even when it shares the period.
const BILL = { ...SILPO, id: "tx-bill", dataOrigin: "Receipt", paymentMethodId: null, description: "Coffee House" };
const RECONCILED = {
  ...SILPO,
  id: "tx-reconciled",
  dataOrigin: "Reconciled",
  description: "Silpo with receipt",
  moneyDelta: 3.25,
};

/** The ledger list's rows, in the order the page shows them. */
function bankRows(): string[] {
  const list = screen.getByRole("list", { name: "Bank transactions" });
  return within(list)
    .getAllByRole("listitem")
    .map((item) => item.textContent ?? "");
}

function importTarget(): HTMLSelectElement {
  return screen.getByLabelText("Import into") as HTMLSelectElement;
}

function statementFileInput(): HTMLInputElement {
  return screen.getByLabelText("Statement file") as HTMLInputElement;
}

function pickCsv(name = "monobank-july.csv") {
  fireEvent.change(statementFileInput(), {
    target: { files: [new File(["csv-body"], name, { type: "text/csv" })] },
  });
}

/** Drop a file onto the zone — the second door to the same preview. */
function dropFile(name: string, type: string) {
  fireEvent.drop(screen.getByLabelText("Statement upload"), {
    dataTransfer: { files: [new File(["body"], name, { type })] },
  });
}

const PREVIEW = {
  paymentMethodId: MONO_CARD.id,
  paymentMethodName: MONO_CARD.name,
  bankProvider: "Monobank",
  rowCount: 42,
  firstTimestamp: "2026-07-01T09:09:25",
  lastTimestamp: "2026-07-11T17:30:53",
  needsReview: [
    {
      rowNumber: 44,
      rawValue: "",
      description: "Переказ готівкою через термінал",
      reason: "No readable date — the row cannot be placed in time, so it was not imported.",
    },
  ],
};

/** Every option the picker offers. There is no placeholder: an answer is always preselected. */
function targetOptions(): string[] {
  return Array.from(importTarget().options).map((option) => option.textContent ?? "");
}

describe("BankStatementsPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.clear();
    mockGetPaymentMethods.mockResolvedValue([CASH_WALLET, BLANK_CARD, MONO_CARD, SAVINGS]);
    mockGetTransactions.mockResolvedValue([]);
  });

  // Wait for the load to land, not merely for the call to start: asserting on the client would
  // return while the page is still in its loading state.
  async function renderView() {
    const view = render(<BankStatementsPage />);
    await waitFor(() => expect(screen.queryByText("Loading…")).toBeNull());
    return view;
  }

  it("is titled as statement import", async () => {
    await renderView();

    expect(screen.getByText("Module // Finance · Bank")).toBeTruthy();
    expect(screen.getByRole("heading", { name: /statement import/i })).toBeTruthy();
    // The management list moved out of this page: no rows, no add button.
    expect(screen.queryByText("Cash Wallet")).toBeNull();
  });

  it("offers only methods that can hold a statement, named with their currency", async () => {
    await renderView();

    expect(targetOptions()).toEqual(["Monobank black — UAH", "Mono save — EUR"]);
  });

  it("preselects the newest selectable method on a first visit", async () => {
    await renderView();

    expect(importTarget().value).toBe(SAVINGS.id);
  });

  it("remembers the chosen method across visits", async () => {
    const firstVisit = await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    // A remount stands in for leaving the page and coming back tomorrow.
    firstVisit.unmount();
    await renderView();

    expect(importTarget().value).toBe(MONO_CARD.id);
  });

  it("falls back to a selectable method when the remembered one is gone", async () => {
    const firstVisit = await renderView();
    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    mockGetPaymentMethods.mockResolvedValue([CASH_WALLET, SAVINGS]);
    firstVisit.unmount();
    await renderView();

    expect(importTarget().value).toBe(SAVINGS.id);
  });

  it("previews a picked file against the chosen target and stores nothing yet", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });
    pickCsv();

    const panel = await screen.findByLabelText("Statement preview");
    expect(mockPreviewBankingFile).toHaveBeenCalledWith(MONO_CARD.id, expect.any(File));
    expect(panel.textContent).toContain("monobank-july.csv — 42 rows");
    expect(panel.textContent).toContain(`Import into ${MONO_CARD.name} — UAH`);
    // Nothing imported on its own: the confirmation is still owed.
    expect(mockImportBankingFile).not.toHaveBeenCalled();
  });

  it("shows the rows needing review in the preview, with line number and reason", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    await renderView();

    pickCsv();
    const panel = await screen.findByLabelText("Statement preview");

    expect(panel.textContent).toContain("Line 44");
    expect(panel.textContent).toContain("No readable date");
  });

  // The dropzone answers a drag the same way it answers a click: same preview, same refusal,
  // and still nothing stored before the page confirms.
  it("previews a dropped CSV and stores nothing yet", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    await renderView();

    dropFile("monobank-july.csv", "text/csv");

    expect(await screen.findByLabelText("Statement preview")).toBeTruthy();
    expect(mockPreviewBankingFile).toHaveBeenCalledWith(SAVINGS.id, expect.any(File));
    expect(mockImportBankingFile).not.toHaveBeenCalled();
  });

  it("refuses a dropped file that is not a statement export, without previewing it", async () => {
    await renderView();

    dropFile("statement.pdf", "application/pdf");

    expect(await screen.findByText(/CSV format/i)).toBeTruthy();
    expect(mockPreviewBankingFile).not.toHaveBeenCalled();
    expect(screen.queryByLabelText("Statement preview")).toBeNull();
  });

  it("imports only after the preview is confirmed, then reports what landed", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    mockImportBankingFile.mockResolvedValue({
      summary: PREVIEW,
      insertedRowCount: 41,
      duplicateRowCount: 1,
      transactions: [],
    });
    await renderView();

    pickCsv();
    fireEvent.click(await screen.findByRole("button", { name: /confirm import/i }));

    const report = await screen.findByText(/Imported 41 rows into Monobank black/);
    expect(report.textContent).toContain("1 already in the ledger were skipped");
    expect(screen.queryByLabelText("Statement preview")).toBeNull();
  });

  it("stores nothing when the preview is cancelled", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    await renderView();

    pickCsv();
    fireEvent.click(await screen.findByRole("button", { name: /cancel/i }));

    expect(mockImportBankingFile).not.toHaveBeenCalled();
    // Back to the plain upload area, ready for another file.
    expect(screen.getByLabelText("Statement upload")).toBeTruthy();
  });

  it("shows the server's refusal reason instead of importing", async () => {
    mockPreviewBankingFile.mockRejectedValue(
      new Error("PUMB card has no bank set, so there is no statement format to read.")
    );
    await renderView();

    pickCsv();

    expect(await screen.findByText(/has no bank set/)).toBeTruthy();
    expect(screen.queryByLabelText("Statement preview")).toBeNull();
    expect(mockImportBankingFile).not.toHaveBeenCalled();
  });

  it("sends an empty household to the payment-methods page instead of showing an empty picker", async () => {
    mockGetPaymentMethods.mockResolvedValue([CASH_WALLET, BLANK_CARD]);
    await renderView();

    expect(screen.queryByLabelText("Import into")).toBeNull();
    expect(screen.getByText(/nothing here can receive a statement/i)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /add a payment method/i }));
    expect(mockNavigate).toHaveBeenCalledWith("/finance/payment-methods");
  });

  it("opens the payment-methods page from the header", async () => {
    await renderView();

    fireEvent.click(screen.getByRole("button", { name: /^payment methods$/i }));

    expect(mockNavigate).toHaveBeenCalledWith("/finance/payment-methods");
  });

  it("lists the chosen account's bank transactions newest-first, bills excluded", async () => {
    mockGetTransactions.mockResolvedValue([SILPO, ALIEXPRESS, SALARY, SAVINGS_ROW, BILL]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    await waitFor(() => expect(bankRows()).toHaveLength(3));
    expect(bankRows()[0]).toContain("Salary");
    expect(bankRows()[1]).toContain("AliExpress");
    expect(bankRows()[2]).toContain("Silpo");
    // A Bill is not a bank row, and another account's rows stay out of this list.
    expect(screen.getByRole("list", { name: "Bank transactions" }).textContent).not.toContain("Coffee House");
    expect(screen.getByRole("list", { name: "Bank transactions" }).textContent).not.toContain("Deposit");
  });

  it("lists a Reconciled purchase once with its state and Money Delta", async () => {
    mockGetTransactions.mockResolvedValue([RECONCILED]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    await waitFor(() => expect(bankRows()).toHaveLength(1));
    expect(bankRows()[0]).toContain("Reconciled");
    expect(bankRows()[0]).toMatch(/Money Delta.*3[.,]25/);
  });

  it("prints the card-currency figure beside a foreign-currency amount", async () => {
    mockGetTransactions.mockResolvedValue([ALIEXPRESS]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    await waitFor(() => expect(bankRows()).toHaveLength(1));
    const [aliexpress] = bankRows();
    expect(aliexpress).toMatch(/13[.,]37/); // what was paid, in its own currency
    expect(aliexpress).toMatch(/598[.,]99/); // what the card was charged
  });

  it("keeps a same-currency row to one figure", async () => {
    mockGetTransactions.mockResolvedValue([SILPO]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    await waitFor(() => expect(bankRows()).toHaveLength(1));
    const [silpo] = bankRows();
    expect(silpo.match(/59[.,]99/g) ?? []).toHaveLength(1);
  });

  it("follows the chosen account", async () => {
    mockGetTransactions.mockResolvedValue([SILPO, SAVINGS_ROW]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: SAVINGS.id } });

    await waitFor(() => expect(bankRows()).toEqual([expect.stringContaining("Deposit")]));
  });

  it("shows the imported rows on this page once the import lands", async () => {
    mockPreviewBankingFile.mockResolvedValue(PREVIEW);
    mockImportBankingFile.mockResolvedValue({
      summary: PREVIEW,
      insertedRowCount: 3,
      duplicateRowCount: 0,
      transactions: [SILPO, ALIEXPRESS, SALARY],
    });
    // The ledger read happens twice: empty on arrival, then holding the file's rows.
    mockGetTransactions
      .mockResolvedValueOnce([])
      .mockResolvedValue([SILPO, ALIEXPRESS, SALARY]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });
    pickCsv();
    fireEvent.click(await screen.findByRole("button", { name: /confirm import/i }));

    await waitFor(() => expect(bankRows()).toHaveLength(3));
    expect(mockGetTransactions).toHaveBeenCalledTimes(2);
  });

  it("says an account has no statement rows yet, instead of an empty list", async () => {
    mockGetTransactions.mockResolvedValue([BILL]);
    await renderView();

    fireEvent.change(importTarget(), { target: { value: MONO_CARD.id } });

    expect(await screen.findByText(/no statement rows for Monobank black yet/i)).toBeTruthy();
  });

  it("says when the ledger could not be read, rather than implying nothing was imported", async () => {
    mockGetTransactions.mockRejectedValue(new Error("boom"));
    await renderView();

    expect(await screen.findByText(/could not read the ledger/i)).toBeTruthy();
    expect(screen.queryByText(/no statement rows .* yet/i)).toBeNull();
  });

  it("says when the methods could not be read, rather than showing an empty page", async () => {
    mockGetPaymentMethods.mockRejectedValue(new Error("boom"));
    render(<BankStatementsPage />);

    expect(await screen.findByText(/could not load payment methods/i)).toBeTruthy();
    // The empty state would blame the user's data for a server that is down.
    expect(screen.queryByText(/nothing here can receive a statement/i)).toBeNull();
  });
});
