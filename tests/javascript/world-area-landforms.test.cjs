const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=(root,name,file='world-metadata.json')=>JSON.parse(fs.readFileSync(`${root}/${name}/${file}`));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6*Math.max(1,Math.abs(a),Math.abs(b)),`${a} / ${b}`);
for(const name of ['negative','positive','weighted']){
 test(`${name}: area rotation preserves complete intent and signed failure history in exported schematic`,()=>{
  const root='generated/area-adaptation-checked',w=read(root,name),r=read(root,name,'recipe.json'),trace=w.LayoutAdaptationPlan;
  assert.equal(w.Nodes.length,7);assert.equal(w.AccessPlan.Sites.length,2);assert.equal(w.RoutePlan.Routes.length,1);
  assert.equal(Schematic.layout(w).boxes.length,7);
  assert.deepEqual(trace.Attempts.map(a=>a.Accepted),[false,true]);
  assert.deepEqual(trace.Attempts.map(a=>a.Steps.orientation),name==='positive'?[0,1]:[0,-1]);
  const original=r.LocalLayouts.Layouts[0],selected=w.LocalLayoutPlan.Definition.Layouts[0];
  assert.equal(selected.AreaPattern.AxisRotationDegrees,0);
  assert.deepEqual({...selected,AreaPattern:original.AreaPattern},original);
  assert.deepEqual(trace.OriginalLayouts,r.LocalLayouts);
  for(const a of w.LocalLayoutPlan.Allocations)assert.ok(a.Shape.Area>=a.Program.MinimumArea);
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
}
for(const name of ['flat','ridge','valley']){
 test(`${name}: full generated-heightfield world retains all content and unchanged grade limits`,()=>{
  const root='generated/layered-landforms-checked',w=read(root,name),recipe=read(root,name,'recipe.json'),f=w.Surface.HeightField,t=w.Surface.Terrain;
  assert.equal(w.Nodes.length,61);assert.equal(w.AccessPlan.Sites.length,60);assert.equal(Schematic.layout(w).boxes.length,61);
  assert.equal(w.Surface.Patches.length,0);assert.equal(f.Heights.length,143);
  assert.equal(w.RoutePlan.Routes.length,35);assert.equal(w.NetworkPlan.Networks[0].Plan.Edges.length,6);
  assert.equal(recipe.RouteRules[0].Profile.MaximumSlope,.1);assert.equal(recipe.Regions.Intents[0].MinimumArea,25000);
  assert.equal(recipe.TerrainDemands.MaximumWork,5000000);
  for(let y=0;y<f.Rows;y++)for(let x=0;x<f.Columns;x++){
   const px=15*x,py=15*y,d=Math.hypot(px-60,Math.max(30-py,0,py-60)),u=Math.max(0,1-d/20);
   near(f.Heights[y*f.Columns+x],.01*px+.005*py+t.Operations[0].Height*u*u*(3-2*u));
   if(x<f.Columns-1&&y<f.Rows-1){const a=y*f.Columns+x,b=a+1,c=a+f.Columns,d=c+1;
    assert.ok(Math.hypot((f.Heights[b]-f.Heights[a])/15,(f.Heights[d]-f.Heights[b])/15)<=.1);
    assert.ok(Math.hypot((f.Heights[d]-f.Heights[c])/15,(f.Heights[c]-f.Heights[a])/15)<=.1);
   }
  }
  const source=w.PlacementPlan.Definition.SourceWorld,nodes=new Map(w.Nodes.map(n=>[n.Id,n]));
  for(const n of source.Nodes)assert.deepEqual(nodes.get(n.Id),n);
  assert.deepEqual(source.RoutePlan.Routes,w.RoutePlan.Routes);assert.deepEqual(source.NetworkPlan.Networks,w.NetworkPlan.Networks);
  const landscape=w.PlacementPlan.PopulationAreas[0];let mass=0;
  for(const s of landscape.Plan.Strata)for(const {Vertices:v} of s.Cells){const a=v[0];for(let i=1;i+1<v.length;i++){
   const b=v[i],c=v[i+1],area=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;
   mass+=area*(1+.002*(a.X+b.X+c.X)/3);
  }}near(mass,landscape.Plan.Mass);assert.equal(Math.floor(mass/250),18);assert.equal(w.ChunkPlan.Members.length,143);
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
}
test('landform counterfactual retains exact game content while protected terrain changes',()=>{
 const worlds=['flat','ridge','valley'].map(n=>read('generated/layered-landforms-checked',n));
 for(const w of worlds.slice(1)){
  assert.deepEqual(w.Nodes.map(({Position,...n})=>n),worlds[0].Nodes.map(({Position,...n})=>n));
  assert.notDeepEqual(w.Surface.HeightField.Heights,worlds[0].Surface.HeightField.Heights);
 }
});

test('area orientation viewer rejects altered targets, bounds, settings and failure history',()=>{
 const source=read('generated/area-adaptation-checked','negative');
 const reject=(edit,error=/Invalid local repartition plan/)=>{const w=structuredClone(source);edit(w);assert.throws(()=>Schematic.layout(w),error);};
 reject(w=>w.LayoutAdaptationPlan.Policy.Dials[0].ProgramId='unexpected');
 reject(w=>w.LayoutAdaptationPlan.Policy.Dials[0].CutId='unexpected');
 reject(w=>w.LayoutAdaptationPlan.Policy.Dials[0].MaximumSteps=2);
 reject(w=>w.LayoutAdaptationPlan.Attempts[0].Accepted=true);
 reject(w=>w.LocalLayoutPlan.Definition.Layouts[0].AreaPattern.AxisRotationDegrees=1,/Invalid local layout plan/);
});
