const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(process.argv[2]||require('node:path').join(__dirname,'..','docs','app.js'),'utf8');
const ids=['project-file','file-status','project-result','open-app','save-project','drop-zone','project-title','project-details'];
const elements=Object.fromEntries(ids.map(id=>[id,{hidden:id==='project-result',events:{},attributes:{},classList:{add(){},remove(){}},addEventListener(name,fn){this.events[name]=fn},setAttribute(name,value){this.attributes[name]=value},removeAttribute(name){delete this.attributes[name]},files:[]} ]));
let blobs=[],urlCalls=0;
const env={document:{getElementById:id=>elements[id]},addEventListener(){},TextEncoder,TextDecoder,Blob,Uint8Array,JSON,Set,Array,Number,String,Object,Error,SyntaxError,TypeError,
 URL:{createObjectURL(blob){blobs.push(blob);return 'blob:qa-'+(++urlCalls)},revokeObjectURL(){}},btoa:s=>Buffer.from(s,'binary').toString('base64')};env.window=env;
vm.runInNewContext(source,env,{timeout:1000});
const fixture={Version:1,Title:'<img src=x onerror=alert(1)>',Nodes:[{Id:'s',Kind:0,Text:'Inizio',Prompt:'',X:0,Y:0,Comment:'Caffè 🐢',Breakpoint:true,BreakpointCondition:'x>0'},{Id:'e',Kind:1,Text:'Fine',Prompt:'',X:0,Y:100}],Edges:[{Id:'edge',From:'s',To:'e',Branch:'next'}],Metadata:{futuro:'da non perdere'}};
function file(buffer){return {size:buffer.length,arrayBuffer:async()=>Uint8Array.from(buffer).buffer}}
(async()=>{
 const original=Buffer.from('\ufeff  '+JSON.stringify(fixture,null,2)+'\r\n','utf8');
 elements['project-file'].files=[file(original)];await elements['project-file'].events.change();
 assert.equal(blobs.length,1);assert.deepEqual(Buffer.from(await blobs[0].arrayBuffer()),original);
 assert.equal(elements['project-title'].textContent,fixture.Title);assert.equal(elements['open-app'].attributes['aria-disabled'],'false');
 const link=elements['open-app'].href;const decoded=JSON.parse(Buffer.from(link.split('/').at(-1),'base64url').toString('utf8'));assert.deepEqual(decoded,fixture);
 const currentHref=elements['save-project'].href;const modern={...fixture,Version:5,VariablePolicy:'declared',Functions:[{Name:'funzione',Body:fixture}]};
 elements['project-file'].files=[file(Buffer.from(JSON.stringify(modern)))];await elements['project-file'].events.change();
 assert.equal(blobs.length,1);assert.equal(elements['save-project'].href,currentHref);assert.match(elements['file-status'].textContent,/formato.*app|app.*formato/i);
 assert.match(elements['file-status'].textContent,/progetto già caricato resta disponibile/);
 console.log(JSON.stringify({status:'PASS',checks:8,scope:'Mocked browser DOM runs unchanged candidate app.js; original BOM/whitespace/metadata bytes preserved, title uses textContent, exact native link and unsupported version never creates reduced download'}));
})().catch(e=>{console.error(e);process.exitCode=1});
