import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import LayoutShell from "./LayoutShell";

/**
 * The left panel entry (Q23=B). Learning is a section of the app next to Home, TODO and Finance, so it has
 * to appear in the one panel definition shared by the desktop rail and the mobile drawer — and be marked
 * as where you are while any `/learning/...` route is open.
 */

jest.mock("@clerk/clerk-react", () => ({
  useUser: () => ({ isLoaded: false, user: null }),
  UserButton: () => null,
}));

const PageStub: React.FC = () => <div data-testid="page" />;

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<LayoutShell />}>
          <Route path="/learning" element={<PageStub />} />
          <Route path="/learning/:courseSlug/:lessonSlug" element={<PageStub />} />
          <Route path="*" element={<PageStub />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

const learningLink = () => screen.getAllByRole("link", { name: "Learning" })[0];

describe("LayoutShell navigation", () => {
  it("offers Learning in the panel, pointing at /learning", () => {
    renderAt("/todo");

    expect(learningLink().getAttribute("href")).toBe("/learning");
  });

  it("marks Learning as the open section on a lesson route (Q15)", () => {
    renderAt("/learning/guitar/0001-first-chords");

    expect(learningLink().hasAttribute("aria-current")).toBe(true);
    expect(screen.getAllByRole("link", { name: "Home" })[0].hasAttribute("aria-current")).toBe(false);
  });
});
