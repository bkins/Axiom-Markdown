# Axiom Markdown review delivery — October 30, 2026

Exact review candidate: **0.1.1+001f084f9875**, Windows x64, unsigned.

Source commit: **001f084f98752840b4239ea09ad4662adcd1c696** ([exact source](https://github.com/bkins/Axiom-Markdown/tree/001f084f98752840b4239ea09ad4662adcd1c696)). Public repository: [bkins/Axiom-Markdown](https://github.com/bkins/Axiom-Markdown).

ENH-272 covers standards alignment; ENH-270 retains Ben's and organizational security acceptance. The original [0.1.0 delivery](DELIVERY-0.1.0.md) remains retained and unaccepted. This candidate has its own folder and state name.

## Verified

- 112 tests pass in Debug and Release; final Release tests use this exact informational version.
- All 15 native checks pass against this exact self-contained executable, including WebView2 rendering, visible sanitation details, unchanged source, blocked remote images, local Mermaid, recovery, file forwarding/dirty-buffer retention, import warning, HTML export and native PDF export.
- Native keyboard typing and Ctrl+S passed on this executable; independent disk reads confirmed typing did not save and Ctrl+S saved the expected text. Computer-use skill provided the native input.
- All 263 payload hashes match after execution. Packaging also verifies archived payload entries.
- The source audit passes across 183 maintained C# files. The [responsibility review](As-Built/StandardsAlignment.md) and [design decisions](As-Built/DesignDecisions.md) explain architectural boundaries and remaining tradeoffs; automated style checks do not establish SOLID compliance.
- The refreshed extraction manifest records 114 retained upstream paths, 332 absent upstream paths and 68 new paths. Type/file moves explain some path changes; these counts do not describe removed functionality. Attribution, dependency inventory and bundled-asset notices remain included.

[Native results](checks/native-result.json) · [Keyboard results](checks/native-keyboard-result.json) · [Standards audit](checks/standards-audit.json).

The earlier dependency advisory checkpoint is recorded in [NuGet](checks/nuget-advisories.json), [Mermaid](checks/mermaid-advisories.json) and [terminal](checks/terminal-advisories.json) reports. This refactor does not change production dependency versions.

## Review package

Local directory: **Axiom-Markdown-0.1.1-001f084f9875-win-x64**. Run **payload/Start-Axiom-Markdown.cmd** for isolated state. The releases directory contains the portable ZIP, installer and update package/metadata. Binaries and feed have not been published to GitHub Releases. Use the [review checklist](REVIEW.md), [User Guide](USER-GUIDE.md), [Security packet](SECURITY-REVIEW.md) and [administrator controls](ADMIN-POLICY.md).

The frozen payload contains build-time documentation; the outer review packet and maintained checklist record completed post-package evidence. Later documentation commits do not change the binary source identity above.

Representative-document usability/conversion quality, accessibility/DPI, organization security approval, signing/deployment and installed-update/feed acceptance remain pending. Terminal controls are application gates, not an OS sandbox. Full Axiom's reviews, stable opener, CP hold and PDF/OCR scope remain separate.

## Release-asset SHA-256

See [machine-readable checksums](checks/SHA256SUMS.txt).
