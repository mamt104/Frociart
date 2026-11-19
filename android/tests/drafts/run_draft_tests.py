#!/usr/bin/env python3
"""Compile production FlowDraftStore against a host-only SharedPreferences signature stub."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--java', default='java')
parser.add_argument('--results', type=Path, default=Path(tempfile.gettempdir()) / 'frociart-draft-tests')
args = parser.parse_args()
test_root = Path(__file__).resolve().parent
project = test_root.parents[1]
sources = [project / 'app/src/main/java/it/frociart/app/FlowDraftStore.java',
           test_root / 'stubs/android/content/SharedPreferences.java',
           test_root / 'FlowDraftStoreTests.java']
args.results.mkdir(parents=True, exist_ok=True)
log = []
with tempfile.TemporaryDirectory(prefix='frociart-draft-classes-') as classes:
    commands = [[args.java, '-m', 'jdk.compiler/com.sun.tools.javac.Main', '--release', '8',
                 '-encoding', 'UTF-8', '-d', classes] + [str(p) for p in sources],
                [args.java, '-ea', '-cp', classes, 'it.frociart.app.FlowDraftStoreTests']]
    for command in commands:
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, timeout=60)
        log.append('$ ' + ' '.join(command) + '\n' + result.stdout)
        print(result.stdout, end='')
        (args.results / 'draft-tests.log').write_text('\n'.join(log), encoding='utf-8')
        if result.returncode:
            raise SystemExit(result.returncode)
match = re.search(r'DRAFT STORE CHECKS PASSED: (\d+)', result.stdout)
if not match:
    raise SystemExit('Missing draft test completion marker')
report = {'recorded_utc': datetime.now(timezone.utc).isoformat(), 'passed': int(match.group(1)),
          'scope': 'JVM behavior of production FlowDraftStore with a host-only SharedPreferences adapter. '
                   'Tests independent drafts, ordering, exact text, legacy migration, corruption, failed '
                   'commit recovery, removal and re-creation from simulated durable state. No Android UI, '
                   'real device filesystem, SharedPreferences scheduling or frame rate claim.',
          'source_sha256': {str(p.relative_to(project)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
          'runtime': subprocess.run([args.java, '-version'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    text=True, check=True).stdout.strip()}
(args.results / 'draft-results.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
