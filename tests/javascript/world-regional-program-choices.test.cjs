const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGIONAL_PROGRAM_ROOT||'generated/regional-program-qualified-v2-native';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['baseline','larger-source','components'])test(`${name}: all selected complete programs remain inspectable`,()=>{
 const w=read(name),before=JSON.stringify(w),p=R.layout(w),o=w.Definition.OwnershipPlan;
 assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,12);assert.equal(p.views.length,3);
 assert.equal(o.CompositionPlan.Attempts.length,4);assert.deepEqual(o.CompositionPlan.Attempts.slice(0,3).map(a=>a.FailureRegionId),['alpha','beta','gamma']);
 for(const packet of w.Regions){const actual=packet.World;
  assert.equal(actual.Nodes.length,4);assert.equal(actual.AccessPlan.Sites.length,3);assert.equal(actual.RoutePlan.Routes.length,1);
  assert.deepEqual(actual.ProgramCompositionPlan.Definition.RequiredRanks,[1]);assert.equal(actual.ProgramCompositionPlan.Attempts.length,1);
  assert.equal(actual.ProgramMixPlan.Allocations[0].Balances[0].Required,4);assert.equal(actual.ProgramMixPlan.Allocations[0].Balances[0].Provided,4);
 }
 if(name==='components')assert.equal(o.PhysicalFeedbackPlan.Attempts.length,1);
});
test('regional choice source, bound ranks and complete work/failure receipts stay mandatory',()=>{
 for(const change of [p=>delete p.CompositionPlan,p=>delete p.Source.ProgramComposition,p=>p.CompositionPlan.EnumerationWork++,
  p=>p.CompositionPlan.Attempts[0].Work++,p=>p.CompositionPlan.Attempts[0].Accepted=true,
  p=>p.CompositionPlan.Attempts[3].Choices[0].RequiredRanks=[0],p=>p.CompositionPlan.Attempts[0].InvariantChoice.Choice.RegionId='beta',
  p=>p.CompositionPlan.Attempts[0].InvariantChoice.NestedSourceEnvelope.Required=1]){
  const w=read('baseline');change(w.Definition.OwnershipPlan);assert.throws(()=>R.layout(w));
 }
});
test('bound per-world program composition rejects missing or mismatched exact prefix',()=>{
 for(const change of [w=>w.ProgramCompositionPlan.Definition.RequiredRanks=[],w=>w.ProgramCompositionPlan.Attempts[0].Prefix=[],
  w=>w.ProgramCompositionPlan.Attempts[0].Prefix=[1,0],w=>w.ProgramCompositionPlan.Attempts.push(structuredClone(w.ProgramCompositionPlan.Attempts[0]))]){
  const w=read('baseline').Regions[0].World;change(w);assert.throws(()=>S.layout(w));
 }
});
test('larger original complete host changes measured capacity without shrinking payloads',()=>{
 const a=read('baseline'),b=read('larger-source');
 assert.ok(b.Definition.OwnershipPlan.ProgramDemands.every((d,i)=>d.EnvelopeArea>a.Definition.OwnershipPlan.ProgramDemands[i].EnvelopeArea));
 assert.deepEqual(a.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId])),b.Regions.map(r=>r.World.Nodes.map(n=>[n.Id,n.ArchetypeId])));
});
