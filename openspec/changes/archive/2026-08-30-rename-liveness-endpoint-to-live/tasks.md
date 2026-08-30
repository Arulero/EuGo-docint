## 1. Failing tests first

- [x] 1.1 In `tests/DocInt.Tests/HealthEndpointsTests.cs`, move the liveness assertions from `/alive` to `/live`: rename `Alive_returns_healthy` → `Live_returns_healthy` and `Alive_is_unaffected_by_a_degraded_dependency` → `Live_is_unaffected_by_a_degraded_dependency`, both requesting `/live`. Update `Info_returns_service_metadata` to assert `/live` is in `endpoints` and `/alive` is not.
- [x] 1.2 Add `Alive_is_no_longer_served` to the same file: `GET /alive` returns `404 Not Found`. This is the spec's "The former path is gone" scenario and the only new test the rename needs beyond moved assertions.
- [x] 1.3 In `tests/DocInt.Tests/TraceFilterTests.cs`, change the `/alive` `InlineData` to `/live` in both theories, and update the class summary comment so it describes the current pair.
- [x] 1.4 Run the gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx` → `dotnet test --no-build src/DocInt.slnx`. Expected RED, and the failure shape is the point — the liveness tests and `Every_excluded_path_is_a_route_that_exists("/live")` fail because `/live` 404s, `Alive_is_no_longer_served` fails because `/alive` still answers 200, and `Only_the_probe_and_scrape_endpoints_are_excluded_from_tracing("/live", false)` fails because the filter still excludes `/alive`. Record the actual failures; anything else failing is a separate problem.

## 2. Route, constant, and `/info`

- [x] 2.1 In `src/ServiceDefaults/Extensions.cs`, change `AlivenessEndpointPath` from `"/alive"` to `"/live"`. Nothing else in that file changes — `MapDefaultEndpoints` stays uncalled and the trace filter keeps composing from the two constants.
- [x] 2.2 In `src/DocInt.Api/Program.cs`, map the liveness endpoint at `/live` and add `/live` in place of `/alive` in the `endpoints` array behind `/info`.
- [x] 2.3 Update the comment block above the health mappings in `Program.cs`: it still explains the `/healthz`-era trace-filter bug (keep that — it is why the constant moved too) but names `/alive` as the path `MapDefaultEndpoints` would collide on. Point it at `/live`.
- [x] 2.4 Update the `/alive` references in the surrounding code comments so they name the served route: `src/DocInt.Api/Health/DependencyHealthCheck.cs`, `src/DocInt.Api/Startup/StartupConnectivityCheck.cs`, `src/DocInt.Api/Admission/AdmissionFilter.cs`. Comment-only; no behavior changes.
- [x] 2.5 Run the gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx` → `dotnet test --no-build src/DocInt.slnx`. Expected GREEN, whole suite. The env-gated `LiveSmokeTests` self-skip without Azure credentials — that is ⏭️, not a pass; list it as blocked rather than counting it.

## 3. Chart and CI

- [x] 3.1 In `charts/eugo-docint/templates/deployment.yaml`, change the liveness probe `httpGet.path` to `/live`. The readiness probe path is unchanged.
- [x] 3.2 In `charts/eugo-docint/templates/NOTES.txt`, update the probe summary line to name `/live`.
- [x] 3.3 Add a `chart-lint` step to `.github/workflows/ci.yml` asserting the rendered probe paths, in the style of the existing named steps: render with no values, then assert the manifest contains `path: /live` and `path: /health` and does not contain `path: /alive`. Use an explicit `if grep -q ... ; then error; fi` for the negative half, matching the convention the neighbouring steps document. This is the spec's "Rendered manifests probe the served paths" requirement, and it is all CI can prove — it cannot see the image.
- [x] 3.4 Run the chart checks locally: `helm lint charts/eugo-docint` and `helm template ci charts/eugo-docint -f charts/eugo-docint/ci/test-values.yaml`, plus the bare `helm template ci charts/eugo-docint` the new step uses. Confirm the new assertions pass and the existing ones still do. If `helm` is unavailable, say so and list this as blocked rather than marking it done.

## 4. Documentation and the final gate

- [x] 4.1 Update the two probe-path references in `README.md` (the `/alive`-is-blind-to-this paragraph and the deployment probes line).
- [x] 4.2 Leave `docs/superpowers/**` untouched — historical records by project rule. Note this explicitly in the merge commit so a future reader knows the stale `/alive` mentions there are deliberate.
- [x] 4.3 Run `graphify update .` so the knowledge graph reflects the renamed route.
- [x] 4.6 Update `docs/document-intelligence-overview.confluence.html`, which documents `GET /alive` under *Health and observability*. Found during 4.5 and not anticipated by this list: it is a current, hand-maintained overview published to Confluence, not a `docs/superpowers/` historical record, so the do-not-update rule does not cover it and the rename would otherwise leave it wrong. One line; no other current doc mentions the probes.
- [x] 4.4 Run the full gate one final time on the complete branch: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx` → `dotnet test --no-build src/DocInt.slnx`. Green is the merge condition.
- [x] 4.5 Confirm no `/alive` remains outside `docs/superpowers/` and the archived openspec changes: `grep -rn "/alive" --include=* . | grep -v "docs/superpowers\|openspec/changes/archive\|graphify-out\|/bin/\|/obj/\|\.git/"`. The only expected hits are this change's own artifacts, which describe the rename.

## 5. Release (lockstep — not part of the merge)

- [ ] 5.1 Cut one `vX.Y.Z` tag from the merge commit so image and chart publish from the same source. Do **not** cut a `chart-vX.Y.P` chart-only tag for this change: a chart carrying `/live` against a previously published image serving `/alive` is exactly the mismatch the design rejects.
- [ ] 5.2 After rollout, verify on the cluster: pods reach `Ready` and restart counts stay at zero across at least three liveness periods (30 s at `periodSeconds: 10`). A path mismatch shows up as restarts, not as a failed `helm upgrade`. ❓ until measured — CI cannot establish this.
