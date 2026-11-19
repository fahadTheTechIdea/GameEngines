const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_GLOBAL_COMPONENT_ROOT||'generated/global-component-native-qualified';
for(const name of ['network','authored','double-change']){
 const world=JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 test(`${name}: complete assignment redesign keeps all source members and actual routes`,()=>{
  const view=R.layout(world),p=world.Definition.OwnershipPlan.PhysicalFeedbackPlan;
  assert.equal(view.boxes.length,name==='double-change'?15:10);assert.equal(p.Attempts.length,name==='double-change'?4:2);
  assert.equal(p.Attempts[0].Accepted,false);assert.equal(p.Attempts.at(-1).Accepted,true);assert.ok(p.Contexts[0].ComponentSearch.Work>0);
  const fixed=name==='double-change'?'outpost':'arrival',choice=a=>a.Selections.find(c=>c.RegionId===fixed);
  assert.deepEqual(choice(p.Attempts[0]),choice(p.Attempts.at(-1)));
  assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
 });
 test(`${name}: forged domains, local pruning, pins, context and retained failure refuse`,()=>{
  for(const mutate of [
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts[0].ComponentSearch=null,
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts[0].ComponentSearch.Work++,
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts[0].ComponentSearch.Domains[0].ContextSha256='bad',
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts[0].ComponentSearch.Domains[0].Options[0].Eligible=false,
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts.at(-1).RequestedComponents[0].Component=4095,
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts.at(-1).RequestedComponents=null,
   w=>w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts[0].Selections[0].Position.X++,
   w=>w.Definition.OwnershipPlan.Anchors[0].Plan.Request.RequiredAssignment=[4095],
   w=>w.Definition.OwnershipPlan.Anchors[0].Plan.Request.ExcludedAssignments=[[0]]
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>R.layout(changed),/Invalid regional ownership|Invalid region intent/);}
  if(name!=='authored'){
   const changed=structuredClone(world);changed.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts[0].NetworkFailure.Plan.Status='Found';
   assert.throws(()=>R.layout(changed),/Invalid regional ownership/);
  }
 });
}
