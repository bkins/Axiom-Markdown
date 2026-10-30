# Axiom Markdown

A separate Windows Markdown editor derived from Axiom. It keeps ordinary Markdown files, editing and preview, recovery, HTML/Word import, multi-format export, Git history/changes, an embedded terminal, and optional automatic updates.

CP/AI connections, semantic analysis, classification/relationship management and the larger personal knowledge-management shell are physically excluded. This repository starts with fresh history; [provenance](docs/PROVENANCE.md) records the originating Axiom commit and selected/excluded files.

This is an unsigned review candidate. Organizational security approval and human acceptance are tracked separately from automated verification.

## Build and verify

Requirements: Windows, .NET 10 SDK and the Microsoft Edge WebView2 Runtime. Git is required only for Git features; the embedded terminal uses Windows PowerShell. Internet access is needed to restore NuGet packages on a new machine.

```powershell
dotnet restore Axiom.Markdown.slnx
dotnet test Axiom.Markdown.slnx
dotnet run --project src/Axiom.App
```

Create an exact-commit portable review package and an optional update-enabled installer using PowerShell 7:

```powershell
.\scripts\Publish-Review.ps1 -WithInstaller
```

The packaging script refuses an existing output directory. It never publishes a release or changes file associations. Run the generated `Start-Axiom-Markdown.cmd` for isolated review state. Direct executable launches and installed copies use `%LOCALAPPDATA%\AxiomMarkdown`; full Axiom's settings and updater are independent.

## Review and controls

- [User Guide](docs/USER-GUIDE.md)
- [Review checklist](docs/REVIEW.md)
- [Security review packet](docs/SECURITY-REVIEW.md)
- [Administrator policy](docs/ADMIN-POLICY.md)
- [Source provenance](docs/PROVENANCE.md)
- [Dependency inventory](docs/dependency-inventory.json)
- [Third-party notices](docs/THIRD-PARTY.md)

Use **Tools → Feature controls and policy** to disable optional features. Organization policy takes precedence. A disabled capability's code remains in this edition; further physical exclusions require a separately verified build.

The preview reports sanitization with a visible notice and details. Remote images remain blocked until you approve the listed destinations for the current document session; original Markdown is unchanged. Mermaid runs locally and document-supplied scripts remain blocked.

Coding and review: [standards](docs/CODING-STANDARDS.md), [responsibility review](docs/As-Built/StandardsAlignment.md), [design decisions](docs/As-Built/DesignDecisions.md). Run scripts/Check-CodingStandards.ps1 alongside the behavioral tests.
