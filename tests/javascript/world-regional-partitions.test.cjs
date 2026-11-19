const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_TERRAIN_PARTITION_ROOT||'generated/terrain-partition-native-qualified';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['baseline','terrain-edit','irregular','pinned','larger-source']){
 test(`${name}: actual terrain qualified the selected complete ownership`,()=>{
  const w=read(name),view=R.layout(w),p=w.Definition.OwnershipPlan,q=p.PartitionQualification;
  assert.equal(view.boxes.length,10);assert.equal(w.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),6);
  assert.equal(q.Attempts[0].Qualified,false);assert.equal(q.SelectedAttempts.length,2);
  assert.equal(q.Work,q.Attempts.reduce((n,a)=>n+a.Work,0));assert.ok(p.Work>=p.Partition.Work+q.Work);
  assert.ok(w.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
  if(name==='larger-source')assert.ok(q.Attempts.filter(a=>a.Qualified).every(a=>a.Request.Intents[0].MinimumClearance>8));
 });
 test(`${name}: forged failure, geometry, selected ownership and qualification ledger refuse`,()=>{
  const original=read(name);
  for(const mutate of [
   p=>delete p.PartitionQualification,p=>p.PartitionQualification.Work++,
   p=>p.PartitionQualification.SelectedAttempts[0]=1,
   p=>p.PartitionQualification.Attempts[0].Failure.Candidates[0].Eligible=true,
   p=>p.PartitionQualification.Attempts[0].Rejection='invented',
   p=>p.PartitionQualification.Attempts[0].Request.Intents[0].MinimumArea++,
   p=>p.PartitionQualification.Attempts.at(-1).Ownership.Vertices[0].X++,
   p=>p.PartitionQualification.Attempts.at(-1).AnchorPlanSha256='bad',
   p=>p.Partition.PartitionAttempts.filter(a=>a.Status==='Rejected').forEach(a=>a.Rejection='invented')
  ]){const changed=structuredClone(original);mutate(changed.Definition.OwnershipPlan);assert.throws(()=>R.layout(changed),/Invalid region/);}
 });
}
