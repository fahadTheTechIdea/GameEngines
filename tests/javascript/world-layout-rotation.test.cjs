const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ROTATION_PACKET||'generated/local-orientation',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('rotated allocation retains complete contents and engine geometry',()=>{
 const view=Schematic.layout(world);
 assert.equal(view.boxes.length,7);assert.deepEqual(view.layoutAdaptation,world.LayoutAdaptationPlan);
 assert.equal(view.layoutAdaptation.Attempts.length,2);
 assert.equal(view.layoutAdaptation.Attempts[1].Steps.orientation,-1);
 assert.equal(view.localLayouts.Definition.Layouts[0].Grid.RotationDegrees,-10);
 assert.equal(view.interfaceRoutes.length,1);
 assert.deepEqual(view.localLayouts,world.LocalLayoutPlan);
});

test('altered rotation, protected anchors, bounds and history refuse display',()=>{
 for(const mutate of [
  w=>w.LocalLayoutPlan.Definition.Layouts[0].Grid.RotationDegrees=0,
  w=>w.LayoutAdaptationPlan.Attempts[1].Steps.orientation=0,
  w=>w.LayoutAdaptationPlan.OriginalLayouts.Layouts[0].Programs[0].Anchor.X+=1,
  w=>w.LayoutAdaptationPlan.Policy.Dials[0].Step=361,
  w=>w.LayoutAdaptationPlan.Policy.Dials[0].MinimumSteps=1,
  w=>w.LayoutAdaptationPlan.Policy.Dials[0].MaximumSteps=9,
  w=>w.LayoutAdaptationPlan.Policy.Dials[0].ProgramId='west-a',
  w=>w.LayoutAdaptationPlan.Policy.Dials.push({...w.LayoutAdaptationPlan.Policy.Dials[0],Id:'duplicate-target'}),
  w=>w.LayoutAdaptationPlan.Policy.MaximumWork=Infinity,
  w=>w.LayoutAdaptationPlan.Attempts[0].Accepted=true,
 ]){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/local repartition|local layout/);
 }
});

test('export embeds the exact current consumer and metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
