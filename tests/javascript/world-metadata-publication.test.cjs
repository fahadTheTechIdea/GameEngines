const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_PUBLICATION_ROOT||'generated/metadata-publication-cli';
function read(path){return JSON.parse(fs.readFileSync(path));}
function receipt(folder){
 const r=read(`${folder}/publication.json`);assert.equal(r.Version,1);assert.ok(Number.isInteger(r.CommitAttempt)&&r.CommitAttempt>=1&&r.CommitAttempt<=4);
 assert.equal(r.EarlierFailures.length,r.CommitAttempt-1);
 r.EarlierFailures.forEach((f,i)=>{assert.equal(f.Attempt,i+1);assert.ok([5,32].includes(f.Win32Code));});
}
test('single world retains complete schematic and a bounded publication receipt',()=>{
 const folder=`${root}/world`,w=read(`${folder}/world-metadata.json`);assert.equal(w.Nodes.length,7);assert.equal(Schematic.layout(w).boxes.length,7);receipt(folder);
 assert.deepEqual(fs.readFileSync(`${folder}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('collection and child receipts preserve exact local worlds and qualified journeys',()=>{
 const folder=`${root}/domains`,w=read(`${folder}/world-domains.json`);receipt(folder);assert.equal(w.Domains.length,3);assert.equal(w.Transitions.length,2);
 assert.equal(w.Journeys[0].Forward.length,11);assert.equal(w.Journeys[0].Return.length,11);
 w.Domains.forEach((d,i)=>{const local=`${folder}/domain-${String(i).padStart(4,'0')}`,child=read(`${local}/world-metadata.json`);
  assert.deepEqual(child,d.World);assert.equal(Schematic.layout(child).boxes.length,7);receipt(local);
  assert.deepEqual(fs.readFileSync(`${local}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
});
test('controlled test receipts retain actual Windows 32/5 failures and exact exhaustion',()=>{
 const results=read('generated/metadata-publication-final-results/results.json');
 for(const [name,code] of [['directory-lock',32],['child-lock',5]]){
  const r=results.find(x=>x.Case===name);assert.ok(r.Receipt.CommitAttempt>=2);assert.equal(r.Validations,r.Receipt.CommitAttempt);
  assert.ok(r.Receipt.EarlierFailures.every(f=>f.Win32Code===code));
 }
 const exhausted=results.find(r=>r.Case==='exhaustion-and-cleanup');assert.equal(exhausted.Receipt.CommitAttempt,4);
 const errors=JSON.parse(exhausted.Error.slice(exhausted.Error.indexOf(':')+1));assert.equal(errors.length,4);assert.ok(errors.every(e=>e.Win32Code===5));
 for(const mode of ['cancel','dependency-drift','destination-race'])assert.ok(results.find(r=>r.Case===mode)?.Failure);
});
