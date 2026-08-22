## Why

An XLSX whose package opens but whose workbook or worksheet part is empty is reported to
the caller as `engine_error` with the message `Object reference not set to an instance of
an object.` — and when it is a worksheet part, every intact sheet in the same workbook is
discarded with it. Reproduced on 2026-08-22 against a running service, using a valid
`.xlsx` package with `xl/workbook.xml` blanked, and separately with one of two
`xl/worksheets/sheet*.xml` blanked.

That is the wrong error code, an unusable message, and needlessly discarded output. Two
`CS8602` nullable-dereference warnings have marked both sites since the engine's first
commit (2026-07-21); they were recorded as deferred on 2026-08-08 and have outlived six
later commits to the same file. They persist because a warm incremental build reports
zero warnings — only a build that actually recompiles `DocInt.Api` surfaces them, so
"0 warnings" locally means "nothing compiled", not "clean".

## What Changes

- An XLSX whose workbook part is present but empty is reported as `corrupt`, with a
  message naming the cause, instead of `engine_error` carrying a runtime null-reference
  message.
- A sheet whose worksheet part is present but empty is skipped with a per-file warning,
  and the workbook's remaining sheets still extract — the same treatment a tab pointing
  at a chartsheet or dialogsheet already gets. Today one such sheet discards the whole
  file's output.
- Both `CS8602` warnings are cleared by fixing the two dereferences, not by suppressing
  them or by widening a catch.
- Nullable warnings become build errors repo-wide, via a root `Directory.Build.props`
  setting `WarningsAsErrors` to the `nullable` category, so this class of defect cannot
  reappear unnoticed behind an incremental build. Deliberately scoped to that category
  rather than a blanket `TreatWarningsAsErrors`: CI floats `dotnet-version: 10.0.x`, and
  a blanket setting lets an SDK bump turn the build red with no commit behind it. With
  the two fixes in place the tree is warning-free, so this is a no-op the moment it is
  switched on.

**Not breaking, and the frozen `/v1` contract is untouched.** No new error code (`corrupt`
is already in the taxonomy), no field added, removed, or retyped, no change to request
handling. EuGo-Web needs no coordinated change and no action. It will observe two
differences for this narrow input class, both strictly better: a `corrupt` code where it
saw `engine_error`, and per-sheet results where it previously saw none.

## Capabilities

### New Capabilities
- `spreadsheet-extraction`: what an XLSX request yields per file and per sheet, including
  how a workbook or sheet whose stored parts are structurally damaged is reported.

### Modified Capabilities

_None._ No existing spec under `openspec/specs/` covers spreadsheet extraction.

## Impact

- `src/DocInt.Api/Engines/SpreadsheetEngine.cs` — the two dereference sites and the
  signature of the internal `ReadRows` helper.
- `tests/DocInt.Tests/SpreadsheetEngineTests.cs` — new failing-first cases. No new golden
  binaries: the file already builds XLSX shapes in memory, with a comment recording why
  (the OpenXML generator writes non-deterministic ids, so the committed fixture set stays
  stable). The repro is an existing fixture rezipped with one entry blanked.
- New `Directory.Build.props` at the repo root — the repo has none today. Scoped to the
  warning policy only; it does not consolidate the `net10.0` / `Nullable` /
  `ImplicitUsings` properties the five csproj files each restate, which is out of scope.
- No dependency, configuration, chart, or pod-security-context change. Nothing here
  touches the read-only root filesystem or the writable `/tmp` mount.
