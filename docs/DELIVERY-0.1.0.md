# Axiom Markdown review delivery — October 30, 2026

Exact review candidate: **0.1.0+a38096d2e00e**, Windows x64, unsigned.

Source commit: **a38096d2e00e994fbdf2ade49b16db940ae02417** ([browse exact source](https://github.com/bkins/Axiom-Markdown/tree/a38096d2e00e994fbdf2ade49b16db940ae02417)). Public repository: [bkins/Axiom-Markdown](https://github.com/bkins/Axiom-Markdown).

Implementation owners ENH-266/269 have separate acceptance owner ENH-270 on the existing multi-project board. Ben's usability review and organizational security approval remain pending.

## Verified

- 112 tests pass in both Debug and Release; Release tests use this exact informational version.
- 15 checks pass against the exact self-contained packaged app: actual WebView2 rendering, source unchanged by sanitization, visible notice, source-editor automatic links disabled, document scripts/handlers absent, remote images blocked, bundled Mermaid, recovery, forwarded second-file activation with dirty-tab retention, explicit Save, import warnings, HTML export and native PDF export.
- Independent native keyboard typing and Ctrl+S were exercised on this exact packaged executable; separate file reads confirmed that typing did not save and Ctrl+S did save.
- 260 portable payload file hashes verified; packaging verified those files survive the installer/portable archive. The payload manifest records the full source commit and version.
- Source extraction: 118 retained upstream paths, 328 excluded upstream paths and 10 new source files. The manifest pins current source bytes; full Axiom's prior history and local state are absent.
- NuGet and the reviewed Mermaid/xterm dependency locks report zero advisory findings at this checkpoint. Mermaid was rebuilt at 12.2.0 to replace inherited affected versions; available third-party licenses/notices and the production dependency inventory/SPDX document are included.

[Native results](checks/native-result-0.1.0.json) · [Keyboard results](checks/native-keyboard-result-0.1.0.json) · [NuGet advisories](checks/nuget-advisories.json) · [Mermaid advisories](checks/mermaid-advisories.json) · [Terminal advisories](checks/terminal-advisories.json).

## Review package

The local candidate directory is **Axiom-Markdown-0.1.0-a38096d2e00e-win-x64**. Run **payload/Start-Axiom-Markdown.cmd** for isolated review state. Its releases directory contains an update-enabled installer, portable ZIP, full update package and generated feed metadata; these binaries/feed have not been published to GitHub Releases. The public repository makes source available for Security's review.

Use the maintained [review checklist](REVIEW-0.1.0.md), [User Guide](USER-GUIDE.md), [Security packet](SECURITY-REVIEW.md) and [administrator controls](ADMIN-POLICY.md). The frozen payload contains the checklist template from its build commit; this delivery record and maintained checklist record completed automation after packaging. Source/binary identity remains the commit above even when later documentation commits update this record.

Installer deployment, signing policy, real installed update/feed acceptance, accessibility/DPI, representative-document conversion/rendering and organizational security approval remain human checks. Terminal feature gates are application controls, not an OS sandbox. Full Axiom, its prior candidates/stable opener and deferred CP/PDF/OCR work remain separate; its inherited Mermaid dependency follow-up is deferred ENH-271.

## Release-asset SHA-256

2CBED80C348CE46DCD6B5A97CB842843EC8DD074698076E78E175F5A9DFC7555  releases\assets.win-x64-stable.json
75F3265F9CD10F2B847BF09F43029A722DE7610219B07CCA43883A920B5ACB57  releases\AxiomMarkdown-0.1.0-win-x64-stable-full.nupkg
4503108C83AE8F448B78CD64073F11BDC6A7E716FF69CA2BDA114C1BF64B0CFC  releases\AxiomMarkdown-win-x64-stable-Portable.zip
47C5658EE194A176EB68506AD5DE32085132ADCF7FFE5F42876D89BD4A1F013C  releases\AxiomMarkdown-win-x64-stable-Setup.exe
43406764FAE9F84EAF0007A9BBB7753AFB1607643E59C5C96F64CFE687A7EA27  releases\RELEASES-win-x64-stable
AFD7AD28C2E77670AA9DBEE170D99A9718520B7A01E6C705BEA587824A7B0F6A  releases\releases.win-x64-stable.json


[Machine-readable release asset checksums](checks/SHA256SUMS.txt).
