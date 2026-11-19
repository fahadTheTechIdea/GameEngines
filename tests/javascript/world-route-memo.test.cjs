const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_ROUTE_MEMO;if(!root)throw new Error('WORLD_ROUTE_MEMO must identify the generated route execution fixture');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const checkpoint=()=>read('edited/generation-session.json');
for(const mode of ['initial','edited'])test(`${mode}: full source program and exact routes reach the current schematic`,()=>{
  const w=read(`${mode}/world-metadata.json`),before=JSON.stringify(w),view=Schematic.layout(w);
  assert.equal(w.Nodes.length,94);assert.equal(w.AccessPlan.Sites.length,91);assert.equal(w.PlacementPlan.ProgramInstances.length,35);
  assert.equal(w.Nodes.filter(n=>n.Kind==='building').length,28);assert.equal(w.Nodes.filter(n=>n.Kind==='equipment').length,56);
  const directions=w.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).concat(w.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse])));
  assert.equal(directions.length,74);assert.equal(view.boxes.length,94);assert.equal(view.routes.length,74);
  for(const r of directions)assert.deepEqual(view.routes.find(x=>x.id===r.Id).points,r.Path.Points.map(p=>({x:p.X,y:p.Y})));
  assert.equal(JSON.stringify(w),before);assert.ok(!JSON.stringify(w).includes('RuntimeRouteMemo'));assert.equal(w.RouteExecutions,undefined);
});
test('saved execution history accounts for actual per-connection reuse and unchanged logical work',()=>{
  const cp=checkpoint(),observations=read('observations.json');
  const expected={'one-policy':[28,3,31],'west-water':[30,1,31],'move-west-destination':[30,1,31],'closed-boundary-contact':[31,0,31]};
  assert.equal(cp.InitialReport.RouteExecutions.filter(r=>r.Source==='Computed').length,31);
  assert.equal(cp.InitialReport.RouteExecutions.filter(r=>r.Source==='Current').length,31);
  for(const edit of cp.Edits){
    const records=edit.Report.RouteExecutions||[],ob=observations.find(o=>o.Edit===edit.Request.Id);assert.ok(ob);assert.deepEqual(ob.Records,records);
    assert.equal(ob.ComputedWork,records.filter(r=>r.Source==='Computed').reduce((s,r)=>s+r.LogicalWork,0));
    assert.equal(ob.AvoidedWork,records.filter(r=>r.Source!=='Computed').reduce((s,r)=>s+r.LogicalWork,0));
    if(expected[edit.Request.Id])assert.deepEqual(['Computed','Accepted','Current'].map(s=>records.filter(r=>r.Source===s).length),expected[edit.Request.Id]);
    else{assert.equal(records.length,0);assert.ok(edit.Report.Stages.every(s=>s.Reused));}
    for(const r of records){assert.match(r.InputSha256,/^[a-f0-9]{64}$/);assert.match(r.OutputSha256,/^[a-f0-9]{64}$/);assert.ok(Number.isSafeInteger(r.LogicalWork)&&r.LogicalWork>0);}
    for(const r of records.filter(r=>r.Source==='Current')){
      const first=records.find(x=>x.ConnectionId===r.ConnectionId&&x.Source!=='Current');assert.ok(first);
      assert.equal(r.InputSha256,first.InputSha256);assert.equal(r.OutputSha256,first.OutputSha256);assert.equal(r.LogicalWork,first.LogicalWork);
    }
  }
  for(const id of ['west-water','move-west-destination'])assert.deepEqual(cp.Edits.find(e=>e.Request.Id===id).Report.RouteExecutions.filter(r=>r.Source==='Accepted').map(r=>r.RuleId),['east-route']);
});
test('destination edit moves only the declared compound and all its descendants',()=>{
  const a=read('initial/world-metadata.json'),b=read('edited/world-metadata.json'),old=new Map(a.Nodes.map(n=>[n.Id,n]));
  const changed=b.Nodes.filter(n=>JSON.stringify(n)!==JSON.stringify(old.get(n.Id))),owner='coastal-mixed-world/planned-0/sites-0';
  assert.equal(changed.length,13);assert.ok(changed.every(n=>n.Id===owner||n.Id.startsWith(owner+'/')));
  const before=a.RoutePlan.Routes.find(r=>r.RuleId==='east-route'),after=b.RoutePlan.Routes.find(r=>r.RuleId==='east-route');
  const original=checkpoint().InitialReport.RouteExecutions.find(r=>r.RuleId==='east-route'&&r.Source==='Computed');
  for(const id of ['west-water','move-west-destination']){
    const reused=checkpoint().Edits.find(e=>e.Request.Id===id).Report.RouteExecutions.find(r=>r.RuleId==='east-route'&&r.Source==='Accepted');
    assert.equal(reused.InputSha256,original.InputSha256);assert.equal(reused.OutputSha256,original.OutputSha256);
  }
  // Final output also includes the later source touching both domain boundaries:
  // it correctly invalidates east and changes work/candidate receipts, not geometry.
  for(const direction of ['Forward','Reverse']){assert.deepEqual(after[direction].Path,before[direction].Path);assert.deepEqual(after[direction].Cost,before[direction].Cost);}
  assert.deepEqual(after.Interface.Center,before.Interface.Center);assert.deepEqual(after.Interface.Span,before.Interface.Span);
});
test('independent domain history exposes one changed search and no hidden work in the other domain',()=>{
  const cp=read('domains/domain-session.json');assert.equal(cp.InitialReport.Domains.length,2);
  assert.ok(cp.InitialReport.Domains.every(d=>d.RouteExecutions.length===1&&d.RouteExecutions[0].Source==='Computed'));
  const report=cp.Edits[0].Report,first=report.Domains.find(d=>d.DomainId==='first'),second=report.Domains.find(d=>d.DomainId==='second');
  assert.equal(first.RouteExecutions.length,1);assert.equal(first.RouteExecutions[0].Source,'Computed');assert.equal(second.RouteExecutions,undefined);assert.ok(second.Stages.every(s=>s.Reused));
});
