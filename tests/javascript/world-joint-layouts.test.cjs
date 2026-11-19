const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
for(const name of ['two-dials','mixed-modes']){
 const folder=`generated/joint-layouts/${name}`,world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${name}: complete joint settings follow independent four-state order`,()=>{
  const view=Schematic.layout(world);assert.equal(view.boxes.length,13);assert.equal(view.interfaceRoutes.length,2);
  assert.deepEqual(view.layoutAdaptation,world.LayoutAdaptationPlan);
  const states=view.layoutAdaptation.Attempts.map(a=>[a.ScopeSettings[0].Steps.expand,
   name==='two-dials'?a.ScopeSettings[1].Steps.expand:(a.ScopeSettings[1].AlternativeId==null?0:1)]);
  assert.deepEqual(states,[[0,0],[0,1],[1,0],[1,1]]);
  assert.deepEqual(view.localLayouts.Allocations.map(a=>a.Shape.Area).sort((a,b)=>a-b),[360,360,440,440]);
 });
 test(`${name}: missing, reordered or unauthorized joint changes refuse display`,()=>{
  for(const mutate of [
   w=>w.LayoutAdaptationPlan.Attempts[3].ScopeSettings=null,
   w=>w.LayoutAdaptationPlan.Attempts[3].ScopeSettings[1].LayoutId='missing',
   w=>w.LayoutAdaptationPlan.Attempts[3].ScopeSettings[0].Steps.expand=0,
   w=>w.LayoutAdaptationPlan.Attempts[1].ScopeSettings.reverse(),
   w=>w.LayoutAdaptationPlan.Policy.AdditionalScopes=[],
   w=>w.LayoutAdaptationPlan.Policy.AdditionalScopes[0].LayoutId=w.LayoutAdaptationPlan.Policy.LayoutId,
   w=>w.LayoutAdaptationPlan.Policy.AdditionalScopes[0].Purpose='',
   w=>w.LocalLayoutPlan.Definition.Layouts.find(l=>l.Id==='east-layout').Programs[0].Anchor.X+=1,
   w=>{const p=w.LayoutAdaptationPlan.Policy;p.Dials=[{Id:'expand',Kind:'GridWidth',Step:.25,MinimumSteps:0,MaximumSteps:8}];
    p.AdditionalScopes[0].Alternatives=null;p.AdditionalScopes[0].Dials=[...p.Dials];}
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local repartition|local layout/);}
 });
 test(`${name}: exported script and metadata match current sources`,()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 });
}
