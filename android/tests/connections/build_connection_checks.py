#!/usr/bin/env python3
"""Build a native connection harness. This script never controls ADB or an emulator.

--baseline-zip compiles the actual delivered 1.2.0 Canvas and Model from that archive
and a probe that reproduces the reported defects. Otherwise current production
sources, the historical 118 canvas checks and the new regression probe are used.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
from zipfile import ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--signing-dir', type=Path)
    parser.add_argument('--baseline-zip', type=Path)
    parser.add_argument('--work-dir', type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2]
    tests = project / 'tests/connections'
    work = args.work_dir.resolve() if args.work_dir else Path(tempfile.mkdtemp(prefix='frociart-connections-build-'))
    source = work / 'project/src/main'
    java = source / 'java/it/frociart/app'
    java.mkdir(parents=True, exist_ok=True)
    copied = {}
    for name in ('FlowModel.java', 'FlowCanvasView.java'):
        if args.baseline_zip:
            with ZipFile(args.baseline_zip) as archive:
                data = archive.read('FrociartAndroid/app/src/main/java/it/frociart/app/' + name)
        else:
            data = (project / 'app/src/main/java/it/frociart/app' / name).read_bytes()
        (java / name).write_bytes(data)
        copied[name] = hashlib.sha256(data).hexdigest()
    probe = 'ConnectionBaselineProbe' if args.baseline_zip else 'ConnectionProbe'
    for name in ('ConnectionChecksActivity.java', probe + '.java'):
        shutil.copy2(tests / name, java / name)
    if not args.baseline_zip:
        shutil.copy2(project / 'tests/canvas/FlowCanvasChecks.java', java / 'FlowCanvasChecks.java')
    (source / 'AndroidManifest.xml').write_text('''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="it.frociart.connectionchecks" android:versionCode="121" android:versionName="1.2.1-connection-checks">
  <uses-sdk android:minSdkVersion="26" android:targetSdkVersion="35"/>
  <application android:label="Frociart connection checks" android:theme="@android:style/Theme.Material.Light.NoActionBar" android:allowBackup="false" android:debuggable="true">
    <activity android:name="it.frociart.app.ConnectionChecksActivity" android:exported="true"/>
  </application>
  <instrumentation android:name="it.frociart.app.''' + probe + '''" android:targetPackage="it.frociart.connectionchecks" android:functionalTest="true"/>
</manifest>\n''', encoding='utf-8')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    command = ['python3', str(project / 'build_apk.py'), str(work / 'project'), '--sdk', str(args.sdk.resolve()),
               '--output', str(args.output.resolve()), '--build-dir', str(work / 'build')]
    if args.signing_dir:
        command += ['--signing-dir', str(args.signing_dir.resolve())]
    with (work / 'build.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    data = dict(mode='delivered-1.2.0-baseline' if args.baseline_zip else 'current-regression',
                production_sha256=copied, apk_sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(),
                apk_path=str(args.output.resolve()), work_dir=str(work), instrumentation='it.frociart.connectionchecks/it.frociart.app.' + probe)
    (work / 'provenance.json').write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(data, indent=2))


if __name__ == '__main__':
    main()
