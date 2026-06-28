using System.Collections.Generic;

namespace MyPlanner.Service.Models;

public enum LlmSchemaType
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Integer
}

public class LlmSchema
{
    public LlmSchemaType Type { get; set; }
    public Dictionary<string, LlmSchema>? Properties { get; set; }
    public LlmSchema? Items { get; set; }
    public List<string>? Required { get; set; }
    public string? Description { get; set; }
    public bool Nullable { get; set; } = false;
}
