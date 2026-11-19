const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_DOMAIN_TRANSFERS;
if(!root)throw new Error('WORLD_DOMAIN_TRANSFERS must identify the generated fixture');
test('domain journey follows qualified directed corridor segments and transitions',()=>{
    const world=JSON.parse(fs.readFileSync(path.join(root,'corridor/world-domains.json'),'utf8'));
    const journey=world.Journeys[0],local=world.Domains.find(d=>d.Id==='physical').World,group=local.CorridorPlan.Groups[0];
    function check(steps,start,end){
        let domain=start.Definition.DomainId,point=start.Position?{X:start.Position[0],Y:start.Position[1]}:null,previous=null,count=0;
        for(const step of steps){
            assert.equal(step.DomainId,domain);
            if(step.Kind==='transition'){
                const t=world.Transitions.find(t=>t.Definition.Id===step.SourceId),a=step.Reverse?t.To:t.From,b=step.Reverse?t.From:t.To;
                assert.deepEqual(point,a.Position?{X:a.Position[0],Y:a.Position[1]}:null);domain=b.Definition.DomainId;point=b.Position?{X:b.Position[0],Y:b.Position[1]}:null;
            }else if(step.Kind==='corridor-segment'){
                count++;assert.equal(step.CorridorGroupId,group.Definition.Id);assert.equal(step.SegmentIndex,-1);
                const segment=group.Segments.find(s=>s.Id===step.CorridorSegmentId),forward=step.CorridorForward;
                assert.deepEqual(point,forward?segment.From:segment.To);point=forward?segment.To:segment.From;
                assert.ok((forward?segment.ForwardJourneys:segment.ReverseJourneys).includes(step.SourceId));
                const source=local.RoutePlan.Routes.find(r=>r.Forward?.Id===step.SourceId||r.Reverse?.Id===step.SourceId);
                assert.ok(source);assert.equal(step.Reverse,source.Reverse?.Id===step.SourceId);
                if(previous?.Kind==='corridor-segment'){
                    const j=group.Junctions.find(j=>j.Position.X===segment[forward?'From':'To'].X&&j.Position.Y===segment[forward?'From':'To'].Y);
                    assert.ok(j.Transitions.some(t=>t.FromSegmentId===previous.CorridorSegmentId&&t.FromForward===previous.CorridorForward&&t.ToSegmentId===step.CorridorSegmentId&&t.ToForward===forward));
                }
            }else{
                assert.equal(step.Kind,'route-segment');const route=local.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).find(r=>r?.Id===step.SourceId);
                assert.ok(route);assert.deepEqual(point,route.Path.Points[step.SegmentIndex]);point=route.Path.Points[step.SegmentIndex+1];
            }
            previous=step;
        }
        assert.ok(count>0);assert.equal(domain,end.Definition.DomainId);
        assert.deepEqual(point,end.Position?{X:end.Position[0],Y:end.Position[1]}:null);
    }
    check(journey.Forward,journey.From,journey.To);check(journey.Return,journey.To,journey.From);
});
