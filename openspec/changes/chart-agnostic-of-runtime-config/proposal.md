## Why

The chart refuses to render unless the release supplies both Foundry endpoints. The intent was
sound — catch a known misconfiguration at install time rather than as a crash-looping rollout —
but the mechanism is wrong: it makes the chart's *packaging* depend on the service's *runtime
wiring*, so the chart cannot be rendered, linted, or exercised by anyone who does not already know
about Foundry.

That cost is not hypothetical. Five `chart-lint` steps in `ci.yml` render without the endpoints and
every one of them fails today, so the assertions covering zero-vs-unset limits, scrape annotations,
the HPA memory metric, and the admission limits have all been dead since the gate was introduced.
The commit that added the gate said it had updated the renders that needed it; it updated three of
eight. A gate that silently disables the test suite guarding the rest of the chart is not paying
for itself.

Endpoints are runtime configuration. The chart should carry them when set and stay out of the way
when they are not.

## What Changes

- **BREAKING (operators):** rendering a release with no endpoint values now **succeeds** and
  produces a pod without those environment variables, where it previously failed with a message
  naming the missing value. A release that genuinely omits them now fails at pod start rather than
  at render.
- Both `required` gates on the Foundry endpoints are removed. Each endpoint renders its environment
  variable when set and is omitted when not — the same is-set contract every other optional value
  in the chart already follows.
- `image.repository` likewise loses its `required` gate and gains a default of
  `ghcr.io/eugo-as/eugo-docint`, so the chart renders with no values supplied at all.
- The `chart-lint` step asserting that a render *fails* without endpoints is inverted: it now
  asserts the render succeeds and that neither endpoint variable appears.
- The five `render()` helpers stop being broken without being touched — the behavior they assert
  becomes reachable again.
- `release.yml`'s packaged-chart validation no longer needs to supply endpoints.

Unchanged, and worth being explicit about: **the service still refuses to start without both
endpoints.** That boot check is the enforcement, and it is not weakened here. What changes is only
*where* a missing endpoint is reported — the pod rather than the operator's terminal.

**The frozen `/v1` contract is untouched.** No request or response shape changes and EuGo-Web has
nothing to do.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `foundry-configuration`: the requirement that the chart refuse to render a release omitting an
  endpoint is replaced by its inverse — the chart renders regardless, and carries each endpoint
  only when it is set. The service's own boot-time requirement is untouched.

## Impact

- `charts/eugo-docint/templates/deployment.yaml` — three `required` calls removed.
- `charts/eugo-docint/values.yaml` — the endpoint comments describing a mandatory value, and
  `image.repository`'s new default.
- `.github/workflows/ci.yml` — the endpoint-refusal step is inverted; the `$FOUNDRY` env var and
  its uses become unnecessary.
- `.github/workflows/release.yml` — the packaged-chart render no longer needs endpoint values.
- `README.md` — the configuration reference documents both endpoints as required by the chart.
- Chart patch version bump; `major.minor` still tracks the image and does not move.
- Sequencing: this lands before `publish-to-ghcr`, whose task 4.2 cannot be verified while
  `chart-lint` is red. The `ghcr.io/eugo-as/eugo-docint` default anticipates that change's
  coordinate; it is a default, overridable per release, and wrong only if that change is abandoned.
