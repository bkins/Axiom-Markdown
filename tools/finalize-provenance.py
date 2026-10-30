"""Refresh an extraction record from the exact upstream tree without modifying source."""
from pathlib import Path
from io import BytesIO
from zipfile import ZipFile
import hashlib, json, subprocess

root = Path(__file__).resolve().parents[1]
upstream = root.parent / 'Axiom'
commit = '5a72d82c4c62b89199573b4b00f633572c030a2e'
archive = subprocess.check_output(['git', 'archive', '--format=zip', commit, 'src'], cwd=upstream)
with ZipFile(BytesIO(archive)) as baseline:
    original = {name: baseline.read(name) for name in baseline.namelist() if not name.endswith('/')}
current = {path.relative_to(root).as_posix(): path.read_bytes() for path in (root / 'src').rglob('*')
           if path.is_file() and not {'bin', 'obj'}.intersection(path.relative_to(root / 'src').parts)}
digest = lambda data: hashlib.sha256(data).hexdigest()
retained = [{'path': name, 'upstreamSha256': digest(original[name]), 'currentSha256': digest(current[name]), 'modified': original[name] != current[name]}
            for name in sorted(original.keys() & current.keys())]
new = [{'path': name, 'sha256': digest(current[name])} for name in sorted(current.keys() - original.keys())]
manifest = {'upstream': 'https://github.com/bkins/Axiom', 'commit': commit, 'retained': retained,
            'excluded': sorted(original.keys() - current.keys()), 'new': new,
            'note': 'Fresh repository history; manifests list current source selections rather than hidden feature flags. No upstream Git history or local state is copied.'}
(root / 'docs' / 'extraction-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(f'{len(retained)} retained upstream files, {len(manifest["excluded"])} excluded upstream files, {len(new)} new source files.')
