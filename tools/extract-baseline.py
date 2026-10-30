"""One-time, explicit source selection. Never copies Git history or local app state."""
from pathlib import Path
import fnmatch, hashlib, json, subprocess

source = Path(__file__).resolve().parents[2] / 'Axiom'
target = Path(__file__).resolve().parents[1]
if any((target / 'src').glob('**/*.cs')):
    raise SystemExit('This one-time extractor refuses to overwrite an existing source tree. See extraction-manifest.json for the maintained final selection.')
baseline = '5a72d82c4c62b89199573b4b00f633572c030a2e'
patterns = {
 'Axiom.Core': '''Appearance.cs ApplicationBuildIdentity.cs AutomaticUpdatePreferences.cs Callout*.cs CSharp*.cs Contracts.cs Documents.cs DocumentService.cs IBatchIdentityStore.cs DocumentHeading.cs DocumentNodeReconciler.cs DocumentSourceRange.cs DocumentStructure*.cs Export*.cs Git*.cs LineDiff*.cs MarkdownExportService.cs MarkdownPreviewService.cs MarkdownStructureService.cs MarkdownDocumentLink.cs MarkdownReferenceDefinition.cs Preview*.cs TerminalInputState.cs WikiLinkInlineParser.cs Workspaces.cs WorkspacePathBoundary.cs PdfPageSize.cs MarkdownEditing.cs MarkdownEditService*.cs MarkdownListEditing.cs MarkdownListEditService*.cs MarkdownImageReferences.cs'''.split(),
 'Axiom.Infrastructure': '''AppDataPaths.cs AutomaticUpdatePreferencesStore.cs DocumentImportService.cs DocumentExportService.cs ImportMarkdownWriter.cs WordImportReader.cs ImportedImageSavePreparation.cs FileDocumentStore.cs Export*.cs HtmlExportWriter.cs TextExportWriter.cs WordExportWriter.cs EpubExportWriter.cs OpenDocumentExportWriter.cs IPdfExportRenderer.cs IStructuredPdfExportRenderer.cs PdfRenderResult.cs Git*.cs WindowsTerminalSession.cs ExternalFileMonitor.cs RollingFileLog.cs'''.split(),
 'Axiom.App': '''App.axaml ChoiceDialog.cs NativeHtmlContent.cs NativePdfExportRenderer.cs MermaidPreviewRenderer.cs GitTerminalView.cs RepositoryTerminal.cs WorkspaceGitView.cs AutomaticUpdateService.cs DocumentImportWindows.cs app.manifest Assets/Axiom.ico Assets/Terminal/* Assets/Mermaid/*'''.split()
}
tracked = subprocess.check_output(['git','ls-tree','-r','--name-only',baseline],cwd=source,text=True).splitlines()
entries=[]
excluded=[]
for name in tracked:
    parts=name.split('/')
    if len(parts)<3 or parts[0]!='src': continue
    project=parts[1]; relative='/'.join(parts[2:])
    if not any(fnmatch.fnmatchcase(relative, pattern) for pattern in patterns.get(project,[])):
        excluded.append(name); continue
    data=subprocess.check_output(['git','show',f'{baseline}:{name}'],cwd=source)
    path=target/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
    entries.append({'path':name,'upstreamSha256':hashlib.sha256(data).hexdigest()})
    if relative.startswith('Assets/') and relative.endswith('.txt'): pass
# Keep licenses belonging to selected imported parser and spelling-independent assets.
for name in ['src/Axiom.Infrastructure/Assets/ThirdParty/AngleSharp-LICENSE.txt']:
    data=subprocess.check_output(['git','show',f'{baseline}:{name}'],cwd=source)
    path=target/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    entries.append({'path':name,'upstreamSha256':hashlib.sha256(data).hexdigest()})
(target/'docs').mkdir(exist_ok=True)
(target/'docs'/'extraction-manifest.json').write_text(json.dumps({'upstream':'https://github.com/bkins/Axiom','commit':baseline,'selected':entries,'excluded':excluded},indent=2)+'\n')
print(f'Selected {len(entries)} files; excluded {len(excluded)} upstream source files. No local-only files copied.')
