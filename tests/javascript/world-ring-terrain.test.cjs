const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_RING_TERRAIN_ROOT;
if(!root)throw new Error('WORLD_RING_TERRAIN_ROOT must identify generated world fixtures');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-metadata.json'),'utf8'));
test('terrain-driven ring alternatives retain all sites and paths in saved views',()=>{
    const a=read('original'),b=read('moved');
    for(const world of [a,b]){
        assert.equal(S.layout(world).boxes.length,5);
        assert.equal(world.AccessPlan.Sites.length,4);
        assert.ok(world.RoutePlan.Routes[0].Forward.Path&&world.RoutePlan.Routes[0].Reverse.Path);
    }
    assert.equal(a.PlacementPlan.PopulationAdaptation.Attempts.length,2);
    assert.equal(b.PlacementPlan.PopulationAdaptation.Attempts.length,1);
    assert.notDeepEqual(a.Nodes.map(n=>n.Position),b.Nodes.map(n=>n.Position));
    const corrupted=structuredClone(a);corrupted.PlacementPlan.PopulationAdaptation.Attempts[1].ChoiceId='changed';
    assert.throws(()=>S.layout(corrupted),/population adaptation/);
});
