namespace ArchitectureTests;

public class CurrentAuthorizationContractTests
{
    [Fact]
    public void EnforcesCurrentKeycloakStateInBothApis()
    {
        var compose = File.ReadAllText(Path.Combine(Root(), "infra", "compose", "compose.yaml"));
        Assert.Contains("Keycloak__Enabled: \"true\"", compose);
        Assert.Contains("Keycloak__CurrentStateTimeoutSeconds: \"2\"", compose);
        Assert.Contains("Keycloak__AdminClientSecret", compose);
        foreach (var api in new[]
                 {
                     Path.Combine(Root(), "backend", "Core", "src", "Api", "Infrastructure",
                         "Identity", "CurrentKeycloakAuthorization.cs"),
                     Path.Combine(Root(), "backend", "Summary", "src", "Api", "Infrastructure",
                         "Identity", "CurrentKeycloakAuthorization.cs")
                 })
        {
            var source = File.ReadAllText(api);
            Assert.Contains("Status503ServiceUnavailable", source);
            Assert.Contains("!state.Enabled", source);
            Assert.Contains("Status403Forbidden", source);
            Assert.Contains("role is \"admin\" or \"operator\"", source);
            Assert.DoesNotContain("role is \"admin\" or \"operator\" or \"auditor\"", source);
        }

        var sharedClient = File.ReadAllText(Path.Combine(Root(), "backend", "BuildingBlocks", "Identity",
            "KeycloakStateClient.cs"));
        Assert.Contains("/admin/realms/", sharedClient);
        Assert.Contains("role-mappings/realm", sharedClient);
    }

    static string Root() =>
        Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
}

public class LocalEnvironmentContractTests
{
    [Fact]
    public void DeclaresAllRequiredComposeServicesAndCommands()
    {
        var compose = File.ReadAllText(Path.Combine(Root(), "infra", "compose", "compose.yaml"));
        foreach (var service in new[]
                     { "frontend:", "core:", "summary:", "postgres:", "rabbitmq:", "keycloak:", "collector:" })
            Assert.Contains(service, compose);
        var readme = File.ReadAllText(Path.Combine(Root(), "infra", "README.md"));
        Assert.Contains("docker compose -f infra/compose/compose.yaml up --build -d", readme);
        Assert.Contains("down -v", readme);
        Assert.Contains("001-init.sql", readme);
        Assert.Contains("cashflow-realm.json", readme);
        var realm = File.ReadAllText(Path.Combine(Root(), "infra", "keycloak", "cashflow-realm.json"));
        Assert.Contains("cashflow-api", realm);
        Assert.Contains("serviceAccountsEnabled", realm);
        Assert.Contains("query-users", realm);
    }

    static string Root() =>
        Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
}

public class IdentityAccessContractTests
{
    [Fact]
    public void UsesKeycloakAdminApiForUserLifecycleAndRoleAssignment()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "backend", "Core", "src", "Api", "Infrastructure",
            "Identity", "KeycloakAdminClient.cs"));
        var controller = File.ReadAllText(Path.Combine(Root(), "backend", "Core", "src", "Api", "Controllers",
            "IdentityController.cs"));
        Assert.Contains("/admin/realms/", source);
        Assert.Contains("role-mappings/realm", source);
        Assert.Contains("client_credentials", source);
        Assert.Contains("/identity/users", controller);
        Assert.Contains("state.Roles.Contains(\"admin\")", controller);
        Assert.Contains("last_active_admin", controller);
        Assert.Contains("LastActiveAdminException", source);
        Assert.DoesNotContain("keycloak_db", source, StringComparison.OrdinalIgnoreCase);
    }

    static string Root() =>
        Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
}

public class BoundedContextDependencyTests
{
    [Fact]
    public void KeepsLedgerAndSummaryModelsAndPortsIndependent()
    {
        var root = Root();
        var ledger = Directory
            .GetFiles(Path.Combine(root, "backend", "Core", "src"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToArray();
        var summary = Directory
            .GetFiles(Path.Combine(root, "backend", "Summary", "src"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToArray();
        Assert.Contains(ledger, source => source.Contains("class CoreDbContext", StringComparison.Ordinal));
        Assert.Contains(summary, source => source.Contains("class SummaryDbContext", StringComparison.Ordinal));
        Assert.Contains(summary, source => source.Contains("ProjectionEvent", StringComparison.Ordinal));
        Assert.DoesNotContain(summary, source => source.Contains("class LedgerEntry", StringComparison.Ordinal));
        Assert.DoesNotContain(ledger, source => source.Contains("class DailySummary", StringComparison.Ordinal));
        Assert.Contains("LedgerEntryCreated.v1",
            File.ReadAllText(Path.Combine(root, "contracts", "events", "ledger-entry.v1.json")));
    }

    static string Root() =>
        Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
}
