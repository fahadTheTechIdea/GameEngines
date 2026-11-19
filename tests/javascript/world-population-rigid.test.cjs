const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_POPULATION_RIGID_ROOT||'generated/population-rigid-probe-20261101';
const nested=process.env.WORLD_POPULATION_RIGID_NESTED||'generated/population-rigid-nested-probe-20261101';
const multi=process.env.WORLD_POPULATION_RIGID_MULTI||'generated/population-rigid-multi-regression-20261101';
for(const [name,file,count] of [['root',`${root}/world/world-metadata.json`,5],['program',`${nested}/ProgramRigid/world-metadata.json`,11],['joint',`${multi}/joint-rigid/world-metadata.json`,9]]){
 const world=JSON.parse(fs.readFileSync(file,'utf8'));
 test(`${name}: combined rule retains all physical sources and both route directions`,()=>{
 assert.equal(S.layout(world).boxes.length,count);assert.equal(world.AccessPlan.Sites.length,count-1);
 assert.ok(world.RoutePlan.Routes.every(r=>r.Forward.Status==='Found'&&r.Reverse.Status==='Found'));
 if(name==='joint')assert.equal(world.PlacementPlan.PopulationAdaptation.Attempts.at(-1).Scopes[0].ChoiceId,'rigid-r1-t1');
 else assert.deepEqual(world.PlacementPlan.PopulationAdaptation.Attempts.map(a=>a.ChoiceId),[null,'rigid-r0-t1','rigid-r1-t0','rigid-r1-t1']);
 });
 test(`${name}: changed rotation, translation and attempt witness are rejected`,()=>{
 for(const mutate of [
  r=>{r.Rotation.AnglesDegrees[0]++;},r=>{r.Translation.StepVectors[0].X++;}
 ]){
  const changed=structuredClone(world),p=changed.PlacementPlan;
  mutate(p.PopulationAdaptation.Policy.Rigid);mutate(p.TemplateDefinition.PopulationAdaptation.Rigid);
  assert.throws(()=>S.layout(changed),/population adaptation/);
 }
 const bad=structuredClone(world);bad.PlacementPlan.PopulationAdaptation.Attempts.at(-1).ChoiceId='rigid-r1-t0';
 assert.throws(()=>S.layout(bad),/population adaptation/);
 });
}
