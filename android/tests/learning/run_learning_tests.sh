#!/usr/bin/env bash
set -euo pipefail
frociart_learning_dir="$(cd "$(dirname "$0")" && pwd)"
frociart_learning_source="$frociart_learning_dir/../../app/src/main/java/it/frociart/app"
frociart_learning_build="$(mktemp -d /tmp/frociart-learning-tests-XXXXXX)"
java -m jdk.compiler/com.sun.tools.javac.Main --release 8 -encoding UTF-8 -d "$frociart_learning_build/classes" \
  "$frociart_learning_source/FlowModel.java" "$frociart_learning_source/FlowCodec.java" \
  "$frociart_learning_source/FlowExamples.java" "$frociart_learning_source/FlowEngine.java" \
  "$frociart_learning_source/FlowLearning.java" "$frociart_learning_source/FlowLearningProgress.java"
java -m jdk.compiler/com.sun.tools.javac.Main --release 8 -encoding UTF-8 -cp "$frociart_learning_build/classes" \
  -d "$frociart_learning_build/classes" "$frociart_learning_dir/LearningTests.java"
mkdir -p "$frociart_learning_dir/results"
java -cp "$frociart_learning_build/classes" it.frociart.app.LearningTests "$frociart_learning_dir" \
  | tee "$frociart_learning_dir/results/learning-host-tests.log"
printf 'Compiled test classes: %s\n' "$frociart_learning_build/classes"
