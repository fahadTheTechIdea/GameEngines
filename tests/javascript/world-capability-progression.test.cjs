const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.CAPABILITY_PACKET||'generated/capability-progression-qualified';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
test('actual packet retains complete hierarchy and acquired-state revisit witness',()=>{
 const view=Schematic.layout(world);assert.equal(view.boxes.length,6);
 const result=world.ProgressionPlan.Results[0],path=result.Paths[0];
 assert.equal(result.ReachableStates,10);assert.equal(path.NodeIds[1],path.NodeIds[3]);
 assert.deepEqual(path.CapabilityStates,[[],[],['bronze'],['bronze'],['bronze','silver'],['bronze','silver']]);
 assert.equal(world.ProgressionPlan.Rules[0].Capabilities.RequireReturnToStart,true);
 // Independent direct witness interpretation using string sets, not engine bit masks.
 const rule=world.ProgressionPlan.Rules[0],c=rule.Capabilities,inventory=new Set(c.InitialCapabilityIds);
 for(let i=0;i<path.NodeIds.length;i++){
  for(const grant of c.Grants.filter(g=>g.NodeId===path.NodeIds[i]))for(const id of grant.CapabilityIds)inventory.add(id);
  assert.deepEqual(new Set(path.CapabilityStates[i]),inventory);
  if(i<path.ConnectionIds.length)for(const gate of c.Gates.filter(g=>g.ConnectionId===path.ConnectionIds[i]))for(const id of gate.RequiredCapabilityIds)assert.ok(inventory.has(id));
 }
});
test('saved transition tampering and invalid capability declarations refuse rendering',()=>{
 for(const change of [
  w=>w.ProgressionPlan.Results[0].Paths[0].CapabilityStates[1].push('bronze'),
  w=>w.ProgressionPlan.Results[0].Paths[0].CapabilityStates[3].pop(),
  w=>w.ProgressionPlan.Results[0].Paths[0].CapabilityStates=null,
  w=>w.ProgressionPlan.Results[0].ReachableStates=2,
  w=>w.ProgressionPlan.Rules[0].Capabilities.Grants[0].CapabilityIds.push('unknown'),
  w=>w.ProgressionPlan.Rules[0].Capabilities.Gates[0].ConnectionId='missing',
  w=>w.ProgressionPlan.Rules[0].Capabilities.CapabilityIds.push('unused'),
  w=>w.ProgressionPlan.Rules[0].Capabilities.InitialCapabilityIds.push('unknown'),
  w=>w.ProgressionPlan.Rules[0].Capabilities.MaximumStates=1,
  w=>w.ProgressionPlan.Results[0].Paths[0].ConnectionIds[2]='missing']){
   const copy=structuredClone(world);change(copy);assert.throws(()=>Schematic.layout(copy),/progression witness/);
 }
});
test('packet embeds exact saved metadata and current renderer',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
