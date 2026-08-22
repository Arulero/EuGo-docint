## Context

See `proposal.md` — *Why*. The constraints that shape the approach:

- `FoundryOptions` already owns three boot-time refusals — the absolute-URI rule, the
  alias-required-when-endpoint-set rule, and the retired-key rejection — all through
  `ValidateOnStart`, all surfacing as `OptionsValidationException`, which `Program`'s top-level
  handler turns into a fatal log and exit code 1. This change adds a fourth rule of the same shape,
  not a new mechanism.
- **Hosted services, and through them every `IStartupProbe`, are resolved before `ValidateOnStart`
  runs.** `DocumentIntelligenceStartupProbe`'s constructor documents this and is written around it:
  it stores `IOptions<FoundryOptions>` without touching `.Value` and defers client construction
  behind a `Lazy<T>`, because an eager read raises the failure mid-resolution, pre-empts the
  validator's clean message, and leaves the host with no hosted-service list to dispose. Any change
  to probe registration has to preserve that deferral.
- The boot-time check (`StartupProbe`) and the periodic one (`DependencyCheck`) are already
  deliberately independent of each other, with opposite consequences — fatal at boot, informational
  afterwards. This change adds a third independent axis: whether a value must be *supplied*, which
  is separate from whether it is *dialled*.
- The chart currently emits each endpoint through `with`, which omits the variable when the value is
  empty. Two CI workflows render the chart from a minimal value set that supplies no endpoints.

## Goals / Non-Goals

**Goals:**

- One enforcement point for "an endpoint must be supplied", firing whether or not the service dials
  anything.
- A missing endpoint fails as early as it can be detected: at chart render for a release, at process
  start for a pod.
- Leave the developer escape hatch intact — supply endpoints, skip the dial — without it doubling as
  a way to skip supplying them.

**Non-Goals:**

- Changing what happens when a *configured* endpoint is unreachable, at boot or afterwards. Both
  behaviours are already specified and correct.
- Changing the `/v1` wire contract in any respect, including withdrawing the `engine_unconfigured`
  code string.
- Reworking how credentials are chosen. `FoundryCredential` and the key/managed-identity decision
  are untouched.

## Decisions

### D1 — The rule lives in `FoundryOptions` validation, not in the connectivity check

Add the presence rule to the existing `AddOptions<FoundryOptions>()` chain in
`Configuration/DocIntOptions.cs`, beside the absolute-URI rules it strengthens.

Two properties follow from placing it there and from nowhere else. It fires when
`DocInt:StartupProbe:Enabled=false`, which is what keeps "must be supplied" independent of "must be
dialled" — the developer escape hatch skips the dial and still demands the values. And it produces
the same exception type, at the same moment, with the same fatal-log-and-exit-1 handling as every
other configuration refusal the service already has, so there is one story for "the configuration is
wrong" rather than two.

*Alternative considered:* enforce inside `AddStartupConnectivityCheck`, which already reads both
endpoint keys straight from configuration. Rejected — that method's entire shape is *one configured
endpoint, one probe, one check*; making it also the arbiter of what must be configured couples
presence to probing, and it would be bypassed exactly when the check is turned off.

### D2 — Probe and dependency-check registration become unconditional

With presence guaranteed, the `IsSet` guards in `AddStartupConnectivityCheck` can only ever be true.
Remove them: two probes and two dependency health checks are registered always.

This is safe **because** the probes already defer every option read behind `Lazy<T>` (see *Context*).
Removing the guard does not expose the resolution-order hazard — it leaves the existing deferral as
the only thing standing between resolution and validation, which is what it was already for. The
`Endpoint` property's comment, "registered only when non-blank", stops being true and must be
rewritten to name validation as the guarantee.

One consequence is an improvement worth naming: `/health` now always reports both dependency
surfaces. Previously an unconfigured surface was not registered as a health check at all, so it
appeared nowhere in the report — the absence was invisible from every direction, which is part of
what made it survive.

### D3 — The `IsConfigured` seam is removed rather than kept as a defence

Remove `IsConfigured` from `ILayoutAnalysisClient` and `IVisionChatClient`, the guards at the top of
`LayoutEngine.ExtractAsync` and `VisionEngine.ExtractAsync`, the `EngineUnconfiguredException`
throws in both Azure clients, the exception type in `Engines/Errors.cs`, and its `catch` clause in
`EngineRouter`. `ErrorCodes.EngineUnconfigured` stays — that is contract vocabulary, and D-scope
here is code, not the wire.

The reasoning is that an interface member that can only return one value misrepresents the seam it
sits on, and every fake in the test suite has to implement it to say so. Defensive code for a state
the system cannot enter is code no test can reach honestly, and it will drift. The whole point of
this change is that the invariant has exactly one enforcement point; leaving a second, weaker one
downstream re-creates the ambiguity in the code that the configuration rule just removed.

*Alternative considered:* keep the guards as an assertion of an invariant enforced elsewhere.
Rejected for the above — and if optionality is ever reintroduced, that is a spec change, and
rebuilding the path is properly part of it rather than a debt carried in advance.

*Consequence, stated plainly:* three existing tests are deleted —
`LayoutEngineTests.Unconfigured_client_yields_engine_unconfigured`,
`VisionEngineTests.Unconfigured_client_yields_engine_unconfigured`, and
`ExtractContractTests.Unconfigured_layout_engine_yields_per_file_engine_unconfigured`. They are
replaced by boot-refusal tests in `OptionsTests`. This is not a loss of coverage: the guarantee moves
from *the unconfigured case is handled* to *the unconfigured case cannot occur*, and the second is
the stronger claim.

### D4 — Test hosts supply endpoints that are unreachable by construction

`DocIntAppFactory` currently blanks both endpoints, and every suite that boots the real `Program`
inherits that. Invert it: default both to reserved-invalid absolute URIs — `.invalid` is reserved by
RFC 2606 and can never resolve — rather than to plausible-looking hostnames.

That choice is deliberate and preserves what the factory's existing comment is about. The comment
records a real incident: `WebApplicationFactory` boots in Development and reads an untracked
`appsettings.Development.json` that on a developer machine holds the real endpoints, silently
turning hermetic tests into live-Azure tests. A `.invalid` host cannot resolve on any machine, so if
a test ever enables a probe by mistake it fails loudly on DNS instead of quietly reaching Azure.
`StartupProbe:Enabled=false` and `DependencyCheck:Enabled=false` stay exactly as they are; the
helper that sets these needs a name that is no longer `Blank`, keeping its
only-if-a-subclass-has-not-asked behaviour. `LiveAppFactory` is untouched — it already carries real
endpoints, which is its whole reason for existing.

### D5 — The chart refuses to render, and CI's minimal value set grows by two

Replace the `with` blocks for both endpoints in the deployment template with Helm's `required`, and
delete the `""` defaults and the "designed degraded mode" comments from `values.yaml`. A default
that renders is indistinguishable from a value an operator chose, which is the same ambiguity D1
removes one layer down.

Four render commands supply no endpoints today and must be updated in the same change: the
minimal-values render and the writable-`/tmp` gate in `ci.yml`, and the two packaged-chart renders in
`release.yml`. `ci/test-values.yaml` already carries both and needs nothing.

The writable-`/tmp` gate keeps its meaning. Its comment says it renders "from the minimal values on
purpose — the mount must not depend on any value being set", and that stays true: the two endpoints
become part of the minimum, and the mount still depends on no value. Adding them is not an exception
to the gate's intent.

**The pod security context and the chart's volume mounts are not touched by this change** — no
`securityContext` change, no change to the writable `/tmp` mount. This change therefore does not need
the real-pod verification (`kind` + `helm install` + a BoM XLSX through `/v1/extract`) that a
security-context or mount change would require; `helm lint` and `helm template` do catch the class of
change being made here, since it is render-time behaviour, and CI exercises both the
endpoints-present and endpoints-absent branches.

**Versioning:** this is a behaviour change in the image, so the image takes a minor bump and the
chart's `major.minor` follows it, per the chart-equals-image rule. `appVersion` is CI-stamped at
package time and is never hand-edited.

## Risks / Trade-offs

- **An EuGo-infra release that has not been updated with both endpoint values now fails.** → That is
  the intended behaviour and it fails at `helm install`, before anything reaches the cluster, rather
  than as a crash-looping rollout. The chart minor bump is the signal, and the handoff is an explicit
  migration step. Nothing is silent in either direction.

- **A developer with no Azure context can no longer boot the service at all, spreadsheet work
  included.** → Two `.invalid` URLs and `DocInt__StartupProbe__Enabled=false` is the documented path,
  and `README.md` gains it. This is a real cost, accepted: the alternative is the escape hatch
  doubling as a way to skip supplying values, which is the thing being removed.

- **Resolution order stays a live hazard.** Hosted services are resolved before `ValidateOnStart`
  runs, so an eager `options.Value` in any probe constructor would pre-empt the validator's message
  and break disposal. Unconditional registration does not create this, but it does remove the guard
  that used to keep blank values away from the probes. → The deferral in both probe constructors is
  load-bearing and must be preserved; a test that boots with a blank endpoint and asserts the
  validator's message — not a DI resolution failure — pins it.

- **Three tests covering a real code path are deleted (D3).** → Replaced by boot-refusal tests
  asserting the stronger property. Recorded here so the deletion reads as a decision rather than as
  coverage quietly going missing.

- **`.invalid` endpoints across the shared test factory make any accidentally-enabled probe fail on
  DNS.** → Intended. A loud failure in a test that should not be dialling is the desired outcome, and
  it is strictly better than the silent live-Azure behaviour the factory comment records.

## Deliberately deferred

- **An opt-out setting** (`DocInt:RequireEndpoints`, or a per-surface `disabled` sentinel). Raised
  first and rejected: it preserves exactly the ambiguity this change exists to remove, since "the
  operator forgot" and "the operator meant it" would remain the same configuration. Revisit only if a
  genuine endpoint-free deployment appears, which would be a spec change.
- **Requiring one surface but not the other** (Document Intelligence mandatory, Azure OpenAI
  optional, on the grounds that image traffic is rarer). Rejected for the same reason at half the
  scale, and it would leave the response-body-only failure mode alive for one file kind.
- **Withdrawing `engine_unconfigured` from the contract vocabulary.** Deferred indefinitely: it is a
  `/v1` change that buys nothing, and an unreachable code costs a caller nothing to keep.
- **Making a runtime dependency outage fail readiness.** Explicitly not this change, and still wrong
  for the reason `DependencyCheck` already records — the endpoints are shared by every replica, so
  failing readiness would empty the Service instead of shedding load, and would take the Azure-free
  spreadsheet path down with it. Boot-time and runtime stay asymmetric on purpose.
- **A "kinds this deployment serves" concept**, which would let a spreadsheet-only deployment declare
  itself and require nothing from Azure. It is the principled version of the escape hatch and would
  make the alias conditional again on something real. No such concept exists today, and inventing one
  to serve a hypothetical deployment is more machinery than the problem justifies.
- **Enforcing the endpoint requirement with a cluster admission policy** rather than chart
  `required`. Rejected as further from the operator: `helm install` fails on their terminal with the
  missing value named, which an admission rejection does not.

## Migration Plan

1. Land the code and configuration change together — the validation rule, the unconditional probe
   registration, the seam removal, and the test-host inversion in one step. Splitting them leaves a
   window where the test suite cannot boot.
2. Update `README.md`'s configuration reference with the endpoints-required rule and the
   `.invalid`-plus-probe-disabled local path, and `CLAUDE.md`'s live-smoke section where it describes
   blank endpoints as supported.
3. Chart: `required` on both values, defaults deleted, version bump, and the four CI render commands
   updated in the same commit. Verify with `helm lint charts/eugo-docint` and both render branches.
4. Cut the image tag before any chart-only tag, or cut them together — the chart release job refuses
   to package a chart whose `major.minor` has no paired image tag.
5. Hand EuGo-infra the requirement that both endpoint values must be present in release values before
   the new chart version is deployed. **This handoff is outside this repository.** The in-cluster
   credential path is unaffected; the pod authenticates with Workload Identity and carries no key.

**Rollback:** revert the commit and redeploy the prior chart minor. Nothing persists and no data
migrates, so rollback is a redeploy. A cluster already running the new chart with both values set
stays working on the old one, since the old chart accepts the values it now requires.
