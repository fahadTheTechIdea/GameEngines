const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const local=process.env.WORLD_BENT_LOCAL_ROOT,regional=process.env.WORLD_BENT_REGIONAL_ROOT,feedback=process.env.WORLD_BENT_FEEDBACK_ROOT;
if(!local||!regional||!feedback)throw Error('Set WORLD_BENT_LOCAL_ROOT, WORLD_BENT_REGIONAL_ROOT and WORLD_BENT_FEEDBACK_ROOT');
const read=(root,file)=>JSON.parse(fs.readFileSync(path.join(root,'world',file),'utf8'));
const area=p=>{const v=p.Vertices,o=v[0];let a=0;for(let i=1;i+1<v.length;i++)a+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(a)/2;};
const corners=p=>{const v=p.Vertices;return v.filter((b,i)=>{const a=v[(i+v.length-1)%v.length],c=v[(i+1)%v.length];return Math.abs((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))>1e-7;}).length;};
// Independent ear-clipping and triangle intersection; no engine geometry code.
const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
function triangles(p){
 let v=p.Vertices.map(q=>({...q}));let changed=true;
 while(changed&&v.length>3){changed=false;for(let i=0;i<v.length;i++)if(Math.abs(cross(v[(i+v.length-1)%v.length],v[i],v[(i+1)%v.length]))<1e-8){v.splice(i,1);changed=true;break;}}
 let signed=0;for(let i=0;i<v.length;i++)signed+=v[i].X*v[(i+1)%v.length].Y-v[(i+1)%v.length].X*v[i].Y;
 if(signed<0)v.reverse();const result=[];
 while(v.length>3){let found=false;
  for(let i=0;i<v.length;i++){const a=v[(i+v.length-1)%v.length],b=v[i],c=v[(i+1)%v.length];if(cross(a,b,c)<=1e-8)continue;
   if(v.some((q,j)=>j!==i&&j!==(i+1)%v.length&&j!==(i+v.length-1)%v.length&&cross(a,b,q)>=-1e-8&&cross(b,c,q)>=-1e-8&&cross(c,a,q)>=-1e-8))continue;
   result.push([a,b,c]);v.splice(i,1);found=true;break;}
  assert.ok(found,'independent polygon triangulation');
 }
 result.push(v);return result;
}
function overlap(a,b){let subject=a;
 for(let i=0;i<3;i++){const x=b[i],y=b[(i+1)%3],input=subject;subject=[];
  for(let j=0;j<input.length;j++){const p=input[j],q=input[(j+1)%input.length],u=cross(x,y,p),v=cross(x,y,q);
   if(u>=-1e-9)subject.push(p);if((u>=0)!==(v>=0)){const t=u/(u-v);subject.push({X:p.X+t*(q.X-p.X),Y:p.Y+t*(q.Y-p.Y)});}}
  if(subject.length===0)return 0;
 }
 return subject.length<3?0:area({Vertices:subject});
}
test('nested physical world renders and checks its actual bent partition',()=>{
 const world=read(local,'world-metadata.json'),plan=world.RegionPlan.IntentPlan.Partitions[0].AreaPlan;
 assert.equal(S.layout(world).boxes.length,11);assert.equal(plan.Pattern.PartitionSearch.Bend,.1);
 if(plan.Pattern.PartitionSearch.BendOffset!=null){
  assert.equal(plan.Pattern.PartitionSearch.BendOffset,2);
  const wrongOffset=structuredClone(plan);wrongOffset.Pattern.PartitionSearch.BendOffset=-2;
  assert.throws(()=>S.areaPartitionWitness(wrongOffset,()=>{throw Error('invalid partition');}),/invalid partition/);
 }
 const forged=structuredClone(plan);forged.Pattern.PartitionSearch.Bend=0;
 assert.throws(()=>S.areaPartitionWitness(forged,()=>{throw Error('invalid partition');}),/invalid partition/);
});
test('disconnected preferred bend selects the declared alternative',()=>{
 const plan=JSON.parse(fs.readFileSync(path.join(local,'fallback-plan.json'),'utf8'));
 assert.ok(plan.PartitionAttempts.some(a=>a.Bend===1&&a.Status==='Rejected'));
 assert.ok(plan.PartitionAttempts.some(a=>a.Bend===.5&&a.Status==='Selected'));
 S.areaPartitionWitness(plan,()=>{throw Error('invalid partition');});
 const forged=structuredClone(plan);forged.PartitionAttempts.find(a=>a.Status==='Selected').Bend=1;
 assert.throws(()=>S.areaPartitionWitness(forged,()=>{throw Error('invalid partition');}),/invalid partition/);
});
if(fs.existsSync(path.join(local,'offset-fallback-plan.json')))test('disconnected bend position selects and records an allowed alternative',()=>{
 const plan=JSON.parse(fs.readFileSync(path.join(local,'offset-fallback-plan.json'),'utf8'));
 assert.ok(plan.PartitionAttempts.some(a=>a.BendOffset===0&&a.Status==='Rejected'));
 assert.ok(plan.PartitionAttempts.some(a=>[3,-3].includes(a.BendOffset)&&a.Status==='Selected'));
 S.areaPartitionWitness(plan,()=>{throw Error('invalid partition');});
 const forged=structuredClone(plan);forged.PartitionAttempts.find(a=>a.Status==='Selected').BendOffset=0;
 assert.throws(()=>S.areaPartitionWitness(forged,()=>{throw Error('invalid partition');}),/invalid partition/);
});
test('regional world retains connected sources and validates bent ownership',()=>{
 const world=read(regional,'world-regions.json'),definition=world.Definition;
 assert.equal(R.layout(world).boxes.length,20);assert.equal(world.Connections.length,3);
 if(definition.OwnershipPlan.Source.Pattern.PartitionSearch.BendOffset!=null){
  assert.equal(definition.OwnershipPlan.Source.Pattern.PartitionSearch.BendOffset,3);
  const wrongOffset=structuredClone(world);
  wrongOffset.Definition.OwnershipPlan.Source.Pattern.PartitionSearch.BendOffset=-3;
  wrongOffset.Definition.OwnershipPlan.Partition.Pattern.PartitionSearch.BendOffset=-3;
  assert.throws(()=>R.layout(wrongOffset),/Invalid regional/);
 }
 assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse?.Status==='Found'));
 assert.ok(definition.Regions.some(r=>corners(r.Ownership)>4));
 const total=definition.Regions.reduce((n,r)=>n+area(r.Ownership),0);assert.ok(Math.abs(total-area(definition.Surface.Boundary))<1e-6);
 for(let i=0;i<definition.Regions.length;i++)for(let j=0;j<i;j++){
  const a=triangles(definition.Regions[i].Ownership),b=triangles(definition.Regions[j].Ownership);
  const shared=a.reduce((sum,x)=>sum+b.reduce((n,y)=>n+overlap(x,y),0),0);assert.ok(shared<1e-6,`overlap ${shared}`);
 }
 const forged=structuredClone(world);forged.Definition.OwnershipPlan.Source.Pattern.PartitionSearch.Bend=0;
 forged.Definition.OwnershipPlan.Partition.Pattern.PartitionSearch.Bend=0;
 assert.throws(()=>R.layout(forged),/Invalid regional/);
});
test('physical feedback reconsiders an authored bend and records the selected boundary',()=>{
 const world=read(feedback,'world-regions.json'),plan=world.Definition.OwnershipPlan.PhysicalFeedbackPlan;
 assert.equal(R.layout(world).boxes.length,48);assert.equal(world.Connections.length,2);assert.ok(plan.Contexts.length>1);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),45);
 assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse?.Status==='Found'));
 for(const r of world.Regions){const local=r.World,source=local.PlacementPlan.Definition.SourceWorld;
  for(const node of source.Nodes)assert.deepEqual(local.Nodes.find(n=>n.Id===node.Id),node);}
 assert.ok(plan.Contexts[0].SelectedPartitions.every(c=>c.Bend===0));
 assert.ok(plan.Contexts.at(-1).SelectedPartitions.some(c=>c.Bend===-.4));
 assert.ok(!plan.Attempts[0].Accepted&&plan.Attempts.at(-1).Accepted);
 const changed=structuredClone(world);changed.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts.at(-1).SelectedPartitions[0].Bend=.8;
 assert.throws(()=>R.layout(changed),/Invalid regional/);
});
if(process.env.WORLD_BENT_SEED_ROOT)test('world seeds select different declared bends without changing required content',()=>{
 const root=process.env.WORLD_BENT_SEED_ROOT;
 const a=JSON.parse(fs.readFileSync(path.join(root,'first','world-regions.json'),'utf8'));
 const b=JSON.parse(fs.readFileSync(path.join(root,'second-world.json'),'utf8'));
 assert.equal(R.layout(a).boxes.length,20);assert.equal(R.layout(b).boxes.length,20);
 const bends=w=>w.Definition.OwnershipPlan.Partition.PartitionAttempts.filter(p=>p.Status==='Selected').map(p=>p.Bend);
 assert.notDeepEqual(bends(a),bends(b));assert.ok([...bends(a),...bends(b)].every(v=>v===.15||v===-.15));
});
if(process.env.WORLD_BEND_POSITION_SEED_ROOT)test('world seed selects only declared bend positions',()=>{
 const root=process.env.WORLD_BEND_POSITION_SEED_ROOT;
 const world=read(root,'world-regions.json');
 const receipt=JSON.parse(fs.readFileSync(path.join(root,'world','world-design.json'),'utf8'));
 assert.equal(R.layout(world).boxes.length,20);assert.ok(receipt.SeedDerivations.some(d=>d.Target==='ownership-partition'));
 const positions=world.Definition.OwnershipPlan.Partition.PartitionAttempts.filter(a=>a.Status==='Selected').map(a=>a.BendOffset);
 assert.ok(positions.length>0&&positions.every(v=>v===0||v===-2));
});
