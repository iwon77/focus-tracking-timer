# AGENTS.md

## Purpose

This repository contains the Focus Tracking Timer project code.

Codex and other LLM agents must read this file before changing project files.
Use this document as the first decision rule for scope, implementation, verification, and reporting.

## Core Rule

Work only on what the user explicitly requested.

Do not add unrelated improvements, future-proofing, cleanup, refactors, design changes, documentation changes, CI changes, or automation changes unless the user asked for them.

If an additional change is required to complete the requested work, stop before making that change and explain:

- Why the extra change is needed
- Which files or behavior it affects
- What options the user has

Continue only after the user approves the additional work.

## Request Scope

Before implementing, identify the exact requested scope.

Include:

- What will be changed
- What will not be changed
- Files likely to be touched
- Any unclear behavior that affects implementation

If the request can be interpreted in multiple ways, do not choose silently.
Explain the difference and ask for confirmation.

## No Unrequested Improvements

Do not perform improvements just because they look useful.

Examples of work that requires user approval first:

- Refactoring unrelated code
- Renaming unrelated files, classes, methods, or variables
- Changing UI text, layout, style, or behavior outside the request
- Changing persistence, database schema, paths, or config outside the request
- Adding abstractions for future use
- Adding new tests that require changing unrelated production code
- Removing dead code discovered outside the requested work

If the improvement may be valuable, report it as a suggestion instead of implementing it.

## File And Function Structure

Do not put too much responsibility into one file.

Use clear, feature-based names for files, classes, and functions.

Prefer small units:

- UI files should focus on UI structure.
- ViewModel files should focus on display state.
- Feature or service files should focus on behavior.
- Model files should describe data shape.
- Utility files should contain shared helper logic only when shared use already exists.

Do not introduce a new layer, folder, abstraction, or helper only because it might be useful later.
Add structure only when it is needed for the requested work.

## Related Files Only

Modify only files directly required for the current task.

Do not change unrelated files, formatting, naming, comments, or styles.

Do not revert, overwrite, or clean up user changes unless the user explicitly asks.

If a build or test failure appears to require changing unrelated code, stop and report:

- The failure
- Why unrelated code appears to be involved
- The recommended next step

## Existing Code And Dead Code

Do not delete existing code just because it appears unused.

If unused or dead code is found, report it first.

Include:

- File path
- What the code appears to do
- Why it may be unused
- Possible impact of removal

Delete it only after user approval.

Exception: unused imports, variables, functions, or files created by the current task may be removed as part of the same task.

## Implementation Rules

Keep implementation narrow and verifiable.

Follow these rules:

- Preserve existing behavior unless the user requested behavior changes.
- Do not add new features while refactoring.
- Do not change UI unless the request is UI-related.
- Do not change storage behavior unless the request is persistence-related.
- Do not change public behavior to make implementation easier.
- Keep comments rare and only add them when they clarify non-obvious logic.

## Verification

After code changes, run the smallest appropriate verification.

Prefer:

- `dotnet build .\FocusTrackingTimer.sln`
- `dotnet test .\FocusTrackingTimer.sln --no-build`

If verification cannot be run, report why.

When a change is documentation-only, build/test may be skipped if the reason is stated.

## Reporting

After completing work, summarize briefly:

- What changed
- Main files changed
- Verification result
- Any risk or follow-up that needs user decision

Do not produce long file-by-file changelogs unless the user asks.

## Git And PR Rules

Do not create a PR without user approval.

Before creating a PR, share:

- PR title
- PR body draft

Create the PR only after the user approves the draft.

PR title should follow commit-message style unless the user says otherwise.

When writing PR content, include:

- Overview
- Changes
- Code flow
- Verification
- Decisions, if relevant
- Review points, if relevant

If no new feature was added, state that clearly.
