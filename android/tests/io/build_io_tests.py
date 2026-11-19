#!/usr/bin/env python3
"""Build the instrumentation APK against the installed application's classes and signing identity."""
import argparse
from pathlib import Path
import subprocess
import tempfile
import zipfile
import os

def run(command):
    subprocess.run([str(value) for value in command], check=True)

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--sdk',type=Path,required=True)
parser.add_argument('--app-classes',type=Path,required=True)
parser.add_argument('--signing-dir',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--java-home',type=Path,required=True)
args=parser.parse_args()
source=Path(__file__).resolve().parent
build=Path(tempfile.mkdtemp(prefix='frociart-io-apk-'))
classes=build/'classes';classes.mkdir()
dex=build/'dex';dex.mkdir()
bt=args.sdk/'build-tools/35.0.0';android=args.sdk/'platforms/android-35/android.jar'
suffix='.exe' if os.name=='nt' else ''
java=args.java_home/('bin/java'+suffix)
run([bt/('aapt2'+suffix),'link','-o',build/'unsigned.apk','-I',android,'--manifest',source/'AndroidManifest.xml','--min-sdk-version','26','--target-sdk-version','35'])
run([java,'-m','jdk.compiler/com.sun.tools.javac.Main','-encoding','UTF-8','-source','8','-target','8','-bootclasspath',str(bt/'core-lambda-stubs.jar')+os.pathsep+str(android),'-classpath',args.app_classes,'-d',classes,source/'IoInstrumentation.java'])
jar=build/'tests.jar'
with zipfile.ZipFile(jar,'w',zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(classes.rglob('*.class')):archive.write(path,path.relative_to(classes).as_posix())
run([java,'-cp',bt/'lib/d8.jar','com.android.tools.r8.D8','--release','--min-api','26','--lib',android,'--classpath',args.app_classes,'--output',dex,jar])
with zipfile.ZipFile(build/'unsigned.apk','a',zipfile.ZIP_DEFLATED) as archive:
    for path in dex.glob('*.dex'):archive.write(path,path.name)
run([bt/('zipalign'+suffix),'-f','-p','4',build/'unsigned.apk',build/'aligned.apk'])
args.output.parent.mkdir(parents=True,exist_ok=True)
run([java,'-jar',bt/'lib/apksigner.jar','sign','--ks',args.signing_dir/'frociart.jks','--ks-key-alias','frociart','--ks-pass','file:'+str(args.signing_dir/'password.txt'),'--v1-signing-enabled','true','--v2-signing-enabled','true','--v3-signing-enabled','true','--v4-signing-enabled','false','--out',args.output,build/'aligned.apk'])
run([java,'-jar',bt/'lib/apksigner.jar','verify',args.output])
print('Instrumentation APK:',args.output)
print('Build directory:',build)
