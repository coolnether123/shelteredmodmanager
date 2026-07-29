# Agent Status Notes

This folder is for coordination between concurrent agents working on the Paralives API refactor.

Each agent should keep one status note and update only its own file unless the assignment explicitly says otherwise. Use a stable file name such as `agent-8-docs-verification.md`.

## Status Note Rules

- Record facts, not intent. If a detail is unknown, write `Unknown` and name the dependency.
- Keep the note current before finishing a turn, before handing work to another agent, and when blocked by another module.
- Do not rewrite another agent's note. Link to it or quote a short relevant fact if needed.
- Include exact command output for failing verification when the failure may be environmental, such as a missing local game path.
- Keep file lists scoped to files actually touched by that agent.

## Suggested Sections

```markdown
# Agent N: Short Scope

Last updated: YYYY-MM-DD

## Scope

## What Changed

## Files Touched

## Documentation Added

## Verification Scripts Added

## Assumptions

## Risks

## Tests And Verification

## Follow-Up Needed
```

If an agent has no code changes, say so explicitly. If a build was not run because only documentation changed, record the specific verification commands that were run instead.
