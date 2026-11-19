const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_DOMAIN_TRANSFERS;
if(!root)throw new Error('WORLD_DOMAIN_TRANSFERS must identify the generated fixture');
test('gated shared corridor keeps actual route sources, turns and capability states',()=>{
  const world=JSON.parse(fs.readFileSync(path.join(root,'gated/world-domains.json'),'utf8'));
  const journey=world.Journeys[0],group=world.Domains.find(d=>d.Id==='physical').World.CorridorPlan.Groups[0];
  function check(steps,states,start,end){
    let domain=start.Definition.DomainId,at=start.Position?{X:start.Position[0],Y:start.Position[1]}:null,previous=null,count=0;
    assert.equal(states.length,steps.length+1);
    for(let i=0;i<steps.length;i++){
      const s=steps[i];assert.equal(s.DomainId,domain);
      if(s.Kind==='transition'){
        const t=world.Transitions.find(t=>t.Definition.Id===s.SourceId),a=s.Reverse?t.To:t.From,b=s.Reverse?t.From:t.To;
        assert.deepEqual(at,a.Position?{X:a.Position[0],Y:a.Position[1]}:null);domain=b.Definition.DomainId;at=b.Position?{X:b.Position[0],Y:b.Position[1]}:null;
      }else if(s.Kind==='corridor-segment'){
        const source=group.Journeys.find(j=>j.Id===s.SourceId),segment=group.Segments.find(x=>x.Id===s.CorridorSegmentId);
        assert.ok(source&&segment);assert.ok(states[i].includes('key'));
        assert.ok((s.CorridorForward?segment.ForwardJourneys:segment.ReverseJourneys).includes(source.Id));
        assert.ok(journey.Requirement.Capabilities.Gates.some(g=>g.Link.DomainId==='physical'&&g.Link.ConnectionId===source.ConnectionId));
        assert.deepEqual(at,s.CorridorForward?segment.From:segment.To);at=s.CorridorForward?segment.To:segment.From;count++;
        if(previous?.Kind==='corridor-segment'){
          const junction=group.Junctions.find(j=>j.Position.X===(s.CorridorForward?segment.From:segment.To).X&&j.Position.Y===(s.CorridorForward?segment.From:segment.To).Y);
          assert.ok(junction.Transitions.some(t=>t.FromSegmentId===previous.CorridorSegmentId&&t.FromForward===previous.CorridorForward&&t.ToSegmentId===s.CorridorSegmentId&&t.ToForward===s.CorridorForward));
        }
      }else{
        const local=world.Domains.find(d=>d.Id===domain).World,route=local.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).find(r=>r?.Id===s.SourceId);
        assert.ok(route);assert.deepEqual(at,route.Path.Points[s.SegmentIndex]);at=route.Path.Points[s.SegmentIndex+1];assert.ok(states[i].includes('key'));
      }
      previous=s;
    }
    assert.ok(count>0);assert.equal(domain,end.Definition.DomainId);assert.deepEqual(at,end.Position?{X:end.Position[0],Y:end.Position[1]}:null);
  }
  check(journey.Forward,journey.ForwardCapabilityStates,journey.From,journey.To);
  check(journey.Return,journey.ReturnCapabilityStates,journey.To,journey.From);
});
