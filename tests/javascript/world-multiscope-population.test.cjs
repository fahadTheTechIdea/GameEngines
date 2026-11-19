const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_POPULATION_MULTISCOPE_ROOT||'generated/population-multiscope-probe-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/world/world-metadata.json`,'utf8'));
test('coordinated population schematic retains every source and route',()=>{
 assert.equal(S.layout(world).boxes.length,9);assert.equal(world.AccessPlan.Sites.length,8);
 assert.equal(world.RoutePlan.Routes.length,2);
 assert.ok(world.RoutePlan.Routes.every(r=>r.Forward.Status==='Found'&&r.Reverse.Status==='Found'));
});
test('scoped choice and second compiled population tampering are rejected',()=>{
 const choice=structuredClone(world);choice.PlacementPlan.PopulationAdaptation.Attempts[3].Scopes[1].ChoiceId=null;
 assert.throws(()=>S.layout(choice),/population adaptation/);
 const geometry=structuredClone(world),binding=geometry.PlacementPlan.Definition.Bindings.find(b=>b.DemandId==='other-0');
 binding.Population.Spiral.StartRadius++;
 assert.throws(()=>S.layout(geometry),/population adaptation/);
});
