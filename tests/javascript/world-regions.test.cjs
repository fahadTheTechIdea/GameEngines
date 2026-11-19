const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/terrain-regions',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('renderer retains engine region geometry and exact content',()=>{
 const layout=Schematic.layout(world);
 assert.deepEqual(layout.generatedRegions,world.RegionPlan);
 assert.deepEqual(layout.generatedRegions.Regions.map(r=>r.Id),['east','west']);
 assert.equal(layout.boxes.length,3);
 const plan=layout.generatedRegions;
 assert.ok(Math.abs(plan.Regions.reduce((n,r)=>n+r.Area,0)+plan.UnclaimedSuitableArea+plan.IneligibleArea-plan.BoundaryArea)<1e-7);
});
test('structural or accounting corruption refuses display',()=>{
 for(const mutate of [w=>w.RegionPlan=null,w=>w.RegionPlan.SuitableArea+=1,w=>w.RegionPlan.Regions[0].Area+=1,
  w=>w.RegionPlan.Regions.pop(),w=>w.RegionPlan.Regions[0].Cells=[],w=>w.RegionPlan.Regions[0].BoundaryEdges=[],
  w=>w.RegionPlan.Regions[0].Cells[0].Vertices[0].X=null,w=>w.RegionPlan.Regions[0].Seed.MinimumArea=1,
  w=>w.RegionPlan.Request.Seeds[0].MinimumArea=100000,w=>w.PlacementPlan.Placement.Request.Demands[0].RegionId='absent']){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/generated region/);
 }
});
test('current renderer loads existing worlds with no region declaration',()=>{
 for(const file of ['generated/corridor-capacity/world-metadata.json','generated/terrain-regions-placement-regression/world-metadata.json','generated/resource-area/world-metadata.json'])
  assert.equal(Schematic.layout(JSON.parse(fs.readFileSync(file))).generatedRegions,null);
});
test('published packet uses current renderer and exact metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
