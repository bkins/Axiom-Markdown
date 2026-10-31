# Axiom Markdown review delivery — October 30, 2026

Exact current review candidate: **0.2.1+9432d162414b**, unsigned Windows x64.

Source commit: **9432d162414be6f0614146d90dc3b858dd35e027** ([exact source](https://github.com/bkins/Axiom-Markdown/tree/9432d162414be6f0614146d90dc3b858dd35e027)). Public repository: [bkins/Axiom-Markdown](https://github.com/bkins/Axiom-Markdown).

ENH-274 implements the four requested interaction fixes. ENH-270 retains Ben's visual/usability and organizational security acceptance. The earlier [0.2.0](DELIVERY-0.2.0.md), [0.1.1](DELIVERY-0.1.1.md) and [0.1.0](DELIVERY-0.1.0.md) candidates remain retained; positive partial UI feedback is not whole-candidate acceptance.

## Four changes

1. One hamburger contains File, Edit, Format, Tools, View and Help, preserving existing feature-policy availability. F10 opens the root and Alt section keys remain available.
2. File/folder and editor context menus are restored for the retained Markdown scope. Shift+F10/context-menu-key requests open on key release so the popup remains visible; mouse selection semantics are preserved.
3. File/folder pointer drags use the same guarded move operation as the context menu. Targets highlight/expand, empty folders are available, and moves require confirmation and clean affected documents. Clean tabs and subsequent drafts follow the new path.
4. Caption controls use centered vector icons and explicit horizontal/vertical alignment, preserving native window roles and unsaved-close protection.

[Interaction responsibilities and design review](As-Built/ContextMenusAndExplorerMoves.md) · [Design decisions](As-Built/DesignDecisions.md) · [User Guide](USER-GUIDE.md).

## Verified

- 124 automated tests pass in exact-version Release, including overwrite/containment/metadata guards, NTFS junction rejection, folder/document/asset retention, and independent duplicate identity with preserved body spacing.
- All 38 native checks pass against this exact self-contained executable. They include the original preview/security/recovery/import/export checks plus hamburger grouping, measured caption centering, context formatting/undo, keyboard-popup lifecycle, dirty-move rejection, clean-tab rebinding, new-path drafts and foreign-drag rejection.
- Real computer-use pointer drags moved a file and a folder on this exact executable. Independent disk reads verified old-path absence, expected document text and exact asset bytes. Mouse file/folder/editor menus, F10 and Shift+F10 were observed. Native typing remained unsaved until Ctrl+S, which saved to the moved path without recreating the old path.
- All 265 payload hashes match after execution. Packaging verifies archived payload entries; production dependency versions are unchanged and the inventory still has 40 production NuGet packages.
- The standards source audit passes across 198 maintained C# files; the responsibility review is separate from mechanical checks. No temporary keyboard tracing remains in shipped source.
- The path-based extraction record now lists 119 retained upstream paths, 327 absent upstream paths and 77 new paths. Type/file moves and retained-scope adaptation explain path changes; these counts do not all represent removed functionality.

[Native results](checks/native-result.json) · [Pointer/menu results](checks/native-pointer-result.json) · [Keyboard/save results](checks/native-keyboard-result.json) · [Standards audit](checks/standards-audit.json) · [Release checksums](checks/SHA256SUMS.txt).

The unchanged dependency advisory checkpoint is recorded in [NuGet](checks/nuget-advisories.json), [Mermaid](checks/mermaid-advisories.json) and [terminal](checks/terminal-advisories.json) reports.

## Review package and limits

Local folder: **Axiom-Markdown-0.2.1-9432d162414b-win-x64**. Run **payload/Start-Axiom-Markdown.cmd** for isolated state. Portable ZIP, installer and update package/metadata are in releases; binaries/feed have not been published to GitHub Releases.

Use the outer **REVIEW-THIS-CANDIDATE.md** and **review/** packet, or the maintained [checklist](REVIEW.md), [Security packet](SECURITY-REVIEW.md) and [administrator controls](ADMIN-POLICY.md). Frozen payload documentation is from the build commit; later documentation commits do not change the source/binary identity above. Deferred ENH-275 tracks making every frozen package-document link self-contained; the outer packet supplies the exact current records.

Relative Markdown/image links are **not automatically rewritten** after moves. Dirty/conflicted affected documents must be saved/resolved explicitly first. Recycle/restore, broad folder/work-document behavior, visual/accessibility/other DPI settings, organizational security approval and signing/deployment/installed-update acceptance remain human checks. The filesystem guards are application checks, not an OS sandbox or a multi-file reference-repair transaction.

Separate deferred observations are recorded on the existing board: ENH-276 investigates existing caret/undo retention across explicit Save; ENH-277 tracks porting the identity-token-only fix to full Axiom. Neither was implemented in full Axiom or silently accepted here. Full Axiom's current reviews, stable opener, CP hold and PDF/OCR scope remain separate.
