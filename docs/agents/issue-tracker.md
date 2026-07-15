# Issue tracker: Local Markdown

Issues and PRDs for this repo live as markdown files under `.scratch/`. No external CLI is needed — agents read and write directly.

## Conventions

- **Create an issue**: Write a new file under `.scratch/<feature>/` (e.g., `.scratch/auth-flow/001-login.md`).
- **Read an issue**: Read the markdown file directly.
- **List issues**: `ls .scratch/<feature>/` or recursive find.
- **Update / comment**: Edit the file in place; append a dated section for notes.
- **Close**: Rename with a `DONE:` prefix or move to `.scratch/done/`.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

Not applicable — all work is local. External contributions would be handled via the chosen collaboration workflow (e.g., GitHub pull requests) but are not part of the triage queue for this repo.

## When a skill says "publish to the issue tracker"

Create a markdown file under `.scratch/<feature>/`.

## When a skill says "fetch the relevant ticket"

Read the corresponding file from `.scratch/`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single markdown file with **child** files as tickets.

- **Map**: A file like `.scratch/map.md` holding Notes / Decisions-so-far / Fog.
- **Child ticket**: A file under `.scratch/<feature>/` linked from the map (e.g., via a task list or frontmatter). Labels encoded as YAML frontmatter tags: `tags: [research, prototype, task]`. Once claimed, add an `assigned-to:` field.
- **Blocking**: Documented in the child ticket body (`Blocked by: 001-login.md`). A ticket is unblocked when every blocker is marked done.
- **Frontier query**: List open children from the map, drop any with unresolved blockers or an assignee; first in map order wins.
- **Claim**: Add `assigned-to: @me` to the child ticket's frontmatter.
- **Resolve**: Update the ticket body with the answer/notes, mark as done, and append a context pointer to the map.
