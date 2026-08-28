## Why

`src/DocInt.Api/appsettings.json` is the file an operator opens to see what this service takes. It
lists every knob under `DocInt:*` down to retry budgets, and omits the only two values whose absence
refuses the boot: `Foundry:DocumentIntelligenceEndpoint` and `Foundry:OpenAIEndpoint`. Today those
keys are discoverable only from prose — a comment block in that file and a table in the README — so
the file that reads as the configuration surface does not enumerate the configuration surface, and
the first time an operator learns a key exists is when the process exits 1 naming it.

Listing both as empty placeholders makes the file self-describing at no behavioural cost: an empty
endpoint already fails start-up exactly as a missing one does, and `appsettings.json` is the
lowest-precedence provider, so a placeholder can never mask a value supplied from user-secrets, the
environment, or the Helm chart.

## What Changes

- `src/DocInt.Api/appsettings.json` gains `Foundry:DocumentIntelligenceEndpoint` and
  `Foundry:OpenAIEndpoint`, both present with an empty value.
- The comment block in that file currently records the opposite decision — *"deliberately absent
  rather than present-and-empty: an empty value renders as a default an operator might mistake for a
  choice"*. It is rewritten to record the new decision, its bound (empty is the ceiling — a tracked
  non-empty endpoint would ship an environment-specific value as a default), and why the key is
  treated differently.
- `Foundry:ApiKey` stays absent, and that asymmetry is deliberate rather than an oversight.
  `StartupConfigurationLog` matches its secret marker on a key's leaf **before** it tests for
  emptiness, so an empty placeholder there would put `Foundry:ApiKey=***redacted***` in every pod's
  boot log — a credential claimed where none is set, which in-cluster (Workload Identity) is always.
- A test pins both halves: the shipped file lists both endpoints as empty, and does not list the key.
- The README's *"`Foundry:ApiKey` is bound by the options class but deliberately absent from the
  committed…"* note is corrected to match.

No observable behaviour changes. Absent and blank are already one case at validation, so the set of
configurations that start and the set that refuse are both unchanged. This does not touch the frozen
`/v1` contract and EuGo-Web needs to do nothing.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `foundry-configuration`: adds a requirement on what the tracked configuration file discloses about
  the keys an operator must supply, and on the bound that keeps a placeholder from becoming a
  shipped default or a phantom credential. The existing endpoint and boot-disclosure requirements
  are unchanged.

## Impact

- `src/DocInt.Api/appsettings.json` — two keys and a rewritten comment block.
- `tests/DocInt.Tests/OptionsTests.cs` — one test reading the shipped file.
- `README.md` — the configuration reference's note on the absent keys.
- No change to the Helm chart, CI, the options classes, or any validator: the placeholders bind
  through the existing `FoundryOptions` path and are rejected by the existing `ValidateOnStart`
  rules.

**Verification note.** An earlier draft of this proposal recorded the offline suite as
non-deterministic and weakened this change's merge gate accordingly. That was wrong, and the cause
was measured on 2026-08-29: this Linux workstation sits at 117 of its 128
`fs.inotify.max_user_instances`, and every `WebApplicationFactory` host opens a configuration-file
watcher, so hosts past the limit fail to build and report
`InvalidOperationException: The entry point exited without ever building an IHost`. It is an
environment limit, not a repository defect. With `DOTNET_USE_POLLING_FILE_WATCHER=1` the suite at
`HEAD` (fd97f7b, pristine worktree) is green: 220 passed, 7 skipped, 0 failed. The standard gate
therefore applies to this change unchanged, with that variable set.
