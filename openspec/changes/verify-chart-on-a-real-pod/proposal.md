## Why

Two regression classes in this repository can only be caught by running the chart on a real pod,
and CI currently catches neither.

The first has already shipped once. The pod runs `readOnlyRootFilesystem: true`, and OpenXML spills
package parts to `/tmp` even though `SpreadsheetEngine` reads from a `MemoryStream`, so removing the
`/tmp` mount breaks every XLSX request on every deployment. It did, until 2026-08-06. The read-only
root comes only from the chart, so the golden tests and the Docker build both pass on a writable
filesystem, and `helm lint` and `helm template` see nothing wrong. The existing `chart-lint`
assertions check that the mount is present in the rendered YAML, which is a check on the text rather
than on the behaviour it is supposed to guarantee.

The second shipped in September 2026. When the GitHub organization was renamed, the chart's default
image coordinate named a namespace that no longer existed, so the chart's default install could not
pull at all - and CI stayed green, because a render is equally happy with a live coordinate and a
dead one. `retarget-ghcr-namespace` corrected the value and deliberately did not add a string
assertion to guard it, on the grounds that the mature charts surveyed do not use one: what
`argoproj/argo-helm` and `kubernetes/ingress-nginx` run instead is `ct install` against a kind
cluster, which pulls the image for real and therefore catches a bad coordinate as a failed pull.

Both gaps have the same shape - the chart is verified as text, never as a running workload - and one
test closes both.

## What Changes

- CI gains a job that stands up a kind cluster, installs the chart, waits for the pod to become
  ready, and puts a BoM XLSX through `POST /v1/extract`, asserting typed numeric cells come back.
  The XLSX path is the one that exercises `/tmp` under a read-only root, which is why it is the
  request the test makes rather than a health probe.
- The job runs on changes that touch the chart, the Dockerfile, or the engines.

## Capabilities

### New Capabilities

- `chart-deployment-verification`: that the chart is proven by running it, covering the pod's
  filesystem constraints and its ability to obtain the image it names by default.

### Modified Capabilities

None.

## Impact

- `.github/workflows/ci.yml` - a new job
- No application code, no chart templates, no contract change. `/v1` is untouched.

## Open decision this change must make

Whether the kind test pulls the image or side-loads it.

- `kind load docker-image` on the image CI already builds is faster, needs no credential, and works
  on a fork. It proves the pod's behaviour, including the `/tmp` regression class - but it overrides
  `image.repository`, so it proves nothing about the default coordinate, and would not have caught
  the September 2026 breakage.
- Pulling from GHCR needs an `imagePullSecret` built from `GITHUB_TOKEN`, and exercises the default
  coordinate end to end. It is the only variant that closes the second gap.

These are not exclusive: the cheap side-loaded install can run on every relevant change, and a
pulling install can run on a narrower trigger. The design should say which triggers which, and must
not assume a side-loaded test covers the coordinate.

One live unknown makes the pulling variant more valuable than it looks. `retarget-ghcr-namespace`
closed without establishing whether the `v0.3.0` image survived the organization rename - the
verification needed a `read:packages` scope that was not granted, and the chart release that
followed could not answer it, because `appVersion` resolution reads git tags rather than the
registry. So the chart currently published at 0.3.3 names an image nobody has confirmed exists.
A pulling install test would settle that on its first run.
