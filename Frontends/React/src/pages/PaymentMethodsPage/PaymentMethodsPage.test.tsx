import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import PaymentMethodsPage from "./PaymentMethodsPage";

/**
 * `/finance/payment-methods` chrome: where the page lives and how you get out of it. The list itself is
 * tested where it is written (PaymentMethodsPanel), so the panel is stubbed here — this test is about
 * the route's framing, not about a second read of the same rules.
 */

const mockNavigate = jest.fn();

jest.mock("@clerk/clerk-react", () => ({
  useAuth: () => ({ getToken: async () => "token" }),
}));

jest.mock("react-router-dom", () => ({ useNavigate: () => mockNavigate }));

// Stub the panel so this page test does not re-run the list's own assertions.
jest.mock("../../components/PaymentMethods/PaymentMethodsPanel", () => {
  const { createElement } = jest.requireActual("react");
  return {
    __esModule: true,
    default: () => createElement("div", { "data-testid": "payment-methods-panel" }),
  };
});

describe("PaymentMethodsPage", () => {
  beforeEach(() => jest.clearAllMocks());

  it("names the page and hands the list to the panel", () => {
    render(<PaymentMethodsPage />);

    expect(screen.getByRole("heading", { level: 1, name: /payment methods/i })).toBeTruthy();
    expect(screen.getByTestId("payment-methods-panel")).toBeTruthy();
  });

  it("goes back to /finance/bank", () => {
    render(<PaymentMethodsPage />);

    fireEvent.click(screen.getByRole("button", { name: /back to bank/i }));

    expect(mockNavigate).toHaveBeenCalledWith("/finance/bank");
  });
});
