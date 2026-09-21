using System.Diagnostics;
using System.Text.RegularExpressions;
using RabbitMQ.Client;

namespace Summary.IntegrationTests;

[CollectionDefinition("RabbitMQ integration", DisableParallelization = true)]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
}

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private string? containerId;
    public string Uri { get; private set; } = "";

    public async Task InitializeAsync()
    {
        containerId = RunDocker(
                "run",
                "--detach",
                "--rm",
                "--publish",
                "0:5672",
                "--env",
                "RABBITMQ_DEFAULT_USER=integration",
                "--env",
                "RABBITMQ_DEFAULT_PASS=integration",
                "rabbitmq:3-management-alpine")
            .Trim();
        var portOutput = RunDocker("port", containerId, "5672/tcp");
        var port = Regex.Match(portOutput, @":(?<port>\d+)\s*$", RegexOptions.Multiline).Groups["port"].Value;
        if (string.IsNullOrWhiteSpace(port))
            throw new InvalidOperationException($"Could not resolve RabbitMQ host port: {portOutput}");
        Uri = $"amqp://integration:integration@localhost:{port}/";

        for (var attempt = 0; attempt < 120; attempt++)
        {
            try
            {
                var connectionFactory = new ConnectionFactory
                {
                    HostName = "localhost",
                    Port = int.Parse(port),
                    UserName = "integration",
                    Password = "integration",
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(1)
                };
                using var connection = connectionFactory.CreateConnection();
                return;
            }
            catch (Exception) when (attempt < 119)
            {
                await Task.Delay(250);
            }
        }
    }

    public Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(containerId)) RunDocker("rm", "--force", containerId);
        return Task.CompletedTask;
    }

    private static string RunDocker(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Docker could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"Docker command failed: {error}");
        return output;
    }
}
