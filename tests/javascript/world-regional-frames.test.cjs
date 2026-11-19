const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const cases=['grid','translated-grid','weighted'];
for(const name of cases){
 const folder=`generated/regional-content/${name}`,world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${name}: complete relative program follows engine-selected terrain`,()=>{
  const view=Schematic.layout(world);assert.equal(view.boxes.length,7);assert.equal(view.interfaceRoutes.length,1);
  assert.deepEqual(view.localLayouts,world.LocalLayoutPlan);
  const seed=world.RegionPlan.Regions[0].Seed.Position;
  for(const source of world.LocalLayoutPlan.Definition.Layouts[0].Programs){
   const actual=world.LocalLayoutPlan.Allocations.find(a=>a.Id===source.Id).Program;
   assert.equal(actual.Anchor.X,seed.X+source.Anchor.X);assert.equal(actual.Anchor.Y,seed.Y+source.Anchor.Y);
  }
 });
 test(`${name}: changed frame, offset or resolved anchor refuses display`,()=>{
  for(const mutate of [
   w=>w.LocalLayoutPlan.Definition.Layouts[0].RegionFrame=null,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].RegionFrame.Purpose='',
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Programs[0].Anchor.X+=1,
   w=>w.LocalLayoutPlan.Allocations[0].Program.Anchor.X+=1,
   w=>w.PlacementPlan.Definition.Request.Anchors[0].Position.Y+=1,
   w=>w.PlacementPlan.TemplateDefinition.AllocationAnchors[0].AllocationId='missing',
   w=>w.PlacementPlan.TemplateDefinition.AllocationAnchors[0].Purpose='',
   w=>w.PlacementPlan.TemplateDefinition.AllocationAnchors.push({...w.PlacementPlan.TemplateDefinition.AllocationAnchors[0]}),
   w=>w.PlacementPlan.Definition.AllocationAnchors=[...w.PlacementPlan.TemplateDefinition.AllocationAnchors]
  ]){
   const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local layout|local repartition|prescribed placement/);
  }
 });
 test(`${name}: packet contains exact current script and metadata`,()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 });
}
