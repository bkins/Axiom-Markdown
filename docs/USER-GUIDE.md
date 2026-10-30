# Axiom Markdown User Guide

## Documents

Open `.md` and `.markdown` files with Open or Ctrl+O. Open folder lists Markdown files under a folder; filter by name and double-click or press Enter to open a match. Ordinary files do not require enrollment or identity markers.

Launching another Markdown file with the same build/state forwards it into the running editor and retains dirty tabs. Different builds cannot share that recovery state; close the older build normally or use the isolated review launcher. This app does not register a file association automatically.

Each document has its own editor, Undo history, cursor, view mode and unsaved marker. Choose Split, Editor or Preview above the document. Ctrl+N creates a new document, Ctrl+S saves, Ctrl+Shift+S saves all, Ctrl+W closes the current tab, and Ctrl+F opens find/replace. Save As chooses another Markdown path. Closing unsaved work offers Save, Discard changes or Cancel.

Use Format for headings, bold, italic, lists, links, tables, code and hard breaks; Ctrl+B and Ctrl+I format the selection. View controls font size, word wrap and System/Light/Dark appearance. Those presentation preferences persist locally.

Draft recovery protects unsaved buffers in local SQLite without saving document files. On the next launch, choose Recover, Discard draft or Keep for later. Closing the recovery prompt keeps the draft. Recovery is not an independent backup of saved files.

External changes reload a clean buffer. If your buffer is dirty, choose Reload saved file, Keep local version or Keep editing. Keep local version authorizes a subsequent explicit Save; Keep editing preserves the conflict and blocks saving over it. External deletion keeps the buffer and directs Save to a new path.

## Preview and trust

Markdown, tables, task lists, fenced code, basic raw HTML and local raster images are supported. Mermaid uses a bundled local runtime with strict settings. Local images must be inside the document folder or selected containing folder and cannot traverse linked filesystem ancestors. SVG image embedding and active HTML elements are blocked.

When HTML sanitization removes or changes content, a visible banner says **Preview sanitized**. Details lists the types of changes. Mermaid output cleanup is reported as well. The Markdown source and its unsaved state are not changed by preview sanitization.

Remote images show as blocked. **Review external content** lists exact destinations and explains the connection/privacy risk. **Keep blocked** is the default; **Load for this session** fetches only the displayed raster images without redirects, cookies or credentials. Loaded images are kept in memory for that document session. **Block external content**, closing the tab or externally reloading the file clears them; closing the app also clears trust. New destinations require review. Organization policy can prohibit remote content entirely. The preview webview never loads HTTP(S) resources directly.

Document scripts, event handlers, stylesheets, embedded frames/objects and unsupported markup do not become trusted when you approve images. External links require deliberate confirmation before opening in the system browser. Relative Markdown links open another tab. Basic wiki links resolve as relative paths; classification/title/alias-based knowledge-management lookup is excluded.

## Import and export

File → Import converts local HTML/HTM or DOCX into a new unsaved Markdown document after a conversion preview with warnings. Originals remain unchanged. Supported images travel with the draft and become local companion assets on explicit Save. PDF and OCR import are not implemented in this edition.

File → Export captures the current buffer, including unsaved edits, without saving it. Choose HTML, Word DOCX, PDF, text, EPUB or ODT. Warnings report simplified/removed content, including raw HTML reduced to text and unsupported images. Existing outputs require confirmation; open/source documents and application state are protected destinations. Export does not fetch remote images; replace remote images with local files if you need them embedded. Mermaid exports as readable diagram source. Windows PDF uses WebView2 and still needs real-document quality review.

## Git and terminal

Tools → Git changes inspects an existing repository, shows saved/index diffs, stages or unstages selected Markdown files and commits selected staged content with confirmation. Unsaved/conflicted buffers are protected. This UI does not auto-save, fetch, pull or push. Non-Markdown and unsupported repository cases are informational and may require external Git.

Tools → Document history browses Git revisions and compares text. Restoring a revision changes the buffer only after confirmation and protects it for recovery; the file changes only on Save. Set up local Git history explicitly creates a repository without a remote. Checkpoint saved document writes a separate local history checkpoint without committing the branch/index or saving the buffer.

Tools → Embedded terminal opens Windows PowerShell in the selected folder after an explanation. It has your account's permissions and can run arbitrary commands; command buttons prepare text without pressing Enter. Closing it stops its owned shell. Treat commands like commands in any other terminal.

## Feature controls and updates

Tools → Feature controls and policy shows effective controls for Updates, Import, Export, Git, Terminal and RemoteContent. Users can turn capabilities off; administrators can deny them through machine policy. Disabled operations are rejected by the underlying service and relevant menu items are disabled. If terminal access is allowed, arbitrary shell commands remain possible regardless of the app's Git or updater switches.

Installed copies check the separate Axiom-Markdown GitHub feed at startup and every six hours, download an available package and wait for a deliberate restart. Tools → Check for updates shows progress in the status line. Save/close dirty documents before restarting. Portable review packages do not install or update themselves. No feed release is published merely by building this repository.

Local data lives under `%LOCALAPPDATA%\AxiomMarkdown`, or the separate review state path printed by its launcher. It includes plaintext recovery drafts, document paths, preferences, diagnostic errors and browser cache; Windows account permissions protect it. No CP service, AI key, backend or model is needed.

Version 0.1.1 reorganizes the maintained source to follow Ben's coding standards. Its document, policy, sanitation-warning and remote-content behavior is intended to remain the same as 0.1.0.

## Familiar Axiom interface (0.2.0)

The top command bar follows original Axiom: New, Open, Save, Reading mode and Settings icons, with File, Edit, Format, Tools, View and Help grouped in the top-left hamburger. Hover over an icon for its name/shortcut. Alt+F/E/M/T/V/H opens the corresponding menu; F10 opens the main menu. The title region supports ordinary Windows dragging and caption controls.

Settings (the sun/gear icon, or View → Editor and appearance settings) saves the theme, quiet/rounded menu buttons, editor font size/wrapping, compact/native title bar and navigator/outline visibility. Cancel leaves preferences unchanged. Light, Dark and System are available.

Workspace Files uses a folder tree. Open a folder, expand its branches and double-click a Markdown file or press Enter on it. The filter icon shows/hides the filename filter; filtering matches relative filenames and shows their containing branches. Folder actions (the ellipsis) opens or refreshes the folder. View can hide the navigator.

The right rail toggles Outline. Select a heading to move the editor caret to its source. Ctrl+Shift+R toggles reading mode and split editing without changing text. View also offers editor-only and split modes. Resize visible pane dividers as needed.

Tabs retain dirty/conflict indicators and explicit close buttons. The bottom bar shows operation/recovery status, source word count/caret position and the exact version. Sanitization and blocked remote images appear in a separate warning bar with Details and the existing trust choices. These notices do not rewrite your document.

## Context menus and explorer moves (0.2.1)

The top-left hamburger now contains File, Edit, Format, Tools, View and Help. F10 opens the main menu; Alt+F/E/M/T/V/H opens a section. Right-click in the editor for undo/redo, clipboard actions, find/replace and Markdown formatting. A selection remains selected when you right-click inside it. Shift+F10 or the context-menu key opens the menu for the focused editor or file tree.

Right-click a file/folder in Workspace Files for relevant actions: open, new file/folder, rename, move, duplicate a Markdown file, recycle, copy path, reveal in File Explorer and refresh. Empty folders are visible and can receive moves. Duplicate copies saved text as UTF-8 with a new document identity when applicable; save dirty source documents explicitly first.

Drag a file or folder onto another folder, then review and confirm Move. Drop onto empty tree space to move to the workspace root; hover near an edge to scroll. Folder targets highlight and expand. Escape cancels a drag. The Move context action provides a keyboard/picker alternative.

Save affected documents and resolve conflicts before moving them. Existing destinations, moves outside the workspace, linked paths, self/descendant moves and version-control metadata are rejected. Moving a folder includes its contents; open clean tabs follow the new paths. Relative Markdown and image links are **not automatically rewritten** and may need adjustment after a move. Cancel leaves the operation unapplied.

Recycle requires explicit confirmation and sends items to the Windows Recycle Bin; restore them there. Dirty/conflicted open documents block recycling. Caption icons are now centered; normal native window actions and unsaved-close protection still apply.
