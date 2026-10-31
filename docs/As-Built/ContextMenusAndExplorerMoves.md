# Context menus, explorer moves and command access

ENH-274 addresses Ben's four-item follow-up to 0.2.0. Original Axiom remains the interaction reference; this edition does not import its larger management/mutation shell.

## UI responsibilities

The command bar has one hamburger containing File, Edit, Format, Tools, View and Help. Existing policy-aware MenuItems are moved into these groups, preserving their service guards and availability. F10 opens the root; Alt+F/E/M/T/V/H opens a section. Shift+F10 or the context-menu key opens the focused editor/navigator context menu.

Editor context actions cover undo/redo, cut/copy/paste, select all, find/replace and Markdown formatting. Right-click preserves an existing selection when invoked inside it. Explorer context actions cover open, new file/folder, rename, move, duplicate Markdown file, recycle, copy path, reveal and refresh. Only the retained Markdown scope is offered; batch management, enrollment, pinning and other unimplemented original-app actions are absent.

The caption buttons use centered 12-unit vector icons with explicit horizontal/vertical content alignment. Their native minimize/maximize/close roles and existing closing protections remain intact. The native probe measures each icon's actual center against its button center.

## Filesystem boundary and state

MarkdownWorkspaceOperations owns local filesystem validation and operations. MainWindow.ExplorerActions owns user review, affected-tab checks, monitoring, clean-buffer path rebinding and identity bookkeeping. Context menus and drag/drop call the same move path. A failed post-move refresh reports that the item already moved instead of claiming the disk operation failed.

Moves reject existing destinations, workspace-root moves, descendant/self moves, paths outside the open root, linked files/directories (including junctions), and version-control metadata. Folder moves include their documents/assets. Empty folders are discoverable as destinations; .git/.hg/.svn metadata is excluded from navigation. This is a local application guard, not a sandbox against another process racing filesystem changes.

Affected dirty/conflicted tabs block a move, duplicate or recycle action. They are not silently saved/discarded. Moves require confirmation, disable affected editors while changing paths, suppress obsolete watchers, rebind open tabs and resume monitoring. Recovery after subsequent editing uses the new file path. The original full-Axiom reference-repair/mutation-receipt engine is not included: relative Markdown/image links are not rewritten, and the confirmation explains this limitation. There is no claimed multi-file reference-repair transaction or crash-reconciliation guarantee for concurrent external filesystem changes.

Duplicate creates a separate UTF-8 Markdown document and replaces an existing identity with a new one. Identity replacement now changes only the ID, preserving the marker's delimiters and body spacing; the new duplicate regression test exposed and verifies this dependency. Source content remains untouched.

Recycle uses the Windows Recycle Bin after explicit app confirmation, with no permanent-delete fallback. Native recycle/restore remains a human review item. Reveal launches the Windows explorer.exe from its fixed Windows directory with structured arguments.

## Drag lifecycle and verification

Dragging starts after a six-unit pointer threshold. A per-drag token and original root validate the application-private payload; foreign/stale drags are rejected. Valid folder targets are highlighted/expanded, edge scrolling is bounded, and leave/drop/cancel/close stop the scroll timer and clear target state. Dropping in empty tree space targets the workspace root. A drop never invokes a separate unchecked filesystem path.

Automated checks cover collisions, containment, metadata, NTFS junctions, exact byte/asset retention, duplicate identity/body separation, dirty-buffer blocking, open-tab rebinding and new-path drafts. The junction unit fixture uses a hidden test-only Windows PowerShell process to create/remove an owned junction; production moves do not run shell commands. Native routed-event checks are recorded separately from actual pointer gestures and independent disk reads. Existing preview sanitation, optional-feature policy, recovery and export checks continue to run. Human visual/DPI, representative-folder and organization security acceptance remain separate.

Keyboard context requests are queued on key-down and opened on key-up. Opening on key-down allowed the context menu's own F10 release handling to close it immediately; native regression checks verify the deferred opening remains visible. The request is cleared on deactivation and menu closure releases the retained menu reference.
