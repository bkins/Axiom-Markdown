# Design decisions

## Separate product and explicit optional capabilities

Axiom Markdown has fresh repository history, its own application identity/state/update target, and physically selected source. CP, AI, semantic/classification/relationship and management features are excluded. Updates/downloads, conversion, Git and terminal remain available with local settings and fixed machine-policy denials. Terminal policy is an application gate, not an operating-system sandbox.

## Sanitized preview with informed remote access

Raw HTML passes through the DOM allowlist. Changes produce a visible notice and inspectable details without rewriting Markdown. Mermaid is bundled locally. Browser requests are blocked; approved remote images are fetched through a bounded application path after a document-session warning. Source editor links require deliberate navigation. Security review remains required before team distribution.

## Standards alignment and UI ownership

ENH-272 separates complete types and gives the window coordinator named responsibility files. Private native/test helpers retain containing partial wrappers to avoid widening accessibility. WebView2 hardening is extracted into PreviewBrowserPolicy. Partial UI organization is not claimed to eliminate the shared UI coordinator; cross-file state remains owned by that window.

FeatureAccess is initialized at startup with a policy provider and has process-wide lifetime. This keeps direct service checks consistent with the UI and avoids adding a container across the inherited services. Tests that replace this provider run without parallel interference. A future multi-policy, multi-host application would require instance-scoped policy injection.

## Exact candidate evidence

Source is committed before packaging. Informational versions include that source commit; payload and release hashes bind review evidence to the package. Later documentation commits do not change the binary identity. Retained candidates are not overwritten or silently accepted. Installed update/signing and representative human checks are independent of automated tests.

## Original Axiom is the presentation reference

ENH-273 adapts the original compact command bar, theme palette, menu treatments, tab chrome, navigator and outline/status layout to the smaller Markdown shell. Visual resources and focused UI responsibility files are reused/adapted without importing the full application's management shell. Appearance preferences have backward-compatible defaults and remain separate from Security policy; sanitization warnings retain a dedicated visible surface. See OriginalAxiomPresentation.md for the exact mapping and limits.

## Retained-scope interaction restoration

ENH-274 groups existing commands under one hamburger and restores focused editor/explorer context actions. All explorer move entry points share a narrow filesystem service and affected-document gate; no full management/mutation dependency is introduced. Dirty/conflicted documents block moves, and relative references are explicitly retained rather than automatically rewritten. Caption glyphs use measured centered vectors. See ContextMenusAndExplorerMoves.md for responsibilities, guards and verification limits.
