const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_RETAINED_WATER_PACKET||'generated/retained-water-cli';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}`));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-8,`${a}/${b}`);
test('actual CLI preserves source bed and exact retained water receipts',()=>{
 const w=read('world-metadata.json'),p=read('water-plan.json'),d=read('drainage-plan.json'),summary=read('summary.json');
 assert.deepEqual(w.WaterPlan,p);assert.deepEqual(w.DrainagePlan,d);assert.deepEqual(w.Surface.HeightField,d.Surface.HeightField);
 assert.equal(d.Surface.HeightField.Heights[12],0);assert.equal(p.Segments.length,0);assert.equal(p.RetainedBodies.length,1);
 near(p.RetainedBodies[0].Region.Area,2);near(p.RetainedBodies[0].Capacity,2/3);
 assert.equal(summary.RetainedWaterBodies,1);near(summary.RetainedWaterArea,2);near(summary.RetainedWaterCapacity,2/3);
 assert.deepEqual(w.WindMoisturePlan.Derivation.Surface,w.Surface);assert.equal(w.Nodes.length,3);
});
test('renderer consumes every actual lake face with level witnesses and no alternate geometry',()=>{
 const w=read('world-metadata.json'),before=JSON.stringify(w),layout=S.layout(w),body=w.WaterPlan.RetainedBodies[0];
 assert.equal(layout.water.bodies.length,1);assert.equal(layout.water.byPatch.size,body.PatchIds.length);assert.equal(layout.boxes.length,3);
 for(const patch of layout.terrain.filter(p=>p.water)){
  assert.equal(patch.water.Definition.Level,1);assert.equal(patch.kind,'water');
  assert.deepEqual(patch.points,w.WaterPlan.Patches.find(p=>p.Id===patch.id).Boundary.Vertices.map(p=>({x:p.X,y:p.Y})));
 }
 assert.equal(JSON.stringify(w),before);assert.deepEqual(fs.readFileSync(`${root}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('bounded saved-witness checks reject lake orphan, level, capacity, depth, source and policy mismatches',()=>{
 const base=read('world-metadata.json');
 const edits=[w=>w.WaterPlan.RetainedBodies=null,w=>w.WaterPlan.RetainedBodies[0].Capacity++,
  w=>w.WaterPlan.RetainedBodies[0].Definition.Level++,w=>w.WaterPlan.RetainedBodies[0].Received++,
  w=>w.WaterPlan.RetainedBodies[0].PatchIds.pop(),w=>w.WaterPlan.RetainedBodies[0].BedFaces[0].Elevation++,
  w=>w.WaterPlan.RetainedBodies[0].BedFaces[0].MinimumDepth=-1,w=>w.WaterPlan.RetainedBodies[0].BoundaryClipped=true,
  w=>w.WaterPlan.RetainedBodies[0].Region.Cells.pop(),w=>w.WaterPlan.Patches[0].Elevation++,
  w=>w.DrainagePlan.Definition.Terminals[0].Kind='Outlet',w=>w.WaterPlan.RetainedBodies[0].PatchIds[0]=w.WaterPlan.RetainedBodies[0].PatchIds[1]];
 for(const edit of edits){const w=structuredClone(base);edit(w);assert.throws(()=>S.layout(w));}
});
test('dry surroundings and land-only passage retain actual engine results',()=>{
 const w=read('world-metadata.json');near(w.RegionPlan.SuitableArea,14);assert.equal(w.RoutePlan.Routes.length,1);
 assert.equal(w.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(w.RoutePlan.Routes[0].Reverse.Status,'Found');
 assert.ok(w.RoutePlan.Routes[0].Forward.Path.Points.length>2);assert.ok(w.EnvironmentPlan.Observations.every(x=>x.Result.Eligible));
});
