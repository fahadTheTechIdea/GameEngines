const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.PROGRESSION_PACKET||'generated/encounter-journey';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
test('logical world retains all nodes, rewards and saved progression without terrain',()=>{
 const view=Schematic.layout(world);
 assert.equal(view.boxes.length,9);assert.equal(world.Nodes.filter(n=>n.Kind==='reward').length,2);
 assert.ok(world.Nodes.every(n=>n.Position===null));assert.equal(world.Surface,null);
 assert.deepEqual(view.world.ProgressionPlan,world.ProgressionPlan);
});
test('saved witnesses respect link identities, direction and mandatory order',()=>{
 const path=world.ProgressionPlan.Results[0].Paths[0];
 assert.deepEqual(path.NodeIds.map(n=>n.split('/').at(-1)),['entry-0','choices-0','relay-0','vault-0','exit-0']);
 for(let i=0;i<path.ConnectionIds.length;i++){
  const link=world.Connections.find(l=>l.Id===path.ConnectionIds[i]);
  assert.equal(link.FromId,path.NodeIds[i]);assert.equal(link.ToId,path.NodeIds[i+1]);
 }
});
test('renderer refuses malformed path, direction, order, scope and accounting',()=>{
 for(const change of [
  w=>w.ProgressionPlan.Results[0].Paths[0].NodeIds.splice(2,1),
  w=>{const l=w.Connections[0];[l.FromId,l.ToId]=[l.ToId,l.FromId];},
  w=>w.ProgressionPlan.Rules[0].RequiredMilestones.reverse(),
  w=>w.ProgressionPlan.Rules[0].ScopeNodeId='encounter-journey/choices-0',
  w=>w.ProgressionPlan.Work++,
  w=>w.ProgressionPlan.Results[0].Paths[0].ConnectionIds[0]='unknown',
  w=>w.ProgressionPlan.Rules[0].GoalNodeIds=null]){
   const copy=structuredClone(world);change(copy);assert.throws(()=>Schematic.layout(copy),/progression witness/);
 }
});
test('optional progression leaves unselected metadata rendering intact',()=>{
 const copy=structuredClone(world);copy.ProgressionPlan=null;
 assert.equal(Schematic.layout(copy).boxes.length,9);
});
test('packet embeds exact saved metadata and current renderer',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
