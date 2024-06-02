using System.ComponentModel.DataAnnotations;

namespace MyPlanner.Data.Entities.Todo;

public class TodoTaskSession : EntityBase
{
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

    [Required]
    public Guid TodoTaskId { get; set; } = Guid.Empty;
}

