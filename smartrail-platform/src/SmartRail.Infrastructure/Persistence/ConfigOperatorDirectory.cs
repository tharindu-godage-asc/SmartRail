using Microsoft.Extensions.Options;
using SmartRail.Application.Features.Assignments;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>The operator accounts the directory knows, written in configuration until the Keycloak adapter exists (spec 17 A3, designed not built).</summary>
public sealed class OperatorDirectoryOptions
{
    public const string Section = "Operators";

    /// <summary>The demo realm's two operators (docker/keycloak/realm-smartrail.json). The ids are the Keycloak subjects.</summary>
    public List<OperatorEntry> Directory { get; set; } =
    [
        new() { UserId = "d0d0d0d0-0000-4000-8000-000000000001", Name = "Nimal Perera" },
        new() { UserId = "d0d0d0d0-0000-4000-8000-000000000002", Name = "Second Operator" },
    ];
}

public sealed class OperatorEntry
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsOperator { get; set; } = true;
}

/// <summary>A port implementation backed by configuration: the same interface the Keycloak adapter will implement.</summary>
public sealed class ConfigOperatorDirectory(IOptions<OperatorDirectoryOptions> options) : IOperatorDirectory
{
    private IEnumerable<DirectoryOperator> All => options.Value.Directory.Select(o => new DirectoryOperator(o.UserId, o.Name, o.Enabled, o.IsOperator));

    public Task<IReadOnlyList<DirectoryOperator>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DirectoryOperator>>(All.ToList());

    public Task<DirectoryOperator?> FindAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(All.FirstOrDefault(o => o.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)));
}
