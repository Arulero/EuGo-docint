## ADDED Requirements

### Requirement: The deployment chart renders independently of runtime configuration

The deployment chart SHALL render successfully with no configuration value supplied. It SHALL carry
each endpoint into the rendered pod's environment when that endpoint is set, and SHALL omit the
corresponding environment variable when it is not. The chart SHALL NOT refuse to render on account
of a value the service needs at run time.

The chart describes how the service is deployed; which endpoints it talks to is runtime wiring. A
chart that cannot render without that wiring cannot be linted, exercised, or installed by anything
that supplies those values through another mechanism — and every assertion made by rendering it is
lost along with it.

The service's own requirement is unaffected: it still refuses to start unless both endpoints are
configured, and that check remains the enforcement point.

#### Scenario: Render with nothing supplied

- **WHEN** a release is rendered with no configuration value set
- **THEN** rendering succeeds and produces a complete manifest
- **AND** neither endpoint environment variable appears in the rendered pod

#### Scenario: Render with one endpoint supplied

- **WHEN** a release is rendered with only one of the two endpoints set
- **THEN** rendering succeeds
- **AND** the rendered pod carries that endpoint's environment variable
- **AND** it does not carry the other's

#### Scenario: Render with both endpoints supplied

- **WHEN** a release is rendered with both endpoint values set
- **THEN** rendering succeeds and the rendered pod carries both as environment variables

#### Scenario: A pod rendered without an endpoint still refuses to start

- **WHEN** a pod is started from a manifest rendered without both endpoints
- **THEN** the service refuses to start and reports the missing endpoint
- **AND** the misconfiguration is reported by the pod rather than by rendering, because the chart
  is not the enforcement point

#### Scenario: Deployment concerns independent of configuration still render

- **WHEN** a release is rendered with no configuration value set
- **THEN** the rendered pod still mounts a writable temporary directory, because that mount depends
  on no configuration value

### Requirement: The chart names an image without being told

The chart SHALL carry a default image repository, so that rendering requires no value. A release
MAY override it, and the resolved value SHALL always be a fully qualified repository rather than a
bare name that would resolve against an unintended registry.

#### Scenario: No image repository supplied

- **WHEN** a release is rendered with no image repository set
- **THEN** rendering succeeds and the rendered pod names the default repository

#### Scenario: Image repository overridden

- **WHEN** a release sets an image repository
- **THEN** the rendered pod names that repository instead of the default

## REMOVED Requirements

### Requirement: A release that omits an endpoint fails before it reaches the cluster

**Reason**: The gate made the chart's ability to render depend on the service's runtime wiring,
which is the wrong coupling. In practice it disabled the chart's own test suite: five `chart-lint`
renders supplied no endpoints and failed, so the assertions covering zero-versus-unset limits,
scrape annotations, the HPA memory metric, and the admission limits stopped running entirely. The
protection it offered — a named value on the operator's terminal instead of a crash-looping pod —
did not cover that cost, and the service's boot-time check already reports the same missing value.

**Migration**: A release that omits an endpoint now renders and installs, and its pod refuses to
start reporting the missing endpoint. Operators who want the earlier fail-at-render behavior should
assert on their own values before install; the chart no longer does it for them. No installed
release changes behavior, because a release that omitted an endpoint could never have been rendered.
