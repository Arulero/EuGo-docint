## 1. Chart coordinate

- [x] 1.1 In `charts/eugo-docint/values.yaml`, change `image.repository` from
      `ghcr.io/eugo-as/eugo-docint` to `ghcr.io/arulero/eugo-docint`. Leave the surrounding comment
      about full qualification intact - it explains why the string is fully qualified, which has not
      changed.
- [x] 1.2 In `charts/eugo-docint/ci/test-values.yaml`, change `image.repository` to the same new
      coordinate. Leave `tag: 0.1.0` alone; it exercises the explicit-tag branch and is not a
      release coordinate.
- [x] 1.3 In `charts/eugo-docint/Chart.yaml`, move `version: 0.3.2` to `0.3.3`. Do not touch
      `appVersion` - CI stamps it at package time.
- [x] 1.4 Confirm the chart still renders and that the rename reached nothing it should not:
      `helm lint charts/eugo-docint`;
      `helm template ci charts/eugo-docint -f charts/eugo-docint/ci/test-values.yaml`;
      `helm template ci charts/eugo-docint --set autoscaling.enabled=false`;
      then assert on a default render that `image: "ghcr.io/arulero/eugo-docint:` appears, that
      `app.kubernetes.io/name: eugo-docint` is unchanged, and that `helm.sh/chart:` now reads
      `eugo-docint-chart-0.3.3`.
- [x] 1.5 Run the gate: `dotnet restore src/DocInt.slnx`, `dotnet build --no-restore src/DocInt.slnx`,
      `dotnet test --no-build src/DocInt.slnx`. No .NET code changes here, so this is a regression
      check rather than a proof of the edit; a red gate means something unrelated is wrong and must
      be resolved before continuing.

## 2. Documentation and comments

- [x] 2.1 In `README.md`, correct the two references to the old coordinate: the `image.repository`
      row of the chart values table, and the Deploy section paragraph naming where the image and
      chart publish.
- [x] 2.2 In `CLAUDE.md`, correct the coordinates in the Helm chart paragraph.
- [x] 2.3 In `.github/workflows/release.yml`, update the comment above the lowercasing step so it
      names the current organization spelling. Change no logic: `${GITHUB_REPOSITORY_OWNER,,}` in
      both jobs already resolves correctly, and the lowercasing remains necessary because `Arulero`
      also carries a capital.
- [x] 2.4 Confirm nothing functional still names the retired organization:
      `grep -rIn --exclude-dir=.git --exclude-dir=graphify-out --exclude-dir=archive
      --exclude-dir=superpowers --exclude-dir=retarget-ghcr-namespace "eugo-as" .` should return
      nothing. Hits under `docs/superpowers/**` and `openspec/changes/archive/**` are expected and
      must be left alone - they are historical records that were accurate when written. This
      change's own artifacts are excluded too: they quote the old coordinate to describe the fix.
- [x] 2.5 Run the gate as in 1.5.

## 3. Confirm what survived the organization rename

NOT DONE, closed by decision on 2026-09-11. The user chose to finish the change without granting
the scope. 3.1 through 3.3 were never executed; the questions they ask remain open.

- [ ] 3.1 Grant the local token the scope needed to see packages: `gh auth refresh -s read:packages`.
      Not granted. `gh auth refresh` also needs `-h github.com` and then runs an interactive device
      flow, so it cannot be completed from inside a tool call.
- [ ] 3.2 List the organization's container packages. Never run.
- [ ] 3.3 Decide on a re-release if the image did not survive. Never reached.
- [x] 3.4 Blocker recorded, as this task requires. Package inventory was blocked because the local
      GitHub token carries no `read:packages` scope: `gh api /orgs/Arulero/packages` answers 403,
      an anonymous GHCR probe answers 403 for every namespace because the packages are private, and
      `helm pull` of the freshly published chart answers `403 denied` with the same token that had
      just logged in successfully.

      This task's own fallback -- carry 3.2 and 3.3 into group 4, "where the publish result answers
      the same question directly" -- turned out to be WRONG, and that matters more than the blocker
      itself. The `chart-v0.3.3` release succeeded, but it proves nothing about the image: the
      workflow resolves `appVersion` from `git tag -l "v0.3.*"`, which reads git, not the registry.
      A green chart release is therefore compatible with `ghcr.io/arulero/eugo-docint:0.3.0` not
      existing at all.

      What is consequently still unknown: whether the image published under the old organization
      survived the rename. If it did not, the chart now published at 0.3.3 names an image that is
      not there -- the same failure shape this change set out to fix, one layer up. Answering it
      needs only `read:packages` and a `helm pull` or a package listing.

## 4. Release and verify

- [x] 4.1 Merge the branch to `main` once the gate is green, and delete the branch.
- [x] 4.2 Tag `chart-v0.3.3` and push it. Confirm the release workflow resolves `appVersion` to
      `0.3.0` from the existing `v0.3.0` image tag, skips the image job, and publishes only the
      chart.
- [ ] 4.3 BLOCKED, partially verified. `helm registry login ghcr.io` succeeds with the local token
      but `helm pull oci://ghcr.io/arulero/eugo-docint-chart --version 0.3.3` returns
      `403 denied` -- the token carries no `read:packages`, the same blocker as group 3. What the
      release log does prove: the chart packaged as `--version "0.3.3" --app-version "0.3.0"`,
      `helm lint` and both renders passed against the packaged artifact, and the push reported
      `Pushed: ghcr.io/arulero/eugo-docint-chart:0.3.3`
      `Digest: sha256:5e6844d3e21ea0282603abb1cfd81b147061da15d99c68326b26554e628e3f74`.
      The packaged `values.yaml` is verified at its source commit rather than in the published
      artifact. Complete this by granting the scope and re-running the pull.
- [x] 4.4 Gate run and green after the release: 222 passed, 0 failed, 7 skipped (the env-gated
      live suite, which self-skips and proves nothing). Archived 2026-09-11 with group 3 and 4.3
      explicitly accepted as unverified rather than silently closed.

## 5. Follow-up

- [x] 5.1 Open a separate proposal for the kind-based install smoke test recorded under design.md's
      Deliberately deferred, carrying forward the decision it must make: side-loading the built
      image with `kind load docker-image` proves the chart but not the default coordinate, whereas
      pulling proves both and needs a pull secret built from `GITHUB_TOKEN`. Note that the same test
      is the only thing that can catch the `readOnlyRootFilesystem` plus `/tmp` regression class.
