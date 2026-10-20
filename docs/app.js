(function(root){
  'use strict';
  const MAX_BYTES=4*1024*1024, MAX_LINK=24000, PREFIX='frociart://import/v1/';
  function invalid(message){throw new Error(message);}
  function object(value){return value!==null&&typeof value==='object'&&!Array.isArray(value);}
  function text(value,max,required){if(value==null&&!required)return null;if(typeof value!=='string'||value.length>max||(required&&!value.trim()))invalid('Il progetto contiene testo o identificatori non validi.');return value;}
  function color(value){if(value!=null&&(typeof value!=='string'||!/^#[0-9a-fA-F]{6}$/.test(value)))invalid('Il progetto contiene un colore non valido.');return value==null?null:value;}
  function validate(value){
    if(!object(value)||value.Version!==1||!Array.isArray(value.Nodes)||!Array.isArray(value.Edges))invalid('Questo file non è un progetto Frociart versione 1.');
    if(value.Nodes.length>500||value.Edges.length>1500)invalid('Il progetto supera il limite di 500 blocchi o 1.500 collegamenti.');
    const ids=new Set(),edgeIds=new Set();
    const nodes=value.Nodes.map(n=>{
      if(!object(n))invalid('Un blocco non è valido.');const id=text(n.Id,128,true);if(ids.has(id))invalid('Il progetto contiene identificatori duplicati.');ids.add(id);
      if(!Number.isInteger(n.Kind)||n.Kind<0||n.Kind>5||!Number.isFinite(n.X)||!Number.isFinite(n.Y)||Math.abs(n.X)>100000||Math.abs(n.Y)>100000)invalid('Un blocco contiene tipo o coordinate non validi.');
      return {Id:id,Kind:n.Kind,Text:text(n.Text,8192,false),Prompt:text(n.Prompt,1024,false),X:n.X,Y:n.Y,TrueLabel:text(n.TrueLabel,60,false),FalseLabel:text(n.FalseLabel,60,false),TrueColor:color(n.TrueColor),FalseColor:color(n.FalseColor)};
    });
    const edges=value.Edges.map(e=>{if(!object(e))invalid('Un collegamento non è valido.');const id=text(e.Id,128,true),from=text(e.From,128,true),to=text(e.To,128,true);if(edgeIds.has(id)||!ids.has(from)||!ids.has(to)||!['next','true','false'].includes(e.Branch))invalid('Il progetto contiene collegamenti non validi.');edgeIds.add(id);return {Id:id,From:from,To:to,Branch:e.Branch};});
    return {Version:1,Title:text(value.Title,200,false),Nodes:nodes,Edges:edges};
  }
  function encodeLink(document){const bytes=new TextEncoder().encode(JSON.stringify(document));if(bytes.length>MAX_BYTES)invalid('Il progetto supera il limite di 4 MB.');if(Math.ceil(bytes.length/3)*4+PREFIX.length>MAX_LINK)return null;let binary='';for(const byte of bytes)binary+=String.fromCharCode(byte);return PREFIX+btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');}
  const api={validate:validate,encodeLink:encodeLink,MAX_BYTES:MAX_BYTES};
  if(typeof module==='object'&&module.exports)module.exports=api;
  if(!root.document)return;
  const $=id=>root.document.getElementById(id),input=$('project-file'),status=$('file-status'),result=$('project-result'),open=$('open-app'),save=$('save-project'),zone=$('drop-zone');let generation=0,blobUrl=null;
  async function read(file){
    if(!file)return;const current=++generation;
    if(file.size===0||file.size>MAX_BYTES){status.className='error';status.textContent='Scegli un file non vuoto, fino a 4 MB.'+(!result.hidden?' Il progetto già caricato resta disponibile.':'');return;}
    status.className='';status.textContent='Leggo il progetto sul dispositivo…';
    try{
      const buffer=await file.arrayBuffer();if(current!==generation)return;if(buffer.byteLength>MAX_BYTES)invalid('Il file supera il limite di 4 MB.');
      const raw=new TextDecoder('utf-8',{fatal:true}).decode(buffer);
      const doc=validate(JSON.parse(raw.replace(/^\uFEFF/,'')));const link=encodeLink(doc);
      if(blobUrl)URL.revokeObjectURL(blobUrl);blobUrl=URL.createObjectURL(new Blob([JSON.stringify(doc)],{type:'application/x-frociart'}));
      save.href=blobUrl;save.download='Progetto.frociart';$('project-title').textContent=doc.Title||'Il tuo progetto';$('project-details').textContent=doc.Nodes.length+' blocchi · '+doc.Edges.length+' collegamenti';
      open.setAttribute('aria-disabled',link?'false':'true');if(link){open.href=link;open.tabIndex=0;}else{open.removeAttribute('href');open.tabIndex=-1;}
      result.hidden=false;status.textContent=link?'Pronto. Premi Apri in Frociart e conferma la richiesta del browser. Se l’app non è installata, usa il download qui sotto.':'Il progetto è grande: salvalo e aprilo con doppio clic dopo aver installato Frociart.';
    }catch(error){if(current!==generation)return;status.className='error';status.textContent=(error instanceof SyntaxError||error instanceof TypeError?'Non riesco a leggere questo progetto. Scegli un file .frociart valido.':error.message)+(!result.hidden?' Il progetto già caricato resta disponibile.':'');}
  }
  input.addEventListener('change',()=>read(input.files[0]));
  zone.addEventListener('dragover',e=>{e.preventDefault();zone.classList.add('dragover');});zone.addEventListener('dragleave',()=>zone.classList.remove('dragover'));zone.addEventListener('drop',e=>{e.preventDefault();zone.classList.remove('dragover');if(e.dataTransfer.files.length===1)read(e.dataTransfer.files[0]);else{status.className='error';status.textContent='Scegli un solo progetto alla volta.';}});
  open.addEventListener('click',()=>{status.className='';status.textContent='Se il browser lo chiede, conferma l’apertura di Frociart. Non compare l’app? Scaricala e installala dal collegamento qui sotto.';});
  root.addEventListener('pagehide',event=>{if(!event.persisted&&blobUrl){URL.revokeObjectURL(blobUrl);blobUrl=null;}});
})(typeof window==='object'?window:globalThis);
