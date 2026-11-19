#!/usr/bin/env python3
"""Verify this migration against the two original release ZIPs, without extracting."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('android_zip', type=Path)
parser.add_argument('windows_zip', type=Path)
args = parser.parse_args()
manifest = json.loads((root / 'tools/ci/source-import.json').read_text())
for archive, record in zip([args.android_zip, args.windows_zip], manifest['archives']):
    if hashlib.sha256(archive.read_bytes()).hexdigest() != record['sha256']:
        raise SystemExit('Archive SHA256 mismatch: ' + str(archive))
    with zipfile.ZipFile(archive) as z:
        files = [i for i in z.infolist() if not i.is_dir()]
        if len(files) != record['files'] or len({i.filename for i in files}) != len(files):
            raise SystemExit('Unexpected or duplicate archive members')
        for info in files:
            name = PurePosixPath(info.filename)
            if name.is_absolute() or '..' in name.parts or name.parts[0] != record['directory']:
                raise SystemExit('Unexpected archive path')
            current = root / str(name)
            expected = manifest['adjustments'].get(str(name), {}).get('sha256_after')
            expected = expected or hashlib.sha256(z.read(info)).hexdigest()
            if not current.is_file() or hashlib.sha256(current.read_bytes()).hexdigest() != expected:
                raise SystemExit('Migration content differs: ' + str(name))
    print(record['name'], 'PASS:', len(files), 'files verified')
print('Only the documented migration adjustments differ from the release ZIPs.')
