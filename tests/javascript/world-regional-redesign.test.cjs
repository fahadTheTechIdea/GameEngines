const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_REDESIGN_ROOT||'generated/partition-redesign-native-qualified';
for(const name of ['network','packing','authored','multi-cut']){
 const read=()=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 test(`${name}: complete physical generation chooses another ownership context`,()=>{
  const w=read(),original=JSON.stringify(w),view=R.layout(w),p=w.Definition.OwnershipPlan.PhysicalFeedbackPlan;
  assert.equal(view.boxes.length,name==='multi-cut'?20:10);assert.equal(JSON.stringify(w),original);
  assert.ok(p.Contexts.length>=2);assert.notEqual(p.Contexts[0].OwnershipSha256,p.Contexts.at(-1).OwnershipSha256);
  assert.equal(p.Attempts.at(-1).Accepted,true);assert.equal(p.Attempts.at(-1).RequestedComponents??null,null);
  assert.ok(w.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
  if(name==='multi-cut'){
   assert.ok(p.Attempts.some(a=>a.PlanningRejected));
   assert.deepEqual(p.Contexts[0].SelectedPartitions.find(c=>c.Members===4),p.Contexts.at(-1).SelectedPartitions.find(c=>c.Members===4));
  }
 });
 test(`${name}: forged cut assignments, ownership contexts and work refuse`,()=>{
  for(const mutate of [
   p=>delete p.PhysicalFeedbackPlan.Contexts,
   p=>p.PhysicalFeedbackPlan.PartitionSearchWork++,
   p=>p.PhysicalFeedbackPlan.Contexts[0].Ordinal=2,
   p=>p.PhysicalFeedbackPlan.Contexts.at(-1).RequestedPartitions=null,
   p=>p.PhysicalFeedbackPlan.Contexts.at(-1).RequestedPartitions[0].AxisOffsetDegrees=45,
   p=>p.PhysicalFeedbackPlan.Contexts.at(-1).Masks[0].Ownership.Vertices[0].X++,
   p=>p.PhysicalFeedbackPlan.Contexts.at(-1).ComponentSearch=p.PhysicalFeedbackPlan.Contexts[0].ComponentSearch,
   p=>p.PhysicalFeedbackPlan.Attempts.at(-1).ContextOrdinal=1,
   p=>p.PhysicalFeedbackPlan.Attempts[0].PlanningRejected=true,
   p=>p.PhysicalFeedbackPlan.Attempts.at(-1).PlanningWork++,
   p=>p.PhysicalFeedbackPlan.Attempts.at(-1).RequestedComponents=p.PhysicalFeedbackPlan.Attempts[0].Selections
  ]){const w=read();mutate(w.Definition.OwnershipPlan);assert.throws(()=>R.layout(w));}
 });
 if(name==='multi-cut')test('planning refusal retains actual pinned terrain qualification',()=>{
  for(const mutate of [a=>a.PartitionFailure=null,a=>a.PartitionFailure.Work++,a=>a.PartitionFailure.SelectedAttempts=[1],a=>a.PlanningRejected=false]){
   const w=read(),a=w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts.find(a=>a.PlanningRejected);mutate(a);assert.throws(()=>R.layout(w));
  }
 });
 if(name==='packing')test('complete source packing failure has an attributed diagnostic',()=>{
  for(const mutate of [a=>a.PlacementFailure.RegionId='missing',a=>a.PlacementFailure.Diagnostic='forged',a=>a.PlacementFailure=null]){
   const w=read();mutate(w.Definition.OwnershipPlan.PhysicalFeedbackPlan.Attempts[0]);assert.throws(()=>R.layout(w));
  }
 });
}
if(process.env.WORLD_REGIONAL_REDESIGN_JOINT_ROOT){
 const read=()=>JSON.parse(fs.readFileSync(path.join(process.env.WORLD_REGIONAL_REDESIGN_JOINT_ROOT,'joint/world-regions.json'),'utf8'));
 test('coupled terrain/count/complete program search reselects ownership',()=>{
  const w=read(),p=w.Definition.OwnershipPlan.JointPlan;assert.ok(R.layout(w).boxes.length>0);
  assert.ok(p.Attempts.some(a=>a.Stable&&a.ConnectionId));assert.ok(p.Attempts.at(-1).RequestedPartitions);assert.equal(p.Attempts.at(-1).Accepted,true);
 });
 test('coupled partition requests and final cut bindings cannot drift',()=>{
  for(const mutate of [p=>p.Attempts.at(-1).RequestedPartitions[0].AxisOffsetDegrees=45,p=>p.Attempts.at(-1).SelectedPartitions=[]]){
   const w=read();mutate(w.Definition.OwnershipPlan.JointPlan);assert.throws(()=>R.layout(w));
  }
 });
}
