"""Build an allowlisted VPM ZIP and release manifest; never publish anything."""
import argparse
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', default=str(ROOT / 'dist~'))
    args = parser.parse_args()
    output = Path(args.output).resolve()
    manifest = json.loads((ROOT / 'package.json').read_text(encoding='utf-8-sig'))
    name, version = manifest['name'], manifest['version']
    archive = f'{name}-{version}.zip'
    manifest['url'] = f'https://github.com/malaybaku/MaterialOverrideVrmVRC/releases/download/{version}/{archive}'
    output.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    files = []
    for folder in ('Editor', 'Setup', 'Runtime'):
        files.extend(p for p in (ROOT / folder).rglob('*') if p.is_file())
        files.append(ROOT / f'{folder}.meta')
    for filename in ('LICENSE', 'README.md', 'README_usage.md', 'package.json.meta'):
        files.append(ROOT / filename)
        meta = ROOT / f'{filename}.meta'
        if meta.exists():
            files.append(meta)
    with ZipFile(output / archive, 'w', ZIP_DEFLATED) as bundle:
        bundle.writestr('package.json', encoded)
        for path in sorted(set(files)):
            if path.is_symlink():
                raise ValueError(f'Symlink is not allowed: {path}')
            bundle.write(path, path.relative_to(ROOT).as_posix())
    (output / 'package.json').write_bytes(encoded)
    print(output / archive)

if __name__ == '__main__':
    main()
