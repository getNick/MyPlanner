import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import LearningPage from "./LearningPage";

/**
 * `/learning` and its two shallower redirects (Q15). fetch is stubbed by URL, so the page is exercised
 * through the same shapes the API produces: a Course list, a lesson list, one page's HTML, and the two
 * failure shapes of Q20 kept apart — an empty library reads calm, a broken mount names its path.
 */

type Mount = {
  courses?: unknown;
  lessons?: Record<string, unknown>;
  pages?: Record<string, string>;
  status?: number;
  errorBody?: string;
};

function mockFetch({ courses = [], lessons = {}, pages = {}, status = 200, errorBody }: Mount = {}) {
  jest.spyOn(global, "fetch").mockImplementation(async (input) => {
    const path = new URL(String(input)).pathname.replace(/^\/api\/learning\/?/, "");
    if (errorBody !== undefined || status >= 400) {
      return new Response(errorBody ?? JSON.stringify({ error: `Unreachable API` }), { status });
    }
    if (path === "courses") return new Response(JSON.stringify(courses), { status });

    const lessonsMatch = path.match(/^courses\/([^/]+)\/lessons$/);
    if (lessonsMatch) return new Response(JSON.stringify(lessons[lessonsMatch[1]] ?? []), { status });

    const pageMatch = path.match(/^courses\/([^/]+)\/lessons\/(.+)$/);
    if (pageMatch) return new Response(pages[`${pageMatch[1]}/${pageMatch[2]}`] ?? "", { status });

    return new Response(null, { status: 404 });
  });
}

const LocationDisplay: React.FC = () => {
  const { pathname } = useLocation();
  return <div data-testid="location">{pathname}</div>;
};

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/learning" element={<LearningPage />} />
        <Route path="/learning/:courseSlug" element={<LearningPage />} />
        <Route path="/learning/:courseSlug/:lessonSlug" element={<LearningPage />} />
      </Routes>
      {/* Outside <Routes>, so the redirect chain is observable wherever the router ends up. */}
      <LocationDisplay />
    </MemoryRouter>
  );
}

const GUITAR = [{ slug: "guitar", title: "How to play guitar" }];
const GUITAR_LESSONS = [
  { slug: "0001-first-chords", title: "First chords" },
  { slug: "0002-barre-chords", title: "Barre chords" },
];
const FIRST_PAGE_HTML = "<!doctype html><html><head><style>body{font:18px system-ui}</style></head><body><h1>First chords</h1></body></html>";

afterEach(() => jest.restoreAllMocks());

describe("LearningPage on a lesson route", () => {
  it("shows the Course, its lesson list, and the Lesson Page inside the frame", async () => {
    mockFetch({ courses: GUITAR, lessons: { guitar: GUITAR_LESSONS }, pages: { "guitar/0001-first-chords": FIRST_PAGE_HTML } });

    renderAt("/learning/guitar/0001-first-chords");

    expect(await screen.findByRole("heading", { level: 1, name: /how to play guitar/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /barre chords/i })).toBeTruthy();
    await waitFor(() => expect(screen.getByTitle("First chords").getAttribute("srcdoc")).toContain("<h1>First chords</h1>"));
  });

  it("marks which lesson is open, and switching lessons replaces the frame's document", async () => {
    mockFetch({
      courses: GUITAR,
      lessons: { guitar: GUITAR_LESSONS },
      pages: {
        "guitar/0001-first-chords": FIRST_PAGE_HTML,
        "guitar/0002-barre-chords": "<!doctype html><html><body><h1>Barre chords</h1></body></html>",
      },
    });

    renderAt("/learning/guitar/0001-first-chords");
    await screen.findByRole("heading", { level: 1, name: /how to play guitar/i });

    fireEvent.click(screen.getByRole("link", { name: /barre chords/i }));

    await waitFor(() => expect(screen.getByTitle("Barre chords").getAttribute("srcdoc")).toContain("<h1>Barre chords</h1>"));
    // The lesson now open carries aria-current; the one left behind does not.
    await waitFor(() => expect(screen.getByRole("link", { name: /barre chords/i }).getAttribute("aria-current")).toBe("page"));
    expect(screen.getByRole("link", { name: /first chords/i }).getAttribute("aria-current")).toBeNull();
  });

  it("on a narrow screen the lesson list hides behind one Lessons control naming what is open (Q24)", async () => {
    mockFetch({ courses: GUITAR, lessons: { guitar: GUITAR_LESSONS }, pages: { "guitar/0001-first-chords": FIRST_PAGE_HTML } });

    renderAt("/learning/guitar/0001-first-chords");
    await screen.findByRole("heading", { level: 1, name: /how to play guitar/i });

    const toggle = screen.getByRole("button", { name: /lessons/i });
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    expect(toggle.textContent).toContain("First chords");

    fireEvent.click(toggle);

    expect(screen.getByRole("button", { name: /lessons/i }).getAttribute("aria-expanded")).toBe("true");
  });
});

describe("LearningPage on the shallower routes (Q15)", () => {
  it("/learning opens the first lesson of the first published Course", async () => {
    mockFetch({ courses: GUITAR, lessons: { guitar: GUITAR_LESSONS }, pages: { "guitar/0001-first-chords": FIRST_PAGE_HTML } });

    renderAt("/learning");

    await waitFor(() => expect(screen.getByTestId("location").textContent).toBe("/learning/guitar/0001-first-chords"));
  });

  it("/learning/guitar opens that Course's first lesson", async () => {
    mockFetch({ courses: GUITAR, lessons: { guitar: GUITAR_LESSONS }, pages: { "guitar/0001-first-chords": FIRST_PAGE_HTML } });

    renderAt("/learning/guitar");

    await waitFor(() => expect(screen.getByTestId("location").textContent).toBe("/learning/guitar/0001-first-chords"));
  });
});

describe("LearningPage failure shapes stay apart (Q20)", () => {
  it("an unpublished library says there is nothing to read yet, without an error", async () => {
    mockFetch({ courses: [] });

    renderAt("/learning");

    expect(await screen.findByText(/no course published yet/i)).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("a mount the manifest points away from names the path it could not read", async () => {
    mockFetch({ status: 503, errorBody: JSON.stringify({ error: "No Course Manifest to read: /app/learning/courses.json does not exist in the Content Mount." }) });

    renderAt("/learning");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("/app/learning/courses.json");
  });
});
