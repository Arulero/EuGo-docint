## Why

The release pipeline publishes to an Azure Container Registry that does not exist. EuGo-infra's
`aks/` stack, which owns the ACR, is still scaffolding, and `release.yml` has consequently never
run — the repository has no tags at all. Every other EuGo repository already publishes to the
GitHub Container Registry under the `EuGo-AS` organization, and EuGo-mcp chose it for the reason
that applies here too: the built-in `GITHUB_TOKEN` authenticates it, so there is nothing to
provision and nothing to configure before the first release.

Moving to GHCR makes the pipeline runnable today, on the repository's own credentials, and removes
a dependency on infrastructure that has no delivery date.

## What Changes

- **BREAKING (operators, not callers):** the image and the chart are published to
  `ghcr.io` instead of `<acr>.azurecr.io`. An existing install's `image.repository` and the
  `helm` source URL both change. No release has ever shipped, so nothing is actually in flight.
- The image is published to `ghcr.io/eugo-as/eugo-docint`; the chart to
  `ghcr.io/eugo-as/eugo-docint-chart`, beside it rather than under a `charts/` prefix.
- Both packages are **private**. Because GHCR has no equivalent of ACR's kubelet managed-identity
  pull, the chart gains an `imagePullSecrets` value naming secrets it does not create. This
  reverses the chart's original "no `imagePullSecrets`" decision, which held only because ACR
  made it unnecessary.
- The Helm chart is renamed to `eugo-docint-chart` so `helm push` targets the intended
  repository, while the Kubernetes resource names and labels it renders stay `eugo-docint`.
- Azure is removed from the release path entirely: no `azure/login`, no `az acr login`, and none
  of the four repository variables (`ACR_NAME`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID`) the README currently lists as release prerequisites. Publishing
  becomes prerequisite-free.
- Registry coordinates are lowercased explicitly rather than relying on an action to do it.

Unchanged: the tag triggers (`v*`, `chart-v*`), the build-test gate, the image-then-chart
ordering, the multi-arch build, the chart/image version-pairing rules, and the render-the-package
validation before push.

**The frozen `/v1` contract is untouched.** No request or response shape changes, no error
vocabulary changes, and EuGo-Web has nothing to do. This change is entirely about how a built
artifact reaches a cluster.

## Capabilities

### New Capabilities
- `release-publication`: what pushing a version tag publishes, where those artifacts live, how the
  image and chart versions stay paired, and what the chart must render for a cluster to pull a
  private image.

### Modified Capabilities
<!-- None. No existing capability's requirements change: foundry-configuration governs the
     service's credential surface and spreadsheet-extraction its XLSX behavior, and neither is
     affected by where the image is hosted. -->

## Impact

- `.github/workflows/release.yml` — registry, authentication, and the lowercase handling.
- `.github/workflows/ci.yml` — the `chart-lint` job renders a chart whose name changes.
- `charts/eugo-docint/Chart.yaml` — `name` becomes `eugo-docint-chart`.
- `charts/eugo-docint/templates/_helpers.tpl` — the name helper is pinned to the literal
  `eugo-docint` so the rename cannot reach `app.kubernetes.io/name`, which is part of the
  Deployment's immutable selector.
- `charts/eugo-docint/templates/deployment.yaml`, `values.yaml`, `ci/test-values.yaml` — the
  `imagePullSecrets` surface.
- `README.md`, `CLAUDE.md`, `openspec/config.yaml` — all three describe the delivery path as
  ending in ACR.
- `charts/eugo-docint/values.yaml` already defaults `image.repository` to
  `ghcr.io/eugo-as/eugo-docint`: `chart-agnostic-of-runtime-config` needed a default in order to
  drop that value's `required` gate, and took this change's coordinate. So the chart already names
  the registry it will be published to, and this change makes that name real rather than
  introducing it.
- Cluster operators gain one manual step: creating a `docker-registry` secret from a classic
  personal access token with `read:packages`, per namespace.
- `docs/superpowers/` specs are **not** updated. They record why ACR was chosen, which is the
  context a reader needs to understand this reversal.
