const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_MIXED_ROUTE_ROOT||'generated/mixed-route-regions-final';
const packet=process.env.WORLD_MIXED_ROUTE_PACKET||'cli';
const read=mode=>JSON.parse(fs.readFileSync(`${root}/${mode}/${packet}/world-metadata.json`,'utf8'));
for(const mode of ['guided','free','water'])test(`${mode}: complete physical programs and mixed-layout interface witnesses reach the current viewer`,()=>{
 const world=read(mode),before=JSON.stringify(world),view=Schematic.layout(world);
 assert.equal(world.Nodes.length,22);assert.equal(world.AccessPlan.Sites.length,21);assert.equal(view.boxes.length,22);
 assert.equal(view.interfaceRoutes.length,3);assert.equal(view.routes.length,6);
 assert.equal(Schematic.routeGuidance(world).length,mode==='free'?0:1);
 assert.equal(world.PlacementPlan.ProgramInstances.length,7);
 for(const saved of world.RoutePlan.Routes){
  const rendered=view.interfaceRoutes.find(r=>r.connectionId===saved.ConnectionId);
  assert.deepEqual(rendered.Forward,saved.Interface.Forward);assert.deepEqual(rendered.Attempts,saved.Interface.Attempts);
  for(const route of [saved.Forward,saved.Reverse]){
   const path=view.routes.find(r=>r.id===route.Id);
   assert.deepEqual(path.points,route.Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(path.guidanceCost||0,route.Cost.Guidance||0);
  }
 }
 if(mode==='water'){
  const regional=world.RoutePlan.Routes.find(r=>r.RuleId==='regional-route');
  assert.equal(regional.Interface.Attempts.length,2);assert.equal(regional.Interface.Attempts[0].Accepted,false);
  assert.deepEqual(regional.Interface.Center,{X:50,Y:7.75});
 }
 assert.equal(JSON.stringify(world),before);
 assert.deepEqual(fs.readFileSync(`${root}/${mode}/${packet}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${mode}/${packet}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
test('broken mixed-region crossing evidence is rejected before display',()=>{
 for(const mutate of [w=>w.RoutePlan.Routes[1].Interface.Center.Y+=1,
  w=>w.RoutePlan.Routes[1].Interface.Forward.Crossing.Path.Points[0].X+=1,
  w=>w.RoutePlan.Routes[1].Interface.Forward.FromLeg.Cost.Guidance=10,
  w=>w.RoutePlan.Routes[1].Interface.Attempts=[],
  w=>w.RoutePlan.Routes[1].Interface.ToDomainId='unknown']){
  const world=read('water');mutate(world);assert.throws(()=>Schematic.layout(world),/physical interface/);
 }
});
