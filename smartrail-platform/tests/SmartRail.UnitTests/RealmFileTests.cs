using System.Text.Json;
using SmartRail.Api.Authentication;
using SmartRail.Infrastructure.Seeding;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>
/// The Keycloak realm file is plain JSON, so its important promises are checked without a Keycloak container
/// (spec section 10.2). What a real token contains is checked by scripts/smoke-auth.sh against the running stack.
/// </summary>
public class RealmFileTests
{
    private static readonly JsonElement Realm = Load();

    private static JsonElement Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "keycloak", "realm-smartrail.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    private static IEnumerable<JsonElement> Clients => Realm.GetProperty("clients").EnumerateArray();

    private static JsonElement Client(string clientId) =>
        Clients.Single(client => client.GetProperty("clientId").GetString() == clientId);

    [Fact]
    public void The_realm_is_called_smartrail()
    {
        Assert.Equal("smartrail", Realm.GetProperty("realm").GetString());
    }

    [Fact]
    public void The_demo_user_ids_equal_the_ids_the_seed_data_uses()
    {
        // The seeded operator assignment and the Keycloak user must be the same person.
        var users = Realm.GetProperty("users").EnumerateArray()
            .Where(user => user.TryGetProperty("id", out _))
            .ToDictionary(user => user.GetProperty("username").GetString()!, user => Guid.Parse(user.GetProperty("id").GetString()!));

        Assert.Equal(DemoIds.Nimal, users["nimal"]);
        Assert.Equal(DemoIds.UnassignedOperator, users["operator2"]);
        Assert.Equal(DemoIds.Controller, users["jayasinghe"]);
    }

    [Fact]
    public void The_realm_has_the_five_fixed_roles_and_the_api_knows_the_same_names()
    {
        var roles = Realm.GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString()!).Order().ToArray();

        Assert.Equal(["caterer", "controller", "operator", "passenger", "system-admin"], roles);
        Assert.Equal(roles, new[] { Roles.Caterer, Roles.Controller, Roles.Operator, Roles.Passenger, Roles.SystemAdmin }.Order().ToArray());
    }

    [Fact]
    public void Each_demo_user_has_exactly_the_role_the_demo_needs()
    {
        var roles = Realm.GetProperty("users").EnumerateArray().ToDictionary(
            user => user.GetProperty("username").GetString()!,
            user => user.GetProperty("realmRoles").EnumerateArray().Select(role => role.GetString()!).ToArray());

        Assert.Equal(["operator"], roles["nimal"]);
        Assert.Equal(["operator"], roles["operator2"]);
        Assert.Equal(["controller"], roles["jayasinghe"]);
        Assert.Equal(["caterer"], roles["service-account-smartrail-caterer"]);
    }

    [Fact]
    public void Token_lifetimes_follow_NFR_11()
    {
        Assert.Equal(900, Realm.GetProperty("accessTokenLifespan").GetInt32()); // 15 minutes
        Assert.Equal(28_800, Realm.GetProperty("ssoSessionMaxLifespan").GetInt32()); // 8 hours
    }

    [Fact]
    public void The_expected_clients_exist()
    {
        var ids = Clients.Select(client => client.GetProperty("clientId").GetString()!).Order().ToArray();

        Assert.Equal(
            ["smartrail-api", "smartrail-caterer", "smartrail-dev-token", "smartrail-operator-app", "smartrail-ops-web"],
            ids);
    }

    [Theory]
    [InlineData("smartrail-operator-app")]
    [InlineData("smartrail-ops-web")]
    public void The_app_clients_use_the_code_flow_with_pkce_and_no_secret(string clientId)
    {
        var client = Client(clientId);

        Assert.True(client.GetProperty("publicClient").GetBoolean());
        Assert.True(client.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(client.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.False(client.TryGetProperty("secret", out _));
        Assert.Equal("S256", client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
    }

    [Fact]
    public void Only_the_caterer_client_is_confidential_and_it_uses_client_credentials_only()
    {
        var caterer = Client("smartrail-caterer");

        Assert.False(caterer.GetProperty("publicClient").GetBoolean());
        Assert.True(caterer.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.False(caterer.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(caterer.GetProperty("directAccessGrantsEnabled").GetBoolean());
    }

    [Theory]
    [InlineData("smartrail-operator-app")]
    [InlineData("smartrail-ops-web")]
    [InlineData("smartrail-caterer")]
    [InlineData("smartrail-dev-token")]
    public void Tokens_carry_the_api_audience_and_a_flat_roles_claim(string clientId)
    {
        var mappers = Client(clientId).GetProperty("protocolMappers").EnumerateArray().ToList();

        var audience = mappers.Single(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper");
        Assert.Equal("smartrail-api", audience.GetProperty("config").GetProperty("included.client.audience").GetString());

        var roles = mappers.Single(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-usermodel-realm-role-mapper");
        Assert.Equal(Roles.ClaimType, roles.GetProperty("config").GetProperty("claim.name").GetString());
        Assert.Equal("true", roles.GetProperty("config").GetProperty("access.token.claim").GetString());
    }

    [Fact]
    public void The_dev_token_client_is_in_the_compose_realm_and_in_no_other_deployment_file()
    {
        // It enables the password grant, so it must never reach test, staging or prod.
        Assert.True(Client("smartrail-dev-token").GetProperty("directAccessGrantsEnabled").GetBoolean());

        var deploy = Path.Combine(FindPlatformRoot(), "deploy");
        var offenders = Directory.EnumerateFiles(deploy, "*", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("smartrail-dev-token", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    private static string FindPlatformRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartRail.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("SmartRail.sln was not found above the test output folder.");
    }
}
