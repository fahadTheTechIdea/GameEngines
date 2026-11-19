const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_POPULATION_ROTATION_ROOT||'generated/population-rotation-probe-20261101';
const nested=process.env.WORLD_POPULATION_ROTATION_NESTED||'generated/population-rotation-nested-probe-20261101';
const mixed=process.env.WORLD_POPULATION_ROTATION_MIXED||'generated/population-rotation-mixed-test-20261101';
for(const [name,file,count,routes] of [['root',`${root}/world/world-metadata.json`,5,1],['oblique',`${root}/oblique/world-metadata.json`,5,1],
 ['program',`${nested}/ProgramRotated/world-metadata.json`,11,1],['mixed',`${mixed}/mixed/world-metadata.json`,9,2]]){
 test(`${name}: rotated population retains sources and required travel`,()=>{
  const w=JSON.parse(fs.readFileSync(file,'utf8'));assert.equal(S.layout(w).boxes.length,count);assert.equal(w.AccessPlan.Sites.length,count-1);
  assert.equal(w.RoutePlan.Routes.length,routes);assert.ok(w.RoutePlan.Routes.every(r=>r.Forward.Status==='Found'&&r.Reverse.Status==='Found'));
 });
 test(`${name}: changed rotation policy or compiled source is refused`,()=>{
  const w=JSON.parse(fs.readFileSync(file,'utf8')),changed=structuredClone(w);
  const selectedAngle=name==='mixed'?1:0;
  changed.PlacementPlan.PopulationAdaptation.Policy.Rotation.AnglesDegrees[selectedAngle]++;
  changed.PlacementPlan.TemplateDefinition.PopulationAdaptation.Rotation.AnglesDegrees[selectedAngle]++;
  assert.throws(()=>S.layout(changed),/population adaptation/);
  const forged=structuredClone(w),plan=forged.PlacementPlan;
  const b=name==='program'?plan.Definition.NestedStages[0].Bindings[0]:plan.Definition.Bindings.find(x=>x.DemandId==='site-0');
  b.Population.Spiral.StartRadius++;
  assert.throws(()=>S.layout(forged),/population adaptation/);
 });
}
