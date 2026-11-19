const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_PROGRAM_SPACES_ROOT||'generated/program-spaces-current';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`));
function near(a,b){assert.ok(Math.abs(a-b)<1e-6*Math.max(1,Math.abs(a),Math.abs(b)),`${a} / ${b}`);}
function integral(cells,weighted){
 let area=0,mass=0,mx=0,my=0;
 for(const cell of cells){const a=cell.Vertices[0];for(let i=1;i+1<cell.Vertices.length;i++){
  const b=cell.Vertices[i],c=cell.Vertices[i+1],ar=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;area+=ar;
  for(const [u,v,w] of [[2/3,1/6,1/6],[1/6,2/3,1/6],[1/6,1/6,2/3]]){
   const x=u*a.X+v*b.X+w*c.X,y=u*a.Y+v*b.Y+w*c.Y,d=weighted?1+.01*x:1;
   mass+=ar*d/3;mx+=ar*x*d/3;my+=ar*y*d/3;
  }
 }}return {area,mass,x:mx/mass,y:my/mass};
}
for(const name of ['uniform','weighted','alternative','unequal']){
 const world=read(name);
 test(`${name}: intents retain exact spaces, centroid anchors and complete source content`,()=>{
  const view=Schematic.layout(world),plan=world.LocalLayoutPlan,source=plan.Definition.Layouts[0],trace=plan.GeneratedAreas[0].AreaPlan;
  assert.equal(view.boxes.length,7);assert.equal(view.interfaceRoutes.length,1);assert.equal(world.PlacementPlan.Placement.Sites.length,2);
  assert.equal(source.Strategy,'AreaBalanced');assert.deepEqual(source.Programs,[]);assert.equal(source.RegionFrame,null);assert.equal(source.ProgramIntents.length,2);
  assert.equal(plan.GeneratedAreas.length,1);assert.equal(plan.GeneratedPartitions,null);near(trace.Area,800);near(plan.Areas[0].ResidualArea,0);
  assert.deepEqual(trace.RelativeWeights,source.ProgramIntents.map(p=>p.MinimumArea));const totalWeight=trace.RelativeWeights.reduce((a,b)=>a+b,0);
  trace.Strata.forEach((s,i)=>{
   const facts=integral(s.Cells,name==='weighted'),intent=source.ProgramIntents[i],allocation=plan.Allocations.find(a=>a.Id===intent.Id);
   near(facts.area,s.Area);near(facts.mass,s.Mass);near(facts.x,s.Position.X);near(facts.y,s.Position.Y);
   near(s.Mass,(name==='weighted'?960:800)*trace.RelativeWeights[i]/totalWeight);assert.deepEqual(allocation.Program.Anchor,s.Position);assert.deepEqual(allocation.Shape.Cells,s.Cells);
   assert.equal(allocation.Program.MinimumArea,intent.MinimumArea);
  });
  if(name==='uniform'||name==='alternative'){near(trace.Strata[0].Position.X,10);near(trace.Strata[1].Position.X,30);}
  if(name==='unequal'){near(trace.Strata[0].Area,800*3/7);near(trace.Strata[1].Area,800*4/7);}
  if(name==='alternative')assert.deepEqual(world.LayoutAdaptationPlan.Attempts.map(a=>a.Accepted),[false,true]);
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: missing provenance, authored coordinates and altered mass/intent refuse display`,()=>{
  for(const mutate of [
   w=>w.LocalLayoutPlan.GeneratedAreas=[],
   w=>w.LocalLayoutPlan.GeneratedAreas[0].LayoutId='absent',
   w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Mass+=100,
   w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.RelativeWeights[0]+=1,
   w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Position.X+=1,
   w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Cells[0].Vertices[0].X+=1,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].ProgramIntents[0].MinimumArea+=1,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Programs.push(structuredClone(w.LocalLayoutPlan.Allocations[0].Program)),
   w=>w.LocalLayoutPlan.Definition.Layouts[0].AreaPattern.DensityFieldId='missing'
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local layout|local repartition/);}
 });
}
test('density counterfactual changes generated physical positions without changing content identities',()=>{
 const a=read('uniform'),b=read('weighted');
 assert.deepEqual(a.Nodes.map(({Position,...node})=>node),b.Nodes.map(({Position,...node})=>node));
 assert.notDeepEqual(a.Nodes.map(n=>n.Position),b.Nodes.map(n=>n.Position));
 assert.deepEqual(a.Surface,b.Surface);assert.deepEqual(a.RegionPlan,b.RegionPlan);
});
