const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const root=path.join(__dirname,'..','docs','download'),m=JSON.parse(fs.readFileSync(path.join(root,'release.json'),'utf8'));
const expected=['Frociart_v1.exe','Frociart_v1_Classico.exe','Frociart_v1_Setup.exe','Frociart_v1_Android.apk'];
assert.deepEqual(m.packages.map(p=>p.file).sort(),expected.sort());
const sums=new Map(fs.readFileSync(path.join(root,'SHA256SUMS_v1.txt'),'utf8').trim().split(/\r?\n/).map(line=>{const i=line.indexOf('  ');return[line.slice(i+2),line.slice(0,i)];}));
let checks=0;
for(const p of [...m.packages,...m.licenses]){
  assert(!p.file.includes('..')&&!path.isAbsolute(p.file)&&!p.file.includes('\\'));
  const data=fs.readFileSync(path.join(root,p.file)),hash=crypto.createHash('sha256').update(data).digest('hex');
  assert.equal(data.length,p.bytes);assert.equal(hash,p.sha256);
  assert.equal(p.url,'https://mamt104.github.io/Frociart/download/'+p.file);
  if(expected.includes(p.file))assert.equal(sums.get(p.file),hash);
  checks++;
}
assert.equal(m.licenses.length,4);assert.equal(sums.size,4);
assert.equal(m.windowsTechnicalVersion,'1.82.0.0');assert.equal(m.androidVersionCode,101);
console.log(JSON.stringify({status:'PASS',artifactsAndNotices:checks,scope:'Exact four release packages, notices, version metadata and checksums; no OS execution claim.'}));
