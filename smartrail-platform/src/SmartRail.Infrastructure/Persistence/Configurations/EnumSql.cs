namespace SmartRail.Infrastructure.Persistence.Configurations;

internal static class EnumSql
{
    /// <summary>Builds the "column IN ('A', 'B')" text of a CHECK constraint from the enum names, so the database and the C# enum cannot drift apart.</summary>
    public static string InList<TEnum>(string column) where TEnum : struct, Enum =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";
}
