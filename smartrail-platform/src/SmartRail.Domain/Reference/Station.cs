namespace SmartRail.Domain.Reference;

public sealed class Station
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ContactPhone { get; set; }
    public bool HasCateringFacility { get; set; }
}

public sealed class StationTranslation
{
    public Guid StationId { get; set; }
    public required string LanguageCode { get; set; }
    public required string Name { get; set; }
}

public sealed class Platform
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public required string Code { get; set; }
}
