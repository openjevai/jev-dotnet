namespace Jev;

/// <summary>
/// Which Jev backend to use. <see cref="TypeSafe"/> is the default;
/// <see cref="OpenJEV"/> routes through the free community gateway at openjev.sh.
/// </summary>
public enum JevProvider
{
    /// <summary>TypeSafe direct (https://api.typesafe.ai, model jev-latest, key TYPESAFE_API_KEY).</summary>
    TypeSafe,

    /// <summary>OpenJEV community gateway (https://api.openjev.sh, model openjev, key OPENJEV_API_KEY).</summary>
    OpenJEV,
}
