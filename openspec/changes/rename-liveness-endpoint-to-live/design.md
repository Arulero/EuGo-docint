## Context

See `proposal.md` — Why. The state that shapes the approach:

`src/ServiceDefaults/Extensions.cs` is stock .NET Aspire service defaults, copied
verbatim from `../EuGo-mcp` and currently **byte-identical** to it. It holds two
private constants:

```
HealthEndpointPath    = "/health"
AlivenessEndpointPath = "/alive"
```

Those constants do two jobs. They are the paths `MapDefaultEndpoints()` would map —
a method this service deliberately does not call — and they are the paths the
OpenTelemetry ASP.NET Core trace filter excludes. `Program.cs` maps the routes
itself, with its own `HealthCheckOptions` per endpoint. Nothing enforces that the
routes and the constants agree; agreement is maintained by hand, and by
`TraceFilterTests`.

That agreement has failed before. The readiness route was `/healthz` while the
constant read `/health`, and because `StartsWithSegments` compares whole segments the
exclusion matched nothing — every readiness probe was traced for most of the
service's life, with no failing test and no changed response. The fix moved the route
to the constant. This change moves a route away from a constant, which is the same
operation that caused the bug.

The chart's `livenessProbe.httpGet.path` is the only other consumer of the path. The
service has no ingress, so nothing outside the cluster can reach either probe.

## Goals / Non-Goals

**Goals:**

- One spelling for one concept: the tag is `live`, the predicate reads
  `Tags.Contains("live")`, and the route becomes `/live`.
- Keep the constant, the route, the chart probe, and `/info` in agreement, enforced by
  a test rather than by care.
- Write the probe contract down, so the next path change has a specification to break
  rather than a comment to overlook.

**Non-Goals:**

- Renaming readiness. `/health` stays `/health` — see *Deliberately deferred*.
- Changing what either endpoint evaluates, its status codes, or its body.
- A compatibility window. `/alive` stops being served in the same release `/live`
  starts.
- Changing EuGo-mcp. It adopts the same scheme in its own change, later.

## Decisions

### Fork `AlivenessEndpointPath` in ServiceDefaults rather than only moving the route

The constant changes from `/alive` to `/live` alongside the route.

**Why:** the constant is what the trace filter excludes. Moving only the route leaves
the filter excluding a path nothing serves, which silently traces every liveness probe
— the exact `/healthz` bug, reintroduced. The alternative, keeping `/alive` in the
constant and adding `/live` to the filter separately, spreads one fact across two
places and is worse than the fork.

**Cost, accepted:** `Extensions.cs` stops being byte-identical to EuGo-mcp's copy.
That is a real loss — the identity is what makes "mirror EuGo-mcp" checkable with
`diff`. It is accepted because EuGo-mcp adopts `/live` later, restoring the identity,
and because the alternative is a known-recurring silent bug. Until then, a re-copy of
that file from a sibling repo or an Aspire template refresh would silently restore
`/alive` in the constant while `Program.cs` maps `/live`.

**What catches that:** `TraceFilterTests` already guards both directions, and needs no
new test — only its `InlineData` updated.

- `Every_excluded_path_is_a_route_that_exists` fails with a 404 if the constant moves
  and the route does not.
- Its own `Assert.False(_filter(...))` precondition fails if the route moves and the
  constant does not.

A half-finished rename cannot be green in either direction. This is why the existing
test suite is sufficient for the ServiceDefaults fork; the new coverage this change
needs is the `/alive` → 404 assertion and the chart render assertion.

### `MapDefaultEndpoints()` stays uncalled, and stays load-bearing

It maps both constants in Development. With the constants now at `/health` and
`/live`, restoring the call would double-register both paths and throw
`AmbiguousMatchException` at request time in Development. The existing comment in
`Program.cs` says this about `/alive`; it needs updating, not removing.

### Lockstep release; no dual-path compatibility window

`/alive` and `/live` are not both served. Image and chart ship together and roll out
together.

**Alternative considered:** map both paths for one minor, switch the chart to `/live`,
drop `/alive` next minor. It costs about three lines and removes the ordering
constraint entirely. Rejected: the service is cluster-internal with one consumer and a
single chart, the chart is versioned in this repo alongside the image, and a
deprecation window is a thing to remember to close. The ordering constraint is
accepted as a release-discipline cost rather than paid down in code.

### The spec covers the whole probe contract, not just the rename

The change could have specified only the new path. Instead `health-probes` captures
what each endpoint evaluates, the degraded-not-evicted property, the admission-control
exemption, and the trace-exclusion/route agreement. Those facts existed only in code
comments and in the historical `docs/superpowers/` designs, which are not updated by
rule. A path rename is the cheapest moment to write them down, and the
trace-exclusion requirement is the one that would have caught the original `/healthz`
bug as a specification violation rather than as an observation years later.

## Risks / Trade-offs

**A chart probing `/alive` meets an image serving `/live` → healthy pods
`CrashLoopBackOff`.** Liveness fails, `failureThreshold` is reached, the kubelet
restarts a pod that was working. → No mitigation in code; this is the accepted cost of
the lockstep decision. Mitigations that do apply: chart and image versions already move
together by repo convention, the spec makes the manifest-must-match-image requirement
explicit and testable, and rollback is symmetric — rolling back the image alone
reintroduces the mismatch, so **both** halves must roll back together.

**`helm lint`, `helm template`, the golden tests, and the Docker build all pass on a
mismatch.** Each half is individually valid. → The chart-render assertion in CI pins
the probe path, so the chart cannot drift from the spec silently. It still cannot see
the image, so it cannot prove the pair matches.

**A future verbatim re-copy of `Extensions.cs` from EuGo-mcp or an Aspire template
silently restores `/alive`.** → `TraceFilterTests` fails immediately with a 404 on the
excluded path. The window is one test run, not one release.

**Pod security context and chart volume mounts:** not touched by this change. The
`readOnlyRootFilesystem: true` setting and the writable `/tmp` mount are unchanged, so
no real-pod verification is required on that account. Should the change grow to touch
either, that part needs a real pod (`kind` + `helm install` + a BoM XLSX through
`/v1/extract`) — render and lint cannot see it, because the read-only root comes only
from the chart and both the golden tests and the Docker build run on a writable
filesystem.

## Migration Plan

1. Merge the code, chart, and doc changes as one branch, through the standard gate
   (`restore` → `build --no-restore` → `test --no-build` against `src/DocInt.slnx`).
2. Cut one `vX.Y.Z` tag — image and chart from the same commit. Chart `major.minor`
   equals the image's, per repo convention; `appVersion` is CI-stamped.
3. Roll out with `helm upgrade` using the new chart and the new image together. Do not
   pin the image independently of the chart for this release.
4. **Verify on the cluster, not only in CI:** after rollout, confirm pods reach `Ready`
   and that restart counts stay at zero across at least three liveness periods
   (30 s at `periodSeconds: 10`). A path mismatch shows up as restarts, not as a
   failed `helm upgrade`.
5. **Rollback:** `helm rollback` to the previous chart revision, which restores the
   previous image reference along with the `/alive` probe path. Rolling back the image
   alone, or the chart alone, recreates the mismatch this design accepts.

## Deliberately deferred

- **Renaming readiness to `/ready`.** `/health` names a category where the sibling
  names a role, so `/health` + `/live` is still asymmetric. Deferred because readiness
  carries the JSON dependency report, the explicit `ResultStatusCodes` map, and the
  `HealthEndpointPath` constant, and because a second probe rename doubles the
  lockstep exposure of this one for a purely cosmetic gain. If it is ever done, it
  belongs in the same release as another probe change, not on its own.
- **Kubernetes-idiomatic spellings (`/livez` + `/readyz`, or `/health/live` +
  `/health/ready`).** Both are defensible conventions — the first from the Kubernetes
  API server, the second from MicroProfile Health. Rejected in favour of `/live`
  because the motivating goal is agreement with the `live` tag already in the code, and
  neither alternative delivers that.
- **Renaming the health-check tag instead of the route.** Zero blast radius on the
  wire, but the tag is also stock Aspire, so it forks the same file with less to show
  for it — and it would leave the URL as the odd spelling out.
- **A dual-path deprecation window.** Considered and priced under *Decisions*; see the
  lockstep entry.
- **Changing EuGo-mcp in this change.** It serves `/health` + `/alive` in Development
  only, via `MapDefaultEndpoints()`, so it has no chart probe at stake and no urgency.
  Cross-repo changes in one branch are not this project's workflow.
