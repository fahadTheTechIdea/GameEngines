const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.NESTED_PROGRAM_PACKET||'generated/nested-programs-qualification';
const cases=[['inland',5,1],['inlet',5,1],['seven-bases',7,1],['double-storage',5,2]];
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));

for(const [name,bases,caches] of cases){
 test(`${name}: two source programs retain every instantiated parent and child`,()=>{
  const world=read(name),plan=world.PlacementPlan,view=Schematic.layout(world);
  assert.equal(world.Nodes.length,1+bases*(3+caches));
  assert.equal(plan.TemplateDefinition.Programs.length,2);
  assert.equal(plan.TemplateDefinition.NestedStages,null);
  assert.equal(plan.ProgramInstances.length,2*bases);
  assert.equal(plan.Definition.NestedStages.length,2*bases);
  assert.equal(view.boxes.filter(b=>b.physicalAccess).length,bases*(3+caches));
  const nodes=new Map(world.Nodes.map(n=>[n.Id,n]));
  for(const instance of plan.ProgramInstances){
   assert.match(instance.StageId,/^program-[a-f0-9]{24}$/);
   const source=plan.TemplateDefinition.Programs.find(p=>p.Id===instance.ProgramId);
   assert.equal(nodes.get(instance.ParentNodeId).ArchetypeId,source.ParentArchetypeId);
   const stage=plan.Definition.NestedStages.find(s=>s.Id===instance.StageId);
   assert.equal(stage.ParentNodeId,instance.ParentNodeId);
   assert.deepEqual(stage.Bindings.map(b=>b.NodeId).sort(),instance.ChildNodeIds);
   for(const id of instance.ChildNodeIds)assert.equal(nodes.get(id).ParentId,instance.ParentNodeId);
  }
  for(const site of world.AccessPlan.Sites)assert.deepEqual(view.boxes.find(b=>b.id===site.NodeId).physicalAccess,site);
 });
 test(`${name}: complete routes and source provenance survive the command-line packet`,()=>{
  const world=read(name),view=Schematic.layout(world);
  assert.equal(world.RoutePlan.Routes.length,3*bases);assert.equal(view.routes.length,6*bases);
  for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.equal(rendered.width,.3);
  }
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const summary=JSON.parse(fs.readFileSync(`${root}/${name}/summary.json`,'utf8'));
  assert.equal(summary.InstancedNestedPrograms,2*bases);
 });
}
test('parent-count growth preserves instance identity; repeated child count preserves source sizes',()=>{
 const small=read('inland'),large=read('seven-bases'),stock=read('double-storage');
 const expanded=new Map(large.PlacementPlan.ProgramInstances.map(i=>[i.ParentNodeId,i.StageId]));
 for(const instance of small.PlacementPlan.ProgramInstances)assert.equal(expanded.get(instance.ParentNodeId),instance.StageId);
 const area=polygon=>Math.abs(polygon.Vertices.reduce((sum,a,i)=>{const b=polygon.Vertices[(i+1)%polygon.Vertices.length];return sum+a.X*b.Y-b.X*a.Y;},0))/2;
 const cacheNodes=new Set(stock.Nodes.filter(n=>n.ArchetypeId==='cache').map(n=>n.Id));
 assert.equal(cacheNodes.size,10);
 for(const site of stock.AccessPlan.Sites.filter(s=>cacheNodes.has(s.NodeId)))assert.ok(Math.abs(area(site.Footprint)-1)<1e-8);
});
