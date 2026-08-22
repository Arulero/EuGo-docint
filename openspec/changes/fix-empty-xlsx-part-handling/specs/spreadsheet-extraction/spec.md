## Purpose

Defines what a caller receives when it submits an XLSX file: the workbook's sheets as
tables of typed cell values alongside a Markdown rendering, and — when part of the stored
workbook cannot be read — which of those results survive and how the rest is reported.

## ADDED Requirements

### Requirement: An XLSX whose stored workbook structure cannot be read is reported as a corrupt file

An XLSX file that opens as a package but whose stored workbook structure is absent or
empty SHALL yield a per-file error with code `corrupt`. The accompanying message SHALL
name the structural element that could not be read, and SHALL NOT carry any part of the
file's content.

#### Scenario: Workbook structure is present but empty

- **WHEN** a caller submits an XLSX file whose package is intact but whose stored workbook
  structure holds no content
- **THEN** that file's result carries error code `corrupt`
- **AND** the message names the structural element that could not be read
- **AND** the result carries no tables and no Markdown

#### Scenario: A damaged workbook fails only itself

- **WHEN** a caller submits one such damaged XLSX file together with a readable one in the
  same request
- **THEN** the response status is 200
- **AND** the readable file's result carries its tables and Markdown with no error
- **AND** the damaged file's result carries error code `corrupt`

### Requirement: A sheet that cannot be read is skipped without discarding the rest of the workbook

Where a workbook lists a sheet whose stored content cannot be read — because the sheet's
stored part is empty, or because the tab is not a worksheet — the service SHALL skip that
sheet, record a per-file warning naming it and the reason, and SHALL still return the
results of every sheet that can be read. Such a sheet SHALL NOT produce a file-level
error.

#### Scenario: One sheet's stored part is empty

- **WHEN** a caller submits an XLSX file of two sheets, one of which has an empty stored
  part and one of which is readable
- **THEN** the file's result carries no error
- **AND** the result carries a warning naming the skipped sheet and stating that its
  stored part is empty
- **AND** the readable sheet is present in the returned tables with its cell values intact

#### Scenario: A tab is not a worksheet

- **WHEN** a caller submits an XLSX file whose tabs include one that holds a chart or a
  dialog rather than a worksheet
- **THEN** the file's result carries no error
- **AND** the result carries a warning naming that tab as skipped
- **AND** every worksheet in the same file is present in the returned tables

#### Scenario: No sheet in the workbook can be read

- **WHEN** a caller submits an XLSX file in which every listed sheet is skipped for one of
  these reasons
- **THEN** the file's result carries no error
- **AND** the result carries one warning per skipped sheet
- **AND** the result carries no tables
