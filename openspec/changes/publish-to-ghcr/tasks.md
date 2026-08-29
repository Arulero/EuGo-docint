## 1. Decouple the chart's package name from its rendered identity

- [ ] 1.1 Add a failing assertion to `.github/workflows/ci.yml`'s `chart-lint`: render the chart and
      assert `app.kubernetes.io/name: eugo-docint` and a resource name ending `-eugo-docint`, so the
      rename in 1.3 cannot silently reach the Deployment's immutable selector. Confirm it fails only
      once 1.3 lands, not before — a guard that never went red proves nothing.
- [ ] 1.2 Pin `eugo-docint.name` in `charts/eugo-docint/templates/_helpers.tpl` to the literal
      `eugo-docint`, with a comment recording that `.Chart.Name` is deliberately not used because
      the published chart name differs and the label is part of an immutable selector.
- [ ] 1.3 Rename the chart in `charts/eugo-docint/Chart.yaml` to `eugo-docint-chart`, leaving
      `version` and `appVersion` untouched.
- [ ] 1.4 Verify locally: `helm lint charts/eugo-docint`, then `helm template` with
      `ci/test-values.yaml` and confirm resource names and `app.kubernetes.io/name` are unchanged
      from before the rename, while `helm.sh/chart` now reads `eugo-docint-chart-<version>`.
- [ ] 1.5 Run the gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore src/DocInt.slnx`
      → `dotnet test --no-build src/DocInt.slnx`. Nothing here should move it; a change that does
      means the chart name reached the application, which it must not.

## 2. Image pull secret surface on the chart

- [ ] 2.1 Add `chart-lint` assertions for both halves, mirroring the existing `/tmp` and zero-limit
      steps: with `--set imagePullSecrets[0].name=ghcr` the rendered pod references that secret by
      name; with the value unset the pod spec contains no `imagePullSecrets` key at all and the
      render still succeeds. Assert against a rendered file, not a pipeline, for the reason the
      existing steps document.
- [ ] 2.2 Add `imagePullSecrets: []` to `charts/eugo-docint/values.yaml` with a comment stating that
      it holds names of secrets that must already exist in the namespace, that the chart creates no
      Secret, and that no credential material is ever accepted as a value.
- [ ] 2.3 Render `spec.imagePullSecrets` in `charts/eugo-docint/templates/deployment.yaml`, guarded
      so an empty list omits the key entirely.
- [ ] 2.4 Confirm no rendered manifest, under any values permutation exercised by `chart-lint`,
      contains credential material — only secret names.
- [ ] 2.5 Verify locally with `helm lint` and both `helm template` renders, then run the gate
      (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 3. Repoint the release workflow to GHCR

- [ ] 3.1 Replace the workflow permissions in `.github/workflows/release.yml`: drop
      `id-token: write`, add `packages: write`.
- [ ] 3.2 Add a step that computes the lowercased owner once and exports it, and derive both the
      image tag and the chart namespace from it. Neither publish may depend on the other having
      normalized the value.
- [ ] 3.3 Replace `azure/login` + `az acr login` in the `image` job with `docker/login-action`
      against `ghcr.io` using `github.actor` and `GITHUB_TOKEN`. Publish to
      `ghcr.io/eugo-as/eugo-docint`, keeping the existing `linux/amd64,linux/arm64` platforms,
      the gha cache, and the strip-`v` version handling.
- [ ] 3.4 Add the `org.opencontainers.image.source` label to the image build so GHCR links the
      package to this repository.
- [ ] 3.5 Replace the `chart` job's Azure auth with `docker/login-action` **and** an explicit
      `helm registry login ghcr.io`, then push to `oci://ghcr.io/eugo-as`. With the chart renamed in
      group 1 this resolves to `ghcr.io/eugo-as/eugo-docint-chart`, distinct from the image.
- [ ] 3.6 Update the `chart` job's `helm package` destination filename, which is derived from the
      chart name and therefore becomes `eugo-docint-chart-<version>.tgz`.
- [ ] 3.7 Confirm the untouched parts are genuinely untouched: tag triggers `v*` / `chart-v*`, the
      `build-test` gate, image-before-chart ordering, both version-pairing checks, and the
      render-the-packaged-`.tgz` validation.
- [ ] 3.8 Grep the whole workflow for `azure`, `acr`, `azurecr`, and `AZURE_` — all four repository
      variables and both Azure steps must be gone.
- [ ] 3.9 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 4. CI workflow and fixture values

- [ ] 4.1 Update `charts/eugo-docint/ci/test-values.yaml`: replace
      `creugoexample.azurecr.io/eugo-docint` with a `ghcr.io/eugo-as/eugo-docint` example.
- [ ] 4.2 Update the `--set image.repository=` examples in `.github/workflows/ci.yml` if any imply
      a registry, and confirm `chart-lint` still passes every existing assertion — the `/tmp` mount,
      the endpoint-refusal, and both zero-limit cases — alongside the new ones from groups 1 and 2.
- [ ] 4.3 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 5. Documentation sweep

- [ ] 5.1 `README.md`: replace every ACR coordinate with its GHCR equivalent; rewrite the "Release
      prerequisites" section to say there are none; document the manual `kubectl create secret
      docker-registry` step with the classic-PAT rationale; note that `helm install` from the OCI
      URL needs `helm registry login ghcr.io` because the chart is private; record that the chart
      is published as `eugo-docint-chart` while rendering `eugo-docint` resources.
- [ ] 5.2 `CLAUDE.md`: update the tech-stack line `Docker → ACR → AKS` and the statement that
      EuGo-infra owns ACR provisioning.
- [ ] 5.3 `openspec/config.yaml`: update the same `Docker → ACR → AKS` phrase in `context`.
- [ ] 5.4 `charts/eugo-docint/values.yaml`: update the `image.repository` example comment.
- [ ] 5.5 Confirm nothing under `docs/superpowers/` was modified — those are historical records and
      their ACR rationale is what makes this reversal legible.
- [ ] 5.6 Repo-wide grep for `azurecr`, `ACR_NAME`, and `acr` outside `docs/superpowers/` and
      `openspec/changes/archive/`; every remaining hit is either intentional history or a miss.
- [ ] 5.7 Run the gate (restore → build --no-restore → test --no-build against `src/DocInt.slnx`).

## 6. First release and the verification only a cluster can give

- [ ] 6.1 Push `v0.3.0-rc.1` and watch the run. Confirm the prerelease suffix passes the
      major.minor pairing check, both jobs succeed, and neither coordinate carries a capital letter.
- [ ] 6.2 Confirm in GHCR that two distinct packages exist — `eugo-docint` and `eugo-docint-chart` —
      that both are **private**, and that both are linked to this repository.
- [ ] 6.3 Pull the chart with `helm registry login` + `helm pull oci://ghcr.io/eugo-as/eugo-docint-chart`
      and confirm its `appVersion` is `0.3.0-rc.1`.
- [ ] 6.4 On a real cluster: create the pull secret by hand, `helm install` with
      `imagePullSecrets[0].name` set, and confirm the pod reaches `Running`. Rendering proves the
      field is well-formed and nothing more — whether a private image actually pulls cannot be
      established by `helm lint` or `helm template`. If no cluster is available, record this as
      blocked rather than closing it; do not substitute a render for it.
- [ ] 6.5 Put a BoM XLSX through `/v1/extract` on that pod. The security context and volume mounts
      are unchanged by this change, but this is the first pod ever run from a published image, so
      the read-only-root/`/tmp` path has never been exercised on one.
- [ ] 6.6 Cut `v0.3.0` once 6.1–6.5 are green, and tell EuGo-infra that release execution now needs
      `helm registry login ghcr.io` and a namespace pull secret.
