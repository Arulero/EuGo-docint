# health-probes Specification

## Purpose

Defines the two probe endpoints an orchestrator uses to decide whether the service
should receive traffic and whether it should be restarted, and the separation
between those two decisions that keeps an unreachable dependency from restarting a
healthy pod.

## Requirements

### Requirement: Liveness endpoint

The service SHALL serve a liveness probe at `GET /live`. It SHALL evaluate only the
service's own responsiveness — never the reachability of any external dependency —
and SHALL answer `200 OK` with the plain-text body `Healthy` whenever the process is
able to serve requests at all.

The service SHALL NOT serve a liveness probe at any other path. `/alive` is not
served.

#### Scenario: Liveness answers on the running service

- **WHEN** a client issues `GET /live`
- **THEN** the response is `200 OK` with the plain-text body `Healthy`

#### Scenario: The former path is gone

- **WHEN** a client issues `GET /alive`
- **THEN** the response is `404 Not Found`

### Requirement: Readiness endpoint

The service SHALL serve a readiness probe at `GET /health` that evaluates every
registered check, including the reachability of each configured Azure dependency,
and SHALL return a JSON body carrying an aggregate `status` and a `checks` array. Each
entry SHALL carry the check's `name` and `status`, and, where known, the dependency
`endpoint`, the UTC timestamp of the last reachability observation, and a one-line
`reason` when the check is not healthy.

The body SHALL NOT contain document content. Only fixed literals, configured Azure
hostnames, and failure reasons appear in it.

#### Scenario: Every dependency surface is reported

- **WHEN** a client issues `GET /health` on a service with both Azure endpoints configured
- **THEN** the response is `200 OK` and the `checks` array names the service's own
  responsiveness check and one entry per configured Azure dependency

### Requirement: A dependency outage degrades readiness without evicting the pod

An unreachable dependency SHALL be reported on the readiness endpoint and SHALL NOT
change the liveness answer. Readiness SHALL return `200 OK` while a dependency is
degraded, and SHALL return `503 Service Unavailable` only when the aggregate status is
unhealthy.

Restarting the process cannot restore an external dependency; a restart during an
outage adds a cold start to it. This separation is the reason the two endpoints
exist, and it SHALL hold regardless of which dependency is unreachable.

#### Scenario: Readiness reports the unreachable dependency

- **WHEN** a configured Azure dependency is unreachable and a client issues `GET /health`
- **THEN** the response is `200 OK`, the failing check's `status` is `Degraded`, and its
  entry carries the dependency `endpoint`, a `lastCheckedUtc` timestamp, and a `reason`
  naming the failure

#### Scenario: Liveness is blind to the outage

- **WHEN** a configured Azure dependency is unreachable and a client issues `GET /live`
- **THEN** the response is `200 OK` with the plain-text body `Healthy`, unchanged from the
  all-dependencies-reachable case

### Requirement: Probes answer while the service is saturated

Both probe endpoints SHALL answer while the extraction route is rejecting work for
capacity reasons. Admission control SHALL apply to document extraction only.

#### Scenario: Probes answer under admission-control rejection

- **WHEN** concurrent extraction requests have saturated admission control and a client
  issues `GET /live` or `GET /health`
- **THEN** each probe answers on its own terms and is not rejected for capacity

### Requirement: Probe traffic is excluded from tracing

Probe requests SHALL NOT produce distributed-tracing spans. An orchestrator polls
them on a fixed interval for the life of every pod, and the spans say nothing.

Every path the tracing configuration excludes SHALL be a path the service actually
serves. An excluded path that nothing serves is a silent failure: the exclusion
matches nothing, the probe it was meant to cover is traced, and no response
changes. Changing a probe path therefore requires the exclusion to move with it.

#### Scenario: Probe requests produce no spans

- **WHEN** a client issues `GET /live` or `GET /health`
- **THEN** no server span is recorded for the request

#### Scenario: Extraction traffic is still traced

- **WHEN** a client issues `POST /v1/extract`
- **THEN** a server span is recorded for the request

#### Scenario: Exclusions and routes agree

- **WHEN** the set of trace-excluded paths is compared against the service's routes
- **THEN** every excluded path returns a success status, and no excluded path is unserved

### Requirement: Probe paths are discoverable

`GET /info` SHALL list both probe paths among the service's endpoints, so a deployment
can be checked against the running image without reading its source.

#### Scenario: Info advertises the probe paths

- **WHEN** a client issues `GET /info`
- **THEN** the `endpoints` array contains `/live` and `/health`, and does not contain `/alive`

### Requirement: Deployment probes match the served paths

The service's deployment manifests SHALL probe the paths this specification defines.
The liveness probe SHALL target `/live` and the readiness probe SHALL target `/health`.

A manifest probing a path the image does not serve fails the probe on a healthy pod
and, for liveness, restarts it until it backs off. No manifest-rendering or
template-validation step can detect this, because both halves are individually valid;
the image and the deployment manifests are therefore released and rolled out together.

#### Scenario: Rendered manifests probe the served paths

- **WHEN** the deployment manifests are rendered
- **THEN** the liveness probe path is `/live` and the readiness probe path is `/health`
