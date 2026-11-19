#!/usr/bin/env python3
"""Compile and execute bounded layout tests with a JDK 17+; no Android SDK required."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
from datetime import datetime, timezone

parser = argparse.ArgumentParser()
parser.add_argument('--java', default='java')
parser.add_argument('--results', type=Path, default=Path(tempfile.gettempdir()) / 'frociart-layout-tests')
args = parser.parse_args()
project = Path(__file__).resolve().parents[2]
source_dir = project / 'app/src/main/java/it/frociart/app'
sources = [source_dir / (name + '.java') for name in (
    'FlowModel', 'FlowCodec', 'FlowFunctions', 'FlowFunctionEditing', 'FlowFunctionTransfer',
    'FlowDeclaration', 'FlowTextFiles', 'FlowFileRecovery', 'FlowTurtle', 'FlowEngine',
    'FlowDebugger', 'FlowPseudocode', 'FlowAutoLayout', 'FlowInsertion', 'FlowPlacement',
    'FlowGridSnap', 'FlowExamples', 'FlowFileBlocks', 'FlowFunctionData', 'FlowSource',
    'FlowSourceText', 'FlowSourcePlan')]
sources.append(Path(__file__).with_name('FlowAutoLayoutTests.java'))
args.results.mkdir(parents=True, exist_ok=True)
classes = args.results / 'classes'
classes.mkdir(exist_ok=True)
commands = [
    [args.java, '-m', 'jdk.compiler/com.sun.tools.javac.Main', '--release', '8', '-encoding', 'UTF-8', '-d', str(classes)] + [str(p) for p in sources],
    [args.java, '-ea', '-cp', str(classes), 'it.frociart.app.FlowAutoLayoutTests'],
]
log = []
for command in commands:
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=90)
    log.append('$ ' + ' '.join(command) + '\n' + result.stdout)
    print(result.stdout, end='')
    (args.results / 'layout-tests.log').write_text('\n'.join(log), encoding='utf-8')
    if result.returncode:
        raise SystemExit(result.returncode)
match = re.search(r'LAYOUT CHECKS PASSED: (\d+)', result.stdout)
if not match:
    raise SystemExit('Missing layout success marker')
metrics = []
for name, median, p95, samples in re.findall(r'METRIC (\S+) median_ms=([\d.]+) p95_ms=([\d.]+) samples=(\d+)', result.stdout):
    metrics.append({'case': name, 'median_ms': float(median), 'p95_ms': float(p95), 'samples': int(samples)})
report = {
    'recorded_utc': datetime.now(timezone.utc).isoformat(),
    'passed': int(match.group(1)),
    'source_sha256': {str(p.relative_to(project)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
    'runtime': subprocess.run([args.java, '-version'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, check=True).stdout.strip(),
    'timing_scope': 'Plain JVM wall-clock time for the complete apply(Document), including graph construction and all layout passes. Excludes fixture construction, Android UI, drawing, animation, device refresh and export. Eight warmups, 25 samples per case. These are environment observations, not a phone latency or FPS guarantee.',
    'metrics': metrics,
}
(args.results / 'layout-results.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
