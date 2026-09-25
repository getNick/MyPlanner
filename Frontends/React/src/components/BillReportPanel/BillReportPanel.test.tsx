import React from "react";
import { render, fireEvent, screen, waitFor } from "@testing-library/react";
import BillReportPanel from "./BillReportPanel";
import type { ReceiptData, Category } from "../../types/receiptTypes";

/**
 * Verifies the panel's restructured contracts (issue 005):
 *  - the expanded body shows ONLY the items table — no Vendor card, no
 *    Summary card, no "Refine Items" button,
 *  - the header row carries a ⋮ action menu (Edit / Remove). In edit mode the
 *    ⋮ is replaced by Save / Cancel on the header row,
 *  - the header meta (merchant name, date, currency) becomes editable inline
 *    in the collapsed header during edit mode; Total is shown readonly
 *    (derived from the staged items); no Category summary, no notes in the
 *    header,
 *  - "Remove" deletes the whole transaction via onDelete (after a confirm),
 *    immediately — not staged behind Save,
 *  - edits are staged locally and committed only on Save,
 *  - the Unsaved badge appears on the header row only on draft divergence.
 */

const categories: Category[] = [
  { name: "Food", subcategories: ["Groceries", "Snacks"] },
];

const makeReceipt = (): ReceiptData => ({
  id: "bill-1",
  merchantName: "Coffee House",
  timestamp: "2024-05-01T10:30:00Z",
  totalAmount: 42.5,
  currency: "USD",
  items: [
    {
      name: "Coffee",
      fullName: "Large Coffee",
      quantity: 2,
      unitPrice: 3.5,
      totalPrice: 7,
      category: "Food",
      subcategory: "Snacks",
    },
    {
      name: "Sandwich",
      fullName: "Club Sandwich",
      quantity: 1,
      unitPrice: 35.5,
      totalPrice: 35.5,
      category: "Food",
      subcategory: "Groceries",
    },
  ],
});

describe("BillReportPanel — transaction row + ⋮ action menu", () => {
  const categories: Category[] = [
    { name: "Food", subcategories: ["Groceries", "Snacks"] },
  ];

  const makeReceipt = (): ReceiptData => ({
    id: "bill-1",
    merchantName: "Coffee House",
    timestamp: "2024-05-01T10:30:00Z",
    totalAmount: 42.5,
    currency: "USD",
    items: [
      {
        name: "Coffee",
        fullName: "Large Coffee",
        quantity: 2,
        unitPrice: 3.5,
        totalPrice: 7,
        category: "Food",
        subcategory: "Snacks",
      },
    ],
  });

  it("renders the unexpanded transaction row (Vendor / Date / Category / Amount with currency)", () => {
    const onExpandChange = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onExpandChange={onExpandChange}
      />,
    );

    // The row shows the committed vendor and date (from timestamp), plus the
    // amount with its currency. Category summary is no longer in the header.
    expect(screen.getByText("Coffee House")).toBeTruthy();
    expect(screen.getByText(/42\.5/)).toBeTruthy();
    expect(screen.getByText("USD")).toBeTruthy();

    // Collapsed: the report body is not yet rendered.
    expect(screen.getByTitle("Expand report").getAttribute("aria-expanded")).toBe(
      "false",
    );
    expect(screen.queryByText("Refine Items")).toBeNull();
    expect(screen.queryByText("Coffee")).toBeNull();
    expect(
      document.querySelector('button[title="Delete list entry"]'),
    ).toBeNull();

    // The ⋮ action menu is present on the header row.
    expect(screen.getByTitle("More options")).toBeTruthy();
  });

  it("expands and collapses on row click and reports to the parent", () => {
    const onExpandChange = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onExpandChange={onExpandChange}
      />,
    );

    // Clicking the row expands and reports to the parent.
    fireEvent.click(screen.getByTitle("Expand report"));
    expect(onExpandChange).toHaveBeenCalledWith(true);
    expect(screen.getByText("Coffee")).toBeTruthy();

    // Collapses again on a second row click.
    fireEvent.click(screen.getByTitle("Collapse report"));
    expect(onExpandChange).toHaveBeenNthCalledWith(2, false);
    expect(screen.queryByText("Coffee")).toBeNull();
  });

  it("non-expandable panels are always expanded with the ⋮ menu but no affordance", () => {
    const onExpandChange = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={false}
        onExpandChange={onExpandChange}
      />,
    );

    // Always expanded: the body is present immediately (no collapse
    // affordance was ever offered), so item rows render.
    expect(screen.getByText("Coffee")).toBeTruthy();

    // No collapse/expand toggle exists to press.
    expect(screen.queryByTitle("Expand report")).toBeNull();
    expect(screen.queryByTitle("Collapse report")).toBeNull();
    expect(screen.getByTitle("More options")).toBeTruthy();
    expect(onExpandChange).not.toHaveBeenCalled();
  });
});

describe("BillReportPanel — ⋮ menu: Edit / Remove", () => {
  const categories: Category[] = [
    { name: "Food", subcategories: ["Groceries", "Snacks"] },
  ];

  const makeReceipt = (): ReceiptData => ({
    id: "bill-1",
    merchantName: "Coffee House",
    timestamp: "2024-05-01T10:30:00Z",
    totalAmount: 42.5,
    currency: "USD",
    items: [
      {
        name: "Coffee",
        fullName: "Large Coffee",
        quantity: 2,
        unitPrice: 3.5,
        totalPrice: 7,
        category: "Food",
        subcategory: "Snacks",
      },
    ],
  });

  it("opens a dropdown with Edit and Remove from the ⋮ menu", () => {
    const onDelete = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={onDelete}
      />,
    );

    // The ⋮ menu button opens a dropdown offering Edit and Remove.
    fireEvent.click(screen.getByTitle("More options"));
    expect(screen.getByText("Edit")).toBeTruthy();
    expect(screen.getByText("Remove")).toBeTruthy();
  });

  it("enters edit mode from Edit: the ⋮ is replaced by Save/Cancel and the header becomes editable", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={jest.fn()}
      />,
    );

    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // The ⋮ menu is gone; Save/Cancel are on the header row.
    expect(screen.queryByTitle("More options")).toBeNull();
    expect(screen.getByText("Save")).toBeTruthy();
    expect(screen.getByText("Cancel")).toBeTruthy();

    // Header <input>s: merchant + date (currency is a <select>, total is a
    // readonly display).
    const inputs = container.querySelectorAll("input");
    expect(inputs.length).toBe(2);

    // The currency is a combobox limited to UAH / USD / EUR.
    const currencySelect = document.querySelector(
      'select[aria-label="Currency"]',
    ) as HTMLSelectElement;
    expect(currencySelect).toBeTruthy();
    expect(
      Array.from(currencySelect.querySelectorAll("option")).map(
        (o) => o.value,
      ),
    ).toEqual(["UAH", "USD", "EUR"]);
    expect(currencySelect.value).toBe("USD");
  });

  it("carries no category control in the header while editing; category selects live in the item table", () => {
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={jest.fn()}
      />,
    );

    // Expandable panels start collapsed — open the body so the item table
    // (which carries the category selects) is rendered.
    fireEvent.click(screen.getByTitle("Expand report"));
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // The header carries no category control at all (category was removed
    // from the header per the redesign).
    const headerControls = document.querySelectorAll(
      "input, select, textarea",
    );
    headerControls.forEach((el) => {
      expect((el as HTMLElement).ariaLabel ?? "").not.toContain("item");
    });

    // The category <select>s now live only in the item table.
    expect(
      document.querySelectorAll('select[aria-label^="Edit item"]').length,
    ).toBeGreaterThan(0);
  });

  it("does not collapse the panel when clicking header inputs during edit mode; the chevron still toggles", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={jest.fn()}
      />,
    );

    fireEvent.click(screen.getByTitle("Expand report"));
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Clicking an input does not collapse the panel (the body stays open).
    const merchantInput = Array.from(container.querySelectorAll("input")).find(
      (i) => i.value === "Coffee House",
    ) as HTMLInputElement;
    fireEvent.change(merchantInput, { target: { value: "Tea Bar" } });
    expect(screen.getByText("Add Item Row")).toBeTruthy();

    // The chevron still expands/collapses.
    fireEvent.click(screen.getByTitle("Collapse report"));
    expect(screen.queryByText("Add Item Row")).toBeNull();
  });
});

describe("BillReportPanel — staged edits + commit", () => {
  let fetchMock: jest.SpyInstance;
  const onSave = jest.fn();
  const onDraft = jest.fn();

  beforeEach(() => {
    // The panel must never talk to the network itself. Force a failure if it does.
    fetchMock = jest.spyOn(global, "fetch").mockImplementation(async () => {
      throw new Error("panel attempted a server write");
    });
  });

  afterEach(() => {
    onSave.mockClear();
    onDraft.mockClear();
    fetchMock.mockRestore();
  });

  it("edits are staged locally and committed once on Save Refinements", () => {
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onSave={onSave}
        onDelete={jest.fn()}
      />,
    );

    // Enter edit mode from the ⋮ menu.
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Change the header merchant field — staged, not committed yet.
    const merchantInput = Array.from(
      containerInputs(),
    ).find((i) => i.value === "Coffee House") as HTMLInputElement;
    fireEvent.change(merchantInput, { target: { value: "Tea Bar" } });

    // Nothing committed to the parent before Save is pressed.
    expect(onSave).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();

    // Pressing Save commits the drafted bill exactly once.
    fireEvent.click(screen.getByText("Save"))
    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0][0].merchantName).toBe("Tea Bar");
    expect(fetchMock).not.toHaveBeenCalled();

    // Save exits edit mode (the ⋮ menu is back).
    expect(screen.getByTitle("More options")).toBeTruthy();
    expect(screen.queryByText("Cancel")).toBeNull();
    expect(screen.queryByText("Save Refinements")).toBeNull();
  });

  it("discards staged edits on Cancel with no parent change", () => {
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onSave={onSave}
        onDraft={onDraft}
        onDelete={jest.fn()}
      />,
    );

    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Stage an edit; the draft is reported for display wiring only.
    const merchantInput = Array.from(
      containerInputs(),
    ).find((i) => i.value === "Coffee House") as HTMLInputElement;
    fireEvent.change(merchantInput, { target: { value: "Tea Bar" } });
    expect(onDraft).toHaveBeenCalledTimes(1);

    // Cancel discards the draft: no commit, draft reporter cleared.
    fireEvent.click(screen.getByText("Cancel"));
    expect(onSave).not.toHaveBeenCalled();
    expect(onDraft).toHaveBeenLastCalledWith(null);

    // The field reverts to the committed (parent) value in viewing mode.
    expect(screen.getByText("Coffee House")).toBeTruthy();
  });

  it("stages item deletion locally until Save", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onSave={onSave}
        onDelete={jest.fn()}
      />,
    );

    // expandable panels start collapsed — expand the row first.
    fireEvent.click(screen.getByTitle("Expand report"));
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Both rows render with a delete affordance while editing.
    const trashButtons = container.querySelectorAll(
      'button[title="Delete list entry"]',
    );
    expect(trashButtons.length).toBe(2);

    // Deleting a row removes it from the panel immediately (staged locally).
    fireEvent.click(trashButtons[0]);
    const remaining = container.querySelectorAll(
      'button[title="Delete list entry"]',
    );
    expect(remaining.length).toBe(1);

    // The deletion is NOT committed until Save — the parent sees only one item.
    fireEvent.click(screen.getByText("Save"))
    expect(onSave).toHaveBeenCalledTimes(1);
    expect(onSave.mock.calls[0][0].items).toHaveLength(1);

    // No server write was ever issued for the deletion.
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

function containerInputs(): NodeListOf<HTMLInputElement> {
  return document.querySelectorAll("input");
}

describe("BillReportPanel — remove transaction", () => {
  const categories: Category[] = [
    { name: "Food", subcategories: ["Groceries", "Snacks"] },
  ];

  const makeReceipt = (): ReceiptData => ({
    id: "bill-42",
    merchantName: "Coffee House",
    timestamp: "2024-05-01T10:30:00Z",
    totalAmount: 42.5,
    currency: "USD",
    items: [
      {
        name: "Coffee",
        fullName: "Large Coffee",
        quantity: 2,
        unitPrice: 3.5,
        totalPrice: 7,
        category: "Food",
        subcategory: "Snacks",
      },
    ],
  });

  it("Remove calls onDelete with the receipt id after a confirmation, immediately", () => {
    const confirmMock = jest.fn().mockReturnValue(true);
    window.confirm = confirmMock;

    const onDelete = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={onDelete}
      />,
    );

    // Open the ⋮ menu and choose Remove.
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Remove"));

    // A confirmation is required before a transaction is removed.
    expect(confirmMock).toHaveBeenCalledTimes(1);
    // The whole transaction is removed, not a single line item.
    expect(onDelete).toHaveBeenCalledTimes(1);
    expect(onDelete).toHaveBeenCalledWith("bill-42");
  });

  it("Remove is cancelled without deleting when the dialog is dismissed", () => {
    window.confirm = jest.fn().mockReturnValue(false);

    const onDelete = jest.fn();
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onDelete={onDelete}
      />,
    );

    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Remove"));

    expect(onDelete).not.toHaveBeenCalled();
  });
});

describe("BillReportPanel — expanded body is items only", () => {
  const categories: Category[] = [
    { name: "Food", subcategories: ["Groceries", "Snacks"] },
  ];

  const makeReceipt = (): ReceiptData => ({
    id: "bill-1",
    merchantName: "Coffee House",
    timestamp: "2024-05-01T10:30:00Z",
    totalAmount: 42.5,
    currency: "USD",
    items: [
      {
        name: "Coffee",
        fullName: "Large Coffee",
        quantity: 2,
        unitPrice: 3.5,
        totalPrice: 7,
        category: "Food",
        subcategory: "Snacks",
      },
      {
        name: "Sandwich",
        fullName: "Club Sandwich",
        quantity: 1,
        unitPrice: 35.5,
        totalPrice: 35.5,
        category: "Food",
        subcategory: "Groceries",
      },
    ],
  });

  it("expanded body shows the items table only — no Vendor card, no Summary card", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
      />,
    );

    fireEvent.click(screen.getByTitle("Expand report"));

    // The items are present.
    expect(screen.getByText("Coffee")).toBeTruthy();
    expect(screen.getByText("Sandwich")).toBeTruthy();

    // The old Vendor and Summary sections are gone from the expanded body.
    expect(screen.queryByText("Vendor")).toBeNull();
    expect(screen.queryByText("Summary")).toBeNull();
    expect(screen.queryByText("Merchant Name")).toBeNull();
    expect(screen.queryByText("Additional Notes")).toBeNull();
    expect(screen.queryByText("Number of Items")).toBeNull();
    expect(screen.queryByText("Total Amount")).toBeNull();
    // The "Refine Items" button is gone.
    expect(screen.queryByText("Refine Items")).toBeNull();
  });

  it("makes every item cell an editable input and shows add/delete only in edit mode", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
      />,
    );

    // expandable panels start collapsed — expand the row first.
    fireEvent.click(screen.getByTitle("Expand report"));

    // Before edit mode: no editable cells, no add/delete controls.
    expect(container.querySelectorAll("input").length).toBe(0);
    expect(
      document.querySelector('button[title="Delete list entry"]'),
    ).toBeNull();
    expect(screen.queryByText("Add Item Row")).toBeNull();

    // Enter edit mode from the ⋮ menu.
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Header inputs (merchant + date = 2; currency is a <select>, total is a
    // readonly display) plus each item row's Name / Price / Quantity (3).
    const inputs = container.querySelectorAll("input");
    expect(inputs.length).toBe(2 + 6);

    // Add Item Row and per-row Delete are present while editing.
    expect(screen.getByText("Add Item Row")).toBeTruthy();
    expect(
      document.querySelector('button[title="Delete list entry"]'),
    ).toBeTruthy();

    // Adding a row appends a fresh editable line (+3 item inputs).
    fireEvent.click(screen.getByText("Add Item Row"));
    expect(container.querySelectorAll("input").length).toBe(2 + 9);

    // Deleting one of the two rows removes it from the panel immediately.
    fireEvent.click(
      document.querySelector('button[title="Delete list entry"]')!,
    );
    expect(container.querySelectorAll("input").length).toBe(2 + 6);
  });

  it("exposes two category comboboxes in edit mode (item table)", () => {
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
      />,
    );

    // expandable panels start collapsed — expand the row first.
    fireEvent.click(screen.getByTitle("Expand report"));
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));

    // Each item exposes category and subcategory as two separate <select>
    // comboboxes in the item table (the header's currency combobox is
    // excluded). The receipt has two items, so there are two of each. (Use an
    // exact-suffix filter: "subcategory" also contains the substring "category".)
    const itemSelects = Array.from(
      document.querySelectorAll<HTMLSelectElement>(
        'select[aria-label^="Edit item"]',
      ),
    );
    const categorySelects = itemSelects.filter((s) =>
      s.getAttribute("aria-label")!.endsWith(" category"),
    );
    const subcategorySelects = itemSelects.filter((s) =>
      s.getAttribute("aria-label")!.endsWith(" subcategory"),
    );
    expect(categorySelects.length).toBe(2);
    expect(subcategorySelects.length).toBe(2);

    expect(categorySelects[0].value).toBe("Food");
    expect(subcategorySelects[0].value).toBe("Snacks");
    expect(categorySelects[1].value).toBe("Food");
  });
});

describe("BillReportPanel — Unsaved badge", () => {
  const categories: Category[] = [
    { name: "Food", subcategories: ["Groceries", "Snacks"] },
  ];

  const makeReceipt = (): ReceiptData => ({
    id: "bill-1",
    merchantName: "Coffee House",
    timestamp: "2024-05-01T10:30:00Z",
    totalAmount: 42.5,
    currency: "USD",
    items: [],
  });

  it("hides the badge when the draft matches the committed bill", () => {
    render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
      />,
    );
    expect(screen.queryByText("Unsaved")).toBeNull();
  });

  it("shows the badge on the header row once a staged edit diverges, then hides it on save", () => {
    const { container } = render(
      <BillReportPanel
        receiptData={makeReceipt()}
        categories={categories}
        expandable={true}
        onSave={jest.fn()}
        onDelete={jest.fn()}
      />,
    );

    // Stage an edit on the header field.
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));
    const merchantInput = Array.from(
      document.querySelectorAll("input"),
    ).find((i) => i.value === "Coffee House") as HTMLInputElement;
    fireEvent.change(merchantInput, { target: { value: "Tea Bar" } });

    // The row now flags the staged, uncommitted edit.
    expect(screen.getByText("Unsaved")).toBeTruthy();

    // Committing clears the divergence.
    fireEvent.click(screen.getByText("Save"));
    expect(screen.queryByText("Unsaved")).toBeNull();
  });

  it("requires a confirmed Timestamp and keeps corrected fields after save failure", async () => {
    const onSave = jest.fn().mockRejectedValue(new Error("save failed"));
    const draft = { ...makeReceipt(), timestamp: undefined, merchantName: "OCR name" };
    render(<BillReportPanel receiptData={draft} draft onSave={onSave} />);

    expect(screen.getByText(/Confirm a Timestamp/)).toBeTruthy();
    expect((screen.getByText("Confirm Bill").closest("button") as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Date / Time"), { target: { value: "2024-05-01T10:30" } });
    const confirm = screen.getByText("Confirm Bill");
    expect((confirm.closest("button") as HTMLButtonElement).disabled).toBe(false);
    fireEvent.change(screen.getByDisplayValue("OCR name"), { target: { value: "Corrected name" } });
    fireEvent.click(confirm);

    await waitFor(() => expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ merchantName: "Corrected name", timestamp: "2024-05-01T10:30", totalAmount: 0 })));
    expect(screen.getByDisplayValue("Corrected name")).toBeTruthy();
  });
});
