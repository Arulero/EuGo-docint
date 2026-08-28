## 1. Baseline

- [x] 1.1 Cut the working branch from `main` (`feat/endpoint-placeholders` already exists locally
      and carries a first cut of 2.1 and 2.2 as uncommitted edits — reuse it or reset it, but do not
      work on `main`).
- [x] 1.2 Establish that the gate is meaningful before trusting it. A full run at `HEAD` in a
      pristine worktree failed non-deterministically (five runs: 28, 33, 18, 30, 30 failures, union
      of 50 test names, never saturating), which is an exhausted `fs.inotify.max_user_instances` on
      this workstation rather than a repository defect — see `design.md`, *Verification gate*. With
      `DOTNET_USE_POLLING_FILE_WATCHER=1` the same worktree is green (220 passed, 7 skipped, 0
      failed), so the standard gate applies and no baseline comparison is needed.

## 2. The change

- [x] 2.1 Failing test first, in `tests/DocInt.Tests/OptionsTests.cs`: read
      `appsettings.json` from the test output directory as its own configuration source and assert
      both endpoint keys resolve to `""` and that no credential key resolves at all. Run it under
      `--filter` and confirm it fails on the endpoint assertion, not on a missing file.
- [x] 2.2 Add `Foundry:DocumentIntelligenceEndpoint` and `Foundry:OpenAIEndpoint` as empty values to
      `src/DocInt.Api/appsettings.json`, and rewrite the surrounding comment block: both keys are
      listed, empty is not a default and not an opt-out, empty is also the ceiling, higher-precedence
      sources override the placeholder, and the credential is absent because the boot log would
      report an empty one as present-and-redacted. Quote the superseded "deliberately absent rather
      than present-and-empty" argument and say why it does not carry to a value that cannot start a
      service.
- [x] 2.3 Confirm the new test passes under `--filter`, then run the gate green:
      `restore` → `build --no-restore` → `test --no-build` against `src/DocInt.slnx`, with
      `DOTNET_USE_POLLING_FILE_WATCHER=1` exported for the test run.

## 3. Documentation

- [x] 3.1 Correct the README's configuration reference: the *"deliberately absent from the committed
      `appsettings.json`"* note and the two endpoint rows' *"(none — required)"* default column now
      read as present-and-empty, while `Foundry:ApiKey` stays *unset — not in `appsettings.json`*
      and gains the boot-log reason.
- [x] 3.2 Update the `appsettings.json` excerpt embedded in the README so it matches the file.
- [x] 3.3 Re-run the gate (§2.3) — the README carries a copy of the config file and
      `RedactionTests` / `StartupConfigurationLoggingTests` read real configuration, so a docs-only
      commit still has to clear it.

## 4. Close out

- [ ] 4.1 Run `graphify update .` to keep the knowledge graph current.
- [ ] 4.2 Merge to `main` once §2.3 and §3.3 both hold, delete the branch, then archive this change
      with the OpenSpec archive workflow.
