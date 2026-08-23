# Developer commenting standard

Write comments for mod authors and maintainers who need to understand a contract without tracing its implementation.

## Document

- Public and protected APIs used by mod authors.
- Loader lifecycle boundaries, including bootstrap, initialization, start, scene changes, and shutdown.
- Reflection, Harmony, threading, persistence, and cleanup rules that are not obvious from the code.
- Preconditions, ownership, failure behavior, and compatibility limits.

## Style

- Use XML documentation on public and protected members and key internal entry points.
- Start summaries with the action or fact. Use "Registers", "Returns", or "Runs before".
- Use the exact type, method, option, and file names from the code.
- Keep one thought per sentence.
- Put a constraint beside the API it limits.
- Use inline comments only for non-obvious runtime behavior.

## Avoid

- Restating the syntax.
- Historical change logs in source files.
- Unverified claims such as "safe", "automatic", or "always".
- Rhetorical headings, marketing language, jokes, and scratch notes.
- Comments that describe an implementation detail as a public guarantee.
