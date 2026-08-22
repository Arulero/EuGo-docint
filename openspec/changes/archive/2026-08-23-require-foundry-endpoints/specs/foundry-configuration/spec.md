## ADDED Requirements

### Requirement: Every API-surface endpoint is required

The service SHALL accept a separate endpoint value for the document-layout surface and for the
image-description surface, because the resource exposes them on different hosts and neither host is
derivable from the other. Every endpoint SHALL be required: the service SHALL refuse to start when
any endpoint is absent, empty, or blank, and the refusal SHALL name the endpoint that is missing. A
configured endpoint MUST be an absolute URI.

A missing endpoint SHALL be treated as a configuration error rather than as a supported deployment
mode. There SHALL be no setting that relaxes this requirement: such a setting would express an
operator's omission and an operator's intent as the same configuration, which is the ambiguity this
requirement exists to remove.

Requiring an endpoint value SHALL remain independent of whether the service dials that endpoint at
start-up. A host that cannot reach the resource may still supply both endpoints and start with the
boot-time reachability check turned off, because an endpoint is a hostname rather than a credential.

Because no running instance can lack an endpoint, no running instance SHALL report a file as
unserved for want of configuration. The `engine_unconfigured` per-file error code remains part of
the response vocabulary and is not withdrawn from the contract, but no configuration can produce it.

#### Scenario: Both endpoints configured

- **WHEN** both endpoints are set to absolute URIs
- **THEN** the service starts and every supported file kind is served by its engine

#### Scenario: One endpoint left blank

- **WHEN** the document-layout endpoint is set and the image-description endpoint is blank
- **THEN** the service refuses to start
- **AND** the refusal names the image-description endpoint as the missing value
- **AND** no request is accepted, because the host never begins listening

#### Scenario: No endpoint configured

- **WHEN** both endpoints are blank
- **THEN** the service refuses to start and names the missing endpoints
- **AND** spreadsheet extraction is unavailable too, even though it calls no Azure surface, because
  the refusal is a whole-process one rather than a per-engine one

#### Scenario: Endpoint present but blank

- **WHEN** an endpoint key is present with an empty or whitespace value
- **THEN** the service refuses to start, treating it the same as an absent key

#### Scenario: Endpoint is not an absolute URI

- **WHEN** an endpoint is set to a value that is not an absolute URI
- **THEN** the service refuses to start and reports which endpoint value was rejected

#### Scenario: Endpoints required but deliberately not dialled

- **WHEN** both endpoints are set to absolute URIs the host cannot reach, and the boot-time
  reachability check is turned off
- **THEN** the service starts
- **AND** requests for Azure-served kinds fail per file against the unreachable endpoints, because
  turning off the check suppresses the boot-time dial and nothing else

#### Scenario: A configured service still reports per-file failure inside a 200

- **WHEN** both endpoints are configured and a request to `/v1/extract` carries one readable PDF and
  one corrupt file
- **THEN** the response status is 200
- **AND** the PDF entry carries its extracted Markdown
- **AND** the corrupt entry carries its own error, leaving the rest of the response unaffected

### Requirement: The vision deployment alias is required

The service SHALL take the name of the image-description model deployment alongside the resource's
key and endpoints. That name SHALL be required unconditionally, and the service SHALL treat it as a
deployment alias rather than a model identity, so the model behind it can change without a
configuration change here. It is unconditional because the endpoint that used to make it conditional
is itself now required.

#### Scenario: Alias missing

- **WHEN** the deployment name is blank
- **THEN** the service refuses to start and reports the missing deployment name

#### Scenario: Alias supplied

- **WHEN** the deployment name is set
- **THEN** the service starts and image requests are served against that deployment
- **AND** the value is carried through unchanged, because it names a deployment rather than a model

### Requirement: A release that omits an endpoint fails before it reaches the cluster

The deployment chart SHALL refuse to render a release that does not supply both endpoints, and the
refusal SHALL name the value that is missing. Rendering a manifest whose pod is guaranteed to refuse
to start would move a known configuration error from install time to run time, where it costs a
failed rollout to discover.

The chart SHALL NOT carry a default value for either endpoint. A default that renders is
indistinguishable from a value an operator chose.

#### Scenario: Release omits an endpoint

- **WHEN** a release is rendered without one of the two endpoint values
- **THEN** rendering fails and the message names the missing value
- **AND** no manifest is produced, so nothing reaches the cluster

#### Scenario: Release supplies both endpoints

- **WHEN** a release is rendered with both endpoint values set
- **THEN** rendering succeeds and the rendered pod carries both as environment variables

#### Scenario: Deployment concerns independent of configuration still render

- **WHEN** a release is rendered with both endpoint values set and no other value supplied
- **THEN** rendering succeeds
- **AND** the rendered pod still mounts a writable temporary directory, because that mount depends
  on no configuration value

## REMOVED Requirements

### Requirement: One endpoint per API surface

**Reason**: Its normative core — "Each endpoint SHALL be independently optional" — is exactly what
this change reverses, and two of its scenarios describe a service that starts with an endpoint
missing. Editing it in place would leave a requirement whose name and scenario titles promise
optionality it no longer grants.

**Migration**: Replaced by "Every API-surface endpoint is required", which carries forward the two
parts that still hold — one endpoint value per surface because the resource uses two hosts, and an
endpoint MUST be an absolute URI — and inverts the optionality. Operators supply both endpoints;
`engine_unconfigured` is no longer reachable, and no caller change is needed because the code
remains defined in the response vocabulary.

### Requirement: The vision deployment alias is part of the resource configuration

**Reason**: The name was required only "whenever the image-description endpoint is set", and its
"Alias unused while the surface is disabled" scenario describes a surface that can no longer be
disabled. The conditionality has no remaining case to cover.

**Migration**: Replaced by "The vision deployment alias is required", identical in substance except
that the requirement is unconditional. A deployment that already sets the endpoint already sets the
alias, so no configuration changes.
