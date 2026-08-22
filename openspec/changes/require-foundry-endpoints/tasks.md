## 1. Make a missing endpoint refuse the boot

The test-host inversion belongs in this group rather than a later one: the moment the validation
rule lands, every suite that boots the real `Program` inherits `DocIntAppFactory`'s blanked
endpoints and fails to start. Splitting them leaves the gate red between groups.

- [x] 1.1 Add failing tests to `tests/DocInt.Tests/OptionsTests.cs` for each refusal: document-layout
      endpoint absent; image-description endpoint absent; an endpoint present but empty; an endpoint
      present but whitespace; both absent. Each asserts the boot fails and that the message names the
      missing key.
- [x] 1.2 Add a failing test asserting the refusal arrives as the options validator's message, not as
      a DI resolution failure — this pins the resolution-order hazard in design D2, where hosted
      services (and every `IStartupProbe`) are constructed before `ValidateOnStart` runs.
- [x] 1.3 Add a failing test asserting a blank `Foundry:DeploymentNameVision` refuses the boot with no
      endpoint condition attached, per spec *The vision deployment alias is required*.
- [x] 1.4 Invert `tests/DocInt.Tests/DocIntAppFactory.cs`: default both endpoints to reserved-invalid
      absolute URIs under `.invalid` (RFC 2606) instead of blanking them, keeping
      `StartupProbe:Enabled=false` and `DependencyCheck:Enabled=false` unchanged and keeping the
      only-if-a-subclass-has-not-asked behaviour. Rename the `Blank` helper, which no longer blanks,
      and rewrite its comment — the hermeticity it protects is now "cannot resolve anywhere" rather
      than "no endpoint set".
- [x] 1.5 Delete `ExtractContractTests.Unconfigured_layout_engine_yields_per_file_engine_unconfigured`,
      which asserts a per-file error that 1.4 makes unreachable through the factory.
- [x] 1.6 Add the presence rules to the `AddOptions<FoundryOptions>()` chain in
      `src/DocInt.Api/Configuration/DocIntOptions.cs`, beside the absolute-URI rules they strengthen
      (design D1). Make the deployment-alias rule unconditional by dropping its
      `OpenAIEndpoint is set` predicate.
- [x] 1.7 Update the `FoundryOptions` doc comments in the same file that describe each endpoint as
      independently optional and blank as "the stub-first path", and the `DeploymentNameVision`
      comment that calls `""` legal "exactly while no endpoint is configured".
- [x] 1.8 Update `src/DocInt.Api/appsettings.json`: remove the two `""` endpoint values and rewrite the
      `Foundry` block comment that documents blank as a supported degraded mode. Confirm
      `OptionsTests.Appsettings_supplies_the_spec_defaults` still passes or is updated to match.
- [x] 1.9 Gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx` →
      `dotnet test --no-build src/DocInt.slnx`.

## 2. Register both probes and both dependency checks unconditionally

- [x] 2.1 (landed with group 1, which is what made its premise unreachable) Replace
      `StartupConnectivityCheckTests.No_endpoint_configured_registers_no_probe`, whose
      premise is now a boot failure, with a test asserting that both probes and both dependency health
      checks are registered whenever the service starts at all.
- [x] 2.2 Add a test asserting `/health` reports both dependency surfaces, closing the gap where an
      unconfigured surface was registered as no health check and appeared nowhere in the report.
- [x] 2.3 Remove the two `IsSet` guards from `AddStartupConnectivityCheck` in
      `src/DocInt.Api/Startup/StartupConnectivityCheck.cs` and update the extension method's summary,
      which describes registering "nothing at all when both are blank".
- [x] 2.4 Rewrite the `Endpoint` property comments in `DocumentIntelligenceStartupProbe` and
      `AzureOpenAIStartupProbe` — "registered only when non-blank" is no longer the guarantee;
      validation is. Leave both constructors' `Lazy<T>` deferral exactly as it is: it is load-bearing
      for the resolution-order hazard and removing the guards does not change that.
- [x] 2.5 Gate: restore → build --no-restore → test --no-build against `src/DocInt.slnx`.

## 3. Remove the `IsConfigured` seam

Design D3. `ErrorCodes.EngineUnconfigured` stays — this group removes code, never contract
vocabulary.

- [x] 3.1 Delete `LayoutEngineTests.Unconfigured_client_yields_engine_unconfigured` and
      `VisionEngineTests.Unconfigured_client_yields_engine_unconfigured`, and drop the `IsConfigured`
      member from every fake client in the test suite.
- [x] 3.2 Remove `IsConfigured` from `ILayoutAnalysisClient` and `IVisionChatClient`, and from
      `AzureLayoutAnalysisClient` and `AzureVisionChatClient`. Simplify both clients' construction so
      the underlying client is non-nullable — the endpoint is guaranteed present.
- [x] 3.3 Remove the guards at the top of `LayoutEngine.ExtractAsync` and `VisionEngine.ExtractAsync`,
      the `EngineUnconfiguredException` throws in both Azure clients, the exception type in
      `src/DocInt.Api/Engines/Errors.cs`, and its `catch` clause in `EngineRouter.RouteAsync`.
- [x] 3.4 Confirm `ErrorCodes.EngineUnconfigured` is still defined and still covered by whatever
      contract test enumerates the per-file error vocabulary; the `/v1` wire contract does not change.
      *(No such test existed, and after 3.3 the constant had no reference anywhere — one code-deleting
      cleanup away from a silent contract narrowing. Added `The_per_file_error_vocabulary_is_the_frozen_v1_set`,
      which pins the whole set by reflection in both directions.)*
- [x] 3.5 Gate: restore → build --no-restore → test --no-build against `src/DocInt.slnx`. Nullable
      warnings are build errors in this repo, so 3.2's non-nullable change either compiles clean or
      is not done.

## 4. Make the chart refuse to render without both endpoints

- [x] 4.1 Replace the `with` blocks for `foundry.documentIntelligenceEndpoint` and
      `foundry.openAIEndpoint` in `charts/eugo-docint/templates/deployment.yaml` with Helm's
      `required`, each naming its value in the message. Leave the `docint.*` limits on their existing
      is-set test — the reasoning in that template comment about `with` treating `0` as empty is
      unrelated and still applies.
- [x] 4.2 Delete the two `""` defaults from `charts/eugo-docint/values.yaml` and rewrite the comments
      that describe an empty value as omitting the variable and yielding `engine_unconfigured`
      "designed degraded mode".
- [x] 4.3 Update the render commands that supply no endpoints (three, not four — both `helm lint`
      calls warn and still exit 0, as they already did for the required `image.repository`): the minimal-values render and the
      writable-`/tmp` regression gate in `.github/workflows/ci.yml`, and the two packaged-chart
      renders in `.github/workflows/release.yml`. Update the `/tmp` gate's comment to say the two
      endpoints are part of the minimal value set — its assertion, that the mount depends on no
      value, is unchanged and must stay.
- [x] 4.4 Add a CI step asserting a render *without* the endpoints fails and names the missing value,
      so the new refusal is covered rather than only its success path.
- [x] 4.5 Set `version: 0.3.0` in `charts/eugo-docint/Chart.yaml`, tracking the image minor this
      change requires. Do not touch `appVersion` — CI stamps it at package time.
- [x] 4.6 Verify: `helm lint charts/eugo-docint`;
      `helm template ci charts/eugo-docint -f charts/eugo-docint/ci/test-values.yaml`; the
      minimal-values render with both endpoints set; and a render with an endpoint omitted, which must
      fail. No pod security context or volume mount is touched by this group, so no real-pod
      verification is required — but if that changes, `helm lint` and `helm template` cannot catch it
      and a `kind` cluster with a BoM XLSX through `/v1/extract` is required.
- [x] 4.7 Gate: restore → build --no-restore → test --no-build against `src/DocInt.slnx`, so the chart
      change is verified against a green solution rather than on its own.

## 5. Documentation

- [ ] 5.1 Update `README.md`'s configuration reference: both endpoints are required, there is no
      opt-out, and the local path for a host that cannot reach Azure is two `.invalid` URLs plus
      `DocInt__StartupProbe__Enabled=false`.
- [ ] 5.2 Update `CLAUDE.md` where it describes blank endpoints as supported — the live-smoke section
      and the contract/testing notes that reference the `engine_unconfigured` path.
- [ ] 5.3 Gate: restore → build --no-restore → test --no-build against `src/DocInt.slnx`.

## 6. Release and handoff

- [ ] 6.1 Cut the image tag `v0.3.0` before any `chart-v0.3.*` tag, or cut them together — the chart
      release job refuses to package a chart whose `major.minor` has no paired image tag.
- [ ] 6.2 Hand EuGo-infra the requirement that both endpoint values must be present in release values
      before the new chart version is deployed, and that a release omitting either now fails at
      `helm install` rather than degrading. **This handoff is outside this repository** and is not
      something this change can complete on its own; record it as done only when it has been
      communicated.
