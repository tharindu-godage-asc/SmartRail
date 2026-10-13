namespace SmartRail.Domain.Reference;

public sealed class Language
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
}
