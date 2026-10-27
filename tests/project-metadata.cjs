const assert=require('node:assert/strict');const fs=require('node:fs');const path=require('node:path');
const root=process.argv[2]||path.join(__dirname,'..','docs');const phase=process.argv[3]||'after';const api=require(path.resolve(root,'app.js'));
const base=()=>({Version:1,Title:'Parità <img src=x onerror=alert(1)>',Nodes:[{Id:'s',Kind:0,Text:'Inizio',Prompt:'',X:0,Y:0,Comment:'Nota da conservare',Breakpoint:true,BreakpointCondition:'x > 0',FoldedCycle:true},{Id:'e',Kind:1,Text:'Fine',Prompt:'',X:0,Y:200}],Edges:[{Id:'edge',From:'s',To:'e',Branch:'next'}],FutureMetadata:{note:'conserva anche campi futuri'}});
let checks=0;function ok(fn){fn();checks++;}
if(phase==='before'){
 const doc=base(),actual=api.validate(doc);assert.equal(actual.Nodes[0].Comment,undefined);assert.equal(actual.FutureMetadata,undefined);
 console.log(JSON.stringify({status:'REPRODUCED',defect:'VALID_VERSION_1_METADATA_LOST',fields:['Comment','Breakpoint','BreakpointCondition','FoldedCycle','FutureMetadata']}));process.exit(0);
}
ok(()=>{const doc=base();assert.deepEqual(api.validate(doc),doc)});
ok(()=>{const doc=base();const link=api.encodeLink(api.validate(doc));const raw=Buffer.from(link.split('/').at(-1).replaceAll('-','+').replaceAll('_','/'),'base64').toString('utf8');assert.deepEqual(JSON.parse(raw),doc)});
ok(()=>{const d=base();d.Version=5;d.VariablePolicy='declared';d.Functions=[{Id:'f',Name:'elabora',Parameters:[],ReturnType:'Void',Body:base()}];assert.throws(()=>api.validate(d),/formato.*app|app.*formato/i)});
ok(()=>{const d=base();d.Nodes[1].Id='s';assert.throws(()=>api.validate(d),/duplicati/)});
ok(()=>{const d=base();d.Nodes[0].X=Infinity;assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Nodes[0].Kind=99;assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Edges[0].To='missing';assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Edges[0].Branch='evil';assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Nodes[0].TrueColor='url(https://evil.invalid)';assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Nodes[0].Text='x'.repeat(8193);assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Title='x'.repeat(201);assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.Nodes=Array.from({length:501},(_,i)=>({...d.Nodes[0],Id:'n'+i}));assert.throws(()=>api.validate(d))});
ok(()=>{const d=base();d.FutureMetadata.large='x'.repeat(4*1024*1024);assert.throws(()=>api.encodeLink(d),/4 MB/)});
ok(()=>{const d=base();d.FutureMetadata.long='x'.repeat(20000);assert.equal(api.encodeLink(d),null)});
const script=fs.readFileSync(path.join(root,'app.js'),'utf8');
ok(()=>assert(!/\b(?:eval|fetch)\s*\(|innerHTML\s*=|XMLHttpRequest|WebSocket/.test(script)));
ok(()=>assert(script.includes('new Blob([buffer]')));
ok(()=>assert(script.includes("$('project-title').textContent=")));
ok(()=>assert(!script.includes('menu Altro')));
console.log(JSON.stringify({status:'PASS',checks,scope:'Read-only browser import, exact metadata/link preservation, original download bytes, unsupported modern schema rejection, bounds and DOM text handling'}));
