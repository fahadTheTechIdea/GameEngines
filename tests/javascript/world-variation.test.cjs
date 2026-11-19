const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const {createHash}=require('node:crypto');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','changed'].map(name=>({name,folder:`generated/variation-${name}`,world:read(`generated/variation-${name}/world-metadata.json`)}));
test('independent SHA-256 ranking reproduces every root and nested choice',()=>{
 for(const {world} of packets)for(const document of [world,world.PlacementPlan.Definition.SourceWorld]){
  for(const a of document.VariationPlan.Allocations){
   const r=a.Request;
   for(const choice of a.Choices){
    const scores=r.Rule.ArchetypeIds.map(id=>({id,score:createHash('sha256').update(JSON.stringify(['world-variation-v1',r.Rule.Seed,r.Rule.Id,r.ParentNodeId,r.PartId,choice.NodeId,id])).digest('hex')})).sort((a,b)=>a.score.localeCompare(b.score)||a.id.localeCompare(b.id));
    assert.equal(choice.ArchetypeId,scores[0].id);assert.equal(choice.ScoreSha256,scores[0].score);
    const node=document.Nodes.find(n=>n.Id===choice.NodeId);assert.equal(node.ArchetypeId,choice.ArchetypeId);assert.equal(node.VariationKey,a.Key);
   }
  }
 }
});
test('all source choices, physical dimensions, nested components and protection geometry survive publication',()=>{
 for(const {world,folder} of packets){
  const source=world.PlacementPlan.Definition.SourceWorld;
  assert.equal(world.Nodes.length,20);assert.equal(source.Nodes.length,12);assert.equal(world.AccessPlan.Sites.length,11);
  assert.equal(world.VariationPlan.Allocations.length,10);
  assert.deepEqual(world.ContextPlan.Protections,source.ContextPlan.Protections);
  for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
  for(const a of source.VariationPlan.Allocations)assert.deepEqual(world.VariationPlan.Allocations.find(b=>b.Key===a.Key),a);
  for(const node of world.Nodes.filter(n=>n.Kind==='marker')){
   assert.equal(world.Nodes.filter(n=>n.ParentId===node.Id&&n.Kind==='component').length,1);
   const shape=world.FootprintDefinitions[node.ArchetypeId].Polygon.Vertices.map(p=>({X:p.X+node.Position[0],Y:p.Y+node.Position[1]}));
   assert.deepEqual(world.AccessPlan.Sites.find(s=>s.NodeId===node.Id).Footprint.Vertices,shape);
  }
  const view=Schematic.layout(world);assert.equal(view.boxes.length,20);
  assert.deepEqual(view.protections,world.ContextPlan.Protections);
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
 assert.equal(fs.existsSync('generated/variation-refused'),false);
});
test('part-scoped seed changes only intended forms',()=>{
 const [a,b]=packets.map(p=>p.world);
 assert.deepEqual(a.Nodes.filter(n=>!n.Id.startsWith('population/markers-')),b.Nodes.filter(n=>!n.Id.startsWith('population/markers-')));
 assert.notDeepEqual(a.Nodes.filter(n=>n.Id.startsWith('population/markers-')),b.Nodes.filter(n=>n.Id.startsWith('population/markers-')));
 for(const node of a.Nodes)assert.deepEqual(node.Position,b.Nodes.find(n=>n.Id===node.Id).Position);
});
test('renderer refuses omitted choices and inconsistent selected identity',()=>{
 const reject=change=>{const world=structuredClone(packets[0].world);change(world);assert.throws(()=>Schematic.layout(world),/Invalid catalog variation record/);};
 reject(w=>w.VariationPlan=null);
 reject(w=>w.VariationPlan.Allocations.pop());
 reject(w=>w.VariationPlan.Allocations[0].Choices[0].ArchetypeId='invented');
 reject(w=>w.VariationPlan.Allocations[0].Choices[0].ScoreSha256='invalid');
 reject(w=>w.Nodes.find(n=>n.VariationKey).VariationKey='missing');
});
