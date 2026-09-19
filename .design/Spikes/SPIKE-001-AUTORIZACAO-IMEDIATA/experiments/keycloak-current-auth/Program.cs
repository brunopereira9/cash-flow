using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

const string BaseUrl = "http://localhost:18080";
const string Realm = "spike";
const string RootUser = "admin";
const string RootPassword = "admin";
const string Password = "spike123";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

static async Task<JsonDocument> Json(HttpResponseMessage response)
{
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    return JsonDocument.Parse(body);
}

static StringContent Body(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

static async Task Ensure(HttpResponseMessage response)
{
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {await response.Content.ReadAsStringAsync()}");
}

async Task<string> Token(string realm, string clientId, string? secret = null, string? username = null, string? password = null)
{
    var fields = new Dictionary<string, string> { ["client_id"] = clientId };
    if (secret is not null) fields["client_secret"] = secret;
    if (username is null) fields["grant_type"] = "client_credentials";
    else
    {
        fields["grant_type"] = "password";
        fields["username"] = username;
        fields["password"] = password!;
    }
    using var response = await http.PostAsync($"{BaseUrl}/realms/{realm}/protocol/openid-connect/token", new FormUrlEncodedContent(fields));
    using var json = await Json(response);
    return json.RootElement.GetProperty("access_token").GetString()!;
}

async Task<HttpResponseMessage> Admin(string token, HttpMethod method, string path, HttpContent? body = null)
{
    using var request = new HttpRequestMessage(method, $"{BaseUrl}/admin/realms/{Realm}{path}") { Content = body };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await http.SendAsync(request);
}

static string DecodeJwtPayload(string token)
{
    var part = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
    return Encoding.UTF8.GetString(Convert.FromBase64String(part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=')));
}

async Task<string> ClientId(string root, string clientId)
{
    using var response = await Admin(root, HttpMethod.Get, $"/clients?clientId={clientId}");
    using var json = await Json(response);
    return json.RootElement[0].GetProperty("id").GetString()!;
}

async Task<string> RoleId(string root, string clientUuid, string name)
{
    using var response = await Admin(root, HttpMethod.Get, $"/clients/{clientUuid}/roles/{name}");
    using var json = await Json(response);
    return json.RootElement.GetProperty("id").GetString()!;
}

async Task<string> RealmRoleId(string root, string name)
{
    using var response = await Admin(root, HttpMethod.Get, $"/roles/{name}");
    using var json = await Json(response);
    return json.RootElement.GetProperty("id").GetString()!;
}

async Task CreateClient(string root, string id, bool direct, bool service)
{
    using var response = await Admin(root, HttpMethod.Post, "/clients", Body(new
    {
        clientId = id,
        enabled = true,
        protocol = "openid-connect",
        publicClient = !service,
        directAccessGrantsEnabled = direct,
        serviceAccountsEnabled = service,
        standardFlowEnabled = false
    }));
    if (response.StatusCode != HttpStatusCode.Conflict) await Ensure(response);
}

async Task<string> CreateUser(string root, string username)
{
    using var response = await Admin(root, HttpMethod.Post, "/users", Body(new { username, firstName = "Spike", lastName = "User", email = "spike@example.test", emailVerified = true, enabled = true, credentials = new[] { new { type = "password", value = Password, temporary = false } } }));
    if (response.StatusCode != HttpStatusCode.Created) await Json(response);
    var location = response.Headers.Location!.ToString();
    return location[(location.LastIndexOf('/') + 1)..];
}

async Task SetRealmRoles(string root, string userId, params string[] roles)
{
    var representations = await Task.WhenAll(roles.Select(async name => new { id = await RealmRoleId(root, name), name }));
    using var response = await Admin(root, HttpMethod.Post, $"/users/{userId}/role-mappings/realm", Body(representations));
    await Ensure(response);
}

async Task<string> ServiceUserId(string root, string clientUuid)
{
    using var response = await Admin(root, HttpMethod.Get, $"/clients/{clientUuid}/service-account-user");
    using var json = await Json(response);
    return json.RootElement.GetProperty("id").GetString()!;
}

async Task<string> Secret(string root, string clientUuid)
{
    using var response = await Admin(root, HttpMethod.Get, $"/clients/{clientUuid}/client-secret");
    using var json = await Json(response);
    return json.RootElement.GetProperty("value").GetString()!;
}

async Task<(bool enabled, string[] roles)> ReadCurrent(string token, string userId)
{
    using var userResponse = await Admin(token, HttpMethod.Get, $"/users/{userId}");
    using var user = await Json(userResponse);
    using var rolesResponse = await Admin(token, HttpMethod.Get, $"/users/{userId}/role-mappings/realm/composite");
    using var roles = await Json(rolesResponse);
    return (user.RootElement.GetProperty("enabled").GetBoolean(), roles.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).Where(x => x is "admin" or "operator" or "auditor").Order().ToArray());
}

async Task AssertSurface(string surface, string token, string userId, bool enabled, string role)
{
    var current = await ReadCurrent(token, userId);
    if (current.enabled != enabled || current.roles.Length != 1 || current.roles[0] != role)
        throw new InvalidOperationException($"{surface}: state mismatch enabled={current.enabled}, roles={string.Join(',', current.roles)}");
    Console.WriteLine($"PASS {surface}: enabled={current.enabled}; role={current.roles[0]}");
}

var root = await Token("master", "admin-cli", username: RootUser, password: RootPassword);
using (var createRealm = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/admin/realms") { Content = Body(new { realm = Realm, enabled = true }) })
{
    createRealm.Headers.Authorization = new AuthenticationHeaderValue("Bearer", root);
    using var response = await http.SendAsync(createRealm);
    await Ensure(response);
}

root = await Token("master", "admin-cli", username: RootUser, password: RootPassword);
// The root admin token is valid across realm admin endpoints; create realm-local roles and clients.
foreach (var role in new[] { "admin", "operator", "auditor" })
{
    using var response = await Admin(root, HttpMethod.Post, "/roles", Body(new { name = role }));
    await Ensure(response);
}
await CreateClient(root, "spike-user", direct: true, service: false);
await CreateClient(root, "core-auth-reader", direct: false, service: true);
await CreateClient(root, "summary-auth-reader", direct: false, service: true);

var subject = await CreateUser(root, "spike-user");
await SetRealmRoles(root, subject, "operator");
var oldJwt = await Token(Realm, "spike-user", username: "spike-user", password: Password);
var oldPayload = DecodeJwtPayload(oldJwt);
if (!oldPayload.Contains("operator", StringComparison.Ordinal)) throw new InvalidOperationException("Old JWT lacks original operator role.");
Console.WriteLine("PASS old JWT issued with operator claim before changes.");

var readers = new Dictionary<string, string>();
foreach (var client in new[] { "core-auth-reader", "summary-auth-reader" })
{
    var clientUuid = await ClientId(root, client);
    var serviceUser = await ServiceUserId(root, clientUuid);
    // Start with exactly the two read privileges required by the tested endpoints.
    var management = await ClientId(root, "realm-management");
    var roleObjects = new[] { "view-users", "view-realm" }.Select(async name => new { id = await RoleId(root, management, name), name }).ToArray();
    using (var response = await Admin(root, HttpMethod.Post, $"/users/{serviceUser}/role-mappings/clients/{management}", Body(await Task.WhenAll(roleObjects)))) await Ensure(response);
    readers[client] = await Secret(root, clientUuid);
}

var coreReader = await Token(Realm, "core-auth-reader", readers["core-auth-reader"]);
var summaryReader = await Token(Realm, "summary-auth-reader", readers["summary-auth-reader"]);
await AssertSurface("Core logical surface / pre-change", coreReader, subject, true, "operator");
await AssertSurface("Summary logical surface / pre-change", summaryReader, subject, true, "operator");

using (var response = await Admin(root, HttpMethod.Put, $"/users/{subject}", Body(new { username = "spike-user", enabled = false }))) await Ensure(response);
await AssertSurface("Core logical surface / disabled with old JWT", coreReader, subject, false, "operator");
await AssertSurface("Summary logical surface / disabled with old JWT", summaryReader, subject, false, "operator");

using (var response = await Admin(root, HttpMethod.Put, $"/users/{subject}", Body(new { username = "spike-user", enabled = true }))) await Ensure(response);
var oldOperator = new[] { new { id = await RealmRoleId(root, "operator"), name = "operator" } };
using (var response = await Admin(root, HttpMethod.Delete, $"/users/{subject}/role-mappings/realm", Body(oldOperator))) await Ensure(response);
await SetRealmRoles(root, subject, "auditor");
await AssertSurface("Core logical surface / role changed with old JWT", coreReader, subject, true, "auditor");
await AssertSurface("Summary logical surface / role changed with old JWT", summaryReader, subject, true, "auditor");

var latencies = new List<double>();
var failures = 0;
for (var second = 0; second < 10; second++)
{
    var started = Stopwatch.StartNew();
    var calls = Enumerable.Range(0, 50).Select(async _ =>
    {
        var watch = Stopwatch.StartNew();
        try { await ReadCurrent(summaryReader, subject); lock (latencies) latencies.Add(watch.Elapsed.TotalMilliseconds); }
        catch { Interlocked.Increment(ref failures); }
    });
    await Task.WhenAll(calls);
    var remaining = TimeSpan.FromSeconds(1) - started.Elapsed;
    if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
}
var p95 = latencies.Order().ElementAt((int)Math.Ceiling(latencies.Count * .95) - 1);
Console.WriteLine($"MEASURE 50 logical requests/s for 10s: completed={latencies.Count}; failures={failures}; p95={p95:F1}ms; administrative reads={latencies.Count * 2}");
using (var docker = Process.Start(new ProcessStartInfo("docker", "stop keycloak-current-auth-spike") { RedirectStandardOutput = true }))
{
    await docker!.WaitForExitAsync();
    if (docker.ExitCode != 0) throw new InvalidOperationException("Could not stop the isolated Keycloak container.");
}
foreach (var (surface, reader) in new[] { ("Core logical surface", coreReader), ("Summary logical surface", summaryReader) })
{
    try
    {
        await ReadCurrent(reader, subject);
        throw new InvalidOperationException($"{surface}: verifier unexpectedly reached a stopped Keycloak.");
    }
    catch (HttpRequestException)
    {
        Console.WriteLine($"PASS {surface} / Keycloak stopped: current state unconfirmable; fail-closed decision=503 authorization_verification_unavailable.");
    }
}
