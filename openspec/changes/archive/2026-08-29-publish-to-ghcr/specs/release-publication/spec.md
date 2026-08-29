## Purpose

Defines what pushing a version tag publishes, where those artifacts are addressable, how the
container image and the Helm chart stay version-paired, and what the chart must render so a
cluster can pull an image from a private registry.

## ADDED Requirements

### Requirement: A version tag publishes the image and the chart

Pushing a tag SHALL be the only trigger that publishes an artifact, and publishing SHALL be
gated on the verification gate passing first. A tag naming an image version SHALL publish both
the container image and the Helm chart, in that order. A tag naming a chart version SHALL
publish only the chart. No other event — a branch push, a pull request, a manual run — SHALL
publish anything.

#### Scenario: Image version tag

- **WHEN** a tag of the form `vX.Y.Z` is pushed and the verification gate passes
- **THEN** the container image is published first
- **AND** the Helm chart is published afterwards, so a published chart never references an image
  that does not exist

#### Scenario: Chart-only version tag

- **WHEN** a tag of the form `chart-vX.Y.P` is pushed and the verification gate passes
- **THEN** the Helm chart is published
- **AND** no container image is published, because a chart-only release changes no application
  code

#### Scenario: Verification gate fails

- **WHEN** a version tag is pushed and the verification gate fails
- **THEN** neither the image nor the chart is published

#### Scenario: A commit without a tag

- **WHEN** a commit is pushed to a branch or a pull request is opened
- **THEN** the chart and the image may be built for validation
- **AND** neither is published to a registry

### Requirement: The image and the chart occupy distinct registry repositories

The container image and the Helm chart SHALL be published to registry repositories whose names
differ. A single repository SHALL NOT hold both, because the image and the chart share a
major and minor version by contract and would therefore contend for the same tag, with one
artifact silently replacing the other.

#### Scenario: Both artifacts published from one tag

- **WHEN** an image version tag publishes both artifacts
- **THEN** the image and the chart are addressable at different repository names
- **AND** publishing the chart leaves the image manifest reachable at its own tag

#### Scenario: Versions that would otherwise collide

- **WHEN** the chart version and the image version are the same string
- **THEN** both artifacts are published successfully, because they do not share a repository

### Requirement: Registry coordinates are lowercase

Every registry coordinate SHALL be lowercase, regardless of the casing of the values it is
derived from. The organization that owns the repository is spelled with capital letters, and a
registry rejects a reference containing them; the coordinate SHALL therefore be lowercased
explicitly rather than depending on any single publishing step to normalize it.

#### Scenario: Coordinate derived from a mixed-case owner

- **WHEN** a registry coordinate is derived from an owner name containing capital letters
- **THEN** the coordinate used to publish is entirely lowercase

#### Scenario: Both publishing steps

- **WHEN** the image is published and the chart is published in the same run
- **THEN** both coordinates are lowercase
- **AND** neither step depends on the other having normalized the value

### Requirement: The chart and image versions stay paired

A published chart SHALL record the image version it is intended to install. The chart version's
major and minor SHALL equal that image version's major and minor; the chart version's patch is
chart-owned and MAY differ. A release whose versions violate this SHALL fail without publishing.

#### Scenario: Chart-only release resolves its image version

- **WHEN** a chart-only tag is published
- **THEN** the chart records the highest existing image version sharing its major and minor
- **AND** the release fails without publishing if no such image version exists

#### Scenario: Drift between image and chart

- **WHEN** an image version tag is pushed whose major and minor differ from the chart's
- **THEN** the release fails, naming both versions, and publishes nothing

#### Scenario: Chart tag disagrees with the chart's own version

- **WHEN** a chart-only tag names a version other than the one the chart declares
- **THEN** the release fails and publishes nothing

### Requirement: The published chart renders before it is published

The exact chart artifact bound for the registry SHALL be rendered successfully before it is
published, not merely validated as metadata. Rendering SHALL cover the configurations that
differ structurally, so a chart that cannot produce manifests is never published.

#### Scenario: Chart that packages but cannot render

- **WHEN** the packaged chart fails to render
- **THEN** it is not published

#### Scenario: Rendering the artifact rather than the sources

- **WHEN** the packaged chart is validated
- **THEN** the validation renders the packaged artifact itself, so what is proven is what ships

### Requirement: Published packages are private and pulled with a named secret

Published packages SHALL be private. Because a private registry admits no ambient cluster
identity, the chart SHALL accept a list of pre-existing secret names to use when pulling the
image, and SHALL render them into the pod specification when the list is non-empty. The chart
SHALL NOT create such a secret and SHALL NOT accept any credential material as a value, so no
credential is ever written into a rendered manifest or a values file.

#### Scenario: Pull secret names supplied

- **WHEN** one or more image pull secret names are configured
- **THEN** the rendered pod specification references those secrets by name

#### Scenario: No pull secret names supplied

- **WHEN** no image pull secret names are configured
- **THEN** the rendered pod specification contains no image pull secret reference
- **AND** the chart renders successfully, because the value is optional and a cluster may obtain
  the image another way

#### Scenario: The chart carries no credential

- **WHEN** the chart is rendered with any supported configuration
- **THEN** no rendered manifest contains registry credential material
- **AND** the chart creates no secret of its own

### Requirement: The chart's package name does not reach its Kubernetes resources

The name under which the chart is published SHALL be independent of the names and labels the
chart renders. The rendered resource names, and the label naming the application, SHALL identify
the service rather than its packaging artifact, so that the published name can change without
altering a Deployment's immutable selector.

#### Scenario: Chart published under a name that differs from the service

- **WHEN** the chart is rendered
- **THEN** the label naming the application identifies the service
- **AND** the rendered resource names identify the service, not the packaging artifact

#### Scenario: Label identifying the packaged chart

- **WHEN** the chart is rendered
- **THEN** the label that names the packaged chart carries the chart's own name and version,
  because that label describes the artifact rather than the workload

### Requirement: Publishing requires no pre-configured registry credentials

Publishing SHALL authenticate with a credential the repository already holds by virtue of the
workflow running in it. It SHALL NOT require a repository variable, a stored secret, or a
federated cloud identity to be configured before the first release, so that a release can be cut
without provisioning anything.

#### Scenario: First release from a fresh repository

- **WHEN** a version tag is pushed in a repository with no release-specific variables or secrets
  configured
- **THEN** both artifacts are published

#### Scenario: No cloud credential in the release path

- **WHEN** a release runs
- **THEN** it performs no cloud provider sign-in and reads no cloud subscription, tenant, or
  client identifier
