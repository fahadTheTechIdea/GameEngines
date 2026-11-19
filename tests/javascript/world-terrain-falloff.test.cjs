const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_TERRAIN_FALLOFF_ROOT||'generated/terrain-falloff-final-20261101';
for(const mode of ['Linear','Quadratic']) test(`${mode} terrain retains the physical world and source`,()=>{
 const world=JSON.parse(fs.readFileSync(`${root}/${mode}/world-metadata.json`,'utf8'));
 const view=S.layout(world);
 assert.equal(view.boxes.length,2);assert.equal(world.AccessPlan.Sites.length,1);
 assert.equal(world.Surface.Terrain.Operations[0].Falloff,mode);
 assert.equal(world.RoutePlan.Routes[0].Forward.Status,'Found');
 assert.equal(world.RoutePlan.Routes[0].Reverse.Status,'Found');
});
