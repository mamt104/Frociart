#!/usr/bin/env python3
"""Run the pure production PDF pagination contract; no Android/device claims."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import tempfile
from datetime import datetime, timezone

root = Path(__file__).resolve().parents[2]
sources = root / "app/src/main/java/it/frociart/app"
tests = root / "tests/pdf"
results = tests / "results"
results.mkdir(parents=True, exist_ok=True)
build = Path(tempfile.mkdtemp(prefix="frociart-pdf-planner-"))
classes = build / "classes"
classes.mkdir()
inputs = [sources / "FlowModel.java", sources / "FlowCodec.java",
          sources / "FlowPdfPages.java", tests / "FlowPdfPagesTests.java",
          tests / "fixtures/parity-real-ui.frociart"]
commands = [
    ["java", "-m", "jdk.compiler/com.sun.tools.javac.Main", "--release", "8",
     "-encoding", "UTF-8", "-d", str(classes), *map(str, inputs[:4])],
    ["java", "-cp", str(classes), "it.frociart.app.FlowPdfPagesTests", str(inputs[4])]
]
log = []
for command in commands:
    done = subprocess.run(command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    log.append(done.stdout)
    print(done.stdout, end="")
    (results / "planner-host.log").write_text("".join(log), encoding="utf-8")
    if done.returncode:
        raise SystemExit(done.returncode)
metadata = {
    "recorded_utc": datetime.now(timezone.utc).isoformat(),
    "scope": "Pure JVM production pagination planner; coverage, page limits, no gaps, no zero-height pages, no document mutations. Real Android PDF render is a separate gate.",
    "source_sha256": {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
    "build_dir": str(build),
    "result": "PASS"
}
(results / "planner-provenance.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
