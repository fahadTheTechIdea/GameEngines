const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_INTERFACE_STEP;if(!root)throw new Error('WORLD_INTERFACE_STEP must identify the generated fixture');
const read=()=>JSON.parse(fs.readFileSync(path.join(root,'world/world-metadata.json'),'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-7,`${a} != ${b}`);
test('composed paths preserve analytic directional climb at a five millimeter source step',()=>{
  const world=read(),route=world.RoutePlan.Routes[0],view=Schematic.layout(world);
  near(route.Forward.Cost.Distance,14);near(route.Forward.Cost.Ascent,2.81);near(route.Reverse.Cost.Descent,.7025);
  near(route.Interface.Forward.JoinCost.Ascent,.01);near(route.Interface.Forward.JoinCost.Descent,0);
  near(route.Interface.Reverse.JoinCost.Descent,.0025);near(route.Interface.Reverse.JoinCost.Ascent,0);
  for(const result of [route.Forward,route.Reverse])assert.deepEqual(view.routes.find(r=>r.id===result.Id).points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
});
test('missing, forged or malformed composed join costs are refused by schematic consumption',()=>{
  for(const mutate of [j=>delete j.JoinCost,j=>j.JoinCost.Ascent=0,j=>j.JoinCost.Descent=-1,j=>j.JoinCost.extra=1,j=>j.JoinCost.Ascent='0.01']){
    const world=read();mutate(world.RoutePlan.Routes[0].Interface.Forward);assert.throws(()=>Schematic.layout(world),/physical interface/);
  }
});
