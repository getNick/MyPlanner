namespace MyPlanner.Service;

public class UpdateSessionModel
{
    public Guid Id { get; set; }
    public DateTime? Start { get; }
    public DateTime? End { get; }
}
