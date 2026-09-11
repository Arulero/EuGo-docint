## Context

See proposal.md - Why for the motivation.

Two prior decisions shape the approach. `chart-agnostic-of-runtime-config` gave the chart a
default `image.repository` at all, and made it fully qualified on purpose: a bare `eugo-docint`
resolves against Docker Hub and pulls something unrelated, which fails later and less clearly than
a wrong registry does. `publish-to-ghcr` then fixed the coordinate that default points at. Both
reasons still hold; only the namespace segment inside the string is now wrong.

The structural weakness is that the chart's default coordinate and the coordinate `release.yml`
publishes to are two independent strings and nothing compares them. `helm lint` and `helm template`
render a dead coordinate exactly as happily as a live one, which is why an organization rename
could break every default install while CI stayed green.

This change touches neither the pod security context nor the chart's volume mounts, so the
real-pod verification rule does not apply to it. The `/tmp` emptyDir and `readOnlyRootFilesystem`
are untouched.

## Goals / Non-Goals

**Goals:**

- The chart's default image coordinate addresses the namespace the release workflow actually
  publishes to.
- The chart version moves according to the versioning contract, so the fix is releasable.
- The reasoning behind keeping the current values shape is recorded, so the alternative is not
  re-derived next time a name changes.

**Non-Goals:**

- Renaming anything. The image, the chart package, the rendered resources, the repository and the
  project keep their names.
- Restructuring how the chart expresses image source.
- Building the missing verification. The gap is real and is recorded below rather than closed here.

## Decisions

### Keep a single fully-qualified `image.repository` string

The alternative was to break the coordinate into named parts so that the segment that just proved
volatile - the organization slug - becomes a first-class value rather than a substring.

A survey of how mature charts express image source, taken 2026-09-11 by pulling the published
charts rather than reading documentation:

| Chart | Shape |
| --- | --- |
| argo-cd 10.9.0 | `global.image.repository: quay.io/argoproj/argocd`, one fully-qualified string, `tag: ""` meaning appVersion |
| kube-prometheus-stack 90.1.0 | `image.registry` + `image.repository` + `tag` + `sha`, with `global.imageRegistry` |
| ingress-nginx 4.15.1 | `controller.image.image` + `tag` + `digest`, registry lifted to `global.image.registry` |
| bitnami/nginx 25.1.11 | `image.registry` + `image.repository` + `tag` + `digest`, with `global.imageRegistry` |
| cert-manager v1.21.2 | `imageRegistry` + `imageNamespace` + `image.name`, with `image.repository` as a full override that takes precedence |

Both shapes are mainstream. The split exists to serve a need this chart does not have: those charts
deploy several images each, often mirrored wholesale into a customer's own registry, and a single
`global.imageRegistry` lets one value redirect all of them at once. This chart ships exactly one
image, and the closest exemplar - argo-cd, also one image, also `tag: ""` meaning appVersion -
carries exactly the string this chart already has.

The split would have made this particular edit a one-line value change, which is a real but narrow
benefit: it optimizes for a company rename, which has happened once. Against it, the split adds a
template branch, more render assertions, a longer values table, and a documented failure mode -
cert-manager deprecated its per-component `image.registry` prefix precisely because prepending
produced double-registry references such as `legacy.example.io/quay.io/jetstack/...`. Keeping one
string also keeps `image.repository` overridable in one `--set`, which is how every local, mirrored
or air-gapped install of this chart is documented to work today.

### Do not add a CI assertion comparing the default to the publish coordinate

The obvious guard is to derive `ghcr.io/${GITHUB_REPOSITORY_OWNER,,}/eugo-docint` in `chart-lint`
and assert `values.yaml` carries it, which would make this class of breakage impossible.

It was rejected after checking whether anyone does it. Neither `argoproj/argo-helm` nor
`kubernetes/ingress-nginx` contains any assertion comparing a chart's default image coordinate to a
publish coordinate. What they run instead is `ct install` against a kind cluster on every chart
change, which pulls the image for real - a wrong coordinate surfaces as a failed pull rather than a
failed string match, and the same test also catches everything else a render cannot.

A string assertion would therefore buy a narrow guarantee while establishing a check nobody else
finds necessary, and it would sit in the way of the install test that supersedes it. The gap is
recorded under Deliberately deferred instead of half-closed here.

One asymmetry is worth recording, because it will be tempting to re-derive: argo-helm and the
argo-cd image publish from different repositories, so those charts have no derivable value and must
hardcode. This repository publishes image and chart from one workflow, so deriving is possible here
in a way it is not for them. The decision above is a judgement about value, not about feasibility.

### Move the chart patch version, not the minor

A `values.yaml` edit is a chart change, and under the versioning contract the chart's patch digit
is chart-owned while `major.minor` tracks the image. 0.3.2 becomes 0.3.3 and releases on a
`chart-v0.3.3` tag. That tag resolves its `appVersion` by finding the highest existing `v0.3.*`
image tag; `v0.3.0` exists, so it resolves. `appVersion` stays CI-stamped and is not hand-edited.

### Change no release-workflow logic

`release.yml` computes `owner=${GITHUB_REPOSITORY_OWNER,,}` independently in both the image job and
the chart job, so both publishes already resolve to `arulero` with no edit. Only a comment naming
the old organization's spelling is stale; the lowercasing it describes remains necessary, because
`Arulero` also carries a capital and an OCI reference admits none.

## Risks / Trade-offs

- The same class of breakage can recur on a future rename, since nothing compares the default to
  the publish coordinate -> accepted deliberately; the install test under Deliberately deferred is
  the intended closure, and this document records why a string assertion was not used as a stand-in.
- The previously published `v0.3.0` image and chart may or may not have survived the organization
  rename at their new coordinates -> unverified, see Open Questions; the corrected coordinate is
  right either way, and the contingency is a re-release rather than a different design.
- An operator who pinned `image.repository` to the old coordinate in their own values file is
  unaffected by this change and still broken -> the coordinate appears in the README, which is
  corrected here; there are no known deployed releases to migrate.

## Migration Plan

No deployed release is known to exist, so there is nothing to upgrade. For any release that does
exist, the change is a pure value edit: `helm upgrade` to chart 0.3.3 replaces the image reference
in the pod template and triggers an ordinary rollout. No resource name, label or selector changes,
so no resource is recreated. Rollback is `helm rollback`, or pinning `image.repository` explicitly.

## Deliberately deferred

- **Splitting the image coordinate into registry / namespace / name (the cert-manager shape).**
  Rejected above on the grounds that this chart ships one image and the split serves multi-image
  mirroring. Worth revisiting only if the chart grows a second image, or if the organization slug
  changes a second time - two renames would make the volatility a pattern rather than an event.
- **A kind-based install smoke test in CI.** This is the guard the mature charts actually run, and
  it would catch a wrong image coordinate by failing the pull. It would also close a gap this
  repository has already written down: the `readOnlyRootFilesystem` plus `/tmp` emptyDir pairing
  can only be verified on a real pod, because the read-only root comes from the chart alone and
  both the golden tests and the Docker build pass on a writable filesystem. Deferred to its own
  change because it is a CI capability rather than a coordinate fix, and because it needs a
  decision this change should not pre-empt: a kind test that side-loads the locally built image
  with `kind load docker-image` proves the chart but not the default coordinate, whereas one that
  pulls needs a pull secret built from `GITHUB_TOKEN`. Only the pulling variant would have caught
  this bug.
- **Automated dependency bumping (Renovate or Dependabot) for the chart's image reference.** Every
  chart surveyed uses one. Out of scope here, where the version already tracks `appVersion` and the
  coordinate is not a dependency in that sense.
- **Adding a requirement to `release-publication` fixing the coordinate's value.** Specs describe
  behavior, and a literal registry namespace is configuration. Writing it into a spec would make
  every future rename a spec change for no verification benefit.

## Open Questions

- Did the `v0.3.0` image and the `eugo-docint-chart` 0.3.2 package move with the organization to
  `ghcr.io/arulero/*`? **Closed unanswered on 2026-09-11**, by decision, not by evidence. It was not
  verifiable from this workstation: both namespaces answer 403 to an anonymous registry probe
  because the packages are private, the available GitHub token lacks `read:packages`, and
  `gh auth refresh` needs an interactive device flow.

  One correction to what this section originally assumed. It said the question "only determines
  whether a re-release is also needed" and could safely ride along with the release verification.
  The first half holds; the second does not. The `chart-v0.3.3` release went green without touching
  the registry's image side at all, because `appVersion` resolution reads `git tag -l "v0.3.*"`.
  A successful chart release is therefore not evidence that `ghcr.io/arulero/eugo-docint:0.3.0`
  exists, and the published 0.3.3 chart names an image nobody has confirmed is there.

  This is cheap to settle whenever someone has `read:packages`: list the organization's container
  packages, or `helm pull` the chart and `docker manifest inspect` the image it names. It is also
  settled automatically the first time anything installs the chart for real, which is one more
  reason the install test in `verify-chart-on-a-real-pod` is worth having.
