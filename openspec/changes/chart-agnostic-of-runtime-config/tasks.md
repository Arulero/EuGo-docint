## 1. Establish the red baseline

- [x] 1.1 Record which `chart-lint` assertions fail today, by running every step's commands locally
      against the unmodified chart. Capture the exact failure so the fix can be checked against it
      rather than against an assumption.
- [x] 1.2 Invert the `rendering without an endpoint fails and names the value` step in
      `.github/workflows/ci.yml`: it must now assert that the render succeeds **and** that neither
      `Foundry__DocumentIntelligenceEndpoint` nor `Foundry__OpenAIEndpoint` appears. Confirm it
      fails now, before the template changes — a guard that was never red proves nothing.

## 2. Remove the render gates

- [x] 2.1 Replace the two `required` calls for the Foundry endpoints in
      `charts/eugo-docint/templates/deployment.yaml` with the `with` form used by
      `foundry.deploymentNameVision`. Replace the comment block explaining `required` with one
      recording why `with` is correct here and why the `DocInt__*` limits below still cannot use it.
- [x] 2.2 Remove the `required` call on `image.repository` in the same file.
- [x] 2.3 Set `image.repository` in `charts/eugo-docint/values.yaml` to
      `ghcr.io/eugo-as/eugo-docint`, with a comment noting it is fully qualified deliberately —
      a bare name would resolve against Docker Hub.
- [x] 2.4 Update the `foundry` block comments in `values.yaml`: both endpoints are optional to the
      chart and required by the service at boot, and omitting one yields a pod that refuses to start
      rather than a render that fails.
- [x] 2.5 Confirm `helm template ci charts/eugo-docint` renders with no `--set` at all, and that
      `helm lint charts/eugo-docint` passes.
- [x] 2.6 Confirm the inverted step from 1.2 now passes, and that a render with one endpoint set
      carries that variable and not the other.
- [x] 2.7 Run the gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx`
      → `dotnet test --no-build src/DocInt.slnx`. The service's boot-time endpoint requirement is
      untouched, so `OptionsTests` and `RetiredConfigurationKeyTests` must stay green — a move here
      means the chart change reached the application.

## 3. Revive the five dead assertions

- [x] 3.1 Run each of the five `render()`-based `chart-lint` steps locally, unmodified: zero limits,
      scrape annotations, HPA memory metric, admission limits, and the `/tmp` mount. Confirm each
      passes now. Treat any that still fails as a real finding about the chart and report it — do
      not adjust the assertion to make it pass.
- [x] 3.2 Remove the job-level `$FOUNDRY` env var from `.github/workflows/ci.yml` and its uses in
      the three steps that reference it, now that no render needs endpoints.
- [x] 3.3 Confirm the `/tmp` mount assertion renders from genuinely no values, so it proves the
      mount depends on no configuration value.
- [x] 3.4 Re-run every `chart-lint` step locally end to end and confirm all pass.
- [x] 3.5 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 4. Release workflow and chart version

- [x] 4.1 Remove the endpoint `--set` values from the packaged-chart validation in
      `.github/workflows/release.yml`, which needed them only to satisfy the gate.
- [x] 4.2 Bump the chart `version` patch in `charts/eugo-docint/Chart.yaml`. `major.minor` tracks
      the image and does not move; leave `appVersion` alone, since CI stamps it at package time.
- [x] 4.3 Verify `helm package` and the packaged-artifact render locally, reproducing what
      `release.yml` does, with no endpoint values supplied.
- [x] 4.4 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 5. Documentation

- [x] 5.1 `README.md`: update the configuration reference so both endpoints read as required by the
      service at run time, not by the chart at render time, and state what happens when one is
      omitted. Update the `image.repository` entry to show it now has a default.
- [x] 5.2 `CLAUDE.md`: the "Both `Foundry:*Endpoint` values are required" note describes the host's
      behavior, which is unchanged — confirm it does not also claim the chart enforces it, and
      correct it if it does.
- [x] 5.3 Confirm nothing under `docs/superpowers/` was modified. The 2026-07-26 chart design spec
      records the original reasoning and is the context that makes this reversal legible.
- [x] 5.4 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 6. Merge and unblock

- [x] 6.1 Confirm the full local equivalent of CI is green: `helm lint`, every `chart-lint` step,
      and the three-step .NET gate.
- [ ] 6.2 Merge to `main` and delete the branch, per the repo workflow.
- [ ] 6.3 Rebase `feat/publish-to-ghcr` onto the new `main` and confirm its task 4.2 is now
      verifiable — `chart-lint` green before that change adds anything to it.
