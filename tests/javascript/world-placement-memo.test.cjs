const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_PLACEMENT_MEMO;if(!root)throw new Error('WORLD_PLACEMENT_MEMO must identify the generated placement fixture');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const initial=()=>read('initial/world-metadata.json');
const sum=(rows,key)=>rows.reduce((n,r)=>n+r[key],0);
for(const mode of ['initial','edited'])test(`${mode}: all nested physical content reaches exact schematic coordinates`,()=>{
  const world=read(`${mode}/world-metadata.json`),before=JSON.stringify(world),view=Schematic.layout(world);
  assert.equal(world.Nodes.length,94);assert.equal(world.AccessPlan.Sites.length,91);assert.equal(world.PlacementPlan.ProgramInstances.length,35);
  assert.equal(world.Nodes.filter(n=>n.Kind==='building').length,28);assert.equal(world.Nodes.filter(n=>n.Kind==='equipment').length,56);
  assert.equal(view.boxes.length,94);assert.equal(view.routes.length,0);assert.equal(world.RoutePlan,null);assert.equal(world.NetworkPlan,null);
  assert.equal(JSON.stringify(world),before);assert.equal(world.PlacementExecutions,undefined);
  assert.ok(!JSON.stringify(world).includes('WorldPlacementEvaluationMemo'));
  const ids=new Set(world.Nodes.map(n=>n.Id));
  for(const node of world.Nodes){if(node.ParentId)assert.ok(ids.has(node.ParentId));assert.ok(node.Position.every(Number.isFinite));}
  for(const site of world.AccessPlan.Sites){
    assert.ok(ids.has(site.NodeId));const node=world.Nodes.find(n=>n.Id===site.NodeId);
    const box=view.boxes.find(b=>b.id===site.NodeId);assert.deepEqual(box.physicalAccess,site);
    assert.ok(Math.abs(box.x+box.width/2-node.Position[0])<1e-10);assert.ok(Math.abs(box.y+box.height/2-node.Position[1])<1e-10);
    const vertices=site.Footprint.Vertices,center=[vertices.reduce((s,p)=>s+p.X,0)/vertices.length,vertices.reduce((s,p)=>s+p.Y,0)/vertices.length];
    assert.deepEqual(center,node.Position);
  }
});
test('a parent edit moves exactly its complete thirteen-member physical subtree',()=>{
  const a=initial(),b=read('move-one-compound.json');
  const changed=a.Nodes.filter((n,i)=>JSON.stringify(n.Position)!==JSON.stringify(b.Nodes[i].Position));assert.equal(changed.length,13);
  const parent=changed.find(n=>n.Kind==='site');assert.ok(parent);
  for(const node of changed){assert.ok(node.Id===parent.Id||node.Id.startsWith(parent.Id+'/'));const moved=b.Nodes.find(n=>n.Id===node.Id);assert.deepEqual(moved.Position,[node.Position[0]+1,node.Position[1]]);}
  assert.equal(changed.filter(n=>n.Kind==='building').length,4);assert.equal(changed.filter(n=>n.Kind==='equipment').length,8);
  for(const site of a.AccessPlan.Sites.filter(s=>!changed.some(n=>n.Id===s.NodeId)))assert.deepEqual(b.AccessPlan.Sites.find(s=>s.NodeId===site.NodeId),site);
});
test('source dimensions survive regeneration without shrinking or removing equipment',()=>{
  const a=read('move-one-compound.json'),b=read('equipment-source-size.json');
  assert.deepEqual(a.Nodes,b.Nodes);
  for(const site of b.AccessPlan.Sites){const node=b.Nodes.find(n=>n.Id===site.NodeId),old=a.AccessPlan.Sites.find(s=>s.NodeId===site.NodeId);
    if(node.Kind!=='equipment'){assert.deepEqual(site,old);continue;}
    const v=site.Footprint.Vertices,ov=old.Footprint.Vertices;
    assert.ok(Math.abs(Math.max(...v.map(p=>p.X))-Math.min(...v.map(p=>p.X))-1.2)<1e-10);
    assert.equal(Math.max(...ov.map(p=>p.X))-Math.min(...ov.map(p=>p.X)),1);
    assert.equal(Math.max(...v.map(p=>p.Y))-Math.min(...v.map(p=>p.Y)),1);
  }
});
test('saved execution reports expose actual reuse, complete scopes and charged work',()=>{
  const cp=read('edited/generation-session.json'),observations=read('observations.json');
  assert.equal(cp.InitialReport.PlacementExecutions.length,91);assert.equal(sum(cp.InitialReport.PlacementExecutions,'Computed'),91);
  const expected={'move-one-compound':[13,78],'equipment-source-size':[56,35],'environment-field':[91,0]};
  for(const edit of cp.Edits){const rows=edit.Report.PlacementExecutions||[],ob=observations.find(x=>x.Edit===edit.Request.Id);assert.ok(ob);assert.deepEqual(rows,ob.Records||[]);
    if(expected[edit.Request.Id])assert.deepEqual([sum(rows,'Computed'),sum(rows,'Accepted')],expected[edit.Request.Id]);
    else{assert.equal(rows.length,0);assert.ok(edit.Report.Stages.every(s=>s.Reused));}
    for(const r of rows){assert.match(r.InputSha256,/^[a-f0-9]{64}$/);for(const k of ['Computed','Current','Accepted','ComputedWork','ReusedWork'])assert.ok(Number.isSafeInteger(r[k])&&r[k]>=0);}
    assert.ok(sum(rows,'ComputedWork')+sum(rows,'ReusedWork')<=ob.Work);
  }
  const initialByKey=new Map(cp.InitialReport.PlacementExecutions.map(r=>[`${r.StageId}/${r.DemandId}`,r]));
  for(const r of cp.Edits.find(e=>e.Request.Id==='move-one-compound').Report.PlacementExecutions.filter(r=>r.Accepted>0)){
    const old=initialByKey.get(`${r.StageId}/${r.DemandId}`);assert.ok(old);assert.equal(r.InputSha256,old.InputSha256);assert.equal(r.ReusedWork,old.ComputedWork);
  }
});
