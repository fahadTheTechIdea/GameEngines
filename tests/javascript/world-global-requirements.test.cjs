const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_GLOBAL_REQUIREMENTS;
if(!root)throw new Error('WORLD_GLOBAL_REQUIREMENTS must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('world-level witnesses match physical parents and parts across generated regions',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.equal(R.layout(world).boxes.length,20);
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    const counts=receipt.Attempts[1].WorldCounts,all=counts.find(c=>c.RequirementId==='all-modules');
    const each=counts.find(c=>c.RequirementId==='each-compound');
    let total=0;
    for(const region of world.Regions){
        const sites=new Set(region.World.AccessPlan.Sites.map(s=>s.NodeId));
        const parents=region.World.Nodes.filter(n=>n.Kind==='compound'&&n.ArchetypeId==='compound');
        const modules=region.World.Nodes.filter(n=>n.Kind==='module'&&n.ArchetypeId==='module'&&sites.has(n.Id));
        total+=modules.length;assert.equal(parents.length,1);
        const children=modules.filter(n=>n.ParentId===parents[0].Id);
        assert.equal(children.length,2);
        const witness=each.Regions.find(r=>r.RegionId===region.RegionId).Scopes.find(s=>s.ParentNodeId===parents[0].Id);
        assert.equal(witness.Count,children.length);assert.equal(witness.Satisfied,true);
        assert.equal(all.Regions.find(r=>r.RegionId===region.RegionId).Count,modules.length);
    }
    assert.equal(total,8);assert.equal(all.Count,total);assert.ok(all.Satisfied&&each.Satisfied);
    assert.equal(world.Connections.length,3);assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
});
