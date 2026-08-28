# foundry-configuration Specification

## Purpose

Defines the credential and endpoint surface an operator sets so the service can reach its Azure AI
Foundry resource, what the service does when a value is absent or retired, and what it discloses
about that configuration at boot.

## Requirements

### Requirement: One credential for the whole Foundry resource

The service SHALL accept a single API key for the Foundry resource and use it for every Azure API
surface it calls. It SHALL NOT accept a separate key per surface, and the choice between key
authentication and the ambient managed identity SHALL be one decision for the resource rather than
an independent decision per surface.

#### Scenario: Key supplied

- **WHEN** the Foundry API key is configured and both endpoints are set
- **THEN** requests to both the document-layout surface and the image-description surface are
  authenticated with that key

#### Scenario: Key omitted

- **WHEN** no Foundry API key is configured and an endpoint is set
- **THEN** calls to that endpoint authenticate with the ambient managed identity
- **AND** the service starts normally, because an absent key is a supported configuration

#### Scenario: The authentication choice is made once

- **WHEN** the credential for either surface is resolved from a configuration whose key is blank or
  absent
- **THEN** the managed-identity branch is chosen
- **AND** the same resolution, given the same configuration, yields the same branch for both
  surfaces, because the choice is made from one value in one place

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

### Requirement: Retired configuration keys fail the service at boot

The service SHALL refuse to start when a retired credential or endpoint key from the superseded
per-surface configuration **carries a value**, and the failure message SHALL name both the retired
key and its replacement. Ignoring such a key would leave the service running with an unintended
authentication path, which surfaces later as an endpoint or network failure rather than as the
configuration error it is.

A retired key present but blank SHALL NOT fail start-up: a blank value selects nothing and changes
no behaviour, and the process environment is folded into configuration wholesale, so failing on
presence alone would reject configurations that are in fact harmless.

#### Scenario: Retired key left behind after migration

- **WHEN** a retired per-surface API key carries a value in any configuration source
- **THEN** the service refuses to start
- **AND** the message names the retired key and the Foundry key that replaces it

#### Scenario: Retired endpoint left behind after migration

- **WHEN** a retired per-surface endpoint or deployment-name key carries a value in any
  configuration source
- **THEN** the service refuses to start
- **AND** the message names the retired key and its replacement

#### Scenario: Retired key present but blank

- **WHEN** a retired key is present with an empty value and no other retired key carries a value
- **THEN** start-up proceeds

#### Scenario: Clean configuration

- **WHEN** no retired key is present
- **THEN** the retired-key check reports nothing and start-up proceeds

### Requirement: Configuration is disclosed at boot without exposing the key

The service SHALL record its effective Foundry configuration once at start-up, so a running
instance's log shows the endpoints and deployment alias it actually resolved and whether a key is
present. The key's value SHALL never be recorded, in any configuration source.

#### Scenario: Key present

- **WHEN** the service starts with a Foundry API key configured
- **THEN** the start-up log contains an entry naming the key with its value redacted
- **AND** the key's value appears nowhere in the log

#### Scenario: Key supplied through the environment

- **WHEN** the key is supplied by an environment variable rather than a configuration file
- **THEN** the start-up log still names the key with its value redacted

#### Scenario: Endpoints and alias

- **WHEN** the service starts with endpoints and a deployment alias configured
- **THEN** the start-up log records each of those values as resolved, since none of them is a
  credential

### Requirement: The tracked configuration file lists every endpoint an operator must supply

The service's tracked application configuration SHALL name both API-surface endpoint keys, each
present with an empty value, so that the file an operator reads to learn what the service takes
enumerates the values whose absence refuses the boot rather than leaving them to surrounding prose.

Those values SHALL remain empty in the tracked file. An endpoint is environment-specific, so a
tracked non-empty value would ship one environment's address as a default and would be
indistinguishable from a value an operator chose.

A listed-but-empty key SHALL carry no meaning of its own. It SHALL NOT relax the requirement that
every endpoint be supplied, and it SHALL NOT participate in resolution: a value supplied from any
other configuration source takes precedence over it, so the placeholder can never mask a configured
endpoint.

The credential SHALL NOT be listed this way. The service's boot disclosure reports a credential-shaped
key as present-and-redacted whenever it carries any value at all, so an empty placeholder for the key
would have every instance report a credential where none is configured — and where the ambient
managed identity is used, none ever is.

#### Scenario: An operator reads the tracked configuration

- **WHEN** an operator opens the tracked application configuration file
- **THEN** both API-surface endpoint keys are named there, each with an empty value
- **AND** no credential key is named there

#### Scenario: A placeholder left unfilled

- **WHEN** the service is started with the tracked configuration as its only source of endpoint
  values, so both endpoints resolve to the empty placeholders
- **THEN** the service refuses to start and names the missing endpoint, exactly as it does when the
  key is absent altogether

#### Scenario: A placeholder is overridden by a supplied value

- **WHEN** an endpoint is supplied from a higher-precedence configuration source — the environment,
  a developer's secret store, or the deployment chart — while the tracked file carries the empty
  placeholder
- **THEN** the supplied value is the one resolved, and the service starts
- **AND** the boot disclosure records the supplied value, not the placeholder

#### Scenario: The tracked file never carries a real endpoint

- **WHEN** the tracked configuration file is inspected for either endpoint key
- **THEN** its value is empty
- **AND** a non-empty value there is a defect, because it would ship one environment's address as
  the service's default

#### Scenario: An unset credential is not disclosed as a present one

- **WHEN** the service starts with no credential configured, authenticating with the ambient managed
  identity
- **THEN** the boot disclosure contains no entry reporting a credential as present-and-redacted,
  because the tracked file lists no credential key for one to resolve from
