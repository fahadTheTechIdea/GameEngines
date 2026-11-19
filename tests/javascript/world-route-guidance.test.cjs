const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_GUIDANCE_ROOT||'generated/route-guidance-complete';
const read=mode=>JSON.parse(fs.readFileSync(`${root}/cli-${mode}/world-metadata.json`,'utf8'));
for(const mode of ['free','aligned','rotated','local'])test(`${mode}: exact engine route and declared preference survive current schematic consumption`,()=>{
 const world=read(mode),before=JSON.stringify(world),plan=Schematic.layout(world),guidance=Schematic.routeGuidance(world);
 assert.equal(plan.boxes.length,3);assert.equal(guidance.length,mode==='free'?0:1);
 assert.deepEqual(world.Nodes.filter(n=>n.Kind==='destination').map(n=>n.Position),[[4,4],[20,12]]);
 const saved=world.RoutePlan.Routes[0];
 for(const result of [saved.Forward,saved.Reverse]){
  const path=plan.routes.find(r=>r.id===result.Id);assert.ok(path);
  assert.deepEqual(path.points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
  assert.equal(path.guidanceCost||0,result.Cost.Guidance||0);
 }
 assert.equal(JSON.stringify(world),before);
 assert.deepEqual(fs.readFileSync(`${root}/cli-${mode}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('malformed or unexplained guidance fails before replacing a view',()=>{
 for(const mutate of [
  w=>w.RoutePlan.Rules[0].Profile.Guidance=[],
  w=>w.RoutePlan.Rules[0].Profile.Guidance[0].CostPerMeter=0,
  w=>w.RoutePlan.Rules[0].Profile.Guidance[0].Axes=[{X:0,Y:0}],
  w=>w.RoutePlan.Rules[0].Profile.Guidance[0].Axes=[{X:1,Y:0},{X:-1,Y:0}],
  w=>w.RoutePlan.Rules[0].Profile.Guidance[0].Boundary.Vertices[0].X=Infinity,
  w=>w.RoutePlan.Routes[0].Forward.Cost.Guidance=-1,
  w=>{delete w.RoutePlan.Rules[0].Profile.Guidance;w.RoutePlan.Routes[0].Forward.Cost.Guidance=1;}
 ]){const bad=read('aligned');mutate(bad);assert.throws(()=>Schematic.layout(bad),/guidance/i);}
});
