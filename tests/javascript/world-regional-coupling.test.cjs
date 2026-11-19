const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_COUPLING_ROOT||'generated/regional-coupling-native-r3';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['baseline','larger-source','reversed-demand','terrain-edit'])test(`${name}: complete coupled habitat world is inspectable`,()=>{
 const world=read(name),before=JSON.stringify(world),layout=R.layout(world),p=world.Definition.OwnershipPlan;
 assert.equal(JSON.stringify(world),before);assert.equal(layout.views.length,3);
 assert.equal(layout.boxes.length,world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0));
 assert.ok(p.CouplingPlan.Attempts.length>1);assert.equal(p.CouplingPlan.Attempts.at(-1).Stable,true);
 for(const region of world.Regions){
  const w=region.World,n=w.TerrainDemandPlan.Results[0].Count;
  assert.equal(w.Nodes.length,16+n);assert.equal(w.RoutePlan.Routes.length,1+n);
  assert.ok(w.RoutePlan.Routes.every(r=>r.Reverse));
  assert.equal(n,Math.floor(w.RegionPlan.SuitableArea/5000));
 }
});
test('missing or tampered convergence/work receipt refuses inspection',()=>{
 for(const change of [p=>delete p.CouplingPlan,p=>p.CouplingPlan.InitializationWork++,
  p=>p.CouplingPlan.Attempts[0].Work++,p=>p.CouplingPlan.Attempts[0].OutputSha256='0'.repeat(64),
  p=>p.CouplingPlan.Attempts.at(-1).Stable=false,p=>p.CouplingPlan.Attempts.at(-1).Demands[0].NodeCount++,
  p=>p.CouplingPlan.Attempts.at(-1).Demands[0].HabitatSha256='0'.repeat(64),p=>delete p.Source.Coupling]){
  const w=read('baseline');change(w.Definition.OwnershipPlan);assert.throws(()=>R.layout(w),/ownership derivation/);
 }
});
test('bound source habitat and whole source context remain required',()=>{
 for(const change of [w=>w.Regions[0].World.PlacementPlan.Definition.SourceWorld.RegionPlan.Request.Boundary.Vertices[0].X++,
  w=>w.Definition.Regions[0].Recipe.PopulationSource.Recipe.Regions.Boundary=w.Definition.Regions[0].Ownership]){
  const w=read('baseline');change(w);assert.throws(()=>R.layout(w));
 }
});
test('demand field and source size affect actual ownership while sources remain complete',()=>{
 const a=read('baseline'),b=read('larger-source'),c=read('reversed-demand');
 assert.notDeepEqual(a.Definition.Regions.map(r=>r.Ownership),b.Definition.Regions.map(r=>r.Ownership));
 assert.notDeepEqual(a.Definition.Regions.map(r=>r.Ownership),c.Definition.Regions.map(r=>r.Ownership));
 for(const w of [a,b,c])for(const r of w.Regions)assert.equal(r.World.PlacementPlan.Definition.SourceWorld.Nodes.length,4);
});
test('moving protected terrain changes actual content poses and paths without reducing scope',()=>{
 const a=read('baseline'),b=read('terrain-edit');
 assert.notDeepEqual(a.Regions.map(r=>r.World.Nodes),b.Regions.map(r=>r.World.Nodes));
 assert.notDeepEqual(a.Regions.map(r=>r.World.RoutePlan.Routes),b.Regions.map(r=>r.World.RoutePlan.Routes));
 assert.deepEqual(a.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId,n.ParentId])),b.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId,n.ParentId])));
});
