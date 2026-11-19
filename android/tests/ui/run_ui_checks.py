#!/usr/bin/env python3
"""Install a signed Frociart update and run native UI checks on its existing AVD.

Build the application and test APK separately. This runner never compiles sources,
reads signing secrets, removes the app, clears app data, or starts another AVD.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

CERTIFICATE = 'c90205c3c890d191967069bfb02fa4c8cced27eced631d67c2175f3190be0be6'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', type=Path, required=True)
    parser.add_argument('--apk', type=Path, required=True)
    parser.add_argument('--test-apk', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--serial', default='emulator-5554')
    parser.add_argument('--android-user-home', type=Path)
    parser.add_argument('--java-home', type=Path, help='JDK used for signature verification')
    parser.add_argument('--scenario', choices=['portrait', 'narrow', 'large', 'both', 'all'], default='both')
    parser.add_argument('--phase', choices=['full', 'layout', 'editor-ime', 'study', 'files', 'drafts', 'journey', 'project-delete', 'mobile', 'details'], default='full')
    args = parser.parse_args()
    output = args.results.resolve()
    output.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ)
    if args.android_user_home:
        env['ANDROID_USER_HOME'] = str(args.android_user_home.resolve())
    suffix = '.exe' if os.name == 'nt' else ''
    adb = args.sdk.resolve() / ('platform-tools/adb' + suffix)
    java = args.java_home / ('bin/java' + suffix) if args.java_home else 'java'
    build_tools = args.sdk.resolve() / 'build-tools/35.0.0'

    def run(command, name, timeout=60, required=True):
        print(name, flush=True)
        result = subprocess.run([str(x) for x in command], env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, timeout=timeout)
        (output / (name + '.log')).write_text(result.stdout)
        if required and result.returncode:
            raise RuntimeError(name + ' failed: ' + result.stdout[-4000:])
        return result.stdout

    def device(arguments, name, timeout=60, required=True):
        return run([adb, '-s', args.serial] + arguments, name, timeout, required)

    apks = {}
    for name, path in [('application', args.apk), ('instrumentation', args.test_apk)]:
        path = path.resolve()
        verification = run([java, '-jar', build_tools / 'lib/apksigner.jar',
                            'verify', '--verbose', '--print-certs', path], name + '-signature')
        actual = re.search(r'certificate SHA-256 digest:\s*([0-9a-fA-F]+)', verification)
        if not actual or actual.group(1).lower() != CERTIFICATE:
            raise RuntimeError(name + ' has a different signing certificate; update cancelled')
        run([build_tools / ('zipalign' + suffix), '-c', '4', path], name + '-alignment')
        badging = run([build_tools / ('aapt2' + suffix), 'dump', 'badging', path], name + '-manifest')
        expected_package = 'it.frociart.app.continua' if name == 'application' else 'it.frociart.uichecks'
        if "package: name='" + expected_package + "'" not in badging:
            raise RuntimeError(name + ' has an unexpected package identity; update cancelled')
        apks[name] = {'path': str(path), 'bytes': path.stat().st_size,
                      'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                      'certificate_sha256': CERTIFICATE}
    (output / 'apk-sha256.json').write_text(json.dumps(apks, indent=2) + '\n')

    boot = device(['shell', 'getprop', 'sys.boot_completed'], 'boot-state').strip()
    if boot != '1':
        raise RuntimeError('The Frociart AVD has not finished booting')
    size = device(['shell', 'wm', 'size'], 'original-size')
    density = device(['shell', 'wm', 'density'], 'original-density')
    ime = device(['shell', 'settings', 'get', 'secure', 'show_ime_with_hard_keyboard'], 'original-ime').strip()
    font_scale = device(['shell', 'settings', 'get', 'system', 'font_scale'], 'original-font-scale').strip()
    old_size = re.search(r'Override size:\s*(\d+x\d+)', size)
    old_density = re.search(r'Override density:\s*(\d+)', density)
    device(['shell', 'pm', 'path', 'it.frociart.app.continua'], 'previous-installation')
    # The completed canvas harness must not be recreated on font/size changes:
    # its synchronous synthetic checks would compete with this UI matrix.
    device(['shell', 'am', 'force-stop', 'it.frociart.canvaschecks'], 'stop-completed-canvas-checks')
    device(['install', '--no-streaming', '-r', args.apk.resolve()], 'install-update', timeout=150)
    device(['install', '--no-streaming', '-r', args.test_apk.resolve()], 'install-instrumentation', timeout=150)
    scenarios = ['portrait', 'narrow', 'large'] if args.scenario == 'all' else ['portrait', 'narrow'] if args.scenario == 'both' else [args.scenario]
    failures = []
    try:
        for scenario in scenarios:
            device(['shell', 'wm', 'density', '180'], scenario + '-density')
            device(['shell', 'wm', 'size', '360x720' if scenario == 'narrow' else '432x960'], scenario + '-size')
            device(['shell', 'settings', 'put', 'system', 'font_scale',
                    '2.0' if scenario == 'large' else '1.3' if scenario == 'narrow' else '1.0'], scenario + '-font-scale')
            device(['shell', 'settings', 'put', 'secure', 'show_ime_with_hard_keyboard', '1'], scenario + '-ime')
            report = device(['shell', 'am', 'instrument', '-w', '-e', 'scenario', scenario, '-e', 'phase', args.phase,
                             'it.frociart.uichecks/it.frociart.uichecks.UiProbe'], scenario + '-instrumentation', timeout=420)
            # am instrument can return exit code zero even when the test catches a
            # failure. Require the test's explicit completion marker and zero errors.
            if (not re.search(r'UI_UPDATE_FINISHED\s+\d+\s+errors=0\b', report) or
                    re.search(r'(?m)^\s*(?:FAIL|UI_UPDATE_ERROR|UI_FINAL_ERROR)\b', report) or
                    'INSTRUMENTATION_FAILED' in report):
                failures.append(scenario)
    finally:
        try:
            capture_folder = 'uiqa-1.4.0' + ('' if args.phase == 'full' else '-' + args.phase)
            device(['pull', '/sdcard/Android/data/it.frociart.app.continua/files/' + capture_folder, output / 'captures'],
                   'pull-captures', timeout=150)
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            failures.append('screenshots: ' + str(error))
        device(['shell', 'wm', 'size', old_size.group(1) if old_size else 'reset'], 'restore-size', required=False)
        device(['shell', 'wm', 'density', old_density.group(1) if old_density else 'reset'], 'restore-density', required=False)
        restore = ['shell', 'settings', 'delete', 'secure', 'show_ime_with_hard_keyboard'] if ime == 'null' else [
                  'shell', 'settings', 'put', 'secure', 'show_ime_with_hard_keyboard', ime]
        device(restore, 'restore-ime', required=False)
        restore_font = ['shell', 'settings', 'delete', 'system', 'font_scale'] if font_scale == 'null' else [
                       'shell', 'settings', 'put', 'system', 'font_scale', font_scale]
        device(restore_font, 'restore-font-scale', required=False)
    if failures:
        raise RuntimeError('Native UI checks failed in: ' + ', '.join(failures))
    print('Native UI checks passed. Results: ' + str(output), flush=True)

if __name__ == '__main__':
    main()
