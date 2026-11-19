const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.REGIONAL_PHYSICAL_WORLD;
if(!root)throw new Error('REGIONAL_PHYSICAL_WORLD must identify the generated output');
test('saved regional and two-floor journey follows the qualified source geometry',()=>{
  const world=JSON.parse(fs.readFileSync(path.join(root,'world.json'),'utf8'));
  const j=world.Domains.Journeys[0],point=p=>({X:p[0],Y:p[1]});
  function walk(steps,start,end){
    let domain=start.Definition.DomainId,at=point(start.Position),regional=0,floor=0,changes=0;
    for(const step of steps){
      assert.equal(step.DomainId,domain);
      if(step.Kind==='transition'){
        const t=world.Domains.Transitions.find(t=>t.Definition.Id===step.SourceId);assert.ok(t);
        assert.ok(!step.Reverse||t.Definition.Bidirectional);
        const a=step.Reverse?t.To:t.From,b=step.Reverse?t.From:t.To;assert.deepEqual(at,point(a.Position));
        const route=world.RegionalRoutes.find(r=>r.TransitionId===step.SourceId);
        if(route){const p=(step.Reverse?route.Reverse:route.Forward).Path.Points;assert.deepEqual(p[0],at);assert.deepEqual(p.at(-1),point(b.Position));regional++;}
        else{assert.ok(['entry','stairs'].includes(step.SourceId));changes++;}
        domain=b.Definition.DomainId;assert.equal(step.ToDomainId,domain);at=point(b.Position);
      }else{
        assert.equal(step.Kind,'route-segment');const local=world.Domains.Domains.find(d=>d.Id===domain).World;
        const routes=[...(local.RoutePlan?.Routes||[]),...(local.InteriorPlan?.Results||[]).flatMap(r=>r.Routes)];
        const source=routes.map(r=>step.Reverse?r.Reverse:r.Forward).find(r=>r?.Id===step.SourceId);assert.ok(source);
        assert.deepEqual(source.Path.Points[step.SegmentIndex],at);at=source.Path.Points[step.SegmentIndex+1];floor++;
      }
    }
    assert.equal(domain,end.Definition.DomainId);assert.deepEqual(at,point(end.Position));assert.ok(regional>0&&floor>1);assert.equal(changes,2);
  }
  walk(j.Forward,j.From,j.To);walk(j.Return,j.To,j.From);
  const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
  for(const text of ['Regional route ','Explicit domain change doorway','Explicit domain change stairs','route-segment'])assert.ok(html.includes(text));
});
