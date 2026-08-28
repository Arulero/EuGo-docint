## ADDED Requirements

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
