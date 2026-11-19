const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=process.env.WORKFLOW_PACKET||'generated/session-operations-sdk-workflow';
const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const name of ['wilderness','interior','compound','topology'])for(const stage of ['baseline','edited'])test(name+'/'+stage+': published inspector preserves complete world data',()=>{
 const folder=path.join(root,name,stage),js=path.join(folder,'schematic.js');
 assert.equal(hash(fs.readFileSync(js)),hash(source),'actual embedded inspector matches current source');
 const S=require(path.resolve(js)),world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'))),before=JSON.stringify(world),view=S.layout(world);
 assert.equal(JSON.stringify(world),before);
 assert.deepEqual(new Set(view.boxes.map(b=>b.node.Id)),new Set(world.Nodes.map(n=>n.Id)));
 for(const site of world.AccessPlan?.Sites||[]){
  const observations=view.suitability.get(site.NodeId)||[];
  assert.ok(observations.some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)),'every full physical polygon reaches the inspector');
 }
 if(name==='topology')assert.equal(world.Surface,null,'topology does not acquire invented terrain');
});
test('area-balanced allocations retain the explicit connected ownership policy',()=>{
 const folder=path.join(root,'compound','baseline'),S=require(path.resolve(folder,'schematic.js'));
 const world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
 world.LocalLayoutPlan.Allocations[0].Program.AllowDisconnected=true;
 assert.throws(()=>S.layout(world),/local layout/);
});
