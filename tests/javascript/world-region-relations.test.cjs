const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/region-relations',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('complete program retains constrained regional relationship',()=>{
 const view=Schematic.layout(world);assert.equal(view.boxes.length,17);assert.equal(world.RoutePlan.Routes.length,11);
 const proof=world.RegionPlan.IntentPlan.Relations[0];
 assert.equal(proof.From.X,120);assert.deepEqual(proof.To,{X:-6,Y:-23});
 assert.ok(Math.abs(proof.Distance-Math.sqrt(126**2+23**2))<1e-9);
 assert.equal(world.ProgramCompositionPlan.Attempts.length,1);
 assert.deepEqual(view.generatedRegions,world.RegionPlan);
});
test('missing, changed, mismatched or infeasible relationship witness refuses display',()=>{
 for(const mutate of [
  w=>w.RegionPlan.IntentPlan.Relations=null,
  w=>w.RegionPlan.IntentPlan.Relations[0].Distance+=1,
  w=>w.RegionPlan.IntentPlan.Relations[0].From.X+=1,
  w=>w.RegionPlan.IntentPlan.Relations[0].Requirement.MaximumDistance=200,
  w=>w.RegionPlan.Request.IntentRelations[0].MaximumDistance=50,
  w=>w.RegionPlan.Request.IntentRelations[0].MinimumDistance=-1,
  w=>w.RegionPlan.Request.IntentRelations[0].ToId='missing',
  w=>w.RegionPlan.Request.IntentRelations[0].Purpose='',
  w=>w.RegionPlan.Request.IntentRelations.push({...w.RegionPlan.Request.IntentRelations[0]}),
  w=>w.RegionPlan.Request.IntentRelations=null,
 ]){
  const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/region plan|region intent|composition/);
 }
});
test('packet contains exact current consumer and full metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
