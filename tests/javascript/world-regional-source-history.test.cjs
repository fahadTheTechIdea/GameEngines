const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_SOURCE_HISTORY_ROOT||'generated/regional-source-history-native-r1';
const read=(state,file='world-regions.json')=>JSON.parse(fs.readFileSync(path.join(root,state,file),'utf8'));
for(const state of ['initial','preferred','rollback'])test(`${state}: complete edited source programs stay inspectable`,()=>{
 const w=read(state),before=JSON.stringify(w),p=R.layout(w),c=read(state,'generation-session.json');
 assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,12);assert.equal(p.views.length,3);
 for(const region of w.Regions){
  const selected=state==='preferred'&&region.RegionId==='alpha',world=region.World;
  assert.equal(world.Nodes.length,4);assert.equal(world.AccessPlan.Sites.length,3);assert.equal(world.RoutePlan.Routes.length,1);
  assert.equal(world.Nodes.find(n=>n.Id==='complete-program/hosts-0').ArchetypeId,selected?'broad':'long');
  assert.deepEqual(world.ProgramCompositionPlan.Definition.RequiredRanks,[selected?0:1]);
  assert.equal(world.ProgramMixPlan.Allocations[0].Balances[0].Required,4);
  assert.equal(world.ProgramMixPlan.Allocations[0].Balances[0].Provided,4);
 }
 assert.deepEqual(c.Edits.map(e=>e.Request.Id),state==='initial'?[]:state==='preferred'?['preferred-feasible']:['preferred-feasible','rollback']);
 if(c.Edits.length)assert.deepEqual(c.Edits.at(-1).Report.Regions.filter(r=>r.Reused).map(r=>r.RegionId),['beta','gamma']);
});
test('source rollback restores exact original world without erasing accepted history',()=>{
 assert.deepEqual(read('initial'),read('rollback'));
 assert.notDeepEqual(read('initial','generation-session.json'),read('rollback','generation-session.json'));
 assert.equal(read('initial','generation-session.json').WorldSha256,read('rollback','generation-session.json').WorldSha256);
});
test('edited selection receipts remain bound to actual complete source and failed choices',()=>{
 for(const mutate of [w=>w.Definition.Regions[0].Recipe.ProgramComposition.RequiredRanks=[1],
  w=>w.Definition.OwnershipPlan.CompositionPlan.Attempts.at(-1).Choices[0].RequiredRanks=[1],
  w=>w.Definition.OwnershipPlan.CompositionPlan.Attempts[0].InvariantChoice.Choice.RegionId='alpha',
  w=>w.Regions[0].World.ProgramCompositionPlan.Attempts[0].Prefix=[1]]){
  const w=read('preferred');mutate(w);assert.throws(()=>R.layout(w));
 }
});
