using System.IO.Pipes;

namespace Ticketing.Tests.Orders.TestSupport;

/// <summary>
/// Probes for a local Docker engine so Testcontainers tests can skip cleanly when
/// Docker is unavailable (e.g. this dev machine). Override with RUN_DOCKER_TESTS=1.
/// </summary>
public static class DockerProbe
{
    private static readonly Lazy<bool> Available = new(() =>
    {
        if (Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS") == "1")
            return true;

        try
        {
            using var pipe = new NamedPipeClientStream(".", "docker_engine", PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(1000);
            return pipe.IsConnected;
        }
        catch
        {
            return false;
        }
    });

    public static bool IsAvailable() => Available.Value;
}

/// <summary>xunit Fact that skips at discovery time when Docker is not reachable.</summary>
public sealed class DockerRequiredFactAttribute : Xunit.FactAttribute
{
    public DockerRequiredFactAttribute()
    {
        if (!DockerProbe.IsAvailable())
            Skip = "Docker is not available in this environment; Testcontainers tests are skipped.";
    }
}
