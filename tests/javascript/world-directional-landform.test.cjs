const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_DIRECTIONAL_LANDFORM;
if(!root)throw new Error('WORLD_DIRECTIONAL_LANDFORM must identify the generated terrain fixture');
const world=JSON.parse(fs.readFileSync(path.join(root,'DirectionalCapture/world-metadata.json'),'utf8'));
test('captured directional landform retains source terrain, sites and paths',()=>{
    const view=S.layout(world);assert.equal(view.boxes.length,2);assert.equal(world.AccessPlan.Sites.length,1);
    assert.equal(world.Surface.Terrain.Operations[0].AxisStretch,2);
    assert.ok(world.Surface.TerrainCapturePlan.Window&&world.RoutePlan.Routes[0].Reverse.Path);
    const bad=structuredClone(world);bad.Surface.TerrainCapturePlan.Window.Work--;
    assert.throws(()=>S.layout(bad),/terrain capture/i);
});
