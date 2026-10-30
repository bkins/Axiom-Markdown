# Axiom Markdown working agreements

## Required coding standards

Ben explicitly requires this project to follow the canonical standards at:

`C:\Users\benho\source\Application Documentation\The CP Universe\Documentation\Development\STANDARDS.md`

Read that file before implementation or review when it is available. It is authoritative; do not assume that inherited Axiom code or a passing build meets it. The project summary in [docs/CODING-STANDARDS.md](docs/CODING-STANDARDS.md) makes the applicable rules available to contributors without that local documentation checkout.

Apply Clean Code and SOLID principles. Give each class/record its own file, including helpers; keep interfaces near implementations. Use leading commas in enum and initializer lists, vertically aligned assignments, descriptive names, and readable statement bodies. Wrapped parameter lists begin on the signature line and continue with aligned leading commas/types, one parameter per line. Do not use single-character lambda/LINQ variables except simple counters.

Keep unrelated findings in the existing backlog with an ID. Preserve exact review candidates and human/security acceptance boundaries. Update implementation evidence, architecture/design decisions and user documentation when applicable. Never mark a security or human review accepted from automated checks.

The existing multi-project board remains the implementation-state authority. App Hub remains Ben's current handoff tracker; preserve one record per project and use its revision-safe tools rather than editing handoff storage directly.
