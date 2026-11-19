#!/usr/bin/env python3
"""Run native app logic on the JVM; this does not execute Android UI/storage."""
from pathlib import Path
import argparse, hashlib, json, subprocess, sys

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--java-home',type=Path,required=True)
parser.add_argument('--desktop-fixtures',type=Path)
parser.add_argument('--output',type=Path,default=Path('test-results/core'))
args=parser.parse_args()
project=Path(__file__).resolve().parents[1]
src=project/'app/src/main/java/it/frociart/app';test=project/'tests'
out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
suffix='.exe' if (args.java_home/'bin/java.exe').is_file() else ''
jdk=args.java_home.resolve()/'bin'
classes=out/'jvm';classes.mkdir(exist_ok=True)
production=['FlowPseudocode','FlowDeclaration','FlowTurtle','FlowTextFiles','FlowFunctionEditing','FlowModel','FlowFunctions','FlowCodec','FlowEngine','FlowExamples','FlowAutoLayout','FlowOpenGuard','FlowPdfPages','FlowGridSnap','FlowViewport','FlowLearning','FlowLearningProgress','FlowPractice','FlowInsertion','FlowPlacement']
tests=['FlowPlacementTests.java','FlowInsertionTests.java','EngineTests.java','CapacityTests.java','FlowOpenGuardTests.java','FlowGridSnapTests.java','FlowViewportTests.java','layout/FlowAutoLayoutTests.java','pdf/FlowPdfPagesTests.java','learning/LearningTests.java','learning/FlowPracticeTests.java']
inputs=[src/(n+'.java') for n in production]+[test/n for n in tests]+[src/'FlowDraftStore.java',test/'drafts/FlowDraftStoreTests.java',test/'drafts/stubs/android/content/SharedPreferences.java']
hashes={str(p.relative_to(project)):hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
(out/'source-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
results=[]
def run(name,command):
    result=subprocess.run([str(v) for v in command],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=120)
    (out/(name+'.log')).write_bytes(result.stdout)
    results.append(dict(name=name,exit_code=result.returncode,status='PASS' if result.returncode==0 else 'FAIL'))
    (out/'checks.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
    print(name,results[-1]['status'],result.stdout.decode('utf-8',errors='replace')[-320:],flush=True)
    if result.returncode:sys.exit(result.returncode)
run('compile-jvm',[jdk/('javac'+suffix),'--release','8','-encoding','UTF-8','-d',classes]+[src/(n+'.java') for n in production])
run('compile-host-tests',[jdk/('javac'+suffix),'-encoding','UTF-8','-cp',classes,'-d',classes]+[test/n for n in tests])
fixtures=args.desktop_fixtures.resolve() if args.desktop_fixtures else None
parity=fixtures/'02_Pari_o_dispari.frociart' if fixtures else project/'examples/02_Pari_dispari_blu_viola.frociart'
for name,values in [('FlowPlacementTests',[]),('FlowInsertionTests',[]),('EngineTests',[fixtures or '--no-desktop-fixtures',out/'roundtrip']),('CapacityTests',[]),('FlowOpenGuardTests',[]),('FlowGridSnapTests',[]),('FlowViewportTests',[]),('FlowAutoLayoutTests',[]),('FlowPdfPagesTests',[parity]),('LearningTests',[test/'learning']),('FlowPracticeTests',[])]:
    run(name,[jdk/('java'+suffix),'-cp',classes,'it.frociart.app.'+name]+values)
drafts=out/'draft-classes';drafts.mkdir(exist_ok=True)
run('compile-drafts',[jdk/('javac'+suffix),'--release','8','-encoding','UTF-8','-d',drafts,test/'drafts/stubs/android/content/SharedPreferences.java',src/'FlowDraftStore.java',test/'drafts/FlowDraftStoreTests.java'])
run('FlowDraftStoreTests',[jdk/('java'+suffix),'-cp',drafts,'it.frociart.app.FlowDraftStoreTests'])
for p in inputs:
    if hashlib.sha256(p.read_bytes()).hexdigest()!=hashes[str(p.relative_to(project))]:raise SystemExit('Source changed during tests; run the audit again.')
print('JVM audit PASS. Android UI, file picker, real SharedPreferences and rendering: NOT_RUN.')

