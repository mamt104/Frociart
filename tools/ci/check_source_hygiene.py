#!/usr/bin/env python3
"""Reject private machine files and recognizable secrets in the tracked tree."""
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[2]
names = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
private_suffixes = {'.jks', '.keystore', '.p12', '.pfx', '.key', '.pem', '.user', '.suo'}
private_names = {'local.properties', 'password.txt', 'credentials.json', 'secrets.json'}
patterns = [
    rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
    rb'gh[pousr]_[A-Za-z0-9_]{30,}', rb'github_pat_[A-Za-z0-9_]{30,}',
    rb'AKIA[0-9A-Z]{16}',
    rb'[A-Za-z]:\\Users\\[^\\\s"<>]+\\',
    rb'/(?:home|Users)/[^/\s"<>]+/',
]
problems = []
for name in filter(None, names):
    p = Path(name)
    if (p.suffix.lower() in private_suffixes or p.name.lower() in private_names
            or p.name == '.env' or p.name.startswith('.env.')
            or any(part in {'.vs', '.idea', 'private-signing'} for part in p.parts)):
        problems.append((name, 'private file'))
        continue
    data = (root / p).read_bytes()
    # Public distribution binaries are outside the source import. Do not parse
    # executable packages as text; they retain the existing integrity tests.
    if p.suffix.lower() in {'.exe', '.apk', '.png', '.jpg', '.ico', '.gif', '.woff2'}:
        continue
    if any(re.search(pattern, data) for pattern in patterns):
        problems.append((name, 'secret or personal machine path'))
for name, reason in problems:
    print(f'{name}: {reason}')  # Never print the matching secret.
if problems:
    raise SystemExit(1)
print('Tracked source hygiene: PASS (filename, key/token and personal-path checks).')
