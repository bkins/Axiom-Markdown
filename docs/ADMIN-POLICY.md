# Administrator feature policy

Production controls use the 64-bit machine registry key `HKEY_LOCAL_MACHINE\SOFTWARE\AxiomMarkdown\Policy`. Use your organization's normal administrator deployment mechanism and retain the usual administrator-only write ACL on this key. The application only reads it; it does not change machine policy or request elevation.

Each optional value is a DWORD: `0` denies the capability; `1` permits a user to enable it. An absent value also permits user choice. A malformed value or unreadable policy denies the affected optional operation. Invalid/unreadable user feature preferences fail closed for all optional capabilities. There is no environment-variable or command-line override of machine policy.

| Value | Controls |
| --- | --- |
| AllowUpdates | App-managed update checks, package downloads and applying an update |
| AllowImport | HTML/DOCX conversion into Markdown |
| AllowExport | HTML/DOCX/PDF/TXT/EPUB/ODT export |
| AllowGit | App-managed Git inspection, history, staging and commits |
| AllowTerminal | Embedded and app-launched external terminal access |
| AllowRemoteContent | Explicitly approved HTTP(S) raster-image retrieval |

User preferences are stored in `features.json` under application state. They can restrict a permitted feature but cannot override a machine denial. Settings shows the effective reason. Optional service entry points re-read policy before work; export and remote image operations recheck before publishing results. A live policy change cancels app-managed update work and closes owned Git/terminal windows on the next recovery/policy tick (normally 500 ms; a busy modal operation may delay that UI tick). Already completed actions cannot be undone by a later policy change.

These are application feature controls, not an operating-system sandbox. An enabled terminal runs arbitrary programs as the user, including Git and download tools. To prevent those activities through the app, deny Terminal as well as the corresponding app-managed capability. OS/network controls remain authoritative outside this application. Disabling Export does not prevent copying text or using Save As for Markdown.

Remote image approval is separate from policy permission. Permission to use remote content never enables scripts, bypasses sanitization, permits HTTP redirects or grants trust to future destinations. Preview HTTP(S) resource loads are blocked at the webview boundary even after approval; approved images are fetched separately and embedded as raster data.

A disabled feature and its dependencies remain present in this build. If review requires excluding them from the source/payload, request a separate build and review its dependency and payload manifests. Do not treat a registry denial as evidence of code removal.
