import LearningService from "./LearningService";

/**
 * The Learning client. Stubbed fetch asserts the HTTP contract of the one anonymous corner of the API
 * (Q9): no Authorization header is sent, because an authenticated read would be a second mechanism the
 * design rejected — and the failure text has to survive to the page, which shows it in an error card
 * naming the missing path (Q20).
 */

function makeService(): LearningService {
  return new LearningService();
}

describe("LearningService.getCourses", () => {
  afterEach(() => jest.restoreAllMocks());

  it("reads the published Courses from the API origin without a token", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      new Response(JSON.stringify([{ slug: "guitar", title: "How to play guitar" }]), { status: 200 }),
    );

    const courses = await makeService().getCourses();

    const [uri, init] = [fetchMock.mock.calls[0][0] as string, fetchMock.mock.calls[0][1] as RequestInit];
    const url = new URL(uri);
    expect(url.pathname).toBe("/api/learning/courses");
    expect(init?.headers ?? {}).not.toHaveProperty("Authorization");
    expect(courses).toEqual([{ slug: "guitar", title: "How to play guitar" }]);
  });

  it("carries the server's named path into the thrown error so the page can show it", async () => {
    jest.spyOn(global, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ error: "No Course Manifest to read: /app/learning/courses.json does not exist." }), { status: 503 }),
    );

    await expect(makeService().getCourses()).rejects.toThrow(/\/app\/learning\/courses\.json/);
  });

  it("an empty library is an empty list, not an error (Q20)", async () => {
    jest.spyOn(global, "fetch").mockResolvedValue(new Response(JSON.stringify([]), { status: 200 }));

    await expect(makeService().getCourses()).resolves.toEqual([]);
  });
});

describe("LearningService.getLessonPageHtml", () => {
  afterEach(() => jest.restoreAllMocks());

  it("returns the Lesson Page file verbatim, styles and quiz script inside it (Q10)", async () => {
    const html = "<!doctype html><html><head><style>body{font:18px system-ui}</style></head><body><h1>First chords</h1></body></html>";
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(new Response(html, { status: 200 }));

    const page = await makeService().getLessonPageHtml("guitar", "0001-first-chords");

    expect(new URL(fetchMock.mock.calls[0][0] as string).pathname).toBe("/api/learning/courses/guitar/lessons/0001-first-chords");
    expect(page).toBe(html);
  });
});
