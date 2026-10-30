# Source provenance

Derived with Ben Hopkins's authorization from `bkins/Axiom` commit **5a72d82c4c62b89199573b4b00f633572c030a2e** (Axiom 1.9.0). This repository has a new root history; it does not import Axiom's prior commits, artifacts, local configuration, review state or document data.

The extraction uses an explicit file selection. The [manifest](extraction-manifest.json) records original SHA-256 values for retained upstream files, their final hashes, excluded upstream paths and new source files. Selected core/infrastructure modules are adapted for policy checks and safe rendering. A new smaller application shell replaces the larger knowledge-management shell; the reduced SQLite store contains only editor recovery/session and document-identity bookkeeping.

Retained components include document sessions and atomic file saving, Markdown editing/structure/rendering, import/export writers, local Git/history services, the native terminal view, bundled Mermaid/xterm and update client. Excluded components include CP/AI setup and clients, semantic analysis/search, classification/relationship management, bulk enrollment/transfer and the larger management/workbench shell. The manifest is an extraction record, not a statement that every retained line is necessary or that every excluded component is insecure.

The repository is public for security review at Ben's explicit request. Copyright/authorship and third-party notices are retained. NuGet package licenses and bundled-asset notices are listed separately; public source publication alone does not select a new project-wide open-source license.

Full Axiom remains independently maintained. Future Markdown fixes can be selectively ported with a new source reference and dependency review; full-project merges are not automatic.

Standards alignment in 0.1.1 moves types into named files and divides the UI coordinator. The manifest is path-based: a removed upstream path may contain types relocated into the new-source list, rather than removed functionality. Git rename/diff history and docs/As-Built/StandardsAlignment.md explain this distinction; the original delivery record retains its original extraction counts.
