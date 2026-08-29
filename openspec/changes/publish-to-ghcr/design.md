## Context

See proposal.md — Why. Three facts about the current state shape everything below.

The release workflow already has the right *shape*: tag triggers, a build-test gate, image before
chart, version-pairing checks, and a render of the packaged `.tgz` before push. Only the registry
and its authentication are wrong. This is a repoint, not a rewrite.

Nothing has ever shipped. `git tag -l` is empty, so `release.yml` has never executed a single time.
There is no installed release to migrate and no published artifact to keep reachable — but equally,
there is no evidence any of this works. The first tag push is simultaneously the first release and
the first test.

The chart was designed against ACR's pull model. Its plan records "No secrets anywhere: Workload
Identity only, no API-key values, no `imagePullSecrets`" — a decision that held because the AKS
kubelet's managed identity authenticates ACR pulls with no secret in the cluster. GHCR offers no
equivalent. That single fact is what forces a chart change rather than a workflow-only change.

## Goals / Non-Goals

**Goals:**

- Publishing works from this repository's own credentials, with nothing provisioned first.
- The chart's published name and its rendered Kubernetes identity are decoupled, so the former can
  change without touching the latter.
- Registry coordinates are correct by construction rather than by an action's side effect.
- Credential material stays out of the chart entirely, even though pulls now need a credential.

**Non-Goals:**

- Automating cluster-side secret creation. The pull secret is created out of band, by hand.
- Deploying to a cluster. Release execution remains EuGo-infra's.
- Changing the pod security context or the chart's volume mounts. Neither is touched here — see
  *Verification that needs a real pod*.
- Retaining an ACR path in parallel. ACR leaves the repository completely.

## Decisions

### The chart is renamed, and the name helper is pinned to a literal

`helm push` accepts a registry namespace, not a target repository name; the repository is the
chart's own `name:` read from the `Chart.yaml` inside the package. Publishing to
`ghcr.io/eugo-as/eugo-docint-chart` therefore *requires* `name: eugo-docint-chart`. The name cannot
be overridden at push time.

But `.Chart.Name` also flows through `_helpers.tpl` into `app.kubernetes.io/name`, which appears in
the Deployment's `spec.selector.matchLabels` — an immutable field. Renaming the chart naively would
rename every rendered resource after its packaging format and make the selector unchangeable at
that new value.

So: rename `Chart.yaml`, and change `eugo-docint.name` to return the literal `eugo-docint` instead
of `.Chart.Name`. The `helm.sh/chart` label continues to read `.Chart.Name`-`.Chart.Version` and
will read `eugo-docint-chart-0.3.0`, which is correct — that label names the artifact, and the
artifact genuinely is the chart.

*Alternatives considered.* Pushing to a `charts/` namespace prefix (`ghcr.io/eugo-as/charts/eugo-docint`)
needs no rename at all, but puts the chart somewhere other than beside the image. Renaming without
pinning the helper is simpler by two lines and was rejected on the immutable-selector grounds above.
Pushing the chart into the image's own repository was rejected outright: the versioning contract
fixes chart and image to the same major and minor, so `0.3.0` would be a live tag collision in which
one artifact silently replaces the other.

### One lowercasing step, used by both publishes

The owning organization is spelled `EuGo-AS`. OCI references must be lowercase. `docker/metadata-action`
lowercases its `images:` input silently, which is how EuGo-mcp gets away with
`ghcr.io/${{ github.repository }}` — but the chart push has no such helper, and
`${{ github.repository_owner }}` expands verbatim. A naive port produces `oci://ghcr.io/EuGo-AS/…`
and fails *after* the image has already been published, which is precisely the half-published state
the existing workflow's own comments worry about.

A single early step computes the lowercased owner and exports it; both the image tag and the chart
namespace are built from that. Neither publish depends on the other having normalized anything.

*Alternative considered.* Adopting `docker/metadata-action` for the image half to mirror EuGo-mcp,
and lowercasing only for the chart. Rejected: it makes the two halves work differently for no gain,
and it puts the correctness of one publish inside an action's undocumented-by-us behavior. A
hardcoded literal was also considered and rejected as a second place for the org name to drift.

### The chart references pull secrets by name and never holds one

`values.yaml` gains `imagePullSecrets: []`, a list of names of secrets that must already exist in
the namespace. The Deployment renders `spec.imagePullSecrets` only when the list is non-empty. The
chart never creates a Secret and accepts no credential material as a value.

This preserves the *substance* of the original "no secrets anywhere" rule — nothing sensitive enters
a values file or a rendered manifest — while admitting the reference that a private registry makes
unavoidable. It is a deliberate, documented reversal of a documented decision, not an oversight.

Leaving the list empty must keep rendering, because a cluster may obtain the image some other way
and because every existing values permutation must continue to work.

### Pull credential: a classic PAT, created by hand, per namespace

The cluster authenticates GHCR pulls with a `kubernetes.io/dockerconfigjson` secret built from a
classic personal access token carrying `read:packages`:

```
kubectl create secret docker-registry ghcr \
  --docker-server=ghcr.io \
  --docker-username=<github-user> \
  --docker-password=<classic-PAT> \
  -n <namespace>
```

Classic over fine-grained: fine-grained tokens expire in at most a year, and a silent
`ImagePullBackOff` months after everyone has forgotten the release is a worse failure than the
broader scope. Manual over automated: creating it needs a token that must not live in this
repository, and generating it from CI would mean storing exactly the credential this design keeps
out. The step is documented in the README and belongs to whoever provisions the namespace.

### Azure leaves the release path entirely

Deleted: both `azure/login` steps, both `az acr login` steps, `permissions: id-token: write`, and
the four repository variables (`ACR_NAME`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`). Added: `permissions: packages: write`, `docker/login-action` against
`ghcr.io` with `GITHUB_TOKEN`, and `helm registry login ghcr.io` with the same token — Helm and
Docker are logged in separately rather than assuming a shared credential store.

The image build carries an `org.opencontainers.image.source` label so GHCR links the package to
this repository; without the link, package permissions have to be granted by hand.

## Risks / Trade-offs

**Every publish path is unproven; the first tag is the first test.** → Cut `v0.3.0-rc.1` first. The
version-pairing check derives major.minor by truncation, so a prerelease suffix passes it, and a
prerelease is excluded from any moving tag. Inspect both packages before cutting `v0.3.0`.

**A chart-push failure on a `v*` tag leaves a published image with no chart.** → Unchanged from
today, and accepted: the lowercasing decision above removes the most likely new cause, and the
packaged chart is still rendered before either artifact is pushed. Re-running the job after a fix
republishes both.

**A classic PAT is broad and, if made non-expiring, permanent.** → Accepted deliberately for the
failure-mode reason above. Scope it to `read:packages` only, and prefer a machine account over a
person's token so revocation is not tangled with anyone's employment.

**GHCR package visibility is a setting, not a workflow output.** → Both packages must be confirmed
private in package settings after the first publish. The workflow cannot assert this, so it is a
one-time manual check alongside the pull-secret step.

**The chart is private too, so `helm install` from the OCI URL now needs a login.** → EuGo-infra's
release execution gains a `helm registry login ghcr.io` step. Worth flagging to that repo rather
than discovering it at deploy time.

**`app.kubernetes.io/name` is now a literal, decoupled from `.Chart.Name`.** → A future reader may
"fix" the helper back to `.Chart.Name` and silently change the Deployment's immutable selector. The
spec pins the behavior and the helper carries a comment saying why.

## Verification that needs a real pod

The pod security context and the chart's volume mounts are **not** changed by this design — the
read-only root filesystem and the writable `/tmp` mount that XLSX extraction depends on are
untouched. No real-pod verification is required on that account.

One thing here genuinely cannot be proven by `helm lint` or `helm template`: whether a private
image actually pulls. Rendering shows the `imagePullSecrets` field is present and well-formed; it
says nothing about whether the token authenticates or the package is reachable. That needs a
cluster, the secret created as above, and a pod reaching `Running` — and it should be checked
before anyone depends on this path.

## Deliberately deferred

- **Automating pull-secret creation from EuGo-infra.** The right long-term home, but it needs a
  decision about where the token lives and how it rotates, which is that repository's to make.
  Documented manual step now.
- **A GitHub App installation token instead of a PAT.** Short-lived and properly scoped, but it
  needs a refresher running in-cluster to renew the secret hourly. Real infrastructure for a
  problem a token solves today.
- **Making the image public.** Would eliminate the pull secret, the PAT, and the rotation risk
  entirely, and docint holds nothing sensitive. Rejected as a posture decision, not a technical
  one; revisit if the pull secret becomes an operational burden.
- **Adopting `docker/metadata-action` and its moving `latest` / `X.Y` tags.** EuGo-mcp publishes
  them; docint publishes only the exact version today. Worth aligning, but it is a tag-policy
  change that deserves its own decision rather than riding along on a registry move.
- **Keeping an ACR path in parallel for when EuGo-infra's `aks/` stack lands.** Rejected: two
  publish targets doubles the surface that can half-fail, for a registry with no delivery date.
  If ACR arrives, moving back is a smaller change than this one — the workflow shape survives.
- **Signing or attesting published artifacts** (cosign, provenance, SBOM). Genuinely valuable and
  entirely orthogonal; adding it here would obscure whether a failure came from the registry move
  or the signing step.
