const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_PHYSICAL_ROOT||'generated/regional-physical-qualified-native';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['baseline','larger-source','coupled'])test(`${name}: full physically qualified region world is inspectable`,()=>{
 const w=read(name),before=JSON.stringify(w),p=R.layout(w),o=w.Definition.OwnershipPlan;
 assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,name==='coupled'?62:48);
 assert.equal(p.views.length,3);assert.equal(o.PhysicalFeedbackPlan.Attempts.length,name==='coupled'?1:2);
 assert.equal(o.PhysicalFeedbackPlan.Attempts.at(-1).Accepted,true);
 if(name!=='coupled'){
  const failed=o.PhysicalFeedbackPlan.Attempts[0],witness=failed.DomainDisconnection;
  assert.equal(failed.FailureRegionId,'alpha');assert.equal(witness.ActorRadius,.2);
  assert.ok(witness.FromComponents.every(n=>!witness.ToComponents.includes(n)));
  assert.deepEqual(failed.Selections.slice(1),o.PhysicalFeedbackPlan.Attempts[1].Selections.slice(1));
 }
});
test('feedback trace, failure owner, complete assignment, qualification work and final selections remain mandatory',()=>{
 for(const change of [p=>delete p.PhysicalFeedbackPlan,p=>delete p.Source.PhysicalFeedback,p=>p.PhysicalFeedbackPlan.Attempts[0].QualificationWork++,
  p=>p.PhysicalFeedbackPlan.Attempts[0].FailureRegionId='beta',p=>p.PhysicalFeedbackPlan.Attempts[0].ConnectionId='forged',
  p=>p.PhysicalFeedbackPlan.Attempts[0].Accepted=true,p=>p.PhysicalFeedbackPlan.Attempts[1].Selections[0].Component=0,
  p=>p.Anchors[0].Plan.Request.ExcludedAssignments=[[0]]]){
  const w=read('baseline');change(w.Definition.OwnershipPlan);assert.throws(()=>R.layout(w));
 }
});
test('disconnection witness stays bounded and cannot claim shared components or a different width',()=>{
 for(const change of [d=>d.ActorRadius=.1,d=>d.FromComponents=d.ToComponents,d=>d.Components[0].Area++,d=>d.Components=[],
  d=>d.Work=0,d=>d.Boundary.Vertices[0].X++,d=>d.ToComponents=[4096]]){
  const w=read('baseline');change(w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts[0].DomainDisconnection);assert.throws(()=>R.layout(w));
 }
});
test('capacity/count convergence ledger composes with physical qualification ledger',()=>{
 const w=read('coupled'),p=w.Definition.OwnershipPlan;
 const countWork=p.CouplingPlan.InitializationWork+p.CouplingPlan.Attempts.reduce((n,a)=>n+a.Work,0);
 assert.equal(countWork,p.PhysicalFeedbackPlan.Attempts[0].PlanningWork);
 assert.equal(p.Work,p.PhysicalFeedbackPlan.Contexts.reduce((n,c)=>n+(c.ComponentSearch?.Work??0),0)+p.PhysicalFeedbackPlan.PartitionSearchWork+p.PhysicalFeedbackPlan.Attempts.reduce((n,a)=>n+a.PlanningWork+a.QualificationWork,0));
 p.CouplingPlan.InitializationWork++;assert.throws(()=>R.layout(w));
});
