# Third-party software

NuGet packages and available license expressions are enumerated in [dependency-inventory.json](dependency-inventory.json) and [dependency-sbom.spdx.json](dependency-sbom.spdx.json). Entries with `NOASSERTION` require separate license inspection; an inventory is not a completed license or security approval.

Bundled browser assets retain their notices:

- [Mermaid notice](../src/Axiom.App/Assets/Mermaid/THIRD-PARTY-NOTICES.txt)
- [Terminal/xterm notice](../src/Axiom.App/Assets/Terminal/THIRD-PARTY-NOTICES.txt)
- [AngleSharp license](../src/Axiom.Infrastructure/Assets/ThirdParty/AngleSharp-LICENSE.txt)

The portable/installed payload carries the bundled-asset, AngleSharp and Microsoft WebView2 notices under `ThirdPartyNotices`. WebView2 itself, Windows PowerShell and installed Git are external runtime dependencies with their own servicing and licenses. Third-party notices do not grant a project-wide license to Axiom Markdown.
