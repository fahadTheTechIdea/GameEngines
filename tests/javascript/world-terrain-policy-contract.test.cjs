const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.TERRAIN_POLICY_ROOT||'generated/terrain-contract-public';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
test('each terrain counterfactual retains complete source geometry and required journeys in the saved-data view',()=>{
 const positions=new Set();
 for(const name of ['open','water','slope','both']){
  const world=read(path.join(root,name,'world-metadata.json')),before=JSON.stringify(world),view=S.layout(world);
  assert.equal(view.boxes.length,2);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,1);
  const box=view.boxes.find(b=>b.physicalAccess);assert.deepEqual(box.physicalAccess,world.AccessPlan.Sites[0]);
  assert.deepEqual(view.world.RegionPlan,world.RegionPlan);assert.deepEqual(view.world.EnvironmentPlan,world.EnvironmentPlan);
  assert.equal(world.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(world.RoutePlan.Routes[0].Reverse.Status,'Found');
  positions.add(JSON.stringify(box.node.Position));assert.equal(JSON.stringify(world),before);
 }
 assert.equal(positions.size,4);
});
test('every publication embeds the exact source data and current renderer',()=>{
 for(const name of ['open','water','slope','both']){
  const folder=path.join(root,name),html=fs.readFileSync(path.join(folder,'schematic.html'),'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),read(path.join(folder,'world-metadata.json')));
  assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
test('ineligible and unavailable complete-world requests retain no accepted publication',()=>{
 assert.equal(fs.existsSync(path.join(root,'flooded')),false);assert.equal(fs.existsSync(path.join(root,'uncovered')),false);
});
