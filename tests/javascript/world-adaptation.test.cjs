const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ADAPTATION_PACKET||'generated/local-repartition',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('repartition preserves engine geometry and exposes rejected baseline',()=>{
 const view=Schematic.layout(world);assert.equal(view.boxes.length,7);assert.deepEqual(view.layoutAdaptation,world.LayoutAdaptationPlan);
 assert.equal(view.layoutAdaptation.Attempts.length,2);assert.equal(view.layoutAdaptation.Attempts[0].Accepted,false);
 assert.equal(view.localLayouts.Allocations.find(a=>a.Id==='west-a').Shape.Area,440);
 assert.equal(view.interfaceRoutes[0].Center.X,22);
});
test('inconsistent source, settings and acceptance history refuse display',()=>{
 for(const mutate of [w=>w.LayoutAdaptationPlan.Attempts=[],w=>w.LayoutAdaptationPlan.Attempts[0].Accepted=true,
  w=>w.LayoutAdaptationPlan.Attempts[1].Steps.expand=0,w=>w.LayoutAdaptationPlan.Attempts[1].LayoutSha256='invalid',
  w=>w.LayoutAdaptationPlan.OriginalLayouts.Layouts[0].Programs[0].Anchor.X+=1,
  w=>w.LayoutAdaptationPlan.Policy.LayoutId='absent',w=>w.LayoutAdaptationPlan.Work=-1,w=>w.LayoutAdaptationPlan.OriginalPlacement=null]){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/local repartition/);
 }
});
test('ordinary allocation packet remains readable without adaptation',()=>{
 assert.equal(Schematic.layout(JSON.parse(fs.readFileSync('generated/local-layouts/world-metadata.json'))).layoutAdaptation,null);
});
test('packet contains exact current renderer and metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
