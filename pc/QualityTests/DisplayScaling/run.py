"""Windows-only native display/provider regressions; generated files go to --output."""
from pathlib import Path
import argparse, hashlib, json, os, subprocess, sys, xml.etree.ElementTree as ET

SOURCE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--pc-root', type=Path, default=SOURCE.parents[1])
parser.add_argument('--app', type=Path, help='Classic Framework EXE; modern .NET assemblies cannot load in this Framework probe')
parser.add_argument('--framework-dir', type=Path, default=Path(os.environ.get('WINDIR', 'C:/Windows'))/'Microsoft.NET/Framework64/v4.0.30319')
parser.add_argument('--mode', choices=['all', 'dpi', 'compact', 'uia', 'msaa'], default='all')
parser.add_argument('--scales', nargs='+', type=int, default=[100,125,150,200])
args = parser.parse_args()
PC, OUT, FX = args.pc_root.resolve(), args.output.resolve(), args.framework_dir.resolve()
APP = (args.app or PC/'bin/framework/Frociart.exe').resolve()
OUT.mkdir(parents=True, exist_ok=True)
TOOLS = OUT/'tools'; TOOLS.mkdir(exist_ok=True)
sha = lambda path: hashlib.sha256(Path(path).read_bytes()).hexdigest()
results = []

def compile_probe(name, sources, references=(), startup=None, resources=()):
    exe = TOOLS/(name+'.exe')
    command = [FX/'csc.exe', '/nologo', '/codepage:65001', '/nowarn:0649', '/target:exe', '/out:'+str(exe)]
    if startup: command += ['/main:'+startup]
    command += list(references)+list(resources)+list(sources)
    run = subprocess.run(list(map(str,command)), capture_output=True, timeout=90, creationflags=subprocess.CREATE_NO_WINDOW)
    (TOOLS/(name+'-compile.log')).write_bytes(run.stdout+run.stderr)
    if run.returncode: raise RuntimeError('Compilation failed: '+str(TOOLS/(name+'-compile.log')))
    return exe

launcher = compile_probe('DesktopLauncher', [SOURCE/'DesktopLauncher.cs'])

def native_run(name, exe, parameters, extra_env=None):
    dest = OUT/name; dest.mkdir(exist_ok=True)
    (dest/'temp').mkdir(exist_ok=True)
    env = dict(os.environ, FROCIART_DATA_DIR=str(dest/'private-data'), TEMP=str(dest/'temp'), TMP=str(dest/'temp'))
    env.pop('FROCIART_DPI_COMPACT', None)
    env.update(extra_env or {})
    with (dest/'run.log').open('wb') as log:
        run = subprocess.run(list(map(str,[launcher,exe,*parameters(dest)])),cwd=dest,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=250,creationflags=subprocess.CREATE_NO_WINDOW)
    if run.returncode: raise RuntimeError('Native test failed: '+str(dest/'run.log'))
    return dest

def display(compact=False):
    stage = OUT/('compact-input-source' if compact else 'dpi-input-source'); stage.mkdir(exist_ok=True)
    native = (PC/'NativeTheme.cs').read_text(encoding='utf-8-sig')
    start = native.index('        internal static float DpiScale(Control control)')
    end = native.index('        internal static void PadEdit', start)
    native = native[:start]+'        internal static float DpiScale(Control control){return DpiLayoutProbe.Scale;}\n\n'+native[end:]
    (stage/'NativeTheme.cs').write_text(native, encoding='utf-8')
    main = (PC/'MainForm.cs').read_text(encoding='utf-8-sig')
    needle = 'base.OnLoad(e);using(var graphics=CreateGraphics())dpiScale=graphics.DpiX/96f;'
    assert needle in main, 'Update the DPI input hook after a production OnLoad refactor'
    (stage/'MainForm.cs').write_text(main.replace(needle,'base.OnLoad(e);dpiScale=DpiLayoutProbe.Scale;'),encoding='utf-8')
    theme = (PC/'Theme.cs').read_text(encoding='utf-8-sig')
    assert 'return new Font("Segoe UI", size, style, GraphicsUnit.Point);' in theme
    theme = theme.replace('return new Font("Segoe UI", size, style, GraphicsUnit.Point);','return new Font("Segoe UI", size*DpiLayoutProbe.Scale, style, GraphicsUnit.Point);').replace('return new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);','return new Font("Consolas", size*DpiLayoutProbe.Scale, FontStyle.Regular, GraphicsUnit.Point);')
    if compact:
        needle = 'var area=Screen.FromControl(form).WorkingArea;'
        assert needle in theme, 'Update the controlled working-area hook after a production refactor'
        theme = theme.replace(needle,'var area=new Rectangle(0,0,1920,1040);')
    (stage/'Theme.cs').write_text(theme,encoding='utf-8')
    ns = {'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
    tree = ET.parse(PC/'Frociart.Framework.csproj')
    sources = [stage/e.attrib['Include'] if e.attrib['Include'] in ('NativeTheme.cs','MainForm.cs','Theme.cs') else PC/e.attrib['Include'] for e in tree.findall('.//m:Compile',ns)]
    refs = []
    for entry in tree.findall('.//m:Reference',ns):
        name = entry.attrib['Include']+'.dll'
        refs.append('/r:'+str(FX/'WPF'/name) if name in ('WindowsBase.dll','PresentationCore.dll') else '/r:'+name)
    resources = ['/resource:'+str(PC/e.attrib['Include'])+','+e.find('m:LogicalName',ns).text for e in tree.findall('.//m:EmbeddedResource',ns)]
    exe = compile_probe('DisplayScaling', [*sources,SOURCE/'DpiLayoutProbe.cs'], ['/r:System.Web.Extensions.dll',*refs], 'DpiLayoutProbe', resources)
    for percentage in ([200] if compact else args.scales):
        if percentage not in (100,125,150,200): raise ValueError('Supported controlled inputs: 100,125,150,200')
        scale = percentage/100
        name = ('compact-' if compact else 'dpi-')+str(percentage)
        dest = native_run(name,exe,lambda dest:[dest,str(scale)],{'FROCIART_DPI_COMPACT':'1'} if compact else {})
        report = json.loads((dest/'layout.json').read_text())
        if not all(row['pass'] for row in report['results']): raise RuntimeError('Display assertion failed: '+str(dest/'layout.json'))
        results.append({'mode':name,'checks':report['checks'],'exeSha256':sha(exe),'evidence':str(dest/'layout.json'),'sourceSha256':{n:sha(PC/n) for n in ('Theme.cs','MainForm.cs','WorkspaceLayouts.cs')}})
        print(name,report['checks'],'PASS',flush=True)

def provider(kind):
    if not APP.is_file(): raise FileNotFoundError('Build the classic EXE first or pass --app: '+str(APP))
    references = ['/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll']
    if kind=='uia': references += ['/r:'+str(FX/'WPF'/(n+'.dll')) for n in ('WindowsBase','UIAutomationClient','UIAutomationTypes')]
    else: references += ['/r:Accessibility.dll']
    source = SOURCE/('UiaProbe.cs' if kind=='uia' else 'MsaaProbe.cs')
    exe = compile_probe(kind,[source],references)
    dest = native_run(kind,exe,lambda dest:[APP,dest])
    report = json.loads((dest/(kind+'.json')).read_text())
    results.append({'mode':kind,'checks':report['checks'],'appSha256':sha(APP),'probeSha256':sha(source),'evidence':str(dest/(kind+'.json'))})
    print(kind,report['checks'],'PASS',flush=True)

try:
    if args.mode in ('all','dpi'): display()
    if args.mode in ('all','compact'): display(True)
    if args.mode in ('all','uia'): provider('uia')
    if args.mode in ('all','msaa'): provider('msaa')
    (OUT/'result.json').write_text(json.dumps({'status':'PASS','results':results,'osDisplayChanged':False,'audioScreenReaderTested':False},indent=2),encoding='utf-8')
except Exception as error:
    (OUT/'result.json').write_text(json.dumps({'status':'FAIL','error':str(error),'completed':results},indent=2),encoding='utf-8')
    raise
