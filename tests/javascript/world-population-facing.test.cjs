const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.FACING_PACKET||'generated/population-facing-source';
const names=['authored','offset','clockwise','nested','translated','reservation-tangent','region','hole-tangent','split','route','reverse-route'];
for(const name of names)test(name+': preserves all oriented geometry',()=>{
 const w=JSON.parse(fs.readFileSync(folder+'/'+name+'/world/world-metadata.json')),before=JSON.stringify(w),view=S.layout(w);
 assert.equal(JSON.stringify(w),before);assert.equal(view.boxes.length,w.Nodes.length);
 assert.deepEqual(view.populationOrientations,w.PlacementPlan.PopulationOrientations);
 for(const site of w.AccessPlan.Sites){
  const box=view.boxes.find(b=>b.node.Id===site.NodeId);
  assert.ok(box);
  const observed=view.suitability.get(site.NodeId).find(e=>JSON.stringify(e.Footprint?.Footprint)===JSON.stringify(site.Footprint));
  assert.ok(observed,'renderer retains the actual oriented source polygon');
  assert.deepEqual(observed.Footprint.Footprint,site.Footprint);
 }
});
test('missing, altered and duplicated orientation witnesses refuse display',()=>{
 const original=JSON.parse(fs.readFileSync(folder+'/authored/world/world-metadata.json'));
 for(const change of [w=>w.PlacementPlan.PopulationOrientations=null,
  w=>w.PlacementPlan.PopulationOrientations[0].TangentDegrees[0]+=180,
  w=>w.PlacementPlan.PopulationOrientations[0].Rotations[0]+=1,
  w=>w.PlacementPlan.PopulationOrientations[0].Facing='Unknown',
  w=>w.PlacementPlan.PopulationOrientations[0].NodeIds[1]=w.PlacementPlan.PopulationOrientations[0].NodeIds[0],
  w=>w.PlacementPlan.PopulationOrientations.push(w.PlacementPlan.PopulationOrientations[0]),
  w=>w.PlacementPlan.PopulationOrientations.find(t=>t.InsideNormalDegrees!=null).InsideNormalDegrees*=-1]){
  const w=structuredClone(original);change(w);assert.throws(()=>S.layout(w),/population orientation/);
 }
});
test('source hole side and actual reversed route cannot be silently replaced',()=>{
 for(const [name,change] of [['hole-tangent',w=>w.PlacementPlan.PopulationOrientations[0].InsideNormalDegrees*=-1],
  ['reverse-route',w=>w.PlacementPlan.Definition.Bindings.find(b=>b.Population?.Facing!=null).Population.Route.Reverse=false]]){
  const w=JSON.parse(fs.readFileSync(folder+'/'+name+'/world/world-metadata.json'));change(w);assert.throws(()=>S.layout(w),/population orientation/);
 }
});
