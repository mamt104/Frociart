const assert=require('node:assert/strict');const api=require('../docs/app.js');

function nativeBoundaryChecks(validate,link,warning){
  const base=()=>({Version:1,Title:'Native boundary',Nodes:[],Edges:[]});let checks=0;
  const verify=(doc,expected)=>{const original=JSON.stringify(doc);assert.equal(validate(doc),doc);assert.equal(!!link(doc),expected);assert.equal(!!warning(doc),!expected);assert.equal(JSON.stringify(doc),original);checks++;};
  for(const depth of [28,29,30,31,32,80,127]){const doc=base();let value=0;for(let i=0;i<depth;i++)value=[value];doc.Extension=value;verify(doc,depth<=29);}
  for(const [value,accepted]of [['\ud800',false],['\udfff',false],['\ud83d\udc22',true]]){verify({...base(),Title:value},accepted);const doc=base();doc[value]=1;verify(doc,accepted);}
  for(const doc of [{__type:'Foreign:#Foreign',...base()},{...base(),__type:'Foreign:#Foreign'},{...base(),Extension:{__type:'Foreign:#Foreign'}}])verify(doc,false);
  for(const count of [39990,40000,50000]){const doc=base();doc.Extension=Array(count).fill(0);assert.equal(validate(doc),doc);assert.equal(link(doc),null);assert.equal(!!warning(doc),count>=40000);assert.equal(doc.Extension.length,count);checks++;}
  return checks;
}
console.log(JSON.stringify({status:'PASS',checks:nativeBoundaryChecks(api.validate,api.encodeLink,api.nativeLinkWarning)}));
