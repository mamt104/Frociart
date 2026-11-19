#!/usr/bin/env python3
"""Run the signed canvas/motion harness on an existing Frociart Android test device.

This runner does not compile, start an emulator, clear app data or inspect private
signing material. It restores the system animation-scale setting even if the
instrumentation process fails before its own cleanup finishes.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

CERTIFICATE = 'f5d2fd4d1f51a0f74562e81b2949a0299fe46869500b07ddd52b5b013572055f'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--test-apk', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--serial', default='emulator-5554')
    parser.add_argument('--android-user-home', type=Path)
    parser.add_argument('--expected-sha256')
    args = parser.parse_args()
    output = args.results.resolve()
    output.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ)
    if args.android_user_home:
        env['ANDROID_USER_HOME'] = str(args.android_user_home.resolve())
    sdk, apk = args.sdk.resolve(), args.test_apk.resolve()
    adb = [sdk / 'platform-tools/adb', '-s', args.serial]
    build_tools = sdk / 'build-tools/35.0.0'

    def run(name, command, timeout=90):
        print(name, flush=True)
        result = subprocess.run([str(x) for x in command], env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, timeout=timeout)
        (output / (name + '.log')).write_text(result.stdout, encoding='utf-8')
        if result.returncode:
            raise RuntimeError(name + ': ' + result.stdout[-2500:])
        return result.stdout

    signature = run('signature', ['java', '-jar', build_tools / 'lib/apksigner.jar',
                                  'verify', '--verbose', '--print-certs', apk])
    actual_certificate = re.search(r'certificate SHA-256 digest:\s*([a-fA-F0-9]+)', signature)
    if not actual_certificate or actual_certificate.group(1).lower() != CERTIFICATE:
        raise RuntimeError('Unexpected signing certificate; installation cancelled')
    digest = hashlib.sha256(apk.read_bytes()).hexdigest()
    if args.expected_sha256 and digest != args.expected_sha256.lower():
        raise RuntimeError('Unexpected test APK SHA-256; installation cancelled')
    manifest = run('manifest', [build_tools / 'aapt2', 'dump', 'badging', apk])
    if "package: name='it.frociart.canvaschecks'" not in manifest:
        raise RuntimeError('Unexpected test package; installation cancelled')
    run('alignment', [build_tools / 'zipalign', '-c', '4', apk])
    (output / 'apk-sha256.json').write_text(json.dumps({
        'path': str(apk), 'bytes': apk.stat().st_size, 'sha256': digest,
        'certificate_sha256': CERTIFICATE,
    }, indent=2) + '\n', encoding='utf-8')
    if run('boot-state', adb + ['shell', 'getprop', 'sys.boot_completed']).strip() != '1':
        raise RuntimeError('Android has not completed boot')
    original = run('original-animator-scale', adb + ['shell', 'settings', 'get', 'global',
                                                   'animator_duration_scale']).strip()
    try:
        run('install', adb + ['install', '--no-streaming', '-r', apk], 150)
        report = run('motion-instrumentation', adb + ['shell', 'am', 'instrument', '-w',
                     'it.frociart.canvaschecks/it.frociart.app.MotionProbe'], 300)
        if (not re.search(r'MOTION CHECKS PASSED: \d+', report) or
                'MOTION CHECKS FAILED' in report or
                'CANVAS CHECKS PASSED: 118' not in report):
            raise RuntimeError('Motion checks failed; inspect motion-instrumentation.log')
        print('\n'.join(report.splitlines()[-6:]), flush=True)
    finally:
        # A completed test Activity must not rerun its synchronous regression
        # checks when the following UI matrix changes orientation/font size.
        run('stop-completed-canvas-checks', adb + ['shell', 'am', 'force-stop', 'it.frociart.canvaschecks'])
        current = run('after-animator-scale', adb + ['shell', 'settings', 'get', 'global',
                                                  'animator_duration_scale']).strip()
        if current != original:
            action = ['delete', 'global', 'animator_duration_scale'] if original == 'null' else [
                     'put', 'global', 'animator_duration_scale', original]
            run('restore-animator-scale', adb + ['shell', 'settings'] + action)
        run('pull-evidence', adb + ['pull', '/sdcard/Android/data/it.frociart.canvaschecks/files/motion-1.2.0',
                                   output / 'captures'], 150)

if __name__ == '__main__':
    main()
