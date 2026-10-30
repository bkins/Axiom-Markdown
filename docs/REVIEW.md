# Axiom Markdown 0.2.0 review

Status: **0.2.0+17be08c5f01c** is ready for original-Axiom presentation review under ENH-273. Exact post-package evidence is recorded in [the delivery record](DELIVERY.md). ENH-270 retains Ben's and Security's acceptance; earlier 0.1.0/0.1.1 candidates are preserved separately.

## Automation can test

- [x] Exact-commit Release build and automated tests; no excluded-feature source/dependency contamination.
- [x] Packaged native WebView2 (24 checks, including shell/options/navigation/window fit): retained Markdown/HTML/Mermaid, visible sanitation details, blocked remote images, no document scripts/event handlers and unchanged source bytes.
- [x] Recovery persistence, explicit Save, save-in-flight typing, external conflicts and selected Git operations.
- [x] Import warning/source protection and HTML/DOCX/TXT/EPUB/ODT export; native PDF smoke check.
- [x] Each feature rejects direct service calls when disabled; policy precedence and malformed settings fail closed.
- [x] Portable/installer payload hashes, isolated product/state/feed, dependency inventory/SBOM and NuGet advisory report.
- [x] Independently observed native keyboard editing and Ctrl+S saved-file verification.

- [x] Source audit passes across 191 maintained C# files; responsibility/design review recorded separately in As-Built. Source trailing whitespace check passes.

## Human needs to test

- [ ] Compare the compact command/title bar, tabs, navigator and outline with original Axiom. Try quiet/rounded menus, both title-bar modes, light/dark/system themes, pane resizing, heading selection and reading mode; report visual differences that matter.
- [ ] Launch the exact portable candidate with its isolated launcher; review font/theme, layout, tabs, view modes, keyboard access, screen reader and relevant DPI settings.
- [ ] Open representative work Markdown. Confirm preview sanitation notices/details, remote-content warning and blocked/default choice, deliberate external-link navigation, local images and Mermaid. Source must remain unchanged until you edit/save.
- [ ] Import representative HTML/DOCX, inspect warnings and conversion quality, save images and reopen; try the export formats you need, especially PDF pagination and raw-HTML simplification.
- [ ] Review Git history/stage/commit and the terminal in a disposable repository; confirm organization policy expectations and the terminal's general command-execution boundary.
- [ ] Have Security review the public source, architecture, removed/retained features, dependencies/notices, local plaintext drafts/cache, network/process behavior and fixed HKLM deployment controls. Record any required feature denials or physical exclusions.
- [ ] Before team distribution, complete the organization's installer/signing/update-feed review and real installed-update checks. Record exact approved commit/package hashes and any restrictions.

Do not mark these checks accepted from silence, packaging, source publication or automated evidence. Full Axiom's prior candidates, stable opener, held CP work and deferred PDF/OCR remain separate.

