# Axiom Markdown review delivery — October 30, 2026

Exact current review candidate: **0.2.0+17be08c5f01c**, unsigned Windows x64.

Source commit: **17be08c5f01cb38910a8c4f3143d667c0d2a97b3** ([exact source](https://github.com/bkins/Axiom-Markdown/tree/17be08c5f01cb38910a8c4f3143d667c0d2a97b3)). Public repository: [bkins/Axiom-Markdown](https://github.com/bkins/Axiom-Markdown).

ENH-273 adapts original Axiom's presentation to the retained Markdown scope. ENH-270 keeps Ben's visual/usability and organizational security acceptance separate. Earlier [0.1.0](DELIVERY-0.1.0.md) and [0.1.1](DELIVERY-0.1.1.md) candidates remain retained and unaccepted.

## What changed

The compact command/title bar, vector controls, quiet/rounded menus, tab treatment, theme palette, folder navigator, outline rail and status layout now follow original Axiom. Ordinary appearance choices persist. Ctrl+Shift+R toggles reading mode; Alt menu keys/F10 expose commands. Startup fits the initial window to the monitor working area after compact title decoration is initialized.

[Presentation mapping and responsibility review](As-Built/OriginalAxiomPresentation.md) · [Design decisions](As-Built/DesignDecisions.md).

The adaptation does not import full Axiom's CP/AI/semantic/classification/management code. Sanitization details and remote-image warnings retain their own visible surface. Optional features still use the existing UI/service policy gates.

## Verified

- 114 tests pass in Debug and Release; final Release tests use this exact informational version.
- All 24 native checks pass against this exact self-contained executable: existing rendering/security/save/recovery/import/export checks plus menu treatments, themes, window fit, outline selection, reading mode, navigator/title options, folder filtering, settings persistence and source/dirty-buffer preservation.
- Independently observed native typing and Ctrl+S saved exactly the expected text; separate disk reads confirmed typing alone did not save. Native Ctrl+Shift+R and F10 also behaved correctly. Input was retried after focus reobservation before recording the successful typing/save result.
- All 264 payload hashes match after execution. Packaging verifies payload entries in the portable archive. No new production dependency versions were introduced; the inventory still contains 40 production NuGet packages.
- The standards audit passes across 191 maintained C# files; responsibility review remains separate from mechanical checks.
- The refreshed path-based extraction record lists 117 retained upstream paths, 329 absent upstream paths and 73 new paths; type/file moves and presentation adaptation explain path changes, rather than all absent paths representing removed functionality.

[Native results](checks/native-result-0.2.0.json) · [Keyboard results](checks/native-keyboard-result-0.2.0.json) · [Standards audit](checks/standards-audit-0.2.0.json) · [Release checksums](checks/SHA256SUMS-0.2.0.txt).

The unchanged dependency advisory checkpoint remains in [NuGet](checks/nuget-advisories.json), [Mermaid](checks/mermaid-advisories.json) and [terminal](checks/terminal-advisories.json) reports.

## Review package

Local folder: **Axiom-Markdown-0.2.0-17be08c5f01c-win-x64**. Run **payload/Start-Axiom-Markdown.cmd** for isolated review state. Portable ZIP, installer and update package/metadata are in releases; binaries/feed have not been published to GitHub Releases.

Use the [current review checklist](REVIEW-0.2.0.md), [User Guide](USER-GUIDE.md), [Security packet](SECURITY-REVIEW.md) and [administrator controls](ADMIN-POLICY.md). Frozen payload documentation is from the build commit; the outer review packet records completed post-package verification. Later documentation commits do not change the binary identity above.

Ben's judgment of visual fidelity, representative-document workflows, accessibility/other DPI settings, organizational security approval and signing/deployment/installed-update acceptance remain pending. Full Axiom's reviews, stable opener, CP hold and PDF/OCR scope remain independent.
