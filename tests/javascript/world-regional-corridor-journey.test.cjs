const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_REGIONAL_CORRIDOR_JOURNEY;
if(!root)throw new Error('WORLD_REGIONAL_CORRIDOR_JOURNEY must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('saved corridor journey uses actual directed segments and recorded turns',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    assert.match(receipt.Attempts[0].Rejection,/^METADATA_REGIONAL_JOURNEY_UNREACHABLE:/);
    const journey=receipt.Attempts[1].WorldJourneys.Journeys[0];
    function check(path,start,end){
        assert.deepEqual(path[0].From,start);assert.deepEqual(path.at(-1).To,end);
        let corridorCount=0;
        for(let i=0;i<path.length;i++){
            const step=path[i];if(i)assert.deepEqual(path[i-1].To,step.From);
            if(!step.CorridorSegmentId)continue;
            corridorCount++;
            const region=world.Regions.find(r=>r.RegionId===step.RegionId);
            const group=region.World.CorridorPlan.Groups.find(g=>g.Definition.Id===step.CorridorGroupId);
            const segment=group.Segments.find(s=>s.Id===step.CorridorSegmentId),forward=step.CorridorForward;
            assert.deepEqual(step.From,forward?segment.From:segment.To);assert.deepEqual(step.To,forward?segment.To:segment.From);
            assert.ok((forward?segment.ForwardJourneys:segment.ReverseJourneys).includes(step.RouteId));
            assert.equal(group.Journeys.find(j=>j.Id===step.RouteId).ConnectionId,step.ConnectionId);
            const prev=path[i-1];
            if(prev?.CorridorSegmentId&&prev.RegionId===step.RegionId&&prev.CorridorGroupId===step.CorridorGroupId){
                const junction=group.Junctions.find(j=>j.Position.X===step.From.X&&j.Position.Y===step.From.Y);
                assert.ok(junction.Transitions.some(t=>t.FromSegmentId===prev.CorridorSegmentId&&t.FromForward===prev.CorridorForward&&
                    t.ToSegmentId===step.CorridorSegmentId&&t.ToForward===forward));
            }
        }
        assert.ok(corridorCount>0);
    }
    check(journey.Forward,journey.From,journey.To);check(journey.Return,journey.To,journey.From);
});
