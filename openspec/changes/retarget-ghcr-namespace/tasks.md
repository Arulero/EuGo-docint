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

- [ ] 3.1 Grant the local token the scope needed to see packages: `gh auth refresh -s read:packages`.
- [ ] 3.2 List the organization's container packages (`gh api "/orgs/Arulero/packages?package_type=container"`)
      and record whether `eugo-docint` and `eugo-docint-chart` are present, and which versions they
      hold. This answers design.md's Open Question.
- [ ] 3.3 If the image `v0.3.0` did not survive the rename, record in the change what re-release is
      needed - a `v0.3.0` re-tag would fail as an existing tag, so this becomes a decision about
      cutting a new image version, not a silent retry. If it did survive, note that no re-release is
      needed and continue.
- [ ] 3.4 If the `read:packages` scope cannot be granted, do not skip this group: record explicitly
      that package inventory was blocked and why, and carry 3.2 and 3.3 into the release
      verification in group 4, where the publish result answers the same question directly.

## 4. Release and verify

- [ ] 4.1 Merge the branch to `main` once the gate is green, and delete the branch.
- [ ] 4.2 Tag `chart-v0.3.3` and push it. Confirm the release workflow resolves `appVersion` to
      `0.3.0` from the existing `v0.3.0` image tag, skips the image job, and publishes only the
      chart.
- [ ] 4.3 Pull the published chart and prove the fix shipped:
      `helm registry login ghcr.io`, then
      `helm pull oci://ghcr.io/arulero/eugo-docint-chart --version 0.3.3`, and confirm the packaged
      `values.yaml` carries `ghcr.io/arulero/eugo-docint` and `Chart.yaml` carries
      `appVersion: 0.3.0`.
- [ ] 4.4 Run the gate as in 1.5 and archive the change.

## 5. Follow-up

- [ ] 5.1 Open a separate proposal for the kind-based install smoke test recorded under design.md's
      Deliberately deferred, carrying forward the decision it must make: side-loading the built
      image with `kind load docker-image` proves the chart but not the default coordinate, whereas
      pulling proves both and needs a pull secret built from `GITHUB_TOKEN`. Note that the same test
      is the only thing that can catch the `readOnlyRootFilesystem` plus `/tmp` regression class.
