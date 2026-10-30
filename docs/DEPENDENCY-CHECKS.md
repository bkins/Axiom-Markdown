# Dependency check checkpoint

October 30, 2026. The restored production NuGet inventory contains 40 packages; an SPDX document accompanies it. The local NuGet advisory scan reported no vulnerable packages. It includes transitive dependencies and is a point-in-time advisory result.

The inherited Mermaid 11.16.1 lock produced three affected-package entries (DOMPurify critical, Mermaid and KaTeX low). This edition rebuilds the offline bundle at pinned Mermaid 12.2.0 with its updated transitive lock. `npm audit --ignore-scripts --json` against the resulting lock reports zero findings. The builder disables lifecycle scripts, refuses external bundle imports, hashes the runtime and regenerates bundled-license notices.

A separate package-lock audit for the retained xterm 6.0.0 and addon-fit 0.11.0 versions also reports zero findings. The original retained asset bytes remain traceable to the extraction manifest. External WebView2, Windows PowerShell and installed Git are separately serviced runtimes and are not covered by a NuGet/npm result.

Reproduction: restore the solution and run `dotnet list Axiom.Markdown.slnx package --vulnerable --include-transitive --format json`. Rebuild Mermaid using its Assets/Mermaid README and run npm audit in that vendor workspace. The [dependency inventory](dependency-inventory.json), [SPDX document](dependency-sbom.spdx.json), runtime manifest and bundled notices identify the reviewed inputs; full release acceptance still requires the security checklist.
