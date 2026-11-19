const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_REGIONAL_JOURNEY;
if(!root)throw new Error('WORLD_REGIONAL_JOURNEY must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('physical journey witnesses follow continuous saved route segments',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    assert.match(receipt.Attempts[0].Rejection,/^METADATA_REGIONAL_JOURNEY_UNREACHABLE:/);
    assert.ok(receipt.Attempts[0].WorldProgression);
    const journey=receipt.Attempts[1].WorldJourneys.Journeys[0];
    function check(steps,start,end){
        assert.deepEqual(steps[0].From,start);assert.deepEqual(steps.at(-1).To,end);
        let previousEnd;
        for(let i=0;i<steps.length;i++){
            const s=steps[i];if(i)assert.deepEqual(steps[i-1].To,s.From);
            let routes,from,to;
            if(s.RegionId===null){const r=world.Connections.find(x=>x.Definition.Id===s.ConnectionId);routes=[r.Forward,r.Reverse];from=r.Definition.From;to=r.Definition.To;}
            else {const region=world.Regions.find(r=>r.RegionId===s.RegionId),r=region.World.RoutePlan.Routes.find(x=>x.ConnectionId===s.ConnectionId);
                const link=region.World.Connections.find(x=>x.Id===s.ConnectionId),rule=region.World.RoutePlan.Rules.find(x=>x.Id===r.RuleId);
                routes=[r.Forward,r.Reverse];from={RegionId:s.RegionId,NodeId:link.FromId,PortId:rule.FromPortId};to={RegionId:s.RegionId,NodeId:link.ToId,PortId:rule.ToPortId};}
            const route=routes.find(r=>r?.Id===s.RouteId);assert.ok(route);
            assert.deepEqual(route.Path.Points[s.SegmentIndex],s.From);assert.deepEqual(route.Path.Points[s.SegmentIndex+1],s.To);
            if(route===routes[1])[from,to]=[to,from];
            if(i&&s.RouteId!==steps[i-1].RouteId){
                assert.ok(previousEnd.PortId&&from.PortId);
                for(const key of ['RegionId','NodeId','PortId'])assert.equal(previousEnd[key],from[key]);
            }
            previousEnd=to;
        }
    }
    assert.ok(journey.Forward.some(s=>s.RegionId==='arrival')&&journey.Forward.some(s=>s.RegionId===null));
    check(journey.Forward,journey.From,journey.To);check(journey.Return,journey.To,journey.From);
    if(journey.Milestones){
        const indexes=journey.Milestones.map(m=>journey.Forward.findIndex(s=>s.To.X===m.Position.X&&s.To.Y===m.Position.Y));
        assert.ok(indexes.every(i=>i>=0));assert.ok(indexes.every((i,n)=>n===0||indexes[n-1]<i));
        for(const milestone of journey.Milestones){
            const region=world.Regions.find(r=>r.RegionId===milestone.Endpoint.RegionId);
            const site=region.World.AccessPlan.Sites.find(s=>s.NodeId===milestone.Endpoint.NodeId);
            assert.deepEqual(site.Ports.find(p=>p.Id===milestone.Endpoint.PortId).ApproachPosition,milestone.Position);
        }
    }
});
