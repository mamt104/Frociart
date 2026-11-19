#!/usr/bin/env python3
"""Compile and run packaged suites on Windows using the actual Framework sources."""
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

if os.name != 'nt':
    raise SystemExit('Run this native test runner on Windows, not an emulated host API.')
root = Path(__file__).resolve().parents[2]
pc = root / 'pc'
out = root / 'artifacts/windows-tests'
out.mkdir(parents=True, exist_ok=True)
temporary = out / 'temp'
temporary.mkdir(exist_ok=True)
env = dict(os.environ, TEMP=str(temporary), TMP=str(temporary),
           FROCIART_DATA_DIR=str(out / 'isolated-user-data'))
framework = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319'
if not (framework / 'csc.exe').is_file():
    framework = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework/v4.0.30319'
tree = ET.parse(pc / 'Frociart.Framework.csproj')
ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
command = [str(framework / 'csc.exe'), '/nologo', '/codepage:65001', '/optimize+',
           '/target:exe', '/main:Frociart.CiTestRunner',
           '/out:' + str(out / 'Frociart.CiTests.exe')]
for reference in tree.findall('.//m:Reference', ns):
    name = reference.attrib['Include'] + '.dll'
    wpf = framework / 'WPF' / name
    command.append('/r:' + (str(wpf) if wpf.is_file() else name))
for resource in tree.findall('.//m:EmbeddedResource', ns):
    path = pc / resource.attrib['Include'].replace('\\', '/')
    logical = resource.find('m:LogicalName', ns).text
    command.append('/resource:' + str(path) + ',' + logical)
command += [str(pc / item.attrib['Include']) for item in tree.findall('.//m:Compile', ns)]
command += [str(pc / (name + '.cs')) for name in ['EngineTests', 'CapabilityTests', 'CloneTests']]
command.append(str(root / 'tools/ci/WindowsTestRunner.cs'))
results = []
for name, values, timeout in [
    ('compile', command, 180),
    ('native-tests', [str(out / 'Frociart.CiTests.exe')], 240),
]:
    try:
        done = subprocess.run(values, cwd=pc, env=env, stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT, timeout=timeout)
        log, code = done.stdout, done.returncode
    except subprocess.TimeoutExpired as error:
        log, code = (error.stdout or b'') + b'\nTIMEOUT\n', 124
    (out / (name + '.log')).write_bytes(log)
    results.append({'name': name, 'exit_code': code, 'status': 'PASS' if code == 0 else 'FAIL'})
    (out / 'checks.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
    print(log.decode('utf-8', errors='replace'), flush=True)
    if code:
        raise SystemExit(code)
print('Scope: native .NET Framework engine, capability and clone suites; no Windows UI automation.')
