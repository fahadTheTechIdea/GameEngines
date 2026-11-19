const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_PROGRESSION_SELECTION;
if(!root)throw new Error('WORLD_REGIONAL_PROGRESSION_SELECTION must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('selected program connects the local entry to the composed regional world',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json');
    assert.equal(R.layout(world).boxes.length,20);
    assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);
    assert.match(receipt.Attempts[0].Rejection,/^METADATA_PROGRESSION_UNREACHABLE:/);
    const path=receipt.Attempts[1].WorldProgression.Results[0].Paths[0];
    assert.equal(path.Nodes[0].RegionId,'arrival');assert.equal(path.Nodes.at(-1).RegionId,'outpost');
    assert.equal(path.Nodes.length,path.Links.length+1);
    for(let i=0;i<path.Links.length;i++){
        const ref=path.Links[i],a=path.Nodes[i],b=path.Nodes[i+1];
        const same=(x,y)=>x.RegionId===y.RegionId&&x.NodeId===y.NodeId;
        if(ref.RegionId===null){
            const edge=world.Connections.find(c=>c.Definition.Id===ref.ConnectionId).Definition;
            assert.ok(same(edge.From,a)&&same(edge.To,b)||edge.Bidirectional&&same(edge.To,a)&&same(edge.From,b));
        }else{
            const region=world.Regions.find(r=>r.RegionId===ref.RegionId),edge=region.World.Connections.find(c=>c.Id===ref.ConnectionId);
            assert.equal(a.RegionId,ref.RegionId);assert.equal(b.RegionId,ref.RegionId);
            assert.ok(edge.FromId===a.NodeId&&edge.ToId===b.NodeId||edge.Bidirectional&&edge.ToId===a.NodeId&&edge.FromId===b.NodeId);
        }
    }
    assert.equal(world.Connections.length,3);assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
});
