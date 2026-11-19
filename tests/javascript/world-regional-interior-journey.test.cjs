const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_REGIONAL_INTERIOR_JOURNEY;
if(!root)throw new Error('WORLD_REGIONAL_INTERIOR_JOURNEY must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('saved journey uses the declared opening and continuous interior and exterior routes',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    assert.match(receipt.Attempts[0].Rejection,/^METADATA_REGIONAL_JOURNEY_UNREACHABLE:/);
    const local=world.Regions.find(r=>r.RegionId==='building').World,inside=local.InteriorPlan.Results[0];
    const entry=inside.Openings.find(o=>o.Entrance),journey=receipt.Attempts[1].WorldJourneys.Journeys[0];
    assert.ok(entry);assert.equal(inside.Openings.length,6);
    const interiorIds=new Set(inside.Routes.map(r=>r.ConnectionId));
    function check(steps,start,end){
        assert.deepEqual(steps[0].From,start);assert.deepEqual(steps.at(-1).To,end);
        assert.ok(steps.some(s=>s.ConnectionId===entry.ConnectionId));
        assert.ok(steps.some(s=>interiorIds.has(s.ConnectionId)));assert.ok(steps.some(s=>s.RegionId===null));
        let previousEnd;
        for(let i=0;i<steps.length;i++){
            const s=steps[i];if(i)assert.deepEqual(steps[i-1].To,s.From);
            let routes,from,to;
            if(s.RegionId===null){const route=world.Connections.find(c=>c.Definition.Id===s.ConnectionId);routes=[route.Forward,route.Reverse];from=route.Definition.From;to=route.Definition.To;}
            else {
                const route=inside.Routes.find(r=>r.ConnectionId===s.ConnectionId)||local.RoutePlan?.Routes.find(r=>r.ConnectionId===s.ConnectionId);
                assert.ok(route);routes=[route.Forward,route.Reverse];
                const link=local.Connections.find(c=>c.Id===s.ConnectionId);
                let fromPort=null,toPort=null;
                if(interiorIds.has(s.ConnectionId)){
                    const opening=inside.Openings.find(o=>o.ConnectionId===s.ConnectionId);assert.ok(opening);
                    if(opening.Entrance){
                        if(!inside.RoomNodeIds.includes(link.FromId))fromPort=opening.Entrance.OutsidePortId;
                        if(!inside.RoomNodeIds.includes(link.ToId))toPort=opening.Entrance.OutsidePortId;
                    }
                }else {const rule=local.RoutePlan.Rules.find(r=>r.Id===route.RuleId);fromPort=rule.FromPortId;toPort=rule.ToPortId;}
                from={RegionId:s.RegionId,NodeId:link.FromId,PortId:fromPort??null};to={RegionId:s.RegionId,NodeId:link.ToId,PortId:toPort??null};
            }
            const route=routes.find(r=>r?.Id===s.RouteId);assert.ok(route);
            assert.deepEqual(route.Path.Points[s.SegmentIndex],s.From);assert.deepEqual(route.Path.Points[s.SegmentIndex+1],s.To);
            if(route===routes[1])[from,to]=[to,from];
            if(i&&s.RouteId!==steps[i-1].RouteId)for(const key of ['RegionId','NodeId','PortId'])assert.equal(previousEnd[key]??null,from[key]??null);
            previousEnd=to;
        }
    }
    check(journey.Forward,journey.From,journey.To);check(journey.Return,journey.To,journey.From);
});
