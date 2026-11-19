const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_TOPOLOGY_ROOT||'generated/regional-topology-native-r1';
for(const name of ['initial','changed','physical']){
 const world=JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 test(`${name}: complete shared local programs and generated regional topology render together`,()=>{
  const view=R.layout(world),p=world.Definition.TopologyPlan;
  assert.equal(view.boxes.length,30);assert.equal(world.Connections.length,7);
  assert.equal(p.Topology.Results[0].Connections.filter(c=>c.Properties.reason==='stage-loop').length,2);
  assert.equal(p.Nodes.length,7);assert.equal(p.Progression.Rules[0].RequiredMilestones.length,1);
  assert.ok(R.regionalTopology(world.Definition)>0);
  assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&(p.Definition.Rules[0].Topology.Directed===true?c.Reverse===null:c.Reverse?.Status==='Found')));
 });
 test(`${name}: stale source, dropped members, changed bindings, branches and work refuse`,()=>{
  for(const mutate of [
   d=>d.TopologyPlan=null,d=>d.OwnershipPlan.Source.Topology=null,
   d=>d.TopologyPlan.Work++,d=>d.TopologyPlan.Definition.Destinations[0].Kind='different',
   d=>d.TopologyPlan.Nodes[1].Kind='different',d=>d.TopologyPlan.Nodes[1].ParentId='different',
   d=>d.TopologyPlan.Topology.Results[0].Stages[0].MemberNodeIds.pop(),
   d=>d.TopologyPlan.Topology.Results[0].Connections[0].Properties.stage='different',
   d=>d.TopologyPlan.Topology.Results[0].Connections[0].FromId='different',
   d=>d.TopologyPlan.Topology.Results[0].Connections[0].Bidirectional=!d.TopologyPlan.Topology.Results[0].Connections[0].Bidirectional,
   d=>{const c=d.TopologyPlan.Topology.Results[0].Connections.find(e=>e.Properties.return===true)||d.TopologyPlan.Topology.Results[0].Connections[0];c.Properties.return=c.Properties.return===true?false:true;},
   d=>d.TopologyPlan.Definition.Rules[0].Topology.Directed=null,
   d=>d.TopologyPlan.Definition.Rules[0].Topology.ReturnConnections=null,
   d=>d.TopologyPlan.Links.pop(),d=>d.TopologyPlan.Links[0].RegionalConnectionId='different',
   d=>d.TopologyPlan.Connections[0].From.PortId='different',
   d=>d.TopologyPlan.Progression.Rules[0].RequiredMilestones=[],
   d=>d.TopologyPlan.Progression.Results[0].Paths[0].NodeIds.pop()
  ]){const changed=structuredClone(world);mutate(changed.Definition);assert.throws(()=>R.layout(changed),/Invalid regional topology|Invalid regional ownership|Invalid progression/);}
 });
}
test('seed edit varies macro links while exact complete local packets remain unchanged',()=>{
 const load=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 const a=load('initial'),b=load('changed');assert.deepEqual(a.Regions,b.Regions);
 const pairs=w=>w.Definition.TopologyPlan.Topology.Results[0].Connections.map(c=>[c.FromId,c.ToId]);
 assert.notDeepEqual(pairs(a),pairs(b));
});
