const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_PROGRESSION;
if(!root)throw new Error('WORLD_REGIONAL_PROGRESSION must identify the generated design directory');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,'world',name),'utf8'));
test('saved progression witness follows the generated directed regional links',()=>{
    const world=read('world-regions.json'),receipt=read('world-design.json'),attempt=receipt.Attempts.at(-1);
    assert.equal(R.layout(world).boxes.length,20);assert.equal(attempt.Accepted,true);
    const witness=attempt.WorldProgression.Results[0].Paths[0];
    assert.equal(witness.Nodes[0].RegionId,'arrival');assert.equal(witness.Nodes.at(-1).RegionId,'outpost');
    assert.ok(witness.Nodes.some(n=>n.RegionId==='grove'));
    assert.equal(witness.Nodes.length,witness.Links.length+1);
    for(let i=0;i<witness.Links.length;i++){
        const link=witness.Links[i];assert.equal(link.RegionId,null);
        const route=world.Connections.find(c=>c.Definition.Id===link.ConnectionId).Definition;
        const a=witness.Nodes[i],b=witness.Nodes[i+1],matches=(x,y)=>x.RegionId===y.RegionId&&x.NodeId===y.NodeId;
        assert.ok(matches(route.From,a)&&matches(route.To,b)||route.Bidirectional&&matches(route.To,a)&&matches(route.From,b));
    }
});
