# TUF platform ZIP compatibility

The native download boundary adds `platform=windows`, `platform=macos`, or
`platform=linux` from the current runtime OS to public TUF mod download requests.
This applies to latest and version-pinned URLs used by catalog installs,
onboarding dependencies, and updates. Existing platform parameters are replaced
so stale links cannot select a different OS. Other query values are retained.

TUF chooses the requested platform ZIP, then a common ZIP fallback, and returns
404 if neither exists. The manager continues downloading through TUF instead of
choosing a GitHub asset itself. Existing generic releases work with the query.
Catalog JSON may contain the new release fields; serde accepts them and the
frontend keeps using TUF slug/version identity. No catalog response expansion or
additional per-mod lookup is necessary.

Direct archive URLs and unrelated hosts/endpoints are untouched. HTTPS checking,
ZIP signature/size validation, download cancellation, and owned temporary-file
handling remain in the same native boundary.

Validation: four download unit tests passed, covering all three platforms,
latest/pinned paths, duplicate/stale query values, unrelated URLs, HTTPS and ZIP
validation. The three existing tests requiring public downloads/native published
engine remain opt-in. This compatibility patch is left uncommitted with the
repository's ongoing desktop migration work.
