const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.REGION_COMPOSITION_PACKET||'generated/terrain-region-composition';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
for(const name of ['baseline','raised-terrain'])test(`${name}: exact regional program and engine geometry survive export`,()=>{
 const world=read(name),view=Schematic.layout(world),plan=world.RegionPlan;
 assert.equal(world.Nodes.length,29);assert.equal(world.AccessPlan.Sites.length,28);
 assert.equal(world.PlacementPlan.ProgramInstances.length,14);assert.equal(world.RoutePlan.Routes.length,19);
 assert.deepEqual(view.generatedRegions,plan);assert.equal(plan.BoundaryArea,11225);
 assert.equal(plan.SuitableArea,11081);assert.equal(plan.IneligibleArea,144);
 assert.equal(plan.Regions.find(r=>r.Id==='woodland').Area,612);
 assert.equal(plan.Regions.find(r=>r.Id==='lowland').Seed.Profile??null,null);
 assert.equal(plan.Regions.find(r=>r.Id==='highland').Seed.Profile.Id,'highland');
 assert.equal(plan.Regions.find(r=>r.Id==='woodland').Seed.Profile.AllowedKinds[0],'forest');
 for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
  assert.deepEqual(view.routes.find(r=>r.id===route[direction].Id).points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
 }
 const html=fs.readFileSync(`${root}/${name}/schematic.html`,'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
 assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('terrain counterfactual changes regions and placements while preserving all content and topology',()=>{
 const a=read('baseline'),b=read('raised-terrain');
 const content=world=>world.Nodes.map(({Position,...node})=>node);
 assert.deepEqual(content(a),content(b));assert.deepEqual(a.Connections,b.Connections);
 assert.notDeepEqual(a.RegionPlan.Regions,b.RegionPlan.Regions);
 assert.notDeepEqual(a.PlacementPlan.Placement.Sites,b.PlacementPlan.Placement.Sites);
 assert.equal(a.RegionPlan.Request.Profile.Id,b.RegionPlan.Request.Profile.Id);
});
for(const name of ['cross-profile-interface','walled-cross-profile-interface'])test(`${name}: a real crossing joins distinct profiles`,()=>{
 const world=read(name),view=Schematic.layout(world),route=world.RoutePlan.Routes[0];
 assert.equal(world.RegionPlan.Regions.length,2);assert.equal(world.RegionPlan.SuitableArea,800);
 assert.equal(route.Interface.Center.X,20);assert.deepEqual(view.generatedRegions,world.RegionPlan);
 assert.equal(view.interfaceRoutes.length,1);
 if(name.startsWith('walled')){assert.equal(world.InteriorPlan.Results[0].Walls.length,10);assert.deepEqual(view.interiorAssemblies,world.InteriorPlan.Results);}
});
test('consumer refuses stale profile witnesses and invalid area accounting',()=>{
 const stale=read('baseline');stale.RegionPlan.Request.Seeds.find(s=>s.Id==='highland').Profile.Id='stale-profile';
 assert.throws(()=>Schematic.layout(stale),/Invalid generated region plan/);
 const area=read('baseline');area.RegionPlan.SuitableArea+=1;
 assert.throws(()=>Schematic.layout(area),/Invalid generated region plan/);
});
