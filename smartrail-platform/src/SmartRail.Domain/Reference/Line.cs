namespace SmartRail.Domain.Reference;

public sealed class Line
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? OperationsPhone { get; set; }
}

public sealed class LineTranslation
{
    public Guid LineId { get; set; }
    public required string LanguageCode { get; set; }
    public required string Name { get; set; }
}
