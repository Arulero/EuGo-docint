## Context

See `proposal.md` — Why. The behavioral requirements are in
`specs/spreadsheet-extraction/spec.md`; this document covers only how to get there.

Three facts about the current code shape the approach:

- The XLSX path already opens with a guard of exactly the form this change needs one line
  further down (`doc.WorkbookPart ?? throw new InvalidDataException(...)`), and the
  surrounding `catch` filter maps `InvalidDataException` to a per-file `corrupt`.
- The per-sheet loop already decides tab-by-tab whether a sheet is usable, skipping a tab
  that resolves to a chart or dialog rather than a worksheet, with a warning, and
  continuing. An unreadable sheet is a second reason to take that same branch.
- The router wraps every engine call in a catch-all, so neither defect can produce a 500.
  That is why they surface as an unusable `engine_error` message rather than as a crash,
  and it is also why no test caught them.

The two dereference sites are the only warnings the solution produces — verified across
Debug, Release, and the `dotnet publish` the Dockerfile runs, with NuGet audit clean.

## Goals / Non-Goals

**Goals:**

- Clear both warnings by removing the unguarded dereferences, so the code and the
  compiler agree rather than one being silenced.
- Make the warning signal trustworthy, so this class cannot recur behind an incremental
  build that reports zero warnings because nothing recompiled.

**Non-Goals:**

- Auditing the rest of the service for nullable defects. The policy change makes new ones
  fail the build; it does not commission a sweep.
- Consolidating the build properties the five project files each restate.
- Any change to the `/v1` contract, the error-code taxonomy, or the chart.

## Decisions

**An empty workbook part fails the file; an empty worksheet part fails only its sheet.**
These look symmetric and are not. Without a workbook there is nothing to extract, and the
damage is to the package — so it throws `InvalidDataException`, lands in the existing
catch filter, and reports `corrupt`, exactly as the neighbouring guard already does for a
missing workbook part. Without one worksheet, every other sheet is still readable, so
failing the file would discard output the caller can use. *Alternative considered:* make
both file-level `corrupt`. Rejected — it is strictly less output for no gain, and it
contradicts how the same loop already treats a chart or dialog tab. *Alternative
considered:* make the workbook case degrade too, via `?.` onto the existing empty-sheet
fallback. Rejected — that reports package damage as a valid workbook that happens to have
no sheets, which hides the damage from the caller entirely.

**The skipped-sheet warning names its own cause.** The loop already emits
`sheet '<name>' skipped: not a worksheet`. An empty part is a different condition and gets
its own wording. *Alternative considered:* reuse the existing message. Rejected — it would
be untrue, and the two conditions call for different responses from whoever produced the
file.

**Pass the worksheet root into the row reader rather than the part.** The null test then
lives in the loop that already makes the per-tab decision, and the reader has nothing
nullable left to handle. This removes the dereference instead of guarding it, which is
what keeps the fix from reading as compiler appeasement.

**Nullable warnings become errors, scoped to that category, in a root
`Directory.Build.props`.** Confirmed working: `-p:WarningsAsErrors=nullable` turns both
CS8602 into errors and fails the build. *Alternative considered:* blanket
`TreatWarningsAsErrors`. Rejected — CI pins `dotnet-version: 10.0.x`, which floats, so any
SDK or analyzer bump could turn the build red with no commit behind it; that trains people
to disable the setting. *Alternative considered:* `WarningsAsErrors=CS8602` alone.
Rejected — it would catch the exact bug already found and miss its siblings (CS8600,
CS8604, CS8618), which is the wrong half of the class to guard. *Alternative considered:*
`-warnaserror` on the CI build step only. Rejected — it leaves local and CI builds
disagreeing, and does nothing about the warm-build blind spot that let this survive.

**Test inputs are built in memory, not committed to `golden/`.** The test file already
constructs XLSX shapes in memory and records why: the OpenXML generator writes
non-deterministic ids, so keeping these shapes out of `golden/` is what holds the
committed fixture set stable. Both inputs here are an existing fixture rezipped with one
entry blanked, which also makes the damage readable in the test. *Alternative considered:*
two new fixtures via `tools/make-golden`. Rejected — it requires a generator run that
rewrites most fixtures, and yields two opaque binaries where a few lines are clearer.

## Risks / Trade-offs

- **An SDK bump changes a referenced package's annotations and reddens the build with no
  commit behind it.** → Scoping to the `nullable` category rather than all warnings keeps
  the exposure to annotations we consume; the fix is local, and CI surfaces it on a PR.
- **The behavior change is observable to EuGo-Web without a contract change.** → It moves
  in one direction only: a meaningful code where there was `engine_error`, and per-sheet
  results where there were none. No coordination needed, per `proposal.md`.
- **The fix depends on the OpenXML SDK returning null for an empty part** — its documented
  behavior ("returns null when the current part is empty or is not an XML content type").
  A future major could throw instead, which would route back to `engine_error`. → The
  tests assert the caller-visible outcome, not the SDK's mechanism, so a change of that
  kind fails a test rather than passing silently.

## Migration Plan

None beyond a normal image release. No configuration key, no chart value, and no change
to the pod security context or the chart's volume mounts — so the real-pod verification
that class of change would demand does not apply here, and the standard gate
(`restore` → `build --no-restore` → `test --no-build` against `src/DocInt.slnx`) is
sufficient. Rollback is a redeploy of the previous image; nothing persists across it.

## Deliberately deferred

- **Widening the XLSX catch filter to cover `XmlException`.** A workbook part holding
  malformed XML rather than no XML would take a different path out of the same call, and
  the filter does not name it. Raised while tracing this bug; **not verified either way**
  — it may be unreachable if the SDK wraps it. Out of scope for a change scoped to the two
  flagged dereferences, and recorded here because it is this bug's nearest neighbour and
  the next person will trace the same lines.
- **Folding the repeated `net10.0`, `Nullable`, and `ImplicitUsings` properties into the
  new `Directory.Build.props`.** The file is introduced for the warning policy; absorbing
  five project files into it is an unrelated refactor that would touch every project in a
  change meant to fix two lines.
- **A repo-wide nullable audit.** The policy change stops new instances at the build;
  hunting existing ones is separate work with a different shape, and the solution is
  warning-free today, so there is nothing queued behind it.
