using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Service.UnitTests;

public static class FolderFixture
{
    public static List<TodoFolder> GetTestFolders(){
        var folders = new List<TodoFolder>();
        for (int projectIndex = 0; projectIndex < 5; projectIndex++)
        {
            var listsLocal = new List<TodoList>();
            for (int taskListIndex = 0; taskListIndex < 5; taskListIndex++)
            {
                var tasksLocal = new List<TodoTask>();
                for (int taskIndex = 0; taskIndex < 10; taskIndex++)
                {
                    tasksLocal.Add(new TodoTask()
                    { Id = Guid.NewGuid(), Title = "Task" + taskIndex });
                }
                listsLocal.Add(new TodoList()
                { Id = Guid.NewGuid(), Title = "TaskList" + taskListIndex, Tasks = tasksLocal });
            }

            folders.Add(new TodoFolder()
            {
                Id = Guid.NewGuid(),
                Title = "Project" + projectIndex,
                Lists = listsLocal,
            });
        }
        return folders;
    }
}
