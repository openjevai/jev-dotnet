using System.Net.Http.Headers;
using Jev.Json;

namespace Jev;

/// <summary>
/// A client for TypeSafe AI's Jev "System One" model. Send a <c>state</c> plus a set
/// of typed <see cref="Question"/>s and get back typed answers with calibrated
/// confidence. Independent questions are evaluated in parallel, so batching them into
/// one call is close to free. Thread-safe; create one and reuse it.
/// </summary>
public sealed class JevClient : IDisposable
{
    private const string TypeSafeEnvKey = "TYPESAFE_API_KEY";
    private const string OpenJEVEnvKey = "OPENJEV_API_KEY";
    private const string ProviderEnvKey = "JEV_PROVIDER";
    private static readonly Uri TypeSafeBaseUrl = new("https://api.typesafe.ai");
    private const string TypeSafeModel = "jev-latest";
    private static readonly Uri OpenJEVBaseUrl = new("https://api.openjev.sh");
    private const string OpenJEVModel = "openjev";
    private static readonly HashSet<int> Retryable = [429, 503, 529];

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly Uri _endpoint;

    /// <summary>Create a client from explicit options.</summary>
    public JevClient(JevClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var provider = ResolveProvider(options);
        var baseUrl = options.BaseUrl;
        var model = options.Model;
        var envKey = provider == JevProvider.OpenJEV ? OpenJEVEnvKey : TypeSafeEnvKey;

        // Swap TypeSafe defaults to OpenJEV when the user hasn't customised them.
        if (provider == JevProvider.OpenJEV)
        {
            if (baseUrl == TypeSafeBaseUrl) baseUrl = OpenJEVBaseUrl;
            if (model == TypeSafeModel) model = OpenJEVModel;
        }

        var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new JevException($"No API key. Set {envKey} or JevClientOptions.ApiKey.");
        }

        _http = new HttpClient(options.Handler ?? new HttpClientHandler(), disposeHandler: true)
        {
            Timeout = options.Timeout,
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _maxRetries = Math.Max(0, options.MaxRetries);
        _endpoint = EndpointFor(baseUrl);
    }

    /// <summary>
    /// Resolve the provider: explicit <see cref="JevClientOptions.Provider"/> wins;
    /// then <c>JEV_PROVIDER</c> env; then TypeSafe if its key is set; then OpenJEV if
    /// its key is set; otherwise TypeSafe (the default, which will fail on a missing key).
    /// </summary>
    private static JevProvider ResolveProvider(JevClientOptions options)
    {
        if (options.Provider is { } explicit)
            return explicit;

        var env = Environment.GetEnvironmentVariable(ProviderEnvKey);
        if (string.Equals(env, "openjev", StringComparison.OrdinalIgnoreCase))
            return JevProvider.OpenJEV;
        if (string.Equals(env, "typesafe", StringComparison.OrdinalIgnoreCase))
            return JevProvider.TypeSafe;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TypeSafeEnvKey)))
            return JevProvider.TypeSafe;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OpenJEVEnvKey)))
            return JevProvider.OpenJEV;

        return JevProvider.TypeSafe;
    }

    // A leading-slash relative Uri would replace BaseUrl's path, dropping e.g. a proxy prefix.
    private static Uri EndpointFor(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return new Uri(baseUrl.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/v1/systemone");
    }

    /// <summary>Create a client with an explicit API key and otherwise-default options.</summary>
    public JevClient(string apiKey)
        : this(new JevClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Create a client that auto-detects its provider and key from the environment (TYPESAFE_API_KEY, OPENJEV_API_KEY, or JEV_PROVIDER).</summary>
    public static JevClient FromEnvironment() => new(new JevClientOptions());

    /// <summary>
    /// Evaluate <paramref name="questions"/> against <paramref name="state"/>. State may
    /// be a string, or any object/collection serializable to JSON (dictionaries give
    /// exact control over field names). Answers come back keyed by the same ids.
    /// </summary>
    public async Task<SystemOneResponse> SystemOneAsync(
        object state,
        IReadOnlyDictionary<string, Question> questions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0)
        {
            throw new ArgumentException("At least one question is required.", nameof(questions));
        }

        var body = RequestWriter.Serialize(state, _model, questions);
        var json = await SendWithRetriesAsync(body, cancellationToken).ConfigureAwait(false);
        return ResponseReader.Parse(json);
    }

    private async Task<string> SendWithRetriesAsync(byte[] body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await PostAsync(body, ct).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            if (Retryable.Contains(status) && attempt < _maxRetries)
            {
                await DelayBeforeRetryAsync(response, attempt, ct).ConfigureAwait(false);
                continue;
            }

            throw ErrorFor(status, payload);
        }
    }

    private async Task<HttpResponseMessage> PostAsync(byte[] body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await _http.SendAsync(request, ct).ConfigureAwait(false);
    }

    private static async Task DelayBeforeRetryAsync(HttpResponseMessage response, int attempt, CancellationToken ct)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var backoff = retryAfter ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        await Task.Delay(backoff + jitter, ct).ConfigureAwait(false);
    }

    private static JevException ErrorFor(int status, string body) => status switch
    {
        401 => new JevAuthException("Unauthorized: invalid or missing API key.", body),
        422 => new JevValidationException("Unprocessable entity: the request was rejected as malformed.", body),
        429 => new JevRateLimitException("Rate limit exceeded; retries exhausted.", body),
        503 => new JevServiceUnavailableException("Service unavailable; retries exhausted.", body),
        529 => new JevOverloadedException("Service overloaded; retries exhausted.", body),
        _ => new JevException($"Unexpected HTTP {status} from Jev.", status, body),
    };

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
