## Why

A blank Foundry endpoint is treated today as a deployment mode rather than a mistake. A pod
released with its endpoints unset starts cleanly, passes liveness and readiness, reports nothing
unusual on `/health` — and answers `engine_unconfigured` for every PDF, DOCX, PPTX, HTML and image
it is handed. The fault is visible only inside a caller's response body, never in the deployment,
and `/health` does not even list a surface that was never configured.

That is precisely the failure the boot-time connectivity check already exists to prevent: when a
*configured* endpoint cannot be reached, the service refuses to start rather than serve traffic it
cannot fulfil. Absence and unreachability produce identical symptoms for a caller; only one of them
is currently treated as an error.

The reason the tolerant behaviour was chosen has expired. It existed to make the stub-first period
workable while EuGo-Web integrated against T2's stubs before the engines landed. All three engines
have shipped and EuGo-Web calls the real service, so what remains is a configuration mistake the
service is built to absorb silently.

## What Changes

- **BREAKING (operator-facing):** both `Foundry:DocumentIntelligenceEndpoint` and
  `Foundry:OpenAIEndpoint` become required. Absent, empty, or whitespace fails start-up the same
  way a retired key or a malformed URI already does — the host does not start, the process exits
  non-zero, and the message names the endpoint that is missing. On AKS this is a CrashLoopBackOff
  naming the fix instead of a healthy pod failing every document.
- **No opt-out flag, deliberately.** An `Enabled`-style escape hatch would preserve exactly the
  ambiguity this change removes: "the operator forgot" and "the operator meant it" would still be
  the same configuration. A deployment that genuinely cannot reach Azure keeps working the way it
  does now — supply the endpoint values, which are hostnames rather than secrets, and turn off the
  dialling with `DocInt:StartupProbe:Enabled=false`. Requiring the value and probing it stay
  separate decisions.
- **`Foundry:DeploymentNameVision` becomes unconditionally required**, as a consequence rather than
  a new rule: it is already required whenever the image-description endpoint is set, and that
  endpoint is now always set.
- **The `engine_unconfigured` per-file error becomes unreachable through configuration.** No
  running deployment can emit it, because no running deployment lacks an endpoint.
- **The chart stops shipping a blank endpoint as a working default.** Rendering a release without
  both endpoints must fail at install time rather than produce a manifest whose pod crash-loops —
  an earlier and more legible signal than the pod's own refusal.
- **XLSX extraction is unaffected in every other respect.** It calls no Azure surface and continues
  to serve normally once the service is up; what changes is that the service will not come up at
  all with endpoints missing, spreadsheet-only traffic included.

**Frozen `/v1` contract:** the wire shape does not change and **EuGo-Web needs no change**. No
request field, response field, status code, or error-code string is added or removed;
`engine_unconfigured` stays a defined member of the per-file error vocabulary and simply stops
being reachable. Removing the string would be a contract change for no benefit, so it stays.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `foundry-configuration`: the "One endpoint per API surface" requirement loses its
  "Each endpoint SHALL be independently optional" clause and gains the opposite obligation — every
  endpoint is required, and a missing one refuses start-up. Its "One endpoint left blank" and "No
  endpoint configured" scenarios describe behaviour that will no longer exist and are replaced. The
  vision-alias requirement's "Alias unused while the surface is disabled" scenario becomes
  unreachable for the same reason.

## Impact

**Behaviour at boot** — `Configuration/DocIntOptions.cs` (`FoundryOptions` validation, which already
owns the absolute-URI and retired-key rules, so this is a rule added where the others live) and
`Startup/StartupConnectivityCheck.cs`, whose per-endpoint registration is currently conditional on
the endpoint being set and therefore becomes unconditional.

**Code that can no longer run** — the `IsConfigured` guards in `Engines/LayoutEngine.cs` and
`Engines/VisionEngine.cs`, and the `EngineUnconfiguredException` throws in
`Engines/AzureLayoutAnalysisClient.cs` and `Engines/AzureVisionChatClient.cs`. Whether these are
removed or kept as an assertion of an invariant enforced elsewhere is a design decision, not a
scope question: either way, no configuration can reach them.

**Tests** — the widest surface. `tests/DocInt.Tests/DocIntAppFactory.cs` deliberately blanks both
endpoints, and every test that boots the real `Program` inherits that: the contract, telemetry,
admission, health, metrics and redaction suites would all fail to start. The factory must instead
supply endpoints with dialling disabled. Three tests assert the unconfigured path directly
(`LayoutEngineTests`, `VisionEngineTests`, `ExtractContractTests`); they exercise it through a fake
client's `IsConfigured`, so what happens to them follows the design decision above.
`OptionsTests` gains the new refusal cases.

**Chart and CI** — `charts/eugo-docint/values.yaml` ships `""` for both endpoints, documented as a
designed degraded mode. Both CI workflows render the chart from minimal values with no endpoints
set (`ci.yml`'s `chart-lint`, including the writable-`/tmp` regression gate, and `release.yml`'s
packaged-chart validation), so making the values required breaks four render commands that must be
updated in the same change. No pod security context or volume mount is touched.

**Documentation** — `README.md`'s configuration reference and `CLAUDE.md`'s live-smoke instructions
describe blank endpoints as supported.

**Handoff outside this repository** — EuGo-infra owns release execution and must have both endpoint
values in its release values before the chart version carrying this change is deployed. A release
that omits them fails at install rather than degrading silently, which is the point, but it fails.
