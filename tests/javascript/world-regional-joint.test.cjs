const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGIONAL_JOINT_ROOT||'generated/regional-joint-terrain-native-r16';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const [name,counts] of [['baseline',[5,5]],['larger-source',[5,5]],['terrain-edit',[4,5]]])test(`${name}: complete terrain-only joint world`,()=>{
 const w=read(name),before=JSON.stringify(w),plan=R.layout(w),j=w.Definition.OwnershipPlan.JointPlan;
 assert.equal(JSON.stringify(w),before);assert.equal(plan.views.length,2);assert.equal(plan.boxes.length,2+2*counts.reduce((a,b)=>a+b,0));
 assert.equal(j.Attempts.at(-1).Accepted,true);assert.ok(j.Attempts.some(a=>a.InvariantChoice));
 assert.equal(j.EnumerationWork+j.Attempts.reduce((n,a)=>n+a.Work,0),w.Definition.OwnershipPlan.Work);
 w.Regions.forEach((r,i)=>{
  const world=r.World,n=counts[i];assert.equal(world.TerrainDemandPlan.Results[0].Count,n);
  assert.equal(world.Nodes.length,1+2*n);assert.equal(world.AccessPlan.Sites.length,2*n);assert.equal(world.RoutePlan.Routes.length,n-1);
  assert.equal(world.PlacementPlan.Definition.SourceWorld.PlacementPlan,null);
  assert.deepEqual(world.RegionPlan,world.PlacementPlan.Definition.SourceWorld.RegionPlan);
  assert.equal(world.ProgramCompositionPlan.Definition.RequiredRanks.length,n+1);
  assert.equal(world.ProgramMixPlan.Allocations.find(a=>a.Request.PartId==='landmarks').Request.ExactCount,n);
 });
});
test('joint branch links, source binding, work and accepted proof remain mandatory',()=>{
 for(const mutate of [w=>delete w.Definition.OwnershipPlan.JointPlan,
  w=>w.Definition.OwnershipPlan.JointPlan.EnumerationWork++,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts.at(-1).Work++,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts.at(-1).OutputChoices[0].RequiredRanks=[0],
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts.at(-1).ParentOrdinal=999,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts.at(-1).Stable=false,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts[0].InvariantChoice.NestedSourceEnvelope.Required=0,
  w=>w.Definition.OwnershipPlan.JointPlan.Attempts[0].InvariantProgramSha256='changed']){
  const w=read('baseline');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('terrain count is authoritative over mixed complete programs',()=>{
 for(const mutate of [w=>delete w.ProgramMixPlan.Allocations.find(a=>a.Request.PartId==='landmarks').Request.ExactCount,
  w=>w.ProgramMixPlan.Allocations.find(a=>a.Request.PartId==='landmarks').Request.ExactCount++,
  w=>w.TerrainDemandPlan.Results[0].Count--]){
  const w=read('baseline').Regions[0].World;mutate(w);assert.throws(()=>S.layout(w));
 }
});
test('terrain-only contexts refuse fabricated source placement or source overrides',()=>{
 for(const mutate of [w=>w.Definition.Regions[0].Recipe.PopulationSource.Context.PlacementAnchorIds=['missing'],
  w=>w.Regions[0].World.PlacementPlan.Definition.SourceWorld.PlacementPlan={},
  w=>w.Definition.Regions[0].Recipe.PopulationSource.Recipe.Origin=[0,0]]){
  const w=read('baseline');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('larger parents retain every original marker; terrain changes counts and geometry',()=>{
 const a=read('baseline'),b=read('larger-source'),c=read('terrain-edit');
 assert.deepEqual(a.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId])),b.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId])));
 assert.ok(b.Definition.OwnershipPlan.ProgramDemands.every((d,i)=>d.EnvelopeArea>a.Definition.OwnershipPlan.ProgramDemands[i].EnvelopeArea));
 assert.equal(c.Definition.Surface.Exclusions.length,1);assert.notDeepEqual(a.Regions,c.Regions);
});
