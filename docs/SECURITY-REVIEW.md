# Security review packet

## Scope and architecture

Axiom Markdown is a separate Windows desktop application. `Axiom.Core` owns Markdown parsing/rendering/editing, document sessions and feature decisions. `Axiom.Infrastructure` owns file/state access, conversion/export, Git process execution, remote image retrieval and the pseudoconsole. `Axiom.Markdown` composes the Avalonia editor and WebView2 preview, optional tool windows and updater.

The full Axiom personal knowledge-management shell, CP/AI clients/setup, semantic services, classification/relationship management and their state tables are excluded from source rather than hidden. This edition has fresh history and no backend/model setup. [Provenance](PROVENANCE.md) and the [extraction manifest](extraction-manifest.json) identify selected/excluded upstream files, final hashes and new code. Selected components retain their original namespaces to keep their origin clear.

The smaller shell is newly composed around selected Axiom components; it is not a cosmetic mode of full Axiom and does not claim feature parity with every full-Axiom workflow. Its Windows application, storage, installer identity and update feed are separate. No existing Axiom association or review opener is retargeted.

One writer owns each state root. Additional file launches use a bounded same-user named pipe and must match the exact informational build identity; queued requests open tabs after the current dialog/operation completes. A different build cannot write into the running build's recovery state. This local IPC opens no TCP listener.

## Document and renderer boundaries

Markdown is data. Markdig creates HTML; an AngleSharp DOM allowlist strips active/unsupported elements, attributes and unsafe links before content is placed in the trusted HTML shell. The parser has no resource loader or scripting engine. The webview CSP denies scripts, frames, objects, connections, fonts and form actions; images are data URIs. Windows WebView2 additionally denies unapproved file and network resource requests, blocks popups and disables host objects/web messaging in the document preview. Link/task/source navigation uses fresh render tokens and validates the current source.

Bundled Mermaid is invoked by host code against sanitized code blocks, with strict configuration, no embedded configuration, text/edge/diagram limits and output filtering. The host reports Mermaid cleanup. Sanitizer details and conversion/export warnings are user visible; preview changes never rewrite Markdown. Raw HTML support intentionally excludes document scripts, event handlers, custom CSS, SVG/MathML, frames, forms and active objects. This is a maintained allowlist, not a claim that arbitrary HTML is safe.

Local preview images are raster files bounded by size, lexical folder containment and rejection of linked ancestors; inline SVG images are blocked. Remote images are blocked until explicit approval of listed destinations for that document session. A separate HTTP client requires policy, disables redirects/cookies/default credentials, bounds time/input/decoded image dimensions and returns raster data. The browser itself cannot load external resources. External links ask before opening in the system browser. User-approved remote requests may reveal IP address and request details; trust is not persisted across app sessions.

Import reads bounded HTML/DOCX snapshots without executing content or fetching remote resources; output is reviewed and opened unsaved. Embedded images remain self-contained until explicit Save. Export uses a snapshot of the active buffer, warns about reduced/removed content and uses temporary output plus atomic publication. It does not fetch remote images. PDF is generated in a separate native webview from the controlled export HTML. Source/open documents and application state are protected export destinations.

## Files, processes and network

The editor reads user-selected Markdown/folders and referenced permitted local images; explicit saves write Markdown and imported companion images. Recovery and preferences are local, under `%LOCALAPPDATA%\AxiomMarkdown`; review launchers choose a separate root. Recovery drafts are plaintext and may contain work content; they are not encrypted by this app. SQLite sessions/drafts and identity bookkeeping replace Axiom's larger metadata schema. Browser cache and unhandled-error diagnostics may contain document/path information. There is no application telemetry service.

WebView2 has its own runtime/processes. Git features launch `git` with inherited repository overrides removed, bounded output and explicit selected-file/hash checks; repository hooks/filters are restricted for supported operations. Terminal starts Windows PowerShell in a selected folder using the Windows pseudoconsole and owns its shell lifecycle. The terminal is intentionally general-purpose and is not sandboxed. Never infer that disabling app-managed Git or updates prevents equivalent commands in an enabled terminal.

The updater uses Velopack and the separate `https://github.com/bkins/Axiom-Markdown` feed. It is gated before creating/checking/downloading/applying update clients; installed copies check at startup and every six hours. A ready download requires an explicit restart and clean buffers. Portable builds do not self-update. No release feed is created by the build script. Packages are unsigned unless a separate signing process is configured; installed feed/update compatibility still requires organizational/native review.

See [administrator policy](ADMIN-POLICY.md) for fixed HKLM controls, user precedence, refresh timing and limits. A feature denied by policy remains in source and dependencies. Machine policy is an application control for the official binary, not protection against a user rebuilding modified source or running other software.

## Review evidence and limits

The candidate's `payload-manifest.json` pins every portable payload file to its SHA-256 and exact source commit. Packaging verifies every hashed payload file survives installer/portable archive creation; generated installer metadata is additional. NuGet dependency inventory and an SPDX document cover restored production-project packages, with bundled JS documented separately. Package hashes and source inspection establish identity, not acceptance.

Automated tests cover malicious HTML/links/images, sanitation notices, disabled direct service calls, editing/recovery/save races, selected Git operations and export formats. The owned-window native probe verifies actual WebView2 headings, scripts/handlers absent, remote images blocked, bundled Mermaid, notices, unchanged source, recovery and explicit Save; it captures the app surface, hosted preview and DOM. Native keyboard input and Ctrl+S are checked against an independently read saved file. See the [review checklist](REVIEW.md) for exact candidate evidence and remaining human checks.

NuGet advisory results are a point-in-time check, not proof that packages are vulnerability-free. Bundled Mermaid/xterm assets and the separately serviced WebView2/Windows/Git runtimes need their own review. Real-document conversion/rendering, accessibility, DPI, installer/update deployment and organization security acceptance remain human gates. Linux/macOS support is not claimed by this Windows candidate.

The inherited Mermaid 11.16.1 dependency tree produced DOMPurify and Mermaid/KaTeX advisory findings during this extraction. This edition rebuilds the offline bundle with pinned Mermaid 12.2.0 and patched transitive dependencies; the updated npm audit reports zero findings. The separately checked xterm 6.0.0/addon-fit 0.11.0 tree and NuGet advisory scan also report zero findings at this checkpoint. These results do not establish exploitability of full Axiom's configuration; its unchanged bundle is a separate follow-up.

Design references: [Microsoft WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security), [AngleSharp parser documentation](https://github.com/AngleSharp/AngleSharp/blob/devel/docs/general/01-Basics.md).
