# Learning Pages in MyPlanner — design

Integrate the standalone HTML lesson pages that live in the Nextcloud-synced vault folder
`Agentic Coding Learning` into MyPlanner as a `/learning` route, with a left-panel entry, an
in-page library, and no rebuild or restart when new pages are added. The prototype renders each
page inside an `<iframe>` served by the existing .NET API from a read-only bind mount; the
inline-rendering alternative that styles lesson content with MyPlanner's own Tailwind is recorded
here as a deliberate later migration, not rejected. There is no visual artifact for this grill.

## Terms

**Course** — One subfolder of the Content Mount holding a learning sequence. Today only
*Agentic Coding Learning*; tomorrow possibly *How to play guitar*. Its public name and URL slug
come from the Course Manifest, not from the folder name itself.
_Avoid_: track, module, curriculum, session, "cource".

**Content Mount** — The synced Nextcloud root bind-mounted read-only into the api container; its
immediate children are Course folders. Files placed there are served without rebuilding any image.
_Avoid_: public folder, static assets, The Bucket (The Bucket is upload content the app writes).

**Course Manifest** — `courses.json` in the Content Mount root. One entry per published Course:
slug, display title, the course folder, its lessons subfolder, and one line per Lesson Page (file,
title, order). Required and authoritative — a folder or file that is not listed is not published.
Read from disk on every request, so editing it needs no restart either.
_Avoid_: cources.json (misspelling), index file, config.

**Lesson Page** — One Self-contained HTML file inside a Course's lessons folder, loaded as a
document in an iframe. For the prototype it is styled by its own inline `<style>` and carries any
quiz script inline.
_Avoid_: article, post, component.

**Self-contained Lesson Page** — A Lesson Page that needs nothing outside its own file: styles
inline, quiz script inline or absent, no relative links to other files. This is why dead links are
acceptable (Q10).
_Avoid_: bundle, export, component.

**Exemplar Lesson Page** — The single page a generator copies for its shape: `lessons/0000-sample.html`.
Since Q22 was reversed it is the *worked example*, not the contract; the Learning Guideline is.
_Avoid_: template, boilerplate.

**Learning Guideline** — `LEARNING-GUIDELINE.md` at the root of the Content Mount, next to the Course
Manifest. The written content contract: Self-contained bundling, mobile-first, the palette (app-matching
neutrals plus one Course accent set), quiz markup names, lesson shape, and publishing steps. Not served,
never listed in the Manifest, and authoritative over the Exemplar where they disagree.
_Avoid_: style guide, README (that is Obsidian course material), `docs/learning-guideline.md` (rejected —
invisible to an agent working in the vault).

**Inline Rendering** — The deferred migration: fetch a Lesson Page's HTML through the API and insert
its body into the React page under a wrapper element, styled by MyPlanner's Tailwind. Explicitly not
in the prototype.
_Avoid_: embedding (embedding is the iframe approach that was chosen).

## Why

In the user's words: there are simple HTML+CSS lesson pages outside the repo, in a Nextcloud/Obsidian
folder (`10 Projects/Agentic Coding Learning`), and they should appear in the React app under a
`/learning` route with its own page, a button in the left panel for navigation, and something inside
the page for navigating between lessons and displaying them. The folder is synced by Nextcloud to the
server and will be mounted into Docker next to the React app, so **adding new pages must not require
rebuilding or restarting the app — only refreshing the browser page**. A simple prototype is required
first. Later the user expects more Courses (several folders, not one library).

## Locked decisions

Each of these passed all three gates: hard to reverse in practice, surprising without context, and a
real trade-off. Rejected options are listed with why they lost.

**1. Lesson Pages are rendered in an `<iframe>` for the prototype; Inline Rendering is the later step (Q3).**
The user chose the simple path deliberately: *"i chose simple way for now, go with iframe, later i
will go with B."* An iframe keeps existing pages byte-for-byte correct — their own stylesheet loads,
their quiz scripts run — and guarantees lesson CSS cannot leak into MyPlanner or vice versa.
- *Rejected — Inline Rendering (fetch + inject body into React, styled by Tailwind):* it would give a
  native look and let the existing authenticated service layer fetch content, but it breaks every
  existing page's stylesheet and quiz, and it collides head-on with Tailwind's build-time purging
  (Verified facts #2). Chosen as the later migration, at which point Q6–Q8 reopen.
- *Rejected — convert pages to React/MDX at build time:* every new page would need a rebuild, which
  contradicts the requirement outright.

**2. The .NET API serves the Lesson Pages; nothing is baked into the frontend image (Q1, Q2).**
One controller, reading files from disk per request.
- *Rejected — `setupProxy.js` + express.static in the frontend container:* same-origin and no new
  container, but it only exists while the container runs `react-scripts start`, i.e. it is a dev-mode
  mechanism carrying production traffic.
- *Rejected — bind-mounting into `Frontends/React/public/`:* zero custom code, but `public/` is also
  copied into the image at build, so the same URL would have two sources of truth.
- *Rejected — an nginx sidecar container:* cleanest static serving and survives a future production
  build, but adds a container and a cross-origin URL for no benefit at prototype scale.

**3. The Learning controller is the one anonymous endpoint in the API (Q9).** Read-only GET of files
under the Content Mount, restricted to the manifest-listed paths and to `.html`. Every other
controller (`FinanceController`, `PagesController`, `NoteController`, `TasksController`) is
`[Authorize]`, but an `<iframe src=…>` performs a plain browser navigation and sends no
`Authorization` header, so a protected endpoint would return 401 and the frame would show nothing.
- *Rejected — keep `[Authorize]` and pass a short-lived Clerk token in the iframe query string:*
  content stays behind login, but tokens land in browser history, server logs and `Referer` headers,
  which is more machinery than a prototype deserves.
- *Rejected — fetch HTML through the authenticated service layer and hand the iframe a `blob:` URL:*
  keeps the page authorized, but relative links inside a blob resolve against nothing, so it ends up
  needing both mechanisms.
- Consequence accepted: whatever lands in a published Course folder is readable by anyone who can
  reach the API with a guessed slug. Nothing private belongs in that folder.

**4. Lesson Pages are Self-contained; outbound links may be dead (Q10).** In the user's words: *"i
expect what api call /learning/<slug> lesson/1 will return html with everything inside, i will
regenerate htmls so no links needed (if some appear and would be dead fine)."* So the API returns one
file's HTML, styles travel inside it, and there is no asset-serving layer for `../assets/lesson.css`.
- *Rejected — serve any file under the Content Mount by relative path:* it makes `../assets/lesson.css`
  and the cheat-sheet link resolve for free, but keeps a serving surface wider than the published one
  and pins pages to a shared stylesheet.
- *Rejected — serve narrowly and rewrite/strip links before sending:* narrowest surface, but adds a
  translation layer whose rules must track how lessons happen to be written.

**5. Route shape: `/learning/{courseSlug}/{lessonSlug}`, with redirects from shallower levels, and one
`Learning` entry in the left panel (Q11 then Q15).** `/learning` redirects to the first lesson of the
first published Course; `/learning/{courseSlug}` redirects to that Course's first lesson. Course
switching lives inside the page, above the lesson list.
- *Rejected — `/learning/:slug` for a single library (Q11 as originally answered):* correct until the
  user revealed there will be several Courses; superseded by Q15.
- *Rejected — course in a query param (`/learning?course=…&lesson=…`):* one route to declare, but
  query-string state is awkward with browser back and reads worse in a shared link.
- *Rejected — a Course picker screen at `/learning`:* a real library landing page, but it costs
  someone who reads one Course an extra screen every visit — which is the case today.
- *Rejected (Q11 option C) — Courses as children in the left panel:* would need `LayoutShell` to fetch
  from the API and would grow the sidebar with every new lesson.

**6. Publishing is explicit at both levels: the Course Manifest is authoritative, and it lists lessons
one by one (Q16=B, Q17=C, Q18=C, Q21=A).** `courses.json` in the Content Mount root declares each
Course (slug, display title, folder path, lessons subfolder) and each Lesson Page (file, title,
order). Nothing unlisted appears. Because the manifest lives inside the mount and is read per request,
editing it needs no rebuild or restart.
- *Rejected — scan folders/files at request time with an optional manifest for names (Q16=A, Q18=A):*
  keeps "drop a file, refresh" true all the way down and avoids HTML parsing choices, but publishes
  anything dropped into a folder and makes labels depend on whatever `<title>` happens to say.
- *Rejected — scan recursively for `.html` under a Course folder (Q17=B):* works whatever shape a
  Course takes, but pulls `reference/` pages and stray Obsidian html into the lesson list.
- *Rejected — manifest carries only the lessons path, titles parsed from `<title>` (Q18=A):* cheapest
  wording source, at the cost of opening every file per index request and inheriting whatever is in
  the tag.
- Consequence accepted: adding a Lesson Page is **sync the `.html` + add one manifest line + refresh**,
  not just refresh. A forgotten line reads as "the page did not appear".

**7. There is no written Learning Guideline; an Exemplar Lesson Page is the contract (Q22=C).** The
user declined both a file in the Content Mount and a repo doc, preferring to point the generator at an
existing page. This makes the choice of Exemplar load-bearing — see Open threads Q26 — and means the
rules (inline `<style>`, inline quiz script, no outbound links, mobile-first) exist only implicitly.
- *Rejected — guideline file inside the Content Mount:* it would be read by whatever agent authors
  lessons in that vault folder, but becomes synced content and drifts from the app's serving rules.
- *Rejected — `docs/learning-guideline.md` in this repo:* versioned next to the code that serves it,
  but invisible to an agent working inside the Obsidian folder.
- **Superseded on 2026-11-14 — see decision 8.** The user asked for a written guideline placed where the
  lessons live, which is exactly the option rejected above. The first two bullets of this decision were a
  guess about the user's tolerance for drift; they lost.

**8. Content rules are written down in `LEARNING-GUIDELINE.md` inside the Content Mount, and Lesson Pages
must match MyPlanner's palette (Q22 reversed; new: background is an app token, not a design choice).** The
guideline sits at the mount root beside `courses.json` — one file for every Course, read by whatever agent
authors lessons there. The Exemplar stays as the worked example and defers to it.
- **Palette rule:** a Lesson Page's neutrals equal MyPlanner's Tailwind tokens exactly — page background
  `#f8fafc` (slate-50, the app's content area), cards `#fff`, text `#18181b` (zinc-900), muted `#64748b`
  (slate-500), rules `#e2e8f0`/`#cbd5e1`. Only three accent hues per Course (`h1`, `h2`/correct, kicker) are
  the Course's own. Full-page tints, gradients, images, and white-as-page-background are refused.
- *Rejected — lesson pages keep their own paper tone (the Exemplar's original `#fbfaf7` cream):* it made
  three surfaces meet — slate canvas, white frame chrome, cream document — two visible seams and a beige
  insert that reads as not-the-app.
- *Rejected — sharing Tailwind classes with lesson content:* the app's build only compiles utilities present
  in `src/`, so a class used only in fetched HTML has no rule (Q6). Hence tokens, copied into the page's own
  `<style>`.
- Consequence: `FRAME_CHROME_CLASS` in `LearningPage.tsx` is `bg-slate-50`, not `bg-white`, so the frame and
  the lesson document paint the same colour — no seam, no white flash before the frame is measured. If a
  future Course tints its page background, that class has to come from the manifest or the page's own meta,
  not from a constant.
- Consequence accepted: the guideline lives outside this repo and is not versioned with the code it describes.
  It can drift from the app's actual Tailwind classes; nothing verifies the two agree.
- All published Lesson Pages carry **byte-identical `<style>` and `<script>` blocks** (checked by hash), so a
  diff between two lessons shows content differences only. A lookup page is a Lesson Page too: the vault's
  cheat sheet moved out of `reference/` into `lessons/` and is listed last, while the Markdown notes that stay
  in `reference/` keep their links to other notes and are never listed.

## Routine choices

- **Q4** — The Content Mount is a new env var (suggested name `LEARNING_CONTENT_PATH`) bind-mounted
  **read-only** into the api container at a fixed in-container path such as `/app/learning`, exactly
  mirroring how `STORAGE_PATH` names The Bucket. Chosen over reusing The Bucket (`/app/storage/learning`)
  and over an rsync/copy step into a repo-adjacent folder.
- **Q5** — Only the lessons folder of a Course appears in the app for now; `reference/` (including
  `agentic-coding-cheat-sheet.html`) is out of scope for the prototype.
- **Q13** — On wide screens the lesson list is a sticky column of numbers and titles inside `/learning`,
  collapsing above the frame on narrow screens, with a Course switcher above it (per Q15).
- **Q14** — `Cache-Control: no-store` on every Learning response, so freshness never depends on
  revalidation. Chosen over mtime-based ETag/`Last-Modified` and over a few minutes of `max-age`, which
  would break the core requirement.
- **Q19** — The iframe has a fixed height (`calc(100vh - header)`) with its own scroll; the app page does
  not scroll beside it. Auto-height by same-origin measurement was deferred as a contained follow-up —
  but see Open threads Q25, which mobile-first reopens.
- **Q20** — Two failure states: an empty list with `200` and a calm "no Course published yet" / "this
  Course has no lessons yet" message when there is simply nothing to read; a distinct error response
  naming the missing path, with an error card in the page, when `courses.json` is unreadable or points
  at a folder that does not exist.
- **Q23** — Mobile-first for both the `/learning` layout and future Lesson Pages: *"lets go mobile first,
  and fix later."* Narrow-screen layout is the designed one; desktop polish comes afterwards.

## Verified facts

Established by reading the repo and the vault folder rather than by asking.

1. **Frontend container runs a dev server.** `Frontends/React/dockerfile` does `npm install`, `npm run
   build`, then `CMD ["npm","start"]` — i.e. `react-scripts start` on port 3000, published as host
   `8000`. This is why the rejected `setupProxy.js` option would technically have worked, and why it was
   judged fragile for production.
2. **Tailwind purges everything not in `src/`.** `tailwind.config.js` content is only
   `./src/**/*.{js,jsx,ts,tsx}`; the built stylesheet measured `build/static/css/main.*.css` = **43 KB**.
   A utility class that appears only in fetched HTML has no rule at all, so Inline Rendering cannot work
   without a safelist or a purge-off build (this is Q6, deferred). Conversely `@tailwind base` plus the
   base layer (`h2 { @apply text-3xl font-bold }`, links blue/underlined) does apply to injected markup.
3. **All existing API controllers are `[Authorize]`** with Clerk JWT bearer auth, and CORS is
   `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()`. Endpoint paths are centralised in
   `MyPlanner.API/ApiEndpoints.cs` as nested const strings under an `api` base.
4. **The Bucket pattern being copied:** `docker-compose.yaml` mounts `${STORAGE_PATH:-./storage}:/app/storage`,
   and `Program.cs` binds `BucketOptions.Path` from `STORAGE_PATH`. The Content Mount follows the same shape.
5. **Vault folder layout** (the Course folder inside the synced vault — its host path lives in `.env` under
   `LEARNING_CONTENT_PATH` and is not recorded here):
   `lessons/` holds 5 `.html` plus 3 `.md` — and three lessons exist twice (e.g.
   `0003-agent-written-tests-that-fail-for-the-right-reason` as both `.html` and `.md`, likewise 0004);
   `reference/` holds `agentic-coding-cheat-sheet.html` + 3 `.md`; `assets/` holds `lesson.css` and
   `quiz.js`; `learning-records/` is `.md` only; the root has 8 `.md` files (README, MISSION,
   LEARNING-PLAN, HOW-IT-WORKS, NOTES, RESOURCES, AGENTIC-CODING-MAP). Number prefixes 0001–0005 give order.
6. **Existing pages are standalone documents.** `<head>` has `<link rel="stylesheet" href="../assets/lesson.css">`;
   body classes are custom (`.card`, `.kicker`, `.path`, `.prompt`); quizzes use either inline
   `answer(true|false)` handlers (lesson 1) or the shared `assets/quiz.js` contract
   `<div class="quiz" data-answer="C"><button data-opt="A">…</button><span class="fb"></span><p class="why" hidden>…</p></div>`
   which locks after the first click. `lesson.css` sets `body{max-width:780px;margin:42px auto}`.
7. **Lesson 1's footer links leave the mount**: `../reference/agentic-coding-cheat-sheet.html` (inside the
   folder, but `reference/` is out of scope per Q5) and `../../../30 Resources/Agentic Coding Resources.md`
   (outside the Content Mount entirely). Under Self-contained pages both are expected to be dead.
8. **The mount's host path is machine-local**, set per environment by `LEARNING_CONTENT_PATH` (`.env` locally,
   a repo variable in CI) and never committed, so on this machine it contains spaces and capitals — hence URL
   slugs are declared in the manifest rather than taken verbatim from folder names.

## Risks

- **Anonymous read surface.** The Learning controller is unauthenticated, so lesson text becomes public
  to anyone who can reach the API (already CORS-wide). Mitigation: only manifest-listed `.html` paths are
  served, and nothing private may be placed in a Course folder. Path traversal must be rejected by
  resolving every path under the mount root before reading.
- **The requirement is now "sync + one manifest line + refresh", not "sync + refresh".** A forgotten
  `courses.json` line is the most likely support ticket this design produces, and it looks identical to a
  silent failure. Q20's error states do not cover it — the file exists and is valid.
- **Two sources describe one Course.** The manifest names the folder and lessons subfolder; renaming a
  folder in Obsidian or Nextcloud breaks that pairing until the manifest follows. Q20 turns part of this
  into a visible named-path error, but not all of it. A duplicate `courses.json` inside the Course folder was
  deleted for the same reason — only the mount-root manifest is ever read, and a nested one silently misleads
  whoever edits content.
- **Nextcloud partial sync** can serve a half-written HTML file, or an empty folder, at exactly the moment
  a user opens `/learning`. No atomicity guarantee exists across the mount.
- **Nested scrolling.** Q19's fixed-height frame plus a lesson column plus a drawer sidebar is up to three
  scroll regions on a phone — against the stated mobile-first priority. Unresolved (Q25).
- **Deferred Inline Rendering has a hard prerequisite.** Moving to it without solving Tailwind purging
  (Q6) yields unstyled content that is *partly* styled — utilities already used in `src/` work, everything
  else silently does not. That failure mode is easy to hit by accident.
- **No written guideline means contract drift.** ~~With Q22=C the only statement of the content rules is one
  Exemplar file~~ — closed on 2026-11-14 by decision 8 (`LEARNING-GUIDELINE.md` in the mount). Still open:
  nothing in the app validates that a published page is Self-contained, so a page that breaks the guideline
  renders unstyled or half-styled rather than being refused.
- **Long lesson titles** ('Agent tooling map: harnesses, MCP and skills') wrap or truncate in any narrow
  picker; mobile-first makes this worse before desktop polish fixes it.

## Deferred

Decisions postponed by Q3 (iframe now) and Q23 (mobile-first). Each reopens at a named trigger.

- **Q6 — Tailwind utilities for fetched HTML.** Under Inline Rendering, classes appearing only in fetched
  HTML have no compiled rule (the app's stylesheet is 43 KB of exactly what `src/` uses). The deferred
  choice was between safelisting a closed vocabulary named in a guideline, building a purge-off stylesheet
  for `/learning` only, or rewriting classes into inline styles in the API. **Reopens when:** Lesson Pages
  are rendered Inline Rendering rather than in an iframe.
- **Q7 — Regenerating the five existing Lesson Pages.** They work untouched inside an iframe today. Under
  Inline Rendering they would need rewriting to Tailwind vocabulary (their `.card`/`.kicker` classes and
  `../assets/lesson.css` link would be meaningless). **Reopens when:** the Inline Rendering migration starts,
  or when the lessons are regenerated as Self-contained/mobile-first anyway (Q10/Q26).
- **Q8 — Retrieval checks under Inline Rendering.** `quiz.js` and inline `answer()` handlers run inside the
  iframe document now. Inserted HTML never executes scripts, so the deferred choice was freezing the
  `data-answer`/`data-opt` markup as a contract hydrated by one React widget, dropping interactive checks,
  or executing page scripts (rejected: arbitrary JS from a synced folder inside an authenticated app).
  **Reopens when:** the Inline Rendering migration starts.

## Open threads

Asked and left undecided — the user pressed Finish while round 9 was open. Each has its options and the
recommendation that was on screen.

Settled during implementation: **Q24 = A, Q25 = A, Q26 = A** — the recommendations above were accepted
unchanged, and each answer's cost is recorded under its option.

- **Q24 — On a phone, how do you pick which lesson to read?** Options: (A) a "Lessons" disclosure button in
  a slim toolbar under the page heading, opening the list over the top of the frame, current lesson named in
  the toolbar when closed; (B) a full-screen lesson index sheet with Back and Prev/Next under the lesson;
  (C) Prev/Next pair plus a native `<select>`. **Recommended: A** — one widget covers picking and "where am
  I", no new route or animation, at the cost of two taps per switch. Unanswered means the mobile picker is
  undefined even though Q23 made mobile the priority.
  **Answered A:** one toolbar control below the heading both picks the lesson and names what is open; the
  wide-screen list stays a sticky column. Two taps per switch is the accepted cost.
- **Q25 — Who scrolls on mobile: the frame or the page?** Options: (A) one scroller everywhere — the frame
  auto-grows via a same-origin `scrollHeight` read and the app page scrolls; (B) keep Q19's viewport-height
  frame with internal scroll at every size; (C) auto-grown on mobile, fixed-height on desktop. **Recommended:
  A** — it is the pattern that behaves on a phone and avoids two strategies, at the cost of owning a
  measurement loop (resize, orientation change, quiz explanations expanding the document) from day one.
  This directly contradicts Q19's routine decision, which was made before mobile-first arrived.
  **Answered A**, so Q19's fixed-height frame is superseded: one scroller at every width, and the
  measurement loop (frame load, window resize, and a `ResizeObserver` for quiz explanations that expand the
  document) is owned from day one.
- **Q26 — Which page becomes the Exemplar Lesson Page?** Because Q22=C left the content contract implicit in
  one file, an Exemplar must exist before anything can be generated or tested. Options: (A) a throwaway
  `lessons/0000-sample.html` that is Self-contained, mobile-first and carries the inline quiz markup — also
  the first smoke test of route + manifest + iframe; (B) regenerate Lesson 1 as the Exemplar; (C) regenerate
  all five now. **Recommended: A** — it settles the contract cheaply and lets app work and content
  regeneration proceed in either order, at the cost of one fake lesson in the real library until deleted.
  **Answered A:** `lessons/0000-sample.html` exists, Self-contained and mobile-first, listed first in
  `courses.json`, and is the file the content contract (Q22=C) is read from. Lessons 1-4 are on disk but were
  **unlisted from the manifest on 2026-11-14**: each still loads `<link href="../assets/lesson.css">` and links
  relative files (`../reference/…`, `../../../30 Resources/…`) that the Content Mount does not serve, so they
  would render unstyled with dead internal links — a breach of Q10's Self-contained rule. Listing is what
  publishes (Q17=C), so unlisting is the honest state until the learning project regenerates them to match the
  Exemplar; relisting is one manifest line each. Their quiz scripts are already inline, so only the stylesheet
  stands in the way. **Followed up:** Lessons 1-4 have since been regenerated to `LEARNING-GUIDELINE.md` — inline
  stylesheet and quiz script, no relative links, app-matched palette — and are listed again; the Exemplar stays
  first in the manifest until a real lesson replaces it.

## Implementation sketch

Not grilled — derived from the decisions above, offered so a reader can start building. Flagged as proposed
naming, not as settled vocabulary.

- `docker-compose.yaml` (api service): `- ${LEARNING_CONTENT_PATH}:/app/learning:ro`, plus
  `LEARNING_CONTENT_PATH: /app/learning` in `environment`, mirroring the `STORAGE_PATH` lines. The untracked
  local `.env` sets `LEARNING_CONTENT_PATH` to the synced vault folder on this machine, and CI passes the
  server's Nextcloud path as a repo variable.
- `courses.json` at the mount root, one entry per Course: `{ slug, title, folder, lessons, pages: [{ file,
  title }] }`, page order taken from array order (Q18=C/Q21=A). Slugs are declared here — by convention the
  folder name lowercased and hyphenated (`agentic-coding-learning`) so URLs survive capitals and spaces.
- `MyPlanner.API/Controllers/LearningController.cs` — **no `[Authorize]`** (Q9), three GETs added to
  `ApiEndpoints`: `api/learning/courses` (manifest → Course list), `api/learning/courses/{courseSlug}/lessons`
  (manifest entry → lesson list), `api/learning/courses/{courseSlug}/lessons/{lessonSlug}` (returns the file's
  HTML as `text/html`, `Cache-Control: no-store`). Every path resolved against the mount root and rejected if
  it escapes it; only manifest-listed files are ever opened. Missing Course/path vs empty folder follow Q20's
  two shapes.

  **Naming amended during implementation:** the sketch wrote the item route as `…/pages/{lessonSlug}`, which put
  a second word on the same concept one segment away from `…/lessons` — and its parameter was already called
  `lessonSlug`. The routes now say one word: `…/courses/{courseSlug}/lessons` is the collection (JSON) and
  `…/courses/{courseSlug}/lessons/{lessonSlug}` is the item (`text/html`). Nothing external depends on the old
  spelling; it was never deployed.
- Frontend: one `Learning` entry in `LayoutShell`'s `NAV_ITEMS`; routes `/learning`,
  `/learning/{courseSlug}`, `/learning/{courseSlug}/{lessonSlug}` under the protected `LayoutShell`; a
  `LearningService` for all three calls (the two lists and the page's HTML); the page renders Course
  switcher + lesson list + `<iframe>`.

  **Amended during implementation, because Q25 = A.** The frame is not pointed at the API URL: a
  cross-origin `src` cannot be measured, so there is no same-origin `scrollHeight` read and no auto-grow.
  Instead `LearningService.getLessonPageHtml` fetches the file as text and hands it to the frame as
  `srcDoc`, which makes the document same-origin (measurement works), keeps lesson CSS out of the app and
  vice versa, and still runs the page's own quiz script. The accepted consequence: the frame itself has no
  address — the URL you share is the React route `/learning/{courseSlug}/{lessonSlug}`, and `srcDoc` re-runs
  the page's scripts on every switch.
