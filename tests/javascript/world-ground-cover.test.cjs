const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_GROUND_COVER_ROOT||'generated/ground-cover-qualified-native';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-metadata.json'),'utf8'));
for(const name of ['site','partial','irregular','terrain-gap','trails','allocations','registered','source-edit','terrain-edit','session']){
 const w=read(name);
 test(`${name}: exact saved ground material cells, sources and area accounting`,()=>{
  const layout=S.layout(w),p=S.groundCover(w);assert.strictEqual(layout.groundCover,w.GroundCoverPlan);assert.strictEqual(p,w.GroundCoverPlan);
  assert.ok(p.Cells.length>0);assert.equal(p.Matches.length,p.Definition.Rules.length);
  assert.ok(Math.abs(p.CoveredArea+p.ExcludedArea+p.UnresolvedArea-p.DomainArea)<1e-6);
  for(const c of p.Cells){assert.ok(c.Area>0);for(const id of c.SourceIds)assert.ok(p.Sources.some(s=>s.Id===id));}
 });
 test(`${name}: missing, forged or contradictory ground records refuse`,()=>{
  const mutations=[w=>w.GroundCoverPlan=null,w=>w.GroundCoverId='forged',w=>w.GroundCoverPlan.ContextSha256='bad',w=>w.GroundCoverPlan.CoveredArea++,
   w=>w.GroundCoverPlan.Cells.pop(),w=>w.GroundCoverPlan.Cells.push(w.GroundCoverPlan.Cells[0]),w=>w.GroundCoverPlan.Cells[0].Area++,
   w=>w.GroundCoverPlan.Matches[0].Count++,w=>w.GroundCoverPlan.Sources[0].MaterialId='missing',w=>w.GroundCoverPlan.Sources[0].Priority++,
   w=>w.GroundCoverPlan.Work=w.GroundCoverPlan.Definition.MaximumWork+1];
  for(const change of mutations){const changed=structuredClone(w);change(changed);assert.throws(()=>S.layout(changed),/Invalid ground cover/);}
 });
}
test('partial scope retains unresolved ground; strict completeness cannot bless it',()=>{
 const w=read('partial');assert.ok(w.GroundCoverPlan.UnresolvedArea>0);assert.ok(w.GroundCoverPlan.Cells.some(c=>c.Status==='Unresolved'));
 w.GroundCoverPlan.Definition.RequireComplete=true;assert.throws(()=>S.layout(w),/Invalid ground cover/);
});
test('complete regional programs preserve individual source ground plans',()=>{
 const w=JSON.parse(fs.readFileSync(path.join(root,'regional/world-regions.json'),'utf8')),p=R.layout(w);assert.equal(p.boxes.length,15);
 for(const v of p.views){assert.strictEqual(v.plan.groundCover,v.packet.World.GroundCoverPlan);assert.equal(v.plan.groundCover.UnresolvedArea,0);}
});
test('authored material IDs can equal status names without merging covered, excluded or unresolved fills',()=>{
 const cells=[{Status:'Covered',MaterialId:'Excluded'},{Status:'Excluded',MaterialId:null},{Status:'Covered',MaterialId:'Unresolved'},{Status:'Unresolved',MaterialId:null},{Status:'Covered',MaterialId:'Excluded'}];
 const batches=S.groundCoverBatches({Cells:cells});assert.equal(batches.length,4);assert.deepEqual(batches.map(b=>b.length),[2,1,1,1]);
 assert.strictEqual(batches[0][0],cells[0]);assert.strictEqual(batches[0][1],cells[4]);assert.strictEqual(batches[1][0],cells[1]);
});
