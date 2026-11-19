const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.PROGRESSION_AUDIT_ROOT||'generated/progression-audit-public';
const native=process.env.PROGRESSION_AUDIT_NATIVE||'generated/progression-audit-native';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const base=read(path.join(root,'baseline/world-metadata.json')),matrix=read(path.join(native,'matrix.json'));
test('every accepted stateful milestone variant retains complete nodes and its exact saved progression',()=>{
 let accepted=0;
 for(const candidate of matrix.Cases.filter(c=>c.Accepted)){
  const world=structuredClone(base);world.Connections=candidate.Connections;world.ProgressionPlan=candidate.Plan;
  const before=JSON.stringify(world),view=S.layout(world);
  assert.equal(view.boxes.length,6);assert.deepEqual(view.world.ProgressionPlan,candidate.Plan);assert.equal(JSON.stringify(world),before);accepted++;
 }
 assert.equal(accepted,24);assert.equal(matrix.Cases.filter(c=>!c.Accepted).length,296);
});
test('the sixteen-capability packet preserves every grant, gate and acquired capability',()=>{
 const world=read(path.join(root,'upper/world-metadata.json')),view=S.layout(world),rule=world.ProgressionPlan.Rules[0];
 assert.equal(view.boxes.length,6);assert.equal(rule.Capabilities.CapabilityIds.length,16);
 assert.deepEqual(world.ProgressionPlan.Results[0].Paths[0].CapabilityStates.at(-1),rule.Capabilities.CapabilityIds);
 assert.equal(world.Surface,null);assert.ok(world.Nodes.every(n=>n.Position===null));
 const changed=structuredClone(world);changed.ProgressionPlan.Results[0].Paths[0].CapabilityStates.at(-1).pop();
 assert.throws(()=>S.layout(changed),/progression witness/);
});
test('upper-bound publication embeds the exact packet and renderer',()=>{
 const folder=path.join(root,'upper'),html=fs.readFileSync(path.join(folder,'schematic.html'),'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),read(path.join(folder,'world-metadata.json')));
 assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
