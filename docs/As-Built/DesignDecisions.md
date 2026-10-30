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
