const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_ROUNDING_ALTERNATIVES;
if(!root)throw new Error('WORLD_REGIONAL_ROUNDING_ALTERNATIVES must identify the generated world');
const world=JSON.parse(fs.readFileSync(path.join(root,'world-regions.json'),'utf8'));
test('regional world retains rejected and accepted rounding attempts in each direction',()=>{
    assert.equal(R.layout(world).boxes.length,10);
    for(const route of [world.Connections[0].Forward,world.Connections[0].Reverse])
        assert.deepEqual(route.RoundingAttempts.map(a=>a.Status),['TraceRejected','Found']);
    const changed=structuredClone(world);changed.Connections[0].Forward.RoundingAttempts[0].Status='Found';
    assert.throws(()=>R.layout(changed),/Invalid regional schematic/);
});
