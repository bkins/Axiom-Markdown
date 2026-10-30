# Original Axiom presentation in the Markdown edition

ENH-273 follows Ben's clarification that full Axiom is the look-and-feel reference. This is a presentation adaptation to the agreed Markdown scope, not a full-app feature port.

| Original presentation | Markdown implementation |
| --- | --- |
| Compact title/command row | Vector New/Open/Save/Reading/Settings controls, File/Edit/Format/Tools/View/Help flyouts, draggable title and native caption roles |
| Quiet and rounded menu treatments | Both persist under Settings; the quiet treatment is the default |
| Workspace sidebar | 280-unit resizable navigator, folder hierarchy, optional filename filter, refresh/open actions and keyboard opening |
| Document tabs | Rounded top corners, active accent, explicit close action, dirty/conflict labels and path tooltip |
| Editor and preview | Same Cascadia Mono/Consolas family and palette, current-line treatment, adjustable split, reading mode and editor-only mode |
| Discovery/tool rail | Markdown outline with heading selection and source navigation; persisted visibility |
| Status layout | Operation status, recovery status, source word/caret position and exact build identity |
| Appearance | Original page/surface/border/text palette in light/dark/system modes; separate compact/native title option |

The sidebar uses the retained WorkspaceTreeBuilder; it adds no enrollment, classification, semantic or CP data. The outline reads the current parsed Markdown source. Optional tools remain in the existing policy-aware menu/service paths. Sanitization and blocked-image notices have a dedicated warning surface beneath the command bar and remain visible when needed.

ShellStyles.axaml contains the adapted visual treatment. MainWindow.Shell owns composition/chrome; Layout owns pane assembly; Discovery owns navigator and outline interaction; EditorSettings owns the ordinary appearance dialog. Core MenuStyle and infrastructure preferences persist user choices with backward-compatible defaults. The existing window coordinator and startup policy-facade tradeoffs remain as documented in DesignDecisions.md.

The exact package receives native probes for both themes, menu treatments, title/navigation options, heading selection, reading mode, filter hierarchy, source/dirty-state retention and persistence, alongside the existing rendering/security/save/recovery checks. Native screenshots and independent keyboard testing supplement these checks. Ben's judgment of the visual match, representative workflows, accessibility/DPI and Security's approval remain separate human gates.

Original Axiom toolbar customization/placement, command palette, metadata/relationship tools, management workflows and other unported features are not implied by this presentation mapping. The full Axiom checkout, stable file opener and existing review candidates remain independent.

Native keyboard inspection found the first window could extend below the current monitor. Startup now bounds its size and position to the monitor working area at the current scale, before document initialization; an exact native check verifies that boundary. No existing user window is moved.
