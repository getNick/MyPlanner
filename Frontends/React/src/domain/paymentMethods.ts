import type {
  BackendCurrency,
  BackendPaymentMethod,
  BackendPaymentMethodType,
} from "../types/paymentMethodTypes";

/**
 * Rules shared by `/finance/bank` (statement import) and `/finance/payment-methods` (management), so the picker and
 * the list cannot disagree about which methods may own a statement. Ticket 02 imports into whatever
 * the picker offers, so `isImportTarget` is the contract it inherits.
 */

/**
 * Banks whose statement format parses today. Keep in lockstep with `BankProfileRegistry`
 * (Backends/DotNet/MyPlanner.Service/Helpers/BankExport/BankProfileRegistry.cs): listing a bank here
 * promises an import its registry does not have. Adding a bank means profile + option in one commit.
 */
export const SUPPORTED_BANK_PROVIDERS: readonly string[] = ["Monobank"];

/**
 * Explicit "this card's bank is not in the list" choice. It stores **null**, so an explicitly-Other
 * card and a legacy never-answered card are the same row (`bankProvider IS NULL`) and one import
 * refusal covers both.
 */
export const OTHER_PROVIDER_LABEL = "Other / not listed";

/**
 * Form-level choice for `Other / not listed`. It is a token, never a stored value: a form holding it
 * posts **null** (see `providerFromChoice`). The empty string stays free to mean “nothing chosen
 * yet”, which is what blocks Save.
 */
export const OTHER_PROVIDER_VALUE = "__other__";

export interface ProviderOption {
  /** Form value: a bank name, or {@link OTHER_PROVIDER_VALUE}. */
  value: string;
  label: string;
}

/** Turn the user's explicit choice into what gets stored: a provider name, or null for “no format”. */
export function providerFromChoice(choice: string): string | null {
  return choice === OTHER_PROVIDER_VALUE ? null : choice;
}

const STATEMENT_CAPABLE_TYPES: readonly BackendPaymentMethodType[] = [
  "BankCard",
  "SavingsAccount",
];

/** Whether a type can hold a bank account a statement could be issued for. */
export function supportsBankStatement(type: BackendPaymentMethodType): boolean {
  return STATEMENT_CAPABLE_TYPES.includes(type);
}

/**
 * Whether this method may be chosen as an import target: a statement-capable type **and** a known
 * Bank Provider. `Cash` and `Other` never qualify (no bank issues a cash statement, and an `Other`
 * method cannot be trusted to have a format), and neither does a card with no provider — there would
 * be no columns to read the file with.
 */
export function isImportTarget(method: BackendPaymentMethod): boolean {
  return supportsBankStatement(method.type) && Boolean(method.bankProvider);
}

/** List order for `/finance/payment-methods`: cards, savings accounts, cash wallets, other; A→Z inside each. */
const TYPE_GROUP_ORDER: readonly BackendPaymentMethodType[] = [
  "BankCard",
  "SavingsAccount",
  "Cash",
  "Other",
];

export function compareForListOrder(a: BackendPaymentMethod, b: BackendPaymentMethod): number {
  const byGroup = TYPE_GROUP_ORDER.indexOf(a.type) - TYPE_GROUP_ORDER.indexOf(b.type);
  return byGroup !== 0 ? byGroup : a.name.localeCompare(b.name);
}

const CURRENCY_LABELS: Record<BackendCurrency, string> = {
  UAH: "UAH",
  USD: "USD",
  EURO: "EUR", // backend enum spelling; the ledger shows ISO 4217
};

export function displayCurrency(currency: BackendCurrency): string {
  return CURRENCY_LABELS[currency] ?? currency;
}

/** Picker text on `/finance/bank`, e.g. `Monobank black — UAH`. */
export function formatImportTargetLabel(method: BackendPaymentMethod): string {
  return `${method.name} — ${displayCurrency(method.currency)}`;
}

/**
 * Provider choices for the form: the banks that parse today, then `Other / not listed`.
 *
 * A stored provider outside that list (a free-text value saved before the choice closed) is offered
 * as-is, so editing a row's name cannot quietly rewrite which bank it claims. It is labelled as
 * unsupported rather than hidden, because it still means "no statement format" to ticket 02.
 */
export function providerOptions(storedProvider?: string | null): ProviderOption[] {
  const options: ProviderOption[] = SUPPORTED_BANK_PROVIDERS.map((bank) => ({
    value: bank,
    label: bank,
  }));

  if (storedProvider && !SUPPORTED_BANK_PROVIDERS.includes(storedProvider)) {
    options.unshift({ value: storedProvider, label: `${storedProvider} (unsupported)` });
  }

  options.push({ value: OTHER_PROVIDER_VALUE, label: OTHER_PROVIDER_LABEL });
  return options;
}

/**
 * The method `/finance/bank` preselects: the remembered last import target when it is still selectable,
 * otherwise the newest selectable one.
 *
 * A payment method carries no created-at column (and this ticket adds none), so the list from the
 * API is the only ordering signal there is; its tail is treated as the newest.
 */
export function pickDefaultImportTarget(
  methods: BackendPaymentMethod[],
  rememberedId: string | null
): BackendPaymentMethod | null {
  const selectable = methods.filter(isImportTarget);

  const remembered = rememberedId
    ? selectable.find((method) => method.id === rememberedId)
    : undefined;

  return remembered ?? (selectable.length ? selectable[selectable.length - 1] : null);
}

const IMPORT_TARGET_KEY = "myplanner.bank.importTargetId";

/**
 * Selection memory across visits, kept in localStorage: re-picking the same card daily is friction
 * with no payoff in a one-user tool. Fails quiet where storage is unavailable (private mode) — the
 * fallback default is a fine answer there.
 */
export function rememberImportTarget(id: string): void {
  try {
    window.localStorage.setItem(IMPORT_TARGET_KEY, id);
  } catch {
    // Nothing to recover from: this only costs a re-pick next visit.
  }
}

export function recalledImportTarget(): string | null {
  try {
    return window.localStorage.getItem(IMPORT_TARGET_KEY);
  } catch {
    return null;
  }
}
