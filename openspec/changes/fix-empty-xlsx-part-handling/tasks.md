## 1. An unreadable workbook reports `corrupt`

- [x] 1.1 Cut `fix/xlsx-empty-workbook-part` from `main`.
- [x] 1.2 Add a failing test in `SpreadsheetEngineTests`: build the input by rezipping the
      committed `bom.xlsx` with the `xl/workbook.xml` entry blanked, run it through the
      existing `Run(byte[])` overload, and assert error code `corrupt` with a message
      naming the structural element. Confirm it first fails with `engine_error` and a
      null-reference message — that failure is the proof the test binds to the defect.
- [x] 1.3 Add the per-file isolation test over `/v1/extract`, following the existing
      `Http_contract_*` pattern in the same file: the damaged file and a readable one in
      one request answer 200, the readable one carrying its tables with no error.
- [x] 1.4 Replace the unguarded workbook-root dereference with a guard that throws
      `InvalidDataException` naming the cause, so it lands in the engine's existing catch
      filter — symmetric with the missing-workbook-part guard directly above it.
- [x] 1.5 Green gate: `dotnet restore src/DocInt.slnx` → `dotnet build --no-restore
      src/DocInt.slnx` → `dotnet test --no-build src/DocInt.slnx`. Build the solution after
      touching the file (a warm build recompiles nothing and reports zero warnings
      regardless) and confirm the count has dropped from 2 to 1, with the remaining one at
      the worksheet site.
- [ ] 1.6 Merge to `main` once green; delete the branch.

## 2. An unreadable sheet is skipped, the rest of the workbook still extracts

- [ ] 2.1 Cut `fix/xlsx-empty-worksheet-part` from `main`.
- [ ] 2.2 Add a failing test: rezip `bom.xlsx` with one of its two `xl/worksheets/sheet*.xml`
      entries blanked, and assert no file-level error, a warning naming the skipped sheet
      and its cause, and the other sheet present in `tables` with its cell values intact.
      Confirm it first fails by returning `engine_error` and no tables at all.
- [ ] 2.3 Add the all-sheets-unreadable case: no error, one warning per skipped sheet, no
      tables — the boundary the skip-and-continue rule implies.
- [ ] 2.4 Move the sheet-root null test into the per-sheet loop beside the existing
      not-a-worksheet skip, with its own warning wording, and pass the worksheet root
      rather than the part into the row reader so the reader has nothing nullable left.
- [ ] 2.5 Confirm the existing chartsheet test still passes unchanged — it covers the
      other branch of the same skip and is the regression guard for this edit.
- [ ] 2.6 Green gate: `restore` → `build --no-restore` → `test --no-build` against
      `src/DocInt.slnx`, after touching the engine file so it genuinely recompiles.
      Expect zero warnings solution-wide.
- [ ] 2.7 Confirm `git status` shows nothing changed under `tests/DocInt.Tests/golden/` —
      both inputs are derived in memory and no fixture is regenerated.
- [ ] 2.8 Merge to `main` once green; delete the branch.

## 3. Nullable warnings become build errors

Ordered last on purpose: switched on before groups 1 and 2 land, it fails the build.

- [ ] 3.1 Cut `chore/nullable-warnings-as-errors` from `main`.
- [ ] 3.2 Add `Directory.Build.props` at the repo root setting `WarningsAsErrors` to append
      the `nullable` category, leaving any inherited value intact. Scoped to that category
      only — see `design.md` for why a blanket setting is rejected against CI's floating
      `dotnet-version: 10.0.x`.
- [ ] 3.3 Verify it binds to all five projects, not just `DocInt.Api`: build the solution
      after a `--no-incremental` pass and confirm every project picks the property up.
- [ ] 3.4 Verify the policy actually bites, then revert the probe: reintroduce one nullable
      dereference locally, confirm the build fails with `error CS8602` rather than a
      warning, and restore the file. A policy that is never observed failing is not
      verified.
- [ ] 3.5 Green gate: `restore` → `build --no-restore` → `test --no-build` against
      `src/DocInt.slnx`, clean rather than warm, and confirm zero warnings and zero errors.
- [ ] 3.6 Merge to `main` once green; delete the branch.

## 4. Close-out

- [ ] 4.1 Run the gate once more on `main` with `--no-incremental` to confirm the merged
      result is clean from cold, not just on each branch.
- [ ] 4.2 Record what could not run: `LiveSmokeTests` self-skips without `DOCINT_LIVE_TESTS`
      and a network path into the VNet, and the Docker and chart jobs run only in CI.
      Nothing here touches the Azure-backed engines, the chart, or the pod security
      context, so none of them gates this change — but list the skip explicitly rather
      than reporting a full pass.
- [ ] 4.3 Archive the change once merged: `openspec archive fix-empty-xlsx-part-handling`.
