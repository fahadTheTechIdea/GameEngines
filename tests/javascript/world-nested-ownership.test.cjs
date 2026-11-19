const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_NESTED_OWNERSHIP_ROOT||'generated/nested-ownership-native-r2';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['baseline','larger-source','terrain-edit','rotated-ownership','mixed-programs'])test(name+': complete nested regions and local allocations',()=>{
 const world=read(name),before=JSON.stringify(world),layout=R.layout(world),p=world.Definition.OwnershipPlan;
 assert.equal(JSON.stringify(world),before);assert.equal(layout.views.length,3);assert.equal(layout.boxes.length,33);
 assert.equal(p.JointPlan.Attempts[0].InputChoices.every(c=>c.RegionSelectionDeferred&&c.RequiredRanks.length===(name==='mixed-programs'?1:0)),true);
 assert.equal(p.JointPlan.Attempts.at(-1).OutputChoices.every(c=>!c.RegionSelectionDeferred&&c.RequiredRanks.length===(name==='mixed-programs'?2:1)),true);
 world.Regions.forEach(packet=>{
  const definition=world.Definition.Regions.find(r=>r.Id===packet.RegionId),source=p.Source.Regions.find(r=>r.Id===packet.RegionId),a=p.ProgramDemands.find(r=>r.RegionId===packet.RegionId).Analysis;
  assert.equal(source.Recipe.Regions.Boundary,null);assert.deepEqual(packet.World.RegionPlan.Request.Boundary,definition.Ownership);
  assert.equal(packet.World.RegionPlan.Regions.length,3);assert.equal(packet.World.LocalLayoutPlan.Allocations.length,3);
  assert.equal(packet.World.Nodes.length,11);assert.equal(packet.World.AccessPlan.Sites.length,9);
  assert.equal(packet.World.RoutePlan.Routes.length,3);assert.equal(a.RegionSelectionDeferred??false,false);
  assert.deepEqual(a.RegionAnalysis.Plan,packet.World.RegionPlan);assert.deepEqual(a.RegionAnalysis.DemandPlan,packet.World.RegionDemandPlan);
 });
});
test('explicit context and original ownership boundary cannot be substituted',()=>{
 for(const mutate of [w=>delete w.Definition.OwnershipPlan.Source.Regions[0].SpatialContext,
  w=>w.Definition.OwnershipPlan.Source.Regions[0].Recipe.Regions.Boundary=w.Definition.Surface.Boundary,
  w=>w.Definition.Regions[0].Recipe.Regions.Boundary=w.Definition.Surface.Boundary,
  w=>w.Definition.OwnershipPlan.Source.Regions[1].Recipe.LocalLayouts.Layouts[0].RegionFrame=null]){
  const w=read('baseline');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('deferred initialization is distinct from an accepted spatial selection',()=>{
 for(const mutate of [w=>delete w.Definition.OwnershipPlan.JointPlan.Attempts[0].InputChoices[0].RegionSelectionDeferred,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts.at(-1).OutputChoices[0].RegionSelectionDeferred=true,
  w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.RegionSelectionDeferred=true,
  w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.RegionAnalysis.Plan.Regions[0].Area++]){
  const w=read('baseline');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('authored metadata coordinates match actual content; physical sources stay unplaced in analysis',()=>{
 for(const mutate of [w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Nodes.find(n=>n.Id.endsWith('/markers-0')).Position[0]++,
  w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Nodes.find(n=>n.Id.endsWith('/first-0')).Position=[1,1],
  w=>w.Definition.OwnershipPlan.Source.Regions[0].Catalog.Archetypes.find(t=>t.Id==='root').Parts.find(p=>p.Id==='markers').Layout=null]){
  const w=read('baseline');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('worlds retain mixed local strategies and respond to ownership/source/terrain inputs',()=>{
 const a=read('baseline'),b=read('larger-source'),c=read('terrain-edit'),d=read('rotated-ownership');
 assert.notDeepEqual(a.Definition.Regions.map(r=>r.Ownership),d.Definition.Regions.map(r=>r.Ownership));
 assert.notDeepEqual(a.Regions.map(r=>r.World.RegionPlan),c.Regions.map(r=>r.World.RegionPlan));
 assert.notDeepEqual(a.Definition.OwnershipPlan.ProgramDemands,b.Definition.OwnershipPlan.ProgramDemands);
 const strategies=new Set(a.Regions.flatMap(r=>r.World.LocalLayoutPlan.Definition.Layouts.map(l=>l.Strategy)));
 assert.deepEqual([...strategies].sort(),['AreaBalanced','PowerDiagram']);
});
