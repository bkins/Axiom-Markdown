# Required coding standards

Ben explicitly designated `The CP Universe/Documentation/Development/STANDARDS.md` as the coding authority for Axiom Markdown on October 30, 2026. The canonical local location is recorded in AGENTS.md. This summary supplies the rules for contributors without that documentation checkout; it does not replace the canonical document.

- Apply Clean Code and SOLID principles; review responsibilities and dependency boundaries as well as formatting.
- Treat acronyms as ordinary words in PascalCase identifiers and filenames. Prefer descriptive full words; avoid single-letter words in identifiers and single-character lambda/LINQ variables except simple counters.
- Give classes and records their own files. Keep interfaces with related implementations; do not introduce an Interfaces folder for a small module.
- Use leading commas in enum and initializer lists. Align assignment operators/property values vertically.
- Wrap long parameter lists one parameter per line. Keep the first parameter on the method-signature line, then align the leading commas and parameter types. Align long fluent chains.
- Use expression-bodied members only for short, trivial expressions. Prefer var when the type is obvious. Expand compound statements so control flow is readable.
- Complete verification, active backlog/status updates, relevant planning changes, architecture/As-Built and durable design decisions, and user-guide changes when applicable.
- Capture unrelated bugs/ideas with backlog IDs instead of fixing them in the active workstream.
- Ground domain-specific statements in inspected evidence and make the evidence reproducible.

Standards compliance is reviewed separately from functional verification and organizational security approval. A successful build, formatter or test suite cannot establish Clean Code/SOLID compliance by itself.
