const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_RING_ROOT;
if(!root)throw new Error('WORLD_RING_ROOT must identify the generated ring fixture');
const world=JSON.parse(fs.readFileSync(path.join(root,'ring/world-metadata.json'),'utf8'));
test('ring physical sites and paths are retained by the schematic',()=>{
    assert.equal(S.layout(world).boxes.length,5);
    assert.equal(world.AccessPlan.Sites.length,4);
    assert.ok(world.RoutePlan.Routes[0].Forward.Path&&world.RoutePlan.Routes[0].Reverse.Path);
    for(const node of world.Nodes.filter(n=>n.Kind==='site'))
        assert.ok(Math.abs(Math.hypot(node.Position[0]-50,node.Position[1]-50)-10)<1e-7);
    const bad=structuredClone(world);bad.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral.StartRadius=0;
    assert.throws(()=>S.layout(bad),/spiral population/);
});
if(process.env.WORLD_RING_FACING_ROOT)test('tangent-facing ring survives saved viewer checks',()=>{
    const faced=JSON.parse(fs.readFileSync(path.join(process.env.WORLD_RING_FACING_ROOT,'Ring/world-metadata.json'),'utf8'));
    assert.equal(S.layout(faced).boxes.length,5);
    assert.equal(faced.PlacementPlan.PopulationOrientations[0].Facing,'Along');
    const bad=structuredClone(faced);bad.PlacementPlan.PopulationOrientations[0].TangentDegrees[0]++;
    assert.throws(()=>S.layout(bad),/population orientation/);
});
