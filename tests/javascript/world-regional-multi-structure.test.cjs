const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_MULTI_STRUCTURE;
if(!root)throw new Error('WORLD_REGIONAL_MULTI_STRUCTURE must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('regional selection preserves each complete physical structure and its journeys',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.equal(R.layout(world).boxes.length,23);
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    assert.deepEqual(receipt.Attempts[0].Counts[0].Scopes.map(s=>s.Count).sort(),[1,2]);
    const region=world.Regions.find(r=>r.RegionId==='arrival').World;
    const sites=new Set(region.AccessPlan.Sites.map(s=>s.NodeId)),parents=region.Nodes.filter(n=>n.Kind==='compound');
    assert.equal(parents.length,2);assert.equal(sites.size,6);
    for(const parent of parents){
        const children=region.Nodes.filter(n=>n.ParentId===parent.Id&&n.Kind==='module'&&sites.has(n.Id));
        assert.equal(children.length,2);
        const proof=receipt.Attempts[1].Counts[0].Scopes.find(s=>s.ParentNodeId===parent.Id);
        assert.equal(proof.Count,2);assert.equal(proof.Satisfied,true);
    }
    assert.equal(region.RoutePlan.Routes.length,2);
    assert.ok(region.RoutePlan.Routes.every(r=>r.Forward.Path&&r.Reverse.Path));
    assert.equal(world.Connections.length,3);
    assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
});
