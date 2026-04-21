using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Quoting;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;

namespace RefinedElement.Kentico.Sentinel.XbyK.Contact;

/// <summary>
/// Typed-HttpClient implementation — the DI container injects the pre-configured client (retry,
/// base address, user agent, etc. set in <c>SentinelServiceCollectionExtensions</c>). The service
/// layer's job is: resolve the endpoint from <see cref="SentinelOptions.ContactOptions.Endpoint"/>,
/// POST JSON, translate exceptions into a result object so callers never have to catch
/// <see cref="HttpRequestException"/> themselves.
/// </summary>
internal sealed class SentinelContactService(
    HttpClient httpClient,
    IOptions<SentinelOptions> options,
    ILogger<SentinelContactService> logger) : ISentinelContactService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SentinelOptions options = options.Value;

    public async Task<QuoteResult> SubmitAsync(QuoteSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var endpoint = ResolveEndpoint();
        logger.LogInformation(
            "Sentinel contact: submitting quote for {FindingsCount} findings to {Endpoint}.",
            submission.Findings.Count,
            endpoint);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(endpoint, submission, JsonOptions, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Sentinel contact: submission succeeded ({Status}).", statusCode);
                return new QuoteResult(true, statusCode, body, null);
            }

            // Non-2xx is still a completed request — surface the body so callers (admin UI) can
            // render "here's what the server said" rather than a generic "submission failed."
            var error = $"HTTP {statusCode} {response.ReasonPhrase}";
            logger.LogWarning("Sentinel contact: submission returned {Status}: {Reason}.", statusCode, response.ReasonPhrase);
            return new QuoteResult(false, statusCode, body, error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-initiated cancellation — don't log as an error. Let the caller decide.
            throw;
        }
        catch (TaskCanceledException)
        {
            // Distinct from user cancellation: HttpClient timed out (no token cancellation).
            logger.LogWarning("Sentinel contact: submission timed out against {Endpoint}.", endpoint);
            return new QuoteResult(false, 0, null, "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Sentinel contact: submission failed against {Endpoint}.", endpoint);
            return new QuoteResult(false, 0, null, ex.Message);
        }
    }

    private string ResolveEndpoint() =>
        !string.IsNullOrWhiteSpace(options.Contact.Endpoint)
            ? options.Contact.Endpoint
            : QuoteClient.DefaultEndpoint;
}
