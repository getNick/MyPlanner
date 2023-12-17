using System.ComponentModel.DataAnnotations;

namespace MyPlanner.Data.Entities;

public class EntityBase
{
    [Key]
    public Guid Id { get; set; } = Guid.Empty;
}
