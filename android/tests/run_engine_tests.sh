#!/usr/bin/env bash
set -euo pipefail
frociart_root="$(cd "$(dirname "$0")/.." && pwd)"
frociart_test_dir="$(mktemp -d /tmp/frociart-java-tests-XXXXXX)"
frociart_source="$frociart_root/app/src/main/java/it/frociart/app"
java -m jdk.compiler/com.sun.tools.javac.Main --release 8 -encoding UTF-8 -d "$frociart_test_dir/classes" "$frociart_source/FlowModel.java" "$frociart_source/FlowCodec.java" "$frociart_source/FlowExamples.java" "$frociart_source/FlowEngine.java"
java -m jdk.compiler/com.sun.tools.javac.Main -encoding UTF-8 -cp "$frociart_test_dir/classes" -d "$frociart_test_dir/classes" "$frociart_root/tests/EngineTests.java" "$frociart_root/tests/CapacityTests.java" "$frociart_root/tests/ReadbackTests.java"
frociart_fixtures="${1:-$frociart_root/../package-1.3.0/Frociart/Esempi}"
if [[ -z "${1:-}" && ! -f "$frociart_fixtures/02_Pari_o_dispari.frociart" ]]; then
  frociart_fixtures="--no-desktop-fixtures"
  printf 'Desktop release fixtures not present: built-in and generated roundtrip tests will still run.\n'
fi
java -cp "$frociart_test_dir/classes" it.frociart.app.EngineTests "$frociart_fixtures" "$frociart_test_dir/output"
java -cp "$frociart_test_dir/classes" it.frociart.app.CapacityTests
if command -v mcs >/dev/null 2>&1 && [[ -f "$frociart_root/../Frociart/Core.cs" ]]; then
  mcs -langversion:6 -r:System.Runtime.Serialization.dll -r:System.Core.dll -out:"$frociart_test_dir/DesktopReadbackTests.exe" "$frociart_root/../Frociart/Core.cs" "$frociart_root/tests/DesktopReadbackTests.cs"
  mono "$frociart_test_dir/DesktopReadbackTests.exe" "$frociart_test_dir/output"
  java -cp "$frociart_test_dir/classes" it.frociart.app.ReadbackTests "$frociart_test_dir/output"
fi
printf 'Test artifacts: %s\n' "$frociart_test_dir"
