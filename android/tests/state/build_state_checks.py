#!/usr/bin/env python3
"""Build four controller regressions for the installed Frociart APK. Never uses ADB."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--sdk', type=Path, required=True)
parser.add_argument('--signing-dir', type=Path)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--work-dir', type=Path)
args = parser.parse_args()
tests = Path(__file__).resolve().parent
project = tests.parents[1]
work = args.work_dir.resolve() if args.work_dir else Path(tempfile.mkdtemp(prefix='frociart-state-build-'))
source = work / 'project/src/main'
java = source / 'java/it/frociart/statechecks'
java.mkdir(parents=True, exist_ok=True)
shutil.copy2(tests / 'StateProbe.java', java / 'StateProbe.java')
shutil.copy2(tests / 'AndroidManifest.xml', source / 'AndroidManifest.xml')
args.output.parent.mkdir(parents=True, exist_ok=True)
command = [sys.executable, str(project / 'build_apk.py'), str(work / 'project'), '--sdk', str(args.sdk.resolve()),
           '--output', str(args.output.resolve()), '--build-dir', str(work / 'build')]
if args.signing_dir:
    command += ['--signing-dir', str(args.signing_dir.resolve())]
with (work / 'build.log').open('w') as log:
    result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT)
if result.returncode:
    print((work / 'build.log').read_text()[-6000:])
    raise SystemExit(result.returncode)
production = project / 'app/src/main/java/it/frociart/app'
report = {'scope': 'Four native component/controller scenarios against the installed target app. '
                   'Fixtures and callbacks via reflection, production dialog View.performClick and native Back; '
                   'not a full manual-touch journey. Built only: runtime status NOT_RUN.',
          'instrumentation': 'it.frociart.statechecks/it.frociart.statechecks.StateProbe',
          'report_device_path': '/data/user/0/it.frociart.app/files/state-1.2.1/report.txt',
          'report_storage': 'Target internal getFilesDir(); resolved path in REPORT_FILE; full report also in instrumentation stream. External storage is not a prerequisite.',
          'production_reference_sha256': {name: hashlib.sha256((production / name).read_bytes()).hexdigest()
                                        for name in ('MainActivity.java', 'Ui.java', 'FlowCanvasView.java', 'FlowModel.java',
                                                     'FlowCodec.java', 'FlowEngine.java', 'FlowStore.java', 'FlowDraftStore.java')},
          'test_source_sha256': {name: hashlib.sha256((tests / name).read_bytes()).hexdigest()
                                for name in ('StateProbe.java', 'AndroidManifest.xml')},
          'apk_sha256': hashlib.sha256(args.output.read_bytes()).hexdigest(),
          'apk_path': str(args.output.resolve()), 'work_dir': str(work), 'signed': bool(args.signing_dir)}
(work / 'provenance.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
