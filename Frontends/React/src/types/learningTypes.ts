/** One published Course: a learning sequence with a display title and a URL slug, both declared in the
 * Course Manifest rather than taken from the synced folder name (which contains spaces and capitals). */
export interface LearningCourse {
  slug: string;
  title: string;
}

/** One published Lesson Page. `slug` is its file name without `.html`, which is what appears in the URL;
 * reading order is the manifest array order, so the position in the list is the lesson number (Q21=A). */
export interface LearningLesson {
  slug: string;
  title: string;
}
