# 001 — Read Lesson Pages from a read-only Content Mount

Design: `docs/learning-pages-integration-design.md` (all 26 questions resolved; Q24/Q25/Q26 settled during
implementation). Status: **DONE** — merged on `main` as `ec2f5f8` plus the review follow-ups.

## What was built

MyPlanner shows standalone HTML **Lesson Pages** written by the Agentic Coding Learning project at
`/learning`, with no rebuild and no restart when new content is added.

- **Backend** — `LearningLibrary` reads one **Course Manifest** (`courses.json`) from the **Content Mount** on
  every call: Courses, their Lesson Pages in manifest order, and a page's HTML byte-for-byte. Only listed
  `.html` files are ever opened; slugs never become paths; any resolved path outside the mount is refused.
  Three anonymous GETs (`ApiEndpoints.Learning`): `api/learning/courses`,
  `.../courses/{courseSlug}/lessons` (collection), `.../courses/{courseSlug}/lessons/{lessonSlug}` (the item,
  `text/html`) — one word for one concept;
  `Cache-Control: no-store`. Missing manifest / missing folder / missing listed file → **503 naming the path**;
  unpublished Course or unlisted Lesson Page → **404**.
- **Deployment** — `docker-compose.yaml` mounts `${LEARNING_CONTENT_PATH:-./learning}:/app/learning:ro` and
  sets `LEARNING_CONTENT_PATH=/app/learning`; `.env` (untracked) points the host side at the local synced
  vault folder; CI passes the server's path as a repo variable (`vars.LEARNING_CONTENT_PATH`, still to be
  set once on GitHub).
- **Frontend** — one `Learning` entry in the left panel; routes `/learning`,
  `/learning/{courseSlug}`, `/learning/{courseSlug}/{lessonSlug}` (the two shallower ones redirect down to the
  first published lesson); Course switcher plus a numbered lesson list inside the page; on narrow screens the
  list hides behind one **Lessons** control that names the open lesson. The Lesson Page is fetched as text and
  handed to an `<iframe>` as `srcDoc`, so the frame is same-origin, auto-grows from `scrollHeight`, and the app
  page is the only scroller (Q25=A). Empty library reads calm; a broken mount shows an error card naming the path.

## Seams covered by tests

- `MyPlanner.UnitTests/Services/LearningLibraryTests.cs` — 13 tests over a temp Content Mount: manifest
  publishes exactly what is listed, order is array order, titles come from the manifest not `<title>`, HTML
  returns unmodified, `.md` refused, traversal refused, each failure shape names its path, manifest edits
  visible without restart.
- `src/services/LearningService.test.ts` — 4 tests: URL contract on the API origin, **no Authorization
  header**, server error text carried through for the card, empty list is not an error, page HTML verbatim.
- `src/pages/LearningPage/LearningPage.test.tsx` — 7 tests: Course + list + frame content, switching lessons
  replaces the frame document and moves `aria-current`, the narrow-screen disclosure, both shallower
  redirects, and the two failure shapes kept apart.
- `src/components/LayoutShell/LayoutShell.test.tsx` — 2 tests: the panel entry exists at `/learning` and is
  marked active on a lesson route.

Not covered by tests (needs a browser): frame auto-height growth, real mobile layout, Nextcloud sync timing.

## Left undone on purpose

- **Inline Rendering** (Q3=B), Tailwind vocabulary for fetched HTML (Q6), retrieval-widget hydration (Q8) —
  reopen when the iframe is dropped.
- **Lessons 1–4 were regenerated to `LEARNING-GUIDELINE.md` and relisted**: stylesheet and quiz
   script inlined, the shared `../assets/lesson.css` and `../assets/quiz.js` gone, internal `.html` links replaced
   by `.path` text, palette moved onto MyPlanner's tokens. All five published pages now carry byte-identical
   `<style>` and `<script>` blocks. Lesson 5 is Markdown only — it becomes a page when generated to the guideline.
- **`vars.LEARNING_CONTENT_PATH` is not set on GitHub.** The deploy workflow passes it; until you set it to the
  server's Nextcloud path, the server falls back to `./learning` and `/learning` reports the path it could not read.
- Lesson Pages are public to anyone who can reach the API (Q9). Nothing private may live in a Course folder.

## How to see it locally

```bash
docker compose up -d database
cd Backends/DotNet && dotnet run --project MyPlanner.API   # reads .env, mount path from env
# or: docker compose up api frontend
```

Then open `/learning` in the app and refresh after syncing a new page plus one `courses.json` line.
