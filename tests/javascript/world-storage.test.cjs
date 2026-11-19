const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path'),crypto=require('node:crypto');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_STORAGE_PACKET||'generated/partitioned-storage-c6e764771480438787a3cb4584242011';
const hash=async bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const manifest=async()=>JSON.parse(await fs.readFile(path.join(folder,'payload.json')));
const readPart=part=>fs.readFile(path.join(folder,'_worlddata',part.Sha256+'.bin'));
const options={readPart,sha256:hash};

test('complete C# stream reassembles with native SHA256 and exact strings/numbers',async()=>{
 const data=await S.readStored(await manifest(),options);
 assert.equal(data.Title,'Complete source 🌎 </script>');assert.equal(data.Text.length,9*1048576);
 for(let i=0;i<9;i++)assert.equal(data.Text.slice(i*1048576,(i+1)*1048576),String.fromCharCode(97+i).repeat(1048576));
 assert.deepEqual(data.Values,Array.from({length:1000},(_,i)=>i));
 const small={SchemaVersion:2,Nodes:[]};assert.equal(await S.readStored(small),small);
});

test('invalid manifests refuse before any file request or large allocation',async()=>{
 for(const change of [m=>m.Version++,m=>m.DocumentBytes=268435457,m=>m.Parts.pop(),m=>m.Parts[0].Bytes++,
  m=>m.Parts[0].Sha256='../outside',m=>m.Path='../outside',m=>m.Parts[0].Path='../outside',m=>m.StorageFormat='other']){
  const data=await manifest();change(data);let reads=0;
  await assert.rejects(()=>S.readStored(data,{...options,readPart:async()=>{reads++;return new Uint8Array();}}));assert.equal(reads,0);
 }
});

test('missing, corrupted, reordered and wrong-sized data never reaches a caller',async()=>{
 const source=await manifest();
 await assert.rejects(()=>S.readStored(source,{...options,readPart:async()=>{throw new Error('missing');}}));
 await assert.rejects(()=>S.readStored(source,{...options,readPart:async p=>{const b=await readPart(p);b[b.length-1]^=1;return b;}}));
 await assert.rejects(()=>S.readStored(source,{...options,readPart:async()=>new Uint8Array(1)}));
 const reversed=structuredClone(source);[reversed.Parts[1],reversed.Parts[2]]=[reversed.Parts[2],reversed.Parts[1]];
 await assert.rejects(()=>S.readStored(reversed,options));
 await assert.rejects(()=>S.readStored({...source,DocumentSha256:'0'.repeat(64)},options));
});

test('cancelled reads stop before exposing partial JSON',async()=>{
 const controller=new AbortController();controller.abort();let reads=0;
 await assert.rejects(()=>manifest().then(m=>S.readStored(m,{...options,signal:controller.signal,readPart:async p=>{reads++;return readPart(p);}})),{name:'AbortError'});
 assert.equal(reads,0);
 const mid=new AbortController();
 await assert.rejects(()=>manifest().then(m=>S.readStored(m,{...options,signal:mid.signal,readPart:async p=>{if(++reads===2)mid.abort();return readPart(p);}})),{name:'AbortError'});
 assert.equal(reads,2);
});
