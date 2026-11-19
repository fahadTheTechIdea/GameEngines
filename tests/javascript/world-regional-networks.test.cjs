const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_NETWORK_ROOT;
const composedRoot=process.env.WORLD_REGIONAL_NETWORK_COMPOSED_ROOT;
if(!root||!composedRoot)throw new Error('WORLD_REGIONAL_NETWORK_ROOT and WORLD_REGIONAL_NETWORK_COMPOSED_ROOT must identify current native publications');
const load=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['initial','cost-changed','redundant','multiple']){
 const world=load(name);
 test(`${name}: all local content and actual terrain-selected network routes inspect together`,()=>{
  const view=R.layout(world);assert.equal(view.boxes.length,20);assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),12);
  assert.ok(world.NetworkPlan.Work>0);assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
  if(name==='redundant')assert.equal(world.NetworkPlan.Networks[0].Plan.Analysis.Bridges.length,0);
  if(name==='multiple')assert.ok(world.Connections.some(c=>c.Definition.From.RegionId===c.Definition.To.RegionId));
 });
 test(`${name}: missing, dropped, moved or contradictory network witnesses refuse`,()=>{
  for(const mutate of [
   w=>w.NetworkPlan=null,w=>w.Definition.NetworkRules=null,w=>w.NetworkPlan.Work++,
   w=>w.NetworkPlan.Networks[0].Plan.Destinations[0].Position.X++,
   w=>w.NetworkPlan.Networks[0].Plan.Options.Profile.KindCosts.bog++,
   w=>w.NetworkPlan.Networks[0].Plan.Analysis.Components[0].pop(),
   w=>w.NetworkPlan.Networks[0].Plan.Analysis.Bridges=['missing'],
   w=>w.NetworkPlan.Networks[0].Plan.Edges.pop(),w=>w.NetworkPlan.Networks[0].Links.pop(),
   w=>w.NetworkPlan.Networks[0].Links[0].RegionalConnectionId='missing',
   w=>w.NetworkPlan.Networks[0].Plan.Attempts[0].Cost=null,
   w=>w.Connections[0].Forward.Path.Points[0].X++,w=>w.Connections.pop(),
   w=>w.Connections[0].Definition.From.PortId='missing'
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>R.layout(changed),/Invalid regional network|Invalid regional ownership|Invalid regional schematic/);}
 });
}
test('travel-cost counterfactual changes the selected graph and preserves exact complete local packets',()=>{
 const a=load('initial'),b=load('cost-changed');assert.deepEqual(a.Regions,b.Regions);
 const pairs=w=>w.NetworkPlan.Networks[0].Plan.Edges.map(e=>[e.Link.FromId,e.Link.ToId]).sort();assert.notDeepEqual(pairs(a),pairs(b));
 assert.equal(a.Definition.NetworkRules[0].Options.Profile.KindCosts.bog,1);assert.equal(b.Definition.NetworkRules[0].Options.Profile.KindCosts.bog,9);
 assert.equal(b.Definition.NetworkRules[0].Options.MaximumWork,50000000);
});
test('required-link failure retains actual rejected pair statuses and no successful replacement',()=>{
 const failure=JSON.parse(fs.readFileSync(path.join(root,'required-failure.json'),'utf8'));
 assert.equal(failure.Status,'RequiredConnectionUnresolved');assert.equal(failure.Attempts.length,1);assert.equal(failure.Edges.length,0);
 assert.ok(failure.Attempts[0].ForwardStatus!=='Found'||failure.Attempts[0].ReverseStatus!=='Found');
});
test('staged topology, terrain-selected links and physical feedback remain one inspectable world',()=>{
 const world=JSON.parse(fs.readFileSync(path.join(composedRoot,'world/world-regions.json'),'utf8'));
 assert.equal(R.layout(world).boxes.length,20);assert.equal(world.Definition.Connections.length,4);assert.equal(world.Connections.length,7);
 assert.ok(world.Definition.TopologyPlan);assert.equal(world.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts.at(-1).Accepted,true);
 assert.equal(world.NetworkPlan.Networks.length,1);
});
