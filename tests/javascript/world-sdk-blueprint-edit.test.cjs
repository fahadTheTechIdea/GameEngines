const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=process.env.WORLD_SDK_BLUEPRINT_ROOT;
if(!root)throw Error('WORLD_SDK_BLUEPRINT_ROOT is required');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const receipt=read('regional-source-workflow-results.json');
const base=read('baseline/world-regions.json'),edited=read('edit-0001/world-regions.json');
const area=p=>{const v=p.Vertices;let sum=0;for(let i=0;i<v.length;i++){const a=v[i],b=v[(i+1)%v.length];sum+=a.X*b.Y-b.X*a.Y;}return Math.abs(sum)/2;};
const close=(a,b)=>assert.ok(Math.abs(a-b)<1e-8,`${a} != ${b}`);
test('receipt pins blueprint inputs and replayed edit',()=>{
 assert.equal(receipt.InputKind,'regional-blueprint');
 assert.equal(receipt.InitialSourceSha256,hash('examples/metadata/regional-blueprint/blueprint.json'));
 assert.equal(receipt.Edits.length,1);
 assert.equal(receipt.Edits[0].SourceSha256,hash('examples/metadata/regional-blueprint/larger-modules.json'));
 assert.ok(receipt.BaselinePublishedWorldReplayed&&receipt.CheckpointReplayed&&receipt.Edits[0].CleanGenerationMatched&&receipt.Edits[0].PublishedWorldReplayed);
 assert.notEqual(receipt.InitialWorldSha256,receipt.FinalWorldSha256);
});
test('every regional program retains all members and adopts the shared size',()=>{
 for(const [world,size] of [[base,1.5],[edited,1.7]]){
  assert.equal(world.Regions.length,4);assert.equal(world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0),20);
  assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),12);
  for(const region of world.Regions){
   const nodes=new Map(region.World.Nodes.map(n=>[n.Id,n]));
   const modules=region.World.AccessPlan.Sites.filter(s=>nodes.get(s.NodeId)?.ArchetypeId==='module');
   assert.equal(modules.length,2,region.RegionId);for(const module of modules)close(area(module.Footprint),size);
  }
  assert.equal(world.Connections.length,3);for(const connection of world.Connections){
   assert.equal(connection.Forward.Status,'Found');assert.equal(connection.Reverse.Status,'Found');
   assert.deepEqual(connection.Forward.Path.Points[0],connection.From);assert.deepEqual(connection.Forward.Path.Points.at(-1),connection.To);
   assert.deepEqual(connection.Reverse.Path.Points[0],connection.To);assert.deepEqual(connection.Reverse.Path.Points.at(-1),connection.From);
  }
 }
});
