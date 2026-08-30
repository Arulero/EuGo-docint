## Why

The liveness probe is exposed at `/alive`, but the concept it selects on is spelled
`live` everywhere else in the service: the health check is registered with the tag
`live`, and the endpoint's predicate reads `r.Tags.Contains("live")`. One word, two
spellings, for no reason other than that `/alive` is the stock .NET Aspire default.

The probe contract is also unspecified. Which checks each endpoint evaluates, what
each returns while a dependency is down, and the fact that the route paths are
coupled to the OpenTelemetry trace filter all live in code comments and in the
historical `docs/superpowers/` designs. That coupling has already failed once in
production-shaped form — for most of the service's life the readiness route was
`/healthz` while the trace filter excluded `/health`, so every readiness probe was
traced. Renaming a probe path is exactly the operation that breaks it again, which
makes this the right moment to write the contract down.

## What Changes

- The liveness endpoint moves from `GET /alive` to `GET /live`. Body, status codes,
  and the set of checks it evaluates are unchanged.
- `AlivenessEndpointPath` in `src/ServiceDefaults/Extensions.cs` changes from
  `/alive` to `/live`, so the OpenTelemetry trace filter keeps excluding the route
  that is actually served. This forks the file from its byte-identical copy in
  EuGo-mcp; EuGo-mcp adopts the same scheme later.
- The Helm chart's `livenessProbe.httpGet.path` moves to `/live`, and the chart's
  `NOTES.txt` follows.
- `GET /info` advertises `/live` in place of `/alive`.
- Chart and image ship **lockstep**: no compatibility window, `/alive` stops being
  served the moment `/live` starts. A chart that probes `/alive` against an image
  that serves `/live` puts a healthy pod into `CrashLoopBackOff`, and neither
  `helm lint`, `helm template`, the golden tests, nor the Docker build can see it.
- The probe contract is captured as a spec: which checks each endpoint evaluates,
  what each answers while a dependency is unreachable, and the requirement that
  every trace-excluded path is a route that exists.

**BREAKING** for any deployment that probes `/alive` — in practice the Helm chart
in this repo, which is updated in the same change. It is **not** a change to the
frozen `/v1` contract: `POST /v1/extract` is untouched, and EuGo-Web has nothing
to do. Nothing outside the cluster can reach either probe; the service is never
exposed via ingress.

## Capabilities

### New Capabilities
- `health-probes`: the liveness and readiness endpoints — their paths, the checks
  each evaluates, their responses while a dependency is unreachable, the
  requirement that a dependency outage never evicts a pod, and the agreement
  between trace-excluded paths and served routes.

### Modified Capabilities

None. No existing spec under `openspec/specs/` describes the probe endpoints.

## Impact

| Area | Effect |
| --- | --- |
| `src/DocInt.Api/Program.cs` | Route `/alive` → `/live`; `/info` endpoint list; the comment explaining why the paths must match the ServiceDefaults constants. |
| `src/ServiceDefaults/Extensions.cs` | `AlivenessEndpointPath` constant. Diverges from EuGo-mcp until that repo follows. |
| `charts/eugo-docint` | `templates/deployment.yaml` liveness probe path; `templates/NOTES.txt`. |
| `tests/DocInt.Tests` | `HealthEndpointsTests`, `TraceFilterTests` — the latter is the existing guard that catches a half-finished rename in either direction. |
| `README.md` | Two probe-path references. |
| Deployment | Image and chart must be released and rolled out together. |
| EuGo-Web / EuGo-mcp | No action. EuGo-mcp adopts the same scheme in its own change, later. |
| `docs/superpowers/` | Not updated — historical records by project rule. |
