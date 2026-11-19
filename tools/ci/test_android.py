#!/usr/bin/env python3
"""Run packaged JVM tests from a clean checkout, without external QA corpora."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/android-tests')
args = parser.parse_args()
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
for old_log in out.glob('*.log'):
    old_log.unlink()
(out / 'checks.json').unlink(missing_ok=True)
classes = out / 'classes'
if classes.exists():
    shutil.rmtree(classes)
classes.mkdir()
src = ROOT / 'android/app/src/main/java'
tests = ROOT / 'android/tests'
learning = out / 'fixtures/tests/learning'
shutil.copytree(tests / 'learning', learning, dirs_exist_ok=True)
(out / 'fixtures/examples').mkdir(parents=True, exist_ok=True)
shutil.copyfile(tests / 'learning/results/report-di-misure.frociart',
                out / 'fixtures/examples/06_Report_di_misure.frociart')
cases = [
    ('EngineTests', ['--no-desktop-fixtures', str(out / 'roundtrip')]),
    ('CapacityTests', []), ('FlowPlacementTests', []), ('FlowInsertionTests', []),
    ('FlowOpenGuardTests', []), ('FlowGridSnapTests', []), ('FlowViewportTests', []),
    ('layout/FlowAutoLayoutTests', []),
    ('pdf/FlowPdfPagesTests', [str(tests / 'pdf/fixtures/parity-real-ui.frociart')]),
    ('learning/LearningTests', [str(learning)]), ('learning/FlowPracticeTests', []),
    ('drafts/FlowDraftStoreTests', []), ('FlowBackupTests', []),
    ('FunctionEditingTests', []), ('CapabilityTests', []), ('CycleViewTests', []),
    ('FileRecoveryTests', [str(out / 'file-recovery')]),
]
results = []


def run(name, command):
    try:
        done = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT, timeout=180)
        log, code = done.stdout, done.returncode
    except subprocess.TimeoutExpired as error:
        log, code = (error.stdout or b'') + b'\nTIMEOUT\n', 124
    (out / (name + '.log')).write_bytes(log)
    results.append({'name': name, 'exit_code': code, 'status': 'PASS' if code == 0 else 'FAIL'})
    (out / 'checks.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
    print(name, results[-1]['status'], flush=True)
    if code:
        print(log.decode('utf-8', errors='replace'), flush=True)
    return code


# Let javac resolve the current production dependency graph. The older packaged
# runners list dependencies manually and no longer compile the complete engine.
inputs = [str(tests / (name + '.java')) for name, _ in cases]
inputs.append(str(tests / 'drafts/stubs/android/content/SharedPreferences.java'))
if run('compile', ['javac', '--release', '17', '-encoding', 'UTF-8',
                   '-sourcepath', str(src), '-d', str(classes)] + inputs):
    raise SystemExit(1)
failed = False
for name, values in cases:
    failed |= bool(run(name.split('/')[-1], ['java', '-ea', '-cp', str(classes),
                                           'it.frociart.app.' + name.split('/')[-1]] + values))
print('Scope: JVM logic and host SharedPreferences adapter; Android UI/storage/rendering not run.')
raise SystemExit(1 if failed else 0)
