import type { LearningCourse, LearningLesson } from "../types/learningTypes";

/**
 * Reads the published Courses, their Lesson Pages, and the pages' HTML through the API — the Content
 * Mount is never read by the browser directly.
 *
 * There is deliberately no token here. These are the only anonymous endpoints in the API (Q9), because
 * a lesson frame performs a plain browser navigation and sends no `Authorization` header; bolting auth
 * onto this client would mean passing a token in a query string or building a `blob:` URL, both of which
 * the design rejected. The consequence is accepted: published lesson text is public to anyone who can
 * reach the API, so nothing private belongs in a Course folder.
 */
export default class LearningService {
  private _baseUrl: string = process.env.REACT_APP_API_URL ?? "http://localhost:5206/api/";

  /** The published Courses in manifest order. An empty array means nothing is published yet — the calm
   * shape (Q20), shown as "no Course published yet" rather than as a failure. */
  public async getCourses(): Promise<LearningCourse[]> {
    return this.getJson<LearningCourse[]>(`learning/courses`);
  }

  /** The Lesson Pages of one Course in manifest order — an unlisted file never appears (Q17=C). */
  public async getLessons(courseSlug: string): Promise<LearningLesson[]> {
    return this.getJson<LearningLesson[]>(
      `learning/courses/${encodeURIComponent(courseSlug)}/lessons`
    );
  }

  /** One Lesson Page's HTML, returned as text so it can be handed to a frame untouched (Q10). */
  public async getLessonPageHtml(courseSlug: string, lessonSlug: string): Promise<string> {
    const response = await fetch(
      `${this._baseUrl}learning/courses/${encodeURIComponent(courseSlug)}/lessons/${encodeURIComponent(lessonSlug)}`
    );
    if (!response.ok) throw await this.readFailure(response);
    return await response.text();
  }

  private async getJson<T>(path: string): Promise<T> {
    const response = await fetch(`${this._baseUrl}${path}`);
    if (!response.ok) throw await this.readFailure(response);
    return (await response.json()) as T;
  }

  /**
   * Why the read failed, in the server's words. The API names the path it could not read (Q20), and that
   * text is what the page shows — "no lessons" and "the mount is broken" must not look alike, and a
   * forgotten `courses.json` line has to be findable from the message alone.
   */
  private async readFailure(response: Response): Promise<Error> {
    try {
      const body = await response.json();
      if (body && typeof body.error === "string") return new Error(body.error);
    } catch {
      // No JSON error body — fall through to the status line.
    }
    return new Error(`Could not read the learning library: ${response.status} ${response.statusText}`);
  }
}
