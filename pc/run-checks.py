from pathlib import Path
import os,subprocess,sys,xml.etree.ElementTree as ET

project=Path(__file__).resolve().parent
out=project/'test-results';out.mkdir(exist_ok=True)
temp=out/'temp';temp.mkdir(exist_ok=True)
env=dict(os.environ,TEMP=str(temp),TMP=str(temp),FROCIART_DATA_DIR=str(out/'isolated-user-data'))
ns={'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
tree=ET.parse(project/'Frociart.Framework.csproj')
sources=[str(project/n.attrib['Include']) for n in tree.findall('.//m:Compile',ns)]
refs=['Accessibility','System','System.Core','System.Drawing','System.Windows.Forms','System.Runtime.Serialization','System.Xml','System.IO.Compression']
resources=['Guide.txt,Frociart.Guide.txt','Learning/lessons.json,Frociart.Learning.lessons.json','Tutor/curriculum.json,Frociart.Tutor.curriculum.json','Tutor/guide.json,Frociart.Tutor.guide.json']
csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
tests=sys.argv[1:] or ['DesktopUxTests','TutorSystematicTests','TutorTests','LearningTests','ImprovementTests','EngineTests']
for name in tests:
    target=out/(name+'.exe')
    command=[str(csc),'/nologo','/codepage:65001','/nowarn:0649','/debug:full','/target:exe','/out:'+str(target),'/main:Frociart.Tests.'+name]
    command+=['/r:'+r+'.dll' for r in refs]
    command+=['/resource:'+str(project/r.split(',')[0])+','+r.split(',')[1] for r in resources]
    command+=sources+[str(project/(name+'.cs'))]
    result=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,env=env,timeout=90)
    (out/(name+'-compile.log')).write_bytes(result.stdout)
    if result.returncode:print(result.stdout.decode('utf-8',errors='replace'));sys.exit(result.returncode)
    args=[str(out/'native-tutor')] if name=='TutorTests' else [str(out/'legacy-native')] if name=='LearningTests' else ['candidate',str(out/'regression')] if name=='ImprovementTests' else []
    result=subprocess.run([str(target)]+args,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,env=env,timeout=240)
    (out/(name+'.log')).write_bytes(result.stdout)
    print(name,':',result.returncode,flush=True);print(result.stdout.decode('utf-8',errors='replace')[-2000:],flush=True)
    if result.returncode:sys.exit(result.returncode)
