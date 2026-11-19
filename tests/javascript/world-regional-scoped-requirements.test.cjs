const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_SCOPED;
if(!root)throw new Error('WORLD_REGIONAL_SCOPED must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('selected region contains every required physical child of each declared parent',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.equal(R.layout(world).boxes.length,20);
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    const region=world.Regions.find(r=>r.RegionId==='arrival').World;
    const sites=new Set(region.AccessPlan.Sites.map(s=>s.NodeId)),parents=region.Nodes.filter(n=>n.Kind==='compound');
    assert.equal(parents.length,1);
    for(const parent of parents){
        const children=region.Nodes.filter(n=>n.ParentId===parent.Id&&n.Kind==='module'&&sites.has(n.Id));
        assert.equal(children.length,2);
        const proof=receipt.Attempts[1].Counts[0].Scopes.find(s=>s.ParentNodeId===parent.Id);
        assert.equal(proof.Count,children.length);assert.equal(proof.Satisfied,true);
    }
});
test('three complete physical parents retain their source-sized parts and journeys',()=>{
    const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
    const world=JSON.parse(fs.readFileSync(path.join(root,'multiple/world-metadata.json'),'utf8'));
    assert.equal(S.layout(world).boxes.length,11);
    const parents=world.Nodes.filter(n=>n.Kind==='compound'),sites=new Set(world.AccessPlan.Sites.map(s=>s.NodeId));
    assert.equal(parents.length,3);
    assert.equal(parents.filter(n=>n.ArchetypeId==='wide').length,1);
    for(const parent of parents)assert.equal(world.Nodes.filter(n=>n.ParentId===parent.Id&&n.Kind==='module'&&sites.has(n.Id)).length,2);
    assert.equal(world.RoutePlan.Routes.length,3);
    assert.ok(world.RoutePlan.Routes.every(r=>r.Forward.Path&&r.Reverse.Path));
    assert.equal(world.NetworkPlan.Networks[0].Plan.Edges.length,2);
});
