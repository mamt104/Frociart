#!/usr/bin/env python3
"""Build a dependency-free Java Android app with official SDK command-line tools.

Example:
  python3 build_apk.py /path/to/project --output /path/to/Frociart.apk \
    --sdk /path/to/Android/Sdk --signing-dir /private/frociart-signing

Project layout: app/src/main/{AndroidManifest.xml,java,res,assets} (or src/main).
Only generated build output is replaced. Application source files are never edited.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import zipfile

def run(command, **kw):
    subprocess.run([str(value) for value in command], check=True, **kw)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project', type=Path)
    parser.add_argument('--sdk', type=Path, default=Path(os.environ.get('ANDROID_SDK_ROOT', str(Path(__file__).resolve().parent / 'sdk'))))
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--build-dir', type=Path)
    parser.add_argument('--signing-dir', type=Path)
    parser.add_argument('--build-tools', default='35.0.0')
    parser.add_argument('--compile-sdk', default='35')
    parser.add_argument('--min-sdk', default='26')
    parser.add_argument('--target-sdk', default='35')
    parser.add_argument('--alias', default='frociart')
    parser.add_argument('--java-home', type=Path, help='JDK directory (for example, an existing JDK 17 or later)')
    args = parser.parse_args()
    started = time.monotonic()
    project = args.project.resolve()
    source = next((p for p in (project / 'app/src/main', project / 'src/main', project) if (p / 'AndroidManifest.xml').exists()), None)
    if source is None:
        parser.error('AndroidManifest.xml not found in project, src/main, or app/src/main')
    sdk = args.sdk.resolve()
    android = sdk / ('platforms/android-' + args.compile_sdk) / 'android.jar'
    bt = sdk / 'build-tools' / args.build_tools
    executable_suffix = '.exe' if os.name == 'nt' else ''
    aapt2, zipalign = (bt/(name+executable_suffix) for name in ('aapt2','zipalign'))
    java_home = args.java_home or (Path(os.environ['JAVA_HOME']) if os.environ.get('JAVA_HOME') else None)
    java = str(java_home/'bin'/('java'+executable_suffix)) if java_home else shutil.which('java')
    compiler = str(java_home/'bin'/('javac'+executable_suffix)) if java_home else shutil.which('javac')
    if not java or not Path(java).is_file():
        parser.error('Java not found. Set --java-home to an existing JDK directory.')
    for tool in (android, aapt2, bt/'lib/d8.jar', zipalign, bt/'lib/apksigner.jar'):
        if not tool.is_file():
            parser.error('Required SDK component missing: ' + str(tool))
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    build = (args.build_dir.resolve() if args.build_dir else Path(tempfile.mkdtemp(prefix='frociart-apk-build-')))
    build.mkdir(parents=True, exist_ok=True)
    classes, generated, dex = (build/n for n in ('classes', 'generated', 'dex'))
    for folder in (classes, generated, dex):
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir()
    resources = build/'resources.zip'
    unsigned = build/'unsigned.apk'
    aligned = build/'aligned.apk'
    link = [aapt2, 'link', '-o', unsigned, '-I', android,
            '--manifest', source/'AndroidManifest.xml', '--java', generated,
            '--min-sdk-version', args.min_sdk, '--target-sdk-version', args.target_sdk,
            '--auto-add-overlay', '--no-version-vectors', '--custom-package', 'it.frociart.app']
    if (source/'res').is_dir():
        if resources.exists():
            resources.unlink()
        run([aapt2, 'compile', '--dir', source/'res', '-o', resources])
        link.append(resources)
    if (source/'assets').is_dir():
        link.extend(['-A', source/'assets'])
    run(link)
    sources = sorted((source/'java').rglob('*.java')) + sorted(generated.rglob('*.java'))
    if not sources:
        parser.error('No Java sources found')
    javac = [compiler] if compiler and Path(compiler).is_file() else [java, '-m', 'jdk.compiler/com.sun.tools.javac.Main']
    # Android bootclasspath rejects host-JDK APIs unavailable on Android.
    bootclasspath = str(bt/'core-lambda-stubs.jar') + os.pathsep + str(android)
    run(javac + ['-encoding', 'UTF-8', '-source', '8', '-target', '8', '-bootclasspath', bootclasspath,
                 '-Xlint:deprecation', '-d', classes] + sources)
    classjar = build/'classes.jar'
    with zipfile.ZipFile(classjar, 'w', zipfile.ZIP_DEFLATED) as jar:
        for path in sorted(classes.rglob('*.class')):
            jar.write(path, path.relative_to(classes).as_posix())
    run([java, '-cp', bt/'lib/d8.jar', 'com.android.tools.r8.D8', '--release', '--min-api', args.min_sdk,
         '--lib', android, '--output', dex, classjar])
    with zipfile.ZipFile(unsigned, 'a', zipfile.ZIP_DEFLATED) as apk:
        for path in sorted(dex.glob('*.dex')):
            apk.write(path, path.name)
    run([zipalign, '-f', '-p', '4', unsigned, aligned])
    if args.signing_dir:
        signing = args.signing_dir.resolve()
        run([java, '-jar', bt/'lib/apksigner.jar', 'sign', '--ks', signing/'frociart.jks',
             '--ks-key-alias', args.alias, '--ks-pass', 'file:'+str(signing/'password.txt'),
             '--v1-signing-enabled', 'true',
             '--v2-signing-enabled', 'true', '--v3-signing-enabled', 'true',
             '--v4-signing-enabled', 'false', '--out', output, aligned])
        run([java, '-jar', bt/'lib/apksigner.jar', 'verify', '--verbose', '--print-certs', output])
    else:
        shutil.copy2(aligned, output)
        print('APK is unsigned; provide --signing-dir before installing.')
    run([zipalign, '-c', '-v', '4', output])
    result = dict(output=str(output), bytes=output.stat().st_size,
                  sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
                  seconds=round(time.monotonic()-started, 3), build_dir=str(build),
                  compile_sdk=int(args.compile_sdk), target_sdk=int(args.target_sdk), min_sdk=int(args.min_sdk),
                  build_tools=args.build_tools, signed=bool(args.signing_dir))
    (build/'build-result.json').write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result, indent=2))

if __name__ == '__main__':
    main()
