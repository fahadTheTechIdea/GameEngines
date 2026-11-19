const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
for(const name of ['grid-to-weighted','weighted-to-grid']){
 const folder=`${process.env.WORLD_LAYOUT_STRATEGIES_ROOT||'generated/layout-strategies'}/${name}`,world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${name}: complete world exposes baseline failure and selected strategy`,()=>{
  const view=Schematic.layout(world);assert.equal(view.boxes.length,7);assert.equal(view.interfaceRoutes.length,1);
  assert.deepEqual(view.localLayouts,world.LocalLayoutPlan);assert.equal(view.layoutAdaptation.Attempts.length,2);
  assert.equal(view.layoutAdaptation.Attempts[0].AlternativeId,null);
  assert.equal(view.layoutAdaptation.Attempts[1].AlternativeId,world.LayoutAdaptationPlan.Policy.Alternatives[0].Id);
  assert.equal(view.localLayouts.Allocations.find(a=>a.Id==='west-a').Shape.Area,440);
 });
 test(`${name}: changed strategy history or protected obligations refuse display`,()=>{
  for(const mutate of [
   w=>w.LayoutAdaptationPlan.Attempts[1].AlternativeId='missing',
   w=>w.LayoutAdaptationPlan.Attempts[0].AlternativeId=w.LayoutAdaptationPlan.Attempts[1].AlternativeId,
   w=>w.LayoutAdaptationPlan.Policy.Alternatives=[],
   w=>w.LayoutAdaptationPlan.Policy.Alternatives[0].Layout.Programs[0].MinimumArea-=1,
   w=>w.LayoutAdaptationPlan.Policy.Alternatives[0].Layout.Programs[0].Anchor.X+=1,
   w=>w.LayoutAdaptationPlan.Policy.Alternatives[0].Layout.RegionFrame=null,
   w=>w.LayoutAdaptationPlan.Policy.Alternatives[0].Purpose='',
   w=>w.LayoutAdaptationPlan.Policy.Dials=[{Id:'width',Kind:'GridWidth',Step:1,MinimumSteps:0,MaximumSteps:1}],
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Strategy='missing',
   w=>{const bad=structuredClone(w.LayoutAdaptationPlan.OriginalLayouts.Layouts[0]);bad.Strategy='OrientedGrid';bad.Grid={Origin:{X:-20,Y:-10},RotationDegrees:0,CellWidth:0,CellHeight:30};
    bad.Programs=bad.Programs.map((p,i)=>({...p,Influence:0,Column:i,Row:0}));w.LayoutAdaptationPlan.Policy.Alternatives.push({Id:'unused-bad',Purpose:'Malformed unused choice',SourceRuleId:'test:bad',Layout:bad});}
  ]){
   const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local repartition|local layout/);
  }
 });
 test(`${name}: exact current script and world embedded in packet`,()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 });
}
