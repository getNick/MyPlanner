import React, { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { AlertTriangle, GraduationCap, ListChecks } from "lucide-react";
import LearningService from "../../services/LearningService";
import type { LearningCourse, LearningLesson } from "../../types/learningTypes";

/**
 * `/learning` — the library inside MyPlanner. Courses and Lesson Pages are read from the Content Mount
 * through the API on every visit, so a new lesson is "sync the file + one `courses.json` line + refresh":
 * no rebuild, no restart (Q1, Q2, Q6).
 *
 * The Lesson Page itself opens in an `<iframe>` whose document is handed over as `srcDoc` (decision 1 plus
 * the auto-height follow-up): the file's own stylesheet and quiz script run inside that frame, lesson CSS
 * cannot leak into the app or the other way round, and because the document is same-origin the frame can
 * be measured — so there is one scroller, the app page, at every width (Q25). Inline Rendering under
 * MyPlanner's own Tailwind stays the deferred migration.
 */

/** A frame this short means the lesson document has not been measured yet. */
const MIN_FRAME_HEIGHT = 320;

/**
 * The frame carries `bg-slate-50` because that is what a Self-contained Lesson Page paints as its own
 * background (see LEARNING-GUIDELINE.md in the Content Mount): same colour on both sides means no seam and
 * no white flash while the document loads or before it is measured. White would frame cream.
 */
const FRAME_CHROME_CLASS = "w-full border-0 rounded-sm bg-slate-50";

export const LearningPage: React.FC = () => {
  const { courseSlug, lessonSlug } = useParams<{ courseSlug?: string; lessonSlug?: string }>();
  const navigate = useNavigate();
  const service = useMemo(() => new LearningService(), []);

  const [courses, setCourses] = useState<LearningCourse[] | null>(null);
  const [lessons, setLessons] = useState<LearningLesson[] | null>(null);
  const [pageHtml, setPageHtml] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [listOpen, setListOpen] = useState(false);

  // ── Courses: read per request, so the manifest never needs a restart ──────────────
  useEffect(() => {
    let active = true;
    service
      .getCourses()
      .then((found) => active && setCourses(found))
      .catch((failure: Error) => active && setError(failure.message));
    return () => {
      active = false;
    };
  }, [service]);

  // /learning → the first published Course (Q15). Its lessons then decide the lesson.
  useEffect(() => {
    if (!courses) return;
    if (!courseSlug && courses.length > 0) navigate(`/learning/${courses[0].slug}`, { replace: true });
  }, [courses, courseSlug, navigate]);

  // ── Lessons of the selected Course ────────────────────────────────────────────────
  useEffect(() => {
    if (!courseSlug) return;
    let active = true;
    setLessons(null);
    setError(null);
    service
      .getLessons(courseSlug)
      .then((found) => active && setLessons(found))
      .catch((failure: Error) => active && setError(failure.message));
    return () => {
      active = false;
    };
  }, [service, courseSlug]);

  // /learning/{courseSlug} → that Course's first lesson (Q15).
  useEffect(() => {
    if (lessons && lessons.length > 0 && !lessonSlug) {
      navigate(`/learning/${courseSlug}/${lessons[0].slug}`, { replace: true });
    }
  }, [lessons, lessonSlug, courseSlug, navigate]);

  // ── The Lesson Page itself ────────────────────────────────────────────────────────
  useEffect(() => {
    if (!courseSlug || !lessonSlug) return;
    let active = true;
    setPageHtml(null);
    setError(null);
    service
      .getLessonPageHtml(courseSlug, lessonSlug)
      .then((html) => active && setPageHtml(html))
      .catch((failure: Error) => active && setError(failure.message));
    return () => {
      active = false;
    };
  }, [service, courseSlug, lessonSlug]);

  const selectedCourse = courses?.find((course) => course.slug === courseSlug);
  const currentLesson = lessons?.find((lesson) => lesson.slug === lessonSlug);

  // ── One scroller: the frame grows to its own document at every width (Q25) ────────
  const frameRef = useRef<HTMLIFrameElement>(null);
  const [frameHeight, setFrameHeight] = useState(MIN_FRAME_HEIGHT);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame || !pageHtml) return;

    let observer: ResizeObserver | undefined;
    const measure = () => {
      const doc = frame.contentDocument;
      if (!doc?.documentElement) return;
      const height = Math.max(doc.documentElement.scrollHeight, doc.body?.scrollHeight ?? 0);
      if (height > 0) setFrameHeight(height);
    };

    const attached = () => {
      measure();
      // A quiz explanation expanding the document changes the height after load, which is exactly the
      // case a single load-time measurement would miss.
      if (frame.contentDocument && typeof ResizeObserver !== "undefined") {
        observer = new ResizeObserver(measure);
        observer.observe(frame.contentDocument.documentElement);
      }
    };

    frame.addEventListener("load", attached);
    window.addEventListener("resize", measure);
    return () => {
      frame.removeEventListener("load", attached);
      window.removeEventListener("resize", measure);
      observer?.disconnect();
    };
  }, [pageHtml]);

  if (error) {
    return <LearningError message={error} />;
  }

  if (!courses) {
    return <p className="font-mono text-sm text-slate-500">Reading the learning library…</p>;
  }

  if (courses.length === 0) {
    return (
      <div className="font-mono space-y-3">
        <LearningHeading title="Learning" kicker="Library // nothing published" />
        <p className="text-sm text-slate-600 max-w-2xl">
          No Course published yet. The Content Mount is mounted and readable, but its{" "}
          <code className="path">courses.json</code> lists no Courses — add one there and refresh; nothing
          has to be rebuilt or restarted.
        </p>
      </div>
    );
  }

  const lessonList = (
    <nav
      id="lesson-list"
      aria-label="Lessons"
      className={`${listOpen ? "block" : "hidden"} lg:block lg:sticky lg:top-4 border border-slate-200 rounded-sm bg-white p-3`}
    >
      <ol className="space-y-1">
        {(lessons ?? []).map((lesson, index) => {
          const current = lesson.slug === lessonSlug;
          return (
            <li key={lesson.slug}>
              <Link
                to={`/learning/${courseSlug}/${lesson.slug}`}
                aria-current={current ? "page" : undefined}
                onClick={() => setListOpen(false)}
                className={`flex gap-2 text-sm no-underline ${
                  current ? "text-zinc-950 font-bold" : "text-slate-600 hover:text-zinc-900"
                }`}
              >
                <span className="w-6 shrink-0 text-right text-slate-400">{String(index + 1).padStart(2, "0")}</span>
                <span className="min-w-0 break-words">{lesson.title}</span>
              </Link>
            </li>
          );
        })}
      </ol>
      {lessons && lessons.length === 0 && (
        <p className="text-xs text-slate-500">This Course has no lessons listed yet.</p>
      )}
    </nav>
  );

  return (
    <div className="space-y-4 font-mono">
      <LearningHeading
        title={selectedCourse?.title ?? "Learning"}
        kicker={`Learning // ${courses.length > 1 ? "Course" : "Library"}`}
      />

      {courses.length > 1 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Courses">
          {courses.map((course) => (
            <Link
              key={course.slug}
              to={`/learning/${course.slug}`}
              className={`text-xs px-3 py-1.5 rounded-sm no-underline border ${
                course.slug === courseSlug
                  ? "border-zinc-900 bg-zinc-900 text-white"
                  : "border-slate-300 text-slate-600 hover:text-zinc-900"
              }`}
            >
              {course.title}
            </Link>
          ))}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,20rem)_minmax(0,1fr)] items-start">
        {lessonList}

        <div className="space-y-2 min-w-0">
          {/* Narrow screens get one control that both picks a lesson and says where you are (Q24). */}
          <button
            type="button"
            aria-expanded={listOpen}
            aria-controls="lesson-list"
            onClick={() => setListOpen((open) => !open)}
            className="lg:hidden w-full flex items-center gap-2 text-xs text-left px-3 py-2 rounded-sm border border-slate-300 bg-white text-slate-700"
          >
            <ListChecks className="w-4 h-4 shrink-0 text-zinc-900" />
            <span>Lessons</span>
            {currentLesson && <span className="truncate text-slate-500">· {currentLesson.title}</span>}
          </button>

          {pageHtml ? (
            <iframe
              ref={frameRef}
              title={currentLesson?.title ?? lessonSlug ?? "Lesson Page"}
              srcDoc={pageHtml}
              className={FRAME_CHROME_CLASS}
              style={{ height: `${frameHeight}px` }}
            />
          ) : (
            <p className="text-sm text-slate-500">Opening the lesson…</p>
          )}
        </div>
      </div>
    </div>
  );
};

const LearningHeading: React.FC<{ title: string; kicker: string }> = ({ title, kicker }) => (
  <div className="border-b border-slate-200 pb-3">
    <div className="flex items-center gap-2 text-xs text-slate-500 uppercase tracking-widest">
      <GraduationCap className="w-4 h-4 text-zinc-900" />
      <span>{kicker}</span>
    </div>
    <h1 className="text-2xl font-bold uppercase tracking-tight text-zinc-900 mt-0.5">{title}</h1>
  </div>
);

/**
 * The loud shape of Q20: the library could not be read, and the message names the path so the fix is
 * visible from the page. Deliberately not the "nothing published yet" wording — a broken mount must not
 * read as an empty shelf.
 */
const LearningError: React.FC<{ message: string }> = ({ message }) => (
  <div role="alert" className="font-mono space-y-3">
    <LearningHeading title="Learning" kicker="Library // cannot read" />
    <div className="border border-orange-300 bg-orange-50 rounded-sm p-4 text-sm text-zinc-800 flex gap-3">
      <AlertTriangle className="w-5 h-5 shrink-0 text-orange-700" />
      <div className="space-y-1">
        <p className="font-bold">The learning library could not be read.</p>
        <p className="break-words">{message}</p>
        <p className="text-xs text-slate-600">
          Check that the Content Mount is bind-mounted into the api container and that its{" "}
          <code className="path">courses.json</code> names folders and files that are on disk.
        </p>
      </div>
    </div>
  </div>
);

export default LearningPage;
