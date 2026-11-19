#!/usr/bin/env python3
from pathlib import Path
import argparse,subprocess
p=argparse.ArgumentParser();p.add_argument('--java-home',type=Path,required=True);p.add_argument('--output',type=Path,default=Path('test-results'));args=p.parse_args()
root=Path(__file__).resolve().parents[1];source=root/'app/src/main/java/it/frociart/app';output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
javac=args.java_home/'bin/javac';java=args.java_home/'bin/java'
if (javac.with_suffix('.exe')).exists():javac=javac.with_suffix('.exe');java=java.with_suffix('.exe')
def run(command):subprocess.run([str(v) for v in command],check=True)
run([javac,'--release','8','-encoding','UTF-8','-d',output]+[source/(n+'.java') for n in ['FlowGridSnap','FlowModel','FlowCodec','FlowExamples','FlowEngine','FlowLearning','FlowLearningProgress']]+[root/'tests/FlowGridSnapTests.java',root/'tests/learning/LearningTests.java'])
run([java,'-cp',output,'it.frociart.app.FlowGridSnapTests'])
run([java,'-cp',output,'it.frociart.app.LearningTests',root/'tests/learning'])
print('Solo test JVM: nessuna compilazione o esecuzione UI Android.')
