const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_OWNERSHIP_ROOT||'generated/ownership-derive-native-r4';
test('regional source filter follows native priority/ordinal ordering and conservative boundary contact',()=>{
 const box=(x,y,w,h)=>({Vertices:[{X:x,Y:y},{X:x+w,Y:y},{X:x+w,Y:y+h},{X:x,Y:y+h}]});
 const boundary=box(0,0,10,10),shape=box(1,1,2,2),touch=box(10,2,2,2),remote=box(20,20,2,2);
 const sources=R.regionalSurfaceSources({Patches:[{Id:'z',Priority:0,Boundary:shape},{Id:'b',Priority:3,Boundary:shape},
  {Id:'a',Priority:3,Boundary:touch},{Id:'remote',Priority:100,Boundary:remote}],Exclusions:[{Id:'z',Boundary:shape},{Id:'a',Boundary:touch},{Id:'remote',Boundary:remote}]},boundary);
 assert.deepEqual(sources.Patches.map(p=>p.Id),['a','b','z']);assert.deepEqual(sources.Exclusions.map(e=>e.Id),['a','z']);
});
for(const name of ['world','moved','weighted','concave']){
 const w=JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 test(`${name}: derived ownership keeps every whole program and terrain-qualified binding`,()=>{
  const view=R.layout(w),proof=R.ownershipDerivation(w.Definition);
  assert.equal(view.boxes.length,15);assert.equal(view.views.length,3);assert.equal(proof.Anchors.length,3);
  assert.equal(w.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),9);
  for(const original of proof.Source.Regions){assert.equal(original.Recipe.Origin,null);assert.equal(original.Recipe.Placement.Request.Boundary,null);assert.deepEqual(original.Recipe.Placement.Request.Anchors,[]);}
 });
 test(`${name}: missing, forged or contradictory source/ownership/anchor proof refuses`,()=>{
  for(const mutate of [
   d=>d.OwnershipPlan=null,d=>d.OwnershipDerivationId='forged',d=>d.OwnershipPlan.Work=d.MaximumWork+1,
   d=>d.OwnershipPlan.Source.Regions[0].Weight++,d=>d.OwnershipPlan.Source.Regions[0].Recipe.Origin=[0,0],
   d=>d.OwnershipPlan.Partition.Strata[0].Area++,d=>d.OwnershipPlan.Anchors[0].Plan.IntentPlan.Selections[0].Seed.Position.X++,
   d=>d.Regions[0].Recipe.Origin[0]++,d=>d.Regions[0].Ownership.Vertices[0].X++,
   d=>d.OwnershipPlan.Anchors[0].Plan.Request.Intents[0].MinimumClearance=0
  ]){const changed=structuredClone(w);mutate(changed.Definition);assert.throws(()=>R.layout(changed),/Invalid regional ownership|Invalid region intent/);}
 });
}
test('native regional source filtering preserves relevant whole exclusions and refuses altered subsets',()=>{
 const w=JSON.parse(fs.readFileSync(path.join(root,'world/world-regions.json'),'utf8'));
 assert.deepEqual(w.Regions.find(r=>r.RegionId==='alpha').World.Surface.Exclusions,[]);
 assert.equal(w.Regions.find(r=>r.RegionId==='beta').World.Surface.Exclusions.length,1);
 for(const regionId of ['alpha','beta']){
  const changed=structuredClone(w),region=changed.Regions.find(r=>r.RegionId===regionId);
  region.World.Surface.Exclusions=regionId==='alpha'?structuredClone(changed.Definition.Surface.Exclusions):[];
  assert.throws(()=>R.layout(changed),/regional source binding/);
 }
});
