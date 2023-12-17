using MyPlanner.Data.Entities.Todo;
using MyPlanner.Data.Repositories;
using MyPlanner.Data.Repositories.InMemory;

namespace MyPlanner.Data.UnitOfWork;

public class InMemoryUnitOfWork : IUnitOfWork
{
    public InMemoryUnitOfWork()
    {
        var (folders, lists, tasks) = PrepareTestData();
        Folders = new InMemoryRepository<TodoFolder>(folders);
        TaskLists = new InMemoryRepository<TodoList>(lists);
        Tasks = new InMemoryRepository<TodoTask>(tasks);
    }
    public IRepository<TodoFolder> Folders { get; set; }
    public IRepository<TodoList> TaskLists { get; set; }
    public IRepository<TodoTask> Tasks { get; set; }

    public void Save()
    {
        var folders = Folders.Get().ToList();
        var lists = TaskLists.Get().ToList();
        var tasks = Tasks.Get().ToList();
        foreach (TodoFolder folder in folders)
        {
            folder.Lists = lists.Where(x => x.FolderId == folder.Id).ToList();
        }
        foreach (TodoList list in lists)
        {
            list.Tasks = tasks.Where(x => x.ListId == list.Id).ToList();
        }
        Folders = new InMemoryRepository<TodoFolder>(folders);
        TaskLists = new InMemoryRepository<TodoList>(lists);
        Tasks = new InMemoryRepository<TodoTask>(tasks);
    }

    private (List<TodoFolder> folders, List<TodoList> lists, List<TodoTask> tasks) PrepareTestData()
    {
        var folders = new List<TodoFolder>();
        var lists = new List<TodoList>();
        var tasks = new List<TodoTask>();
        var listOutsideOfFolder = new TodoList()
        {
            Id = Guid.NewGuid(),
            Title = "ListWithoutFolder",
            Tasks = new List<TodoTask>()
                {
                    new TodoTask() { Id = Guid.NewGuid(), Title = "Task1" },
                    new TodoTask() { Id = Guid.NewGuid(), Title = "Task2" }
                }
        };
        lists.Add(listOutsideOfFolder);

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

        foreach (TodoFolder folder in folders.OfType<TodoFolder>())
        {
            lists.AddRange(folder.Lists);
            foreach (TodoList list in folder.Lists)
            {
                tasks.AddRange(list.Tasks);
                list.FolderId = folder.Id;
                list.Tasks.ForEach(x => x.ListId = list.Id);
            }
        }
        return (folders, lists, tasks);
    }

    public void Dispose()
    {
        Save();
    }
}
