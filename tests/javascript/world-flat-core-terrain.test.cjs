const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_FLAT_CORE_TERRAIN;
if(!root)throw new Error('WORLD_FLAT_CORE_TERRAIN must identify the generated terrain fixture');
const world=JSON.parse(fs.readFileSync(path.join(root,'FlatCoreCapture/world-metadata.json'),'utf8'));
test('saved schematic accepts flat-core terrain and rejects an incorrect work witness',()=>{
    assert.equal(world.Surface.Terrain.Operations[0].InnerRadius,3);
    assert.equal(S.layout(world).boxes.length,2);
    assert.equal(world.AccessPlan.Sites.length,1);
    assert.ok(world.RoutePlan.Routes[0].Forward.Path&&world.RoutePlan.Routes[0].Reverse.Path);
    const altered=structuredClone(world);altered.Surface.TerrainCapturePlan.Window.Work--;
    assert.throws(()=>S.layout(altered),/terrain capture/i);
});
