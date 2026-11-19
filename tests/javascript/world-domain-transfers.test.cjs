const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_DOMAIN_TRANSFERS;
if(!root)throw new Error('WORLD_DOMAIN_TRANSFERS must identify the generated fixture');
test('qualified shared vertex needs an explicit connection between route tracks',()=>{
    assert.equal(fs.existsSync(path.join(root,'closed')),false);
    const world=JSON.parse(fs.readFileSync(path.join(root,'open/world-domains.json'),'utf8'));
    const local=world.Domains.find(d=>d.Id==='physical').World,journey=world.Journeys[0];
    const a=local.RoutePlan.Routes.find(r=>r.ConnectionId.endsWith('/corridor-a')).Forward.Path.Points;
    const b=local.RoutePlan.Routes.find(r=>r.ConnectionId.endsWith('/corridor-b')).Forward.Path.Points;
    assert.ok(a.some(p=>p.X===25.5&&p.Y===22));assert.ok(b.some(p=>p.X===25.5&&p.Y===22));
    for(const [steps,reverse] of [[journey.Forward,false],[journey.Return,true]]){
        assert.ok(steps.some(s=>s.Kind==='route-segment'&&s.SourceId.includes('/direct/')));
        assert.ok(steps.some(s=>s.Kind==='transition'&&s.SourceId==='portal'&&s.Reverse===reverse));
        for(const step of steps.filter(s=>s.Kind==='route-segment')){
            const route=local.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).find(r=>r?.Id===step.SourceId);
            assert.ok(route);assert.ok(step.SegmentIndex>=0&&step.SegmentIndex+1<route.Path.Points.length);
        }
    }
});
