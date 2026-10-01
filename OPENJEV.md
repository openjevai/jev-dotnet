# OpenJEV Support

This fork adds optional [OpenJEV](https://openjev.sh) support alongside the original
TypeSafe backend. Jev is built by [TypeSafe](https://typesafe.ai); OpenJEV is a free
community gateway to the same Jev model. TypeSafe remains the default — anyone with a
TypeSafe key sees zero behaviour change.

## What was added

| File | Change |
| --- | --- |
| `src/Jev.Client/JevProvider.cs` | New `JevProvider` enum (`TypeSafe`, `OpenJEV`). |
| `src/Jev.Client/JevClientOptions.cs` | New `Provider` property (nullable, auto-detect by default). |
| `src/Jev.Client/JevClient.cs` | Provider resolution logic; swaps endpoint/model/key defaults when OpenJEV is selected. Added HTTP 503 to retryable statuses and error mapping. |
| `src/Jev.Client/JevException.cs` | New `JevServiceUnavailableException` for HTTP 503. |
| `README.md` | OpenJEV note after intro; 503 in errors table; `Provider` in configuration example. |

No TypeSafe code paths were renamed, removed, or re-defaulted.

## Provider selection rule

1. **Explicit choice wins** — set `JevClientOptions.Provider` or the `JEV_PROVIDER`
   environment variable (`openjev` / `typesafe`).
2. **TypeSafe if its key is set** — `TYPESAFE_API_KEY` in the environment (unchanged
   default).
3. **OpenJEV if only its key is set** — `OPENJEV_API_KEY` in the environment.

When OpenJEV is selected (and the user hasn't customised `BaseUrl`/`Model`), the
defaults swap automatically:

| | TypeSafe (default) | OpenJEV |
| --- | --- | --- |
| Endpoint | `https://api.typesafe.ai/v1/systemone` | `https://api.openjev.sh/v1/systemone` |
| Model | `jev-latest` | `openjev` |
| Key env | `TYPESAFE_API_KEY` | `OPENJEV_API_KEY` |
| Overload status | 529 | 503 |

## How to configure

```csharp
// Option A — auto-detect (recommended): set only OPENJEV_API_KEY in the environment
using var client = JevClient.FromEnvironment();

// Option B — force OpenJEV explicitly
using var client = new JevClient(new JevClientOptions
{
    Provider = JevProvider.OpenJEV,  // or set JEV_PROVIDER=openjev
});
```

Or set `JEV_PROVIDER=openjev` in the environment to force OpenJEV regardless of which
keys are present.

## How it was verified

- **Live API check**: a `POST` to `https://api.openjev.sh/v1/systemone` with model
  `openjev`, state `ping`, and one noul question returned HTTP 200.
- **Grep**: no hardcoded `api.typesafe.ai` default was introduced or removed; TypeSafe
  defaults remain untouched in source.
- No repository code was executed (no build, test, or install).

## Upstream

Original project: https://github.com/CMaintz/jev-dotnet by @CMaintz (MIT license).
