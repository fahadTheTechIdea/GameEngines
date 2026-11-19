const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const folder=process.env.WORLD_FENCE_PACKET||'generated/fence-assembly-example-cli';
const S=require(path.resolve(folder,'schematic.js'));
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'),'utf8'));
test('complete physical fence and gate route reach the saved schematic without regenerated placement',()=>{
 const before=JSON.stringify(world),view=S.layout(world);
 assert.equal(view.boxes.length,67);assert.equal(world.AccessPlan.Sites.length,63);
 assert.equal(view.boxes.filter(b=>b.physicalAccess?.Occupancy==='Solid').length,61);
 assert.equal(world.PlacementPlan.PopulationBoundaries.reduce((n,t)=>n+t.NodeIds.length,0),61);
 assert.equal(world.RoutePlan.Routes.length,1);assert.ok(world.RoutePlan.Routes[0].Reverse);
 assert.equal(JSON.stringify(world),before);
 assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('changed fence poses cannot masquerade as the saved boundary assembly',()=>{
 const changed=structuredClone(world);changed.Nodes.find(n=>n.Kind==='panel').Position[0]+=.5;
 assert.throws(()=>S.layout(changed));
 const movedWitness=structuredClone(world);movedWitness.PlacementPlan.PopulationBoundaries[0].Boundary.Positions[0].X+=.5;
 assert.throws(()=>S.layout(movedWitness));
});
