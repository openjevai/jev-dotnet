namespace Jev;

/// <summary>Configuration for a <see cref="JevClient"/>.</summary>
public sealed class JevClientOptions
{
    /// <summary>
    /// Which backend to use. When null (default), the provider is auto-detected:
    /// <see cref="JevProvider.TypeSafe"/> if <c>TYPESAFE_API_KEY</c> is set,
    /// <see cref="JevProvider.OpenJEV"/> if only <c>OPENJEV_API_KEY</c> is set.
    /// Set explicitly, or via the <c>JEV_PROVIDER</c> environment variable (<c>openjev</c> / <c>typesafe</c>),
    /// to force a choice. TypeSafe remains the default; anyone with a TypeSafe key sees no change.
    /// </summary>
    public JevProvider? Provider { get; set; }

    /// <summary>API key. If null, the provider's key env var is used (<c>TYPESAFE_API_KEY</c> or <c>OPENJEV_API_KEY</c>).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Service base address; <c>/v1/systemone</c> is appended to its path (so a proxy prefix is kept).</summary>
    public Uri BaseUrl { get; set; } = new("https://api.typesafe.ai");

    /// <summary>Model id. <c>jev-latest</c> tracks the current recommended model; OpenJEV uses <c>openjev</c>.</summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>Per-request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum retries after a 429, 503 or 529 before giving up.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Transport override. Inject a stub <see cref="HttpMessageHandler"/> to test
    /// without a network; leave null for the default handler.
    /// </summary>
    public HttpMessageHandler? Handler { get; set; }
}
