namespace MyPlanner.Service;

public class CreateSessionModel
{
    public CreateSessionModel(Guid taskId, DateTime start, DateTime end)
    {
        TaskId = taskId;
        Start = start;
        End = end;
    }
    public Guid TaskId { get; }
    public DateTime Start { get; }
    public DateTime End { get; }

}
