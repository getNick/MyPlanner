namespace MyPlanner.API;

public static class ApiEndpoints
{
    private const string ApiBase = "api";


    public static class Learning
    {
        private const string LearningBase = $"{ApiBase}/learning";

        /// <summary>The published Courses — the Course Manifest read from the Content Mount.</summary>
        public const string Courses = $"{LearningBase}/courses";

        /// <summary>The Lesson Pages of one published Course, in manifest order.</summary>
        public const string Lessons = $"{Courses}/{{courseSlug}}/lessons";

        /// <summary>One Lesson Page's HTML — the item under the lesson collection, served as
        /// <c>text/html</c>. One word for one concept: this is the same object the list above names.
        /// The only endpoint in the API without [Authorize] (Q9).</summary>
        public const string LessonPage = $"{Lessons}/{{lessonSlug}}";
    }

    public static class Todo
    {
        private const string TodoBase = $"{ApiBase}/todo";
        private const string TasksBase = $"{TodoBase}/tasks";
        public static class Tasks
        {
            private const string Base = TasksBase;
            public const string Create = Base;
            public const string Get = $"{Base}/{{id:guid}}";
            public const string Update = Base;
            public const string Delete = $"{Base}/{{id:guid}}";
        }

        public static class TaskSessions
        {
            private const string Base = $"{TasksBase}/{{taskId:guid}}/sessions";
            public const string GetSessions = Base;
            public const string CreateSession = Base;
            public const string StartSession = $"{Base}/start";
            public const string StopSession = $"{Base}/stop";
            public const string DeleteSession = $"{Base}/{{sessionId:guid}}";
            public const string UpdateSession = $"{Base}/{{sessionId:guid}}";
        }

    }
}
