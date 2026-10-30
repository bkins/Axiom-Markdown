# Axiom Markdown 0.1.0 review

Status: Candidate preparation underway. Exact commit/version, package paths/hashes and final verification results will be recorded in the delivery record before handoff. Ben's and Security's approval remain pending under ENH-270; implementation owners are ENH-266 and ENH-269 on the existing multi-project board.

## Automation can test

- [ ] Exact-commit Release build and automated tests; no excluded-feature source/dependency contamination.
- [ ] Packaged native WebView2: retained Markdown/HTML/Mermaid, visible sanitation details, blocked remote images, no document scripts/event handlers and unchanged source bytes.
- [ ] Recovery persistence, explicit Save, save-in-flight typing, external conflicts and selected Git operations.
- [ ] Import warning/source protection and HTML/DOCX/TXT/EPUB/ODT export; native PDF smoke check.
- [ ] Each feature rejects direct service calls when disabled; policy precedence and malformed settings fail closed.
- [ ] Portable/installer payload hashes, isolated product/state/feed, dependency inventory/SBOM and NuGet advisory report.
- [ ] Independently observed native keyboard editing and Ctrl+S saved-file verification.

## Human needs to test

- [ ] Launch the exact portable candidate with its isolated launcher; review font/theme, layout, tabs, view modes, keyboard access, screen reader and relevant DPI settings.
- [ ] Open representative work Markdown. Confirm preview sanitation notices/details, remote-content warning and blocked/default choice, deliberate external-link navigation, local images and Mermaid. Source must remain unchanged until you edit/save.
- [ ] Import representative HTML/DOCX, inspect warnings and conversion quality, save images and reopen; try the export formats you need, especially PDF pagination and raw-HTML simplification.
- [ ] Review Git history/stage/commit and the terminal in a disposable repository; confirm organization policy expectations and the terminal's general command-execution boundary.
- [ ] Have Security review the public source, architecture, removed/retained features, dependencies/notices, local plaintext drafts/cache, network/process behavior and fixed HKLM deployment controls. Record any required feature denials or physical exclusions.
- [ ] Before team distribution, complete the organization's installer/signing/update-feed review and real installed-update checks. Record exact approved commit/package hashes and any restrictions.

Do not mark these checks accepted from silence, packaging, source publication or automated evidence. Full Axiom's prior candidates, stable opener, held CP work and deferred PDF/OCR remain separate.
