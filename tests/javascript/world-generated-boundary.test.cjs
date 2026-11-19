const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.BOUNDARY_PACKET||'generated/generated-boundary-owner';
for(const name of ['nested','translated','wider','footprint','reservation','region','hole','allocation','disconnected-allocation'])test(name+': preserves all source-derived surroundings witnesses',()=>{
 const world=JSON.parse(fs.readFileSync(folder+'/'+name+'/world/world-metadata.json')),before=JSON.stringify(world),view=S.layout(world);
 assert.equal(view.boxes.length,world.Nodes.length);assert.equal(JSON.stringify(world),before);
 assert.deepEqual(view.populationBoundaries,world.PlacementPlan.PopulationBoundaries);
 for(const t of view.populationBoundaries)for(let i=0;i<t.NodeIds.length;i++){
  const n=world.Nodes.find(n=>n.Id===t.NodeIds[i]);assert.deepEqual(n.Position,[t.Boundary.Positions[i].X,t.Boundary.Positions[i].Y]);
 }
});
test('missing and altered boundary receipts refuse display',()=>{
 const original=JSON.parse(fs.readFileSync(folder+'/nested/world/world-metadata.json'));
 for(const change of [w=>w.PlacementPlan.PopulationBoundaries=null,w=>w.PlacementPlan.PopulationBoundaries[0].Boundary.Positions[0].X+=1,
  w=>w.PlacementPlan.PopulationBoundaries[0].Boundary.Pattern.Side='Inside',w=>w.PlacementPlan.PopulationBoundaries[0].Boundary.LoopCount=0,
  w=>w.PlacementPlan.PopulationBoundaries[0].Boundary.SourceGeometrySha256='invalid',w=>w.PlacementPlan.PopulationBoundaries[0].NodeIds[1]=w.PlacementPlan.PopulationBoundaries[0].NodeIds[0],
  w=>w.PlacementPlan.PopulationBoundaries[0].Boundary.SourceNodeId=w.Id]){
  const w=structuredClone(original);change(w);assert.throws(()=>S.layout(w),/generated boundary/);
 }
});
