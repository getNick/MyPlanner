namespace MyPlanner.API;

public static class ApiEndpoints
{
    private const string ApiBase = "api";


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
