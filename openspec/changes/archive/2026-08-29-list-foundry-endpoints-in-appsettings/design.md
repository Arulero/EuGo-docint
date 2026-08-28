## Context

See `proposal.md` — *Why*. Three pieces of existing behaviour shape the approach, and all three were
measured rather than assumed (2026-08-29, at `fd97f7b`):

- **Blank and absent are already one case.** `FoundryOptions` validation tests both endpoints with a
  blank check, and `OptionsTests` already pins `null`, `""` and `"   "` as equivalent theories. So a
  placeholder cannot widen or narrow the set of configurations that boot.
- **`appsettings.json` is outranked by every source that carries a real value.** The offline test
  suite is the proof available in-repo: `DocIntAppFactory` supplies both endpoints as `.invalid`
  hosts, and host-booting tests still start with the placeholders present in the shipped file.
- **The boot disclosure redacts before it reports emptiness.** `StartupConfigurationLog` matches its
  secret markers on a key's last segment and emits `***redacted***` for any match; only a
  non-secret key reaches the `(empty)` branch. An empty `Foundry:ApiKey` would therefore be
  disclosed as a present credential.

This change also reverses a decision recorded in the file it edits. The current comment reads
*"deliberately absent rather than present-and-empty: an empty value renders as a default an operator
might mistake for a choice"*. That reasoning is sound for a value that can be **used** — it is why
the Helm chart still refuses to render a default endpoint — but it does not transfer to a value that
cannot start a service. The rewritten comment has to say so, or the next reader will restore the old
shape on the strength of the old argument.

## Goals / Non-Goals

**Goals:**

- The tracked configuration file enumerates every endpoint an operator must supply.
- The reversal is recorded where the old decision lives, with the distinction that makes it safe.
- The asymmetry with `Foundry:ApiKey` is pinned by a test, not only by a comment.

**Non-Goals:**

- Changing what boots and what refuses to boot. If the set of accepted configurations moves at all,
  the change is wrong.
- Listing `Foundry:ApiKey`, in any form.
- Fixing the non-deterministic offline suite described in `proposal.md` — *Impact*. It is a real
  defect and it is not this change's.
- Touching the Helm chart. Its endpoint values are required-with-no-default and stay that way; a
  chart default would render into a manifest, which is exactly the failure mode the placeholders
  avoid by being unusable.

## Decisions

**Placeholders for the two endpoints, absence for the key.** The distinction is what the boot log
does with each. An unfilled endpoint placeholder produces a start-up refusal naming the key — the
loudest possible signal, and the one an operator already gets today. An empty key placeholder
produces `Foundry:ApiKey=***redacted***` in every pod's log, which reads as *a credential is
configured* at exactly the moment none is — the in-cluster Workload Identity case, i.e. production.
Alternatives considered: (a) list the key too and reorder `StartupConfigurationLog` to test
emptiness before the secret marker — rejected, it widens a redaction path for a documentation
benefit, and a mistake there leaks a key; (b) list the key with a non-empty sentinel such as
`"<from user-secrets>"` — rejected, a non-empty key is a *usable* value and would be sent to Azure
as one; (c) leave both endpoints absent and improve the README only — rejected, it is the status
quo the proposal argues against.

**Pin the shipped file by reading it, not by booting the app.** The test asserts against
`appsettings.json` as a configuration source of its own. Booting a host would prove nothing here:
every factory overrides both endpoints, so the placeholders are invisible from inside a running
app — which is precisely the property that makes them safe, and precisely why the assertion has to
be made outside one. The file is already copied to the test output directory by the project
reference, so it is reachable without a path walk to the repo root.

**Assert emptiness, not just presence.** The interesting failure is not someone deleting the keys;
it is someone filling one in — pasting `https://aif-eugo-swc.cognitiveservices.azure.com/` into the
tracked file while debugging and committing it. `Assert.Equal("", …)` catches that; a presence check
would not. This is the test's main job.

**Verification gate: the standard one, with the file watcher set to polling.** An earlier draft of
this design substituted a like-for-like failing-set comparison for the usual green suite, on the
belief that the offline suite was non-deterministic. It is not. Measured 2026-08-29 at `HEAD` in a
pristine worktree: five consecutive full runs failed 28, 33, 18, 30 and 30 tests with a union of 50
distinct test names that never saturated, every failure reporting
`InvalidOperationException: The entry point exited without ever building an IHost` — but the
underlying error, visible only in the runs that surfaced it, is
`IOException: The configured user limit (128) on the number of inotify instances has been reached`.
This workstation holds 117 of those 128 instances before the suite starts, and each
`WebApplicationFactory` host opens a configuration-file watcher. With
`DOTNET_USE_POLLING_FILE_WATCHER=1` the same worktree is green: 220 passed, 7 skipped, 0 failed.

So the gate is `restore` → `build --no-restore` → `test --no-build` against `src/DocInt.slnx`,
green, with that variable exported for the run. Raising `fs.inotify.max_user_instances` via `sysctl`
is the other remedy and needs root; the environment variable needs nothing and changes only how the
test process watches files. Neither is a code change, and neither belongs in the repository.

## Risks / Trade-offs

- **The empty placeholder is read as an opt-out** ("it's listed, so it's optional") → the rewritten
  comment states the requirement and the consequence at the key, and an unfilled placeholder still
  exits the process naming it — the behaviour teaches the same lesson within one boot.
- **Someone fills a placeholder in and commits it** → the new test asserts the value is empty, so
  the commit fails the gate rather than shipping an environment's address as a default.
- **A future edit adds `Foundry:ApiKey` alongside them for symmetry** → the same test asserts the key
  is absent, and both the comment and this document record the boot-log reason.
- **The reversal is re-reversed by someone reading only the old rationale** → the old argument is
  quoted in the new comment together with the reason it does not apply to an unusable value.
- **The comparison gate hides a regression inside the flaky set** → the targeted tests are also run
  under a narrow `--filter`, where the suite is deterministic, and the compared sets are
  test-name sets rather than counts.

## Migration Plan

None. Nothing outside the repository changes: no chart value, no environment variable, no deployment
step. An existing deployment that already supplies both endpoints is unaffected, because a supplied
value outranks the placeholder. Rollback is reverting the commit.

## Deliberately deferred

- **Raising this workstation's inotify limits.** The suite's apparent flakiness was an exhausted
  `fs.inotify.max_user_instances` (see the verification decision above), cleared for a test run by
  `DOTNET_USE_POLLING_FILE_WATCHER=1`. Raising the sysctl permanently is a machine-configuration
  change requiring root, it affects every process on the host, and nothing in this repository can
  or should carry it. Recorded here so the next person to meet
  `The entry point exited without ever building an IHost` does not re-diagnose it as a test defect —
  it took five full runs and a look past the outer exception to see it the first time.
- **Listing `DocInt:*` keys that already carry shipped values.** Not needed: they are all present.
- **A schema or generator for the configuration surface** (JSON Schema, an options dump on `/info`).
  A larger idea with its own maintenance cost; the file listing its own keys is the cheap 90%.
- **Reordering `StartupConfigurationLog`'s secret and emptiness checks** so a blank credential
  reports `(empty)` rather than `***redacted***`. Arguably an improvement on its own merits — a
  blank value is not a secret — but it changes what every pod logs, so it belongs to a change about
  boot disclosure, not to this one. Recorded here because it is the one edit that would remove the
  reason for the key's asymmetry.
