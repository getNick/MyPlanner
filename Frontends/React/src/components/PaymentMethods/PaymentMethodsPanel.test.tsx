import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import PaymentMethodsPanel from "./PaymentMethodsPanel";
import { OTHER_PROVIDER_VALUE } from "../../domain/paymentMethods";

/**
 * Tests for the live payment-methods panel — the management screen `/finance/payment-methods`, reached
 * from `/finance/bank` and deliberately absent from the sidebar. The finance client is mocked at the class
 * level, so these exercise the panel's own rules: nothing is minted on load, rows are soft-grouped
 * cards → savings → cash → other, every row (cash wallets included) can be edited and deleted behind
 * a second click, a refusal keeps the row on screen with the server's count, and the form will not
 * post until an explicit Bank Provider has been chosen. `getToken` is a prop precisely so no auth
 * provider has to be mounted.
 */

const mockGetPaymentMethods = jest.fn();
const mockCreatePaymentMethod = jest.fn();
const mockUpdatePaymentMethod = jest.fn();
const mockDeletePaymentMethod = jest.fn();

jest.mock("../../services/FinanceService", () => {
  class MockFinanceService {
    getPaymentMethods = mockGetPaymentMethods;
    createPaymentMethod = mockCreatePaymentMethod;
    updatePaymentMethod = mockUpdatePaymentMethod;
    deletePaymentMethod = mockDeletePaymentMethod;
  }
  return { __esModule: true, default: MockFinanceService };
});

const getToken = async () => "tok";

export const CASH_WALLET = {
  id: "pm-cash",
  userId: "u1",
  name: "Cash Wallet",
  type: "Cash",
  currency: "UAH",
  bankProvider: null,
};

export const MONO_CARD = {
  id: "pm-mono",
  userId: "u1",
  name: "Monobank black",
  type: "BankCard",
  currency: "UAH",
  bankProvider: "Monobank",
};

export const BLANK_SAVINGS = {
  id: "pm-legacy",
  userId: "u1",
  name: "Old savings",
  type: "SavingsAccount",
  currency: "USD",
  bankProvider: null,
};

const ROWS = [CASH_WALLET, BLANK_SAVINGS, MONO_CARD];

describe("PaymentMethodsPanel", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetPaymentMethods.mockResolvedValue(ROWS);
    mockCreatePaymentMethod.mockResolvedValue(undefined);
    mockUpdatePaymentMethod.mockResolvedValue(true);
    mockDeletePaymentMethod.mockResolvedValue({ status: "deleted" });
  });

  // Waits for the read to land rather than for one particular row, so tests can override the list.
  async function renderPanel() {
    const view = render(<PaymentMethodsPanel getToken={getToken} />);
    await waitFor(() => expect(screen.queryByText("Loading…")).toBeNull());
    return view;
  }

  const providerSelect = (): HTMLSelectElement =>
    screen.getByLabelText("Bank provider") as HTMLSelectElement;

  // Rows in the order they appear, read from their edit controls.
  function rowOrder(): string[] {
    return screen
      .getAllByRole("button", { name: /^Edit / })
      .map((button) => button.getAttribute("aria-label")?.replace(/^Edit /, "") ?? "");
  }

  /** Open the create form, fill in a name, and pick a provider option by its label. */
  async function openCreateWith(name: string) {
    fireEvent.click(screen.getByRole("button", { name: /add payment method/i }));
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: name } });
  }

  it("lists the household's payment methods without creating anything", async () => {
    await renderPanel();

    expect(mockGetPaymentMethods).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Cash Wallet")).toBeTruthy();
    // No auto-mint: the panel writes on load in no shape or form.
    expect(mockCreatePaymentMethod).not.toHaveBeenCalled();
  });

  it("orders rows cards, savings accounts, cash wallets, then other", async () => {
    mockGetPaymentMethods.mockResolvedValue([
      { ...CASH_WALLET, name: "Zeta cash" },
      { ...MONO_CARD, name: "Beta card" },
      { ...BLANK_SAVINGS, name: "Alpha savings" },
      { id: "pm-other", userId: "u1", name: "Vouchers", type: "Other", currency: "UAH", bankProvider: null },
      { ...MONO_CARD, id: "pm-mono-2", name: "Aardvark card" },
    ]);
    await renderPanel();

    // Row order is read off the per-row controls, so it does not depend on how a row is laid out.
    expect(rowOrder()).toEqual([
      "Aardvark card",
      "Beta card",
      "Alpha savings",
      "Zeta cash",
      "Vouchers",
    ]);
  });

  it("blocks Save until a Bank Provider is chosen, then posts the pick", async () => {
    await renderPanel();
    await openCreateWith("PUMB salary");

    // Nothing chosen yet — the form must refuse to post an unanswered row.
    expect(providerSelect().value).toBe("");
    fireEvent.click(screen.getByRole("button", { name: /create payment method/i }));

    expect(mockCreatePaymentMethod).not.toHaveBeenCalled();
    expect(await screen.findByText(/bank provider is required/i)).toBeTruthy();

    fireEvent.change(screen.getByLabelText("Bank provider"), { target: { value: "Monobank" } });
    fireEvent.click(screen.getByRole("button", { name: /create payment method/i }));

    await waitFor(() => expect(mockCreatePaymentMethod).toHaveBeenCalledTimes(1));
    expect(mockCreatePaymentMethod).toHaveBeenCalledWith({
      name: "PUMB salary",
      type: "BankCard",
      currency: "UAH",
      bankProvider: "Monobank",
    });
    // The list is re-read from the server rather than optimistically patched.
    await waitFor(() => expect(mockGetPaymentMethods).toHaveBeenCalledTimes(2));
  });

  it("stores Other / not listed as a null provider", async () => {
    await renderPanel();
    await openCreateWith("Mystery card");

    fireEvent.change(screen.getByLabelText("Bank provider"), { target: { value: "" } });
    // The option's own value is null, serialised to "" by the DOM; picking it must not be read as
    // "still unanswered".
    fireEvent.change(screen.getByLabelText("Bank provider"), {
      target: { value: OTHER_PROVIDER_VALUE },
    });

    fireEvent.click(screen.getByRole("button", { name: /create payment method/i }));

    await waitFor(() => expect(mockCreatePaymentMethod).toHaveBeenCalledTimes(1));
    expect(mockCreatePaymentMethod).toHaveBeenCalledWith({
      name: "Mystery card",
      type: "BankCard",
      currency: "UAH",
      bankProvider: null,
    });
  });

  it("saves a Cash payment method with no provider field in sight", async () => {
    await renderPanel();
    await openCreateWith("Cash Wallet");
    fireEvent.change(screen.getByLabelText("Type"), { target: { value: "Cash" } });

    // A cash wallet has no statement format, so the field does not apply to that shape.
    expect(screen.queryByLabelText("Bank provider")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: /create payment method/i }));

    await waitFor(() => expect(mockCreatePaymentMethod).toHaveBeenCalledTimes(1));
    expect(mockCreatePaymentMethod).toHaveBeenCalledWith({
      name: "Cash Wallet",
      type: "Cash",
      currency: "UAH",
      bankProvider: null,
    });
  });

  it("edits a Cash payment method through PUT", async () => {
    await renderPanel();

    fireEvent.click(screen.getByRole("button", { name: /edit cash wallet/i }));
    // A cash wallet has no issuing bank, so the provider field never gates its save.
    expect(screen.queryByLabelText("Bank provider")).toBeNull();
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Cash wallet UAH" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() =>
      expect(mockUpdatePaymentMethod).toHaveBeenCalledWith({
        id: "pm-cash",
        name: "Cash wallet UAH",
        type: "Cash",
        currency: "UAH",
        bankProvider: null,
      })
    );
  });

  it("does not post a blank name", async () => {
    await renderPanel();
    fireEvent.click(screen.getByRole("button", { name: /add payment method/i }));

    fireEvent.click(screen.getByRole("button", { name: /create payment method/i }));

    expect(mockCreatePaymentMethod).not.toHaveBeenCalled();
    expect(await screen.findByText(/name is required/i)).toBeTruthy();
  });

  it("edits an existing method through PUT, keeping its provider", async () => {
    await renderPanel();

    fireEvent.click(screen.getByRole("button", { name: /edit monobank black/i }));
    expect(providerSelect().value).toBe("Monobank");
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Monobank classic" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => expect(mockUpdatePaymentMethod).toHaveBeenCalledTimes(1));
    expect(mockUpdatePaymentMethod).toHaveBeenCalledWith({
      id: "pm-mono",
      name: "Monobank classic",
      type: "BankCard",
      currency: "UAH",
      bankProvider: "Monobank",
    });
  });

  it("opens a provider-less row with nothing chosen and blocks Save until the user picks", async () => {
    await renderPanel();

    fireEvent.click(screen.getByRole("button", { name: /edit old savings/i }));
    // A legacy row with no provider opens unanswered, not silently defaulted to some bank.
    expect(providerSelect().value).toBe("");

    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));
    expect(mockUpdatePaymentMethod).not.toHaveBeenCalled();

    // The legacy row repairs itself the first time you touch it.
    fireEvent.change(screen.getByLabelText("Bank provider"), { target: { value: "Monobank" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => expect(mockUpdatePaymentMethod).toHaveBeenCalledWith({
      id: "pm-legacy",
      name: "Old savings",
      type: "SavingsAccount",
      currency: "USD",
      bankProvider: "Monobank",
    }));
  });

  it("says which rows cannot receive a statement, and only those", async () => {
    await renderPanel();

    // Old savings only: Cash Wallet never holds a statement, Monobank black has its format.
    const hints = screen.getAllByText(/no statement format/i);
    expect(hints).toHaveLength(1);
  });

  it("asks for a second click before deleting, then refetches", async () => {
    await renderPanel();

    fireEvent.click(screen.getByRole("button", { name: /delete monobank black/i }));
    expect(mockDeletePaymentMethod).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: /confirm delete/i }));

    await waitFor(() => expect(mockDeletePaymentMethod).toHaveBeenCalledWith("pm-mono"));
    await waitFor(() => expect(mockGetPaymentMethods).toHaveBeenCalledTimes(2));
  });

  it("offers edit and delete for a Cash Wallet like any other row", async () => {
    await renderPanel();

    expect(screen.getByRole("button", { name: /edit cash wallet/i })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /delete cash wallet/i }));
    fireEvent.click(screen.getByRole("button", { name: /confirm delete/i }));

    await waitFor(() => expect(mockDeletePaymentMethod).toHaveBeenCalledWith("pm-cash"));
  });

  it("keeps the row and states the count when deletion is refused", async () => {
    mockDeletePaymentMethod.mockResolvedValue({
      status: "in-use",
      transactionCount: 3,
      explanation: null,
    });
    await renderPanel();

    fireEvent.click(screen.getByRole("button", { name: /delete monobank black/i }));
    fireEvent.click(screen.getByRole("button", { name: /confirm delete/i }));

    expect(await screen.findByText(/3 transactions/)).toBeTruthy();
    expect(screen.getByText("Monobank black")).toBeTruthy();
    expect(mockGetPaymentMethods).toHaveBeenCalledTimes(1); // no refetch: nothing changed
  });

  it("reports a load failure instead of an empty-looking ledger", async () => {
    mockGetPaymentMethods.mockRejectedValue(new Error("boom"));

    render(<PaymentMethodsPanel getToken={getToken} />);

    expect(await screen.findByText(/could not load payment methods/i)).toBeTruthy();
  });
});
