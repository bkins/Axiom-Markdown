# Standards alignment — October 30, 2026

ENH-272 applies Ben's canonical coding standards to the standalone edition. The canonical document is `C:\Users\benho\source\Application Documentation\The CP Universe\Documentation\Development\STANDARDS.md`; the global Codex AGENTS.md now requires it across projects. The project summary is [CODING-STANDARDS.md](../CODING-STANDARDS.md).

## Responsibility review

Core owns document/session contracts, Markdown transformations and feature-policy contracts. Infrastructure owns persistence, filesystem/process/native integration, conversion writers and registry policy. The application composes these services and handles Avalonia/WebView2 interactions. Interfaces remain beside their modules. Optional capabilities enforce policy at service entry points as well as menu controls.

Each complete class, record, interface and enum now has its own named file. Private helper types use containing partial declarations in separate files, preserving encapsulation. MainWindow is divided into composition, layout, lifecycle, commands, tabs, files, external changes, editor, preview, navigation, settings, import/export, Git, updates and review support. These partial files organize one UI coordinator; they are not independent services. PreviewBrowserPolicy is a separate platform boundary for WebView2 settings and request interception.

The review checked ownership, dependency direction, policy enforcement, constructor execution order and preservation of document/source/recovery behavior. FeatureAccess remains a startup-configured policy facade; replacing it with broad dependency injection is unnecessary for this bounded refactor. Its process-wide lifetime and test serialization are recorded in the design decisions.

## Source conventions and verification

The refactor expands compound statements and long expression bodies, names lambda/local parameters descriptively, and aligns initializer assignments, parameter types and leading commas. Native structure field names and serialized/browser protocol members retain required external spelling. Third-party bundled JavaScript and generated build outputs retain upstream form and separate dependency/license review.

Run `scripts/Check-CodingStandards.ps1` for syntax, type/file organization, leading commas, wrapped signatures, compound statements and long expression members. The report covers maintained C# in src, tests and tools. It does not establish SOLID compliance or runtime safety; the responsibility review and behavioral evidence are separate.

No feature additions or deliberate behavior changes are part of this alignment. The original 0.1.0 candidate remains retained and unaccepted; 0.1.1 is a distinct review candidate. Human usability, organization security and installed-update acceptance remain ENH-270.
