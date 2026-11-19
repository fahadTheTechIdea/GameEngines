const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const folder=process.env.WORLD_ROUNDED_NETWORK;
if(!folder)throw new Error('WORLD_ROUNDED_NETWORK must identify a generated world');
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-regions.json'),'utf8'));
test('regional viewer retains generated network and rounded edge paths',()=>{
    assert.equal(R.layout(world).boxes.length,20);
    assert.equal(world.Connections.length,3);
    assert.ok(world.Connections.some(c=>c.Forward.RoundingWitness||c.Reverse.RoundingWitness));
    const broken=structuredClone(world),edge=broken.Connections.find(c=>c.Forward.RoundingWitness||c.Reverse.RoundingWitness);
    const direction=edge.Forward.RoundingWitness?'Forward':'Reverse';
    edge[direction].RoundingWitness.SourceCost.Distance++;
    const network=broken.NetworkPlan.Networks[0],index=network.Links.findIndex(x=>x.RegionalConnectionId===edge.Definition.Id);
    network.Plan.Edges[index][direction].RoundingWitness.SourceCost.Distance++;
    assert.throws(()=>R.layout(broken),/Invalid regional schematic/);
});
