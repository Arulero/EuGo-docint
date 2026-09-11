## Why

The company was renamed to Arulero and the GitHub organization moved with it. The old
organization is gone, not redirected: `api.github.com/orgs/eugo-as` answers 404, and the
repository's own remote is already `https://github.com/Arulero/EuGo-docint.git`.

The Helm chart's default image coordinate is still `ghcr.io/eugo-as/eugo-docint`. That names a
registry namespace that no longer exists, so a `helm install` that does not override
`image.repository` renders a pod that can never pull its image. The default is fully qualified on
purpose, which is what makes it wrong on its own rather than merely incomplete.

Nothing in CI catches this. `helm lint` and `helm template` render a dead coordinate exactly as
happily as a live one, and the chart's default and the coordinate `release.yml` publishes to are
two independent strings that nothing compares.

## What Changes

- The chart's default `image.repository` becomes `ghcr.io/arulero/eugo-docint`, and the CI render
  values carrying the same string follow it.
- The chart's patch version moves 0.3.2 -> 0.3.3, because a `values.yaml` edit is a chart change
  and the patch digit is chart-owned. It releases on a `chart-v0.3.3` tag, which pairs against the
  already-existing `v0.3.0` image tag.
- Documentation quoting the old coordinate is corrected, and a release-workflow comment naming the
  old organization spelling is brought up to date. The lowercasing mechanism that comment describes
  is unchanged and still load-bearing: `Arulero` also carries a capital, and an OCI reference
  admits none.

Not changing, deliberately:

- **No name changes.** The repository, the project, the published image (`eugo-docint`), the
  published chart (`eugo-docint-chart`) and every rendered Kubernetes resource name keep the names
  they have. Only the registry namespace segment moves. In particular
  `app.kubernetes.io/name: eugo-docint` is untouched, so no Deployment selector is disturbed and no
  consumer's in-cluster URL changes.
- **No release-workflow logic changes.** `release.yml` already derives its registry namespace from
  `${GITHUB_REPOSITORY_OWNER,,}` for both the image and the chart push, so both publishes resolve
  to `arulero` without edits.
- **No values-schema change.** The single fully-qualified `image.repository` string is kept rather
  than split into registry/namespace/name parts. See design.md for the survey that settled this.

This change does not touch the frozen `/v1` contract. EuGo-Web has nothing to do in response.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. The change sets `skip_specs: true`.

`openspec/specs/release-publication` is deliberately namespace-agnostic: it constrains what a tag
publishes, that the image and chart occupy distinct repositories, that coordinates are lowercase,
and that versions stay paired -- never what any coordinate's value is. Its "Registry coordinates
are lowercase" requirement describes a mechanism that continues to hold unchanged under the new
organization name. Correcting a coordinate's value changes no requirement, and no requirement was
invented here to give the change a delta.

## Impact

- `charts/eugo-docint/values.yaml` -- the default image coordinate
- `charts/eugo-docint/ci/test-values.yaml` -- the same string, used by CI renders
- `charts/eugo-docint/Chart.yaml` -- chart patch version
- `README.md`, `CLAUDE.md` -- documented coordinates
- `.github/workflows/release.yml` -- one stale comment, no logic

Out of scope and untouched: `docs/superpowers/**` and `openspec/changes/archive/**`, which are
historical records that were accurate when written; the golden fixtures containing the old company
name, which `RedactionTests` asserts against by exact string; the `EuGo.DocInt` meter name; the
`/info` and `/` service banner; and `model-eugo-docint-vision`, which is an Azure-side deployment
alias owned outside this repository.
