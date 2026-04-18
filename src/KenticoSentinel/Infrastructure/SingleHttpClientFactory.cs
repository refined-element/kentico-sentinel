namespace RefinedElement.Kentico.Sentinel.Infrastructure;

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> for a CLI process. One shared <see cref="HttpClient"/> for the
/// entire run — we don't need connection rotation or named clients for a single-invocation tool.
/// </summary>
internal sealed class SingleHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly HttpClient _client;

    public SingleHttpClientFactory()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("RefinedElement.Kentico.Sentinel/0.1.0");
    }

    public HttpClient CreateClient(string name) => _client;

    public void Dispose() => _client.Dispose();
}
