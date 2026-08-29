## 1. Decouple the chart's package name from its rendered identity

- [x] 1.1 Add a failing assertion to `.github/workflows/ci.yml`'s `chart-lint`: render the chart and
      assert `app.kubernetes.io/name: eugo-docint` and a resource name ending `-eugo-docint`, so the
      rename in 1.3 cannot silently reach the Deployment's immutable selector. Confirm it fails only
      once 1.3 lands, not before — a guard that never went red proves nothing.
- [x] 1.2 Pin `eugo-docint.name` in `charts/eugo-docint/templates/_helpers.tpl` to the literal
      `eugo-docint`, with a comment recording that `.Chart.Name` is deliberately not used because
      the published chart name differs and the label is part of an immutable selector.
- [x] 1.3 Rename the chart in `charts/eugo-docint/Chart.yaml` to `eugo-docint-chart`, leaving
      `version` and `appVersion` untouched.
- [x] 1.4 Verify locally: `helm lint charts/eugo-docint`, then `helm template` with
      `ci/test-values.yaml` and confirm resource names and `app.kubernetes.io/name` are unchanged
      from before the rename, while `helm.sh/chart` now reads `eugo-docint-chart-<version>`.
- [x] 1.5 Run the gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx`
      → `dotnet test --no-build src/DocInt.slnx`. Nothing here should move it; a change that does
      means the chart name reached the application, which it must not.

## 2. Image pull secret surface on the chart

- [x] 2.1 Add `chart-lint` assertions for both halves, mirroring the existing `/tmp` and zero-limit
      steps: with `--set imagePullSecrets[0].name=ghcr` the rendered pod references that secret by
      name; with the value unset the pod spec contains no `imagePullSecrets` key at all and the
      render still succeeds. Assert against a rendered file, not a pipeline, for the reason the
      existing steps document.
- [x] 2.2 Add `imagePullSecrets: []` to `charts/eugo-docint/values.yaml` with a comment stating that
      it holds names of secrets that must already exist in the namespace, that the chart creates no
      Secret, and that no credential material is ever accepted as a value.
- [x] 2.3 Render `spec.imagePullSecrets` in `charts/eugo-docint/templates/deployment.yaml`, guarded
      so an empty list omits the key entirely.
- [x] 2.4 Confirm no rendered manifest, under any values permutation exercised by `chart-lint`,
      contains credential material — only secret names.
- [x] 2.5 Verify locally with `helm lint` and both `helm template` renders, then run the gate
      (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 3. Repoint the release workflow to GHCR

- [x] 3.1 Replace the workflow permissions in `.github/workflows/release.yml`: drop
      `id-token: write`, add `packages: write`.
- [x] 3.2 Add a step that computes the lowercased owner once and exports it, and derive both the
      image tag and the chart namespace from it. Neither publish may depend on the other having
      normalized the value.
- [x] 3.3 Replace `azure/login` + `az acr login` in the `image` job with `docker/login-action`
      against `ghcr.io` using `github.actor` and `GITHUB_TOKEN`. Publish to
      `ghcr.io/eugo-as/eugo-docint`, keeping the existing `linux/amd64,linux/arm64` platforms,
      the gha cache, and the strip-`v` version handling.
- [x] 3.4 Add the `org.opencontainers.image.source` label to the image build so GHCR links the
      package to this repository.
- [x] 3.5 Replace the `chart` job's Azure auth with `docker/login-action` **and** an explicit
      `helm registry login ghcr.io`, then push to `oci://ghcr.io/eugo-as`. With the chart renamed in
      group 1 this resolves to `ghcr.io/eugo-as/eugo-docint-chart`, distinct from the image.
- [x] 3.6 Update the `chart` job's `helm package` destination filename, which is derived from the
      chart name and therefore becomes `eugo-docint-chart-<version>.tgz`.
- [x] 3.7 Confirm the untouched parts are genuinely untouched: tag triggers `v*` / `chart-v*`, the
      `build-test` gate, image-before-chart ordering, both version-pairing checks, and the
      render-the-packaged-`.tgz` validation.
- [x] 3.8 Grep the whole workflow for `azure`, `acr`, `azurecr`, and `AZURE_` — all four repository
      variables and both Azure steps must be gone.
- [x] 3.9 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 4. CI workflow and fixture values

- [x] 4.1 Update `charts/eugo-docint/ci/test-values.yaml`: replace
      `creugoexample.azurecr.io/eugo-docint` with a `ghcr.io/eugo-as/eugo-docint` example.
- [x] 4.2 Confirm `chart-lint` still passes all eleven existing assertions alongside the new ones
      from groups 1 and 2 — including the two added when the render gate was removed (`rendering
      with no values supplied…` and `one endpoint set carries only that endpoint`). The
      endpoint-refusal step this task used to name is gone: it was inverted, not kept.
- [x] 4.3 Bump the chart `version` patch in `charts/eugo-docint/Chart.yaml` to `0.3.2` — the rename
      and the `imagePullSecrets` value are both chart-owned changes, and republishing different
      chart content under `0.3.1` would make the version meaningless. `major.minor` still tracks the
      image; leave `appVersion` alone, since CI stamps it at package time.
- [x] 4.4 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 5. Documentation sweep

- [x] 5.1 `README.md`: replace every ACR coordinate with its GHCR equivalent; rewrite the "Release
      prerequisites" section to say there are none; document the manual `kubectl create secret
      docker-registry` step with the classic-PAT rationale; note that `helm install` from the OCI
      URL needs `helm registry login ghcr.io` because the chart is private; record that the chart
      is published as `eugo-docint-chart` while rendering `eugo-docint` resources.
- [x] 5.2 `CLAUDE.md`: update the tech-stack line `Docker → ACR → AKS` and the statement that
      EuGo-infra owns ACR provisioning.
- [x] 5.3 `openspec/config.yaml`: update the same `Docker → ACR → AKS` phrase in `context`.
- [x] 5.4 `charts/eugo-docint/values.yaml`: confirm `image.repository` already reads
      `ghcr.io/eugo-as/eugo-docint` — `chart-agnostic-of-runtime-config` set it ahead of this
      change — so the only edit left in this file is group 2's `imagePullSecrets` block.
- [x] 5.5 Confirm nothing under `docs/superpowers/` was modified — those are historical records and
      their ACR rationale is what makes this reversal legible.
- [x] 5.6 Repo-wide grep for `azurecr`, `ACR_NAME`, and `acr` outside `docs/superpowers/` and
      `openspec/changes/archive/`; every remaining hit is either intentional history or a miss.
- [x] 5.7 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 6. First release and the verification only a cluster can give

- [x] 6.1 Push `v0.3.0` and watch the run. Confirm both jobs succeed and neither coordinate carries
      a capital letter. Plain `vX.Y.Z`, deliberately not a release candidate: `sort -V` orders a
      prerelease *after* its release, so an `-rc` tag would leave every later chart-only release
      resolving `appVersion` to the candidate. See design.md — Risks.
- [x] 6.2 Confirm in GHCR that two distinct packages exist — `eugo-docint` and `eugo-docint-chart` —
      that both are **private**, and that both are linked to this repository.
      Verified 2026-08-29: both published (`:0.3.0` and `:0.3.2`), both refuse an anonymous pull
      with HTTP 403 — a behavioural check, stronger than the API's visibility field. The
      `org.opencontainers.image.source` label was applied at build. The repository *link* itself
      was NOT confirmed: reading it needs `read:packages`, which the available token lacks. Check
      it in package settings alongside the visibility confirmation.
- [ ] 6.3 **BLOCKED — needs a classic PAT with `read:packages`.** Pull the chart with
      `helm registry login` + `helm pull oci://ghcr.io/eugo-as/eugo-docint-chart` and confirm its
      `appVersion` is `0.3.0`. Attempted 2026-08-29 with the ambient `gh` token: `helm registry
      login` succeeds and the pull is then refused with `403: denied`, because that token carries
      `repo`/`workflow` but not `read:packages`. Login succeeding is not access — worth knowing,
      since it is the same failure an under-scoped pull secret would give a cluster. The
      credential is deliberately not held in this repo; it is the same PAT 6.4 needs.
- [ ] 6.4 **BLOCKED — no cluster reachable from this machine** (`kubectl` reports no contexts,
      `kind` is not installed). On a real cluster: create the pull secret by hand, `helm install` with
      `imagePullSecrets[0].name` set, and confirm the pod reaches `Running`. Rendering proves the
      field is well-formed and nothing more — whether a private image actually pulls cannot be
      established by `helm lint` or `helm template`. If no cluster is available, record this as
      blocked rather than closing it; do not substitute a render for it.
- [ ] 6.5 **BLOCKED on 6.4.** Put a BoM XLSX through `/v1/extract` on that pod. The security context and volume mounts
      are unchanged by this change, but this is the first pod ever run from a published image, so
      the read-only-root/`/tmp` path has never been exercised on one.
- [ ] 6.6 Tell EuGo-infra that release execution now needs `helm registry login ghcr.io` and a
      namespace pull secret, and that the image and chart moved to GHCR. (This task previously also
      cut `v0.3.0` after an RC; 6.1 now cuts it directly.)
