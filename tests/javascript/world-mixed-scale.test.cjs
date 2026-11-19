const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.MIXED_SCALE_PACKET||'generated/mixed-purpose-scale';
for(const name of ['inland','inlet','opposite-slope']){
 test(`${name}: complete mixed programs and engine routes survive CLI packaging`,()=>{
  const world=JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
  const view=Schematic.layout(world),plan=world.RoutePlan;
  assert.equal(world.Nodes.length,57);assert.equal(world.AccessPlan.Sites.length,56);
  assert.equal(world.PlacementPlan.ProgramInstances.length,28);
  assert.equal(plan.Routes.length,37);assert.equal(view.routes.length,74);
  assert.ok(plan.Work>0&&plan.Work<=plan.Budget.MaximumWork);
  assert.equal(plan.Budget.MaximumWork,200000000);
  for(const site of world.AccessPlan.Sites)
   assert.deepEqual(view.boxes.find(b=>b.id===site.NodeId).physicalAccess,site);
  for(const route of plan.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.ok(route[direction].SearchModel.endsWith('/first-feasible'));
   assert.equal(rendered.width,.3);
  }
  const summary=JSON.parse(fs.readFileSync(`${root}/${name}/summary.json`,'utf8'));
  assert.equal(summary.ConsumedRouteWork,plan.Work);
  assert.equal(summary.InstancedNestedPrograms,28);
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 });
}
