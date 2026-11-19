const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_FOOTPRINT_PACKET||'generated/footprint-anchor-e66abe41d2664a88b41fc59d26b83c08';
const worlds=process.env.WORLD_DIVERSE_PACKET||'generated/diverse-final-worlds';
const read=p=>JSON.parse(fs.readFileSync(p+'/world-metadata.json'));

test('slanted and protected-hole anchors retain exact source geometry and declared clearance',()=>{
 for(const [name,x,y] of [['slanted',9,8],['hole',2.9999,6]]){
  const world=read(folder+'/'+name+'/world'),before=JSON.stringify(world),view=S.layout(world),proof=world.PlacementPlan.FootprintAnchorResults[0].Plan;
  assert.equal(view.boxes.length,2);assert.ok(Math.abs(proof.Position.X-x)<1e-8&&Math.abs(proof.Position.Y-y)<1e-8);
  assert.equal(JSON.stringify(world),before);assert.ok(proof.Contacts>0&&proof.Candidates>0);
 }
});
test('missing, duplicate, moved, unbounded and mismatched footprint witnesses refuse display',()=>{
 const base=read(folder+'/slanted/world');
 for(const change of [p=>p.FootprintAnchorResults=[],p=>p.FootprintAnchorResults.push(p.FootprintAnchorResults[0]),
  p=>p.FootprintAnchorResults[0].Plan.Position.X++,p=>p.FootprintAnchorResults[0].DemandId='missing',
  p=>p.FootprintAnchorResults[0].Plan.Preferred.X++,p=>p.FootprintAnchorResults[0].Plan.Candidates=20000,
  p=>p.FootprintAnchorResults[0].Plan.FootprintSha256='invalid',p=>p.FootprintAnchorResults[0].Plan.Work=0,
  p=>p.TemplateDefinition.AllocationAnchors[0].Interior={Precision:.1,MaximumCells:65536,MinimumClearance:0},
  p=>p.TemplateDefinition.AllocationAnchors[0].Footprint.Search.MinimumClearance=-1]){
  const world=structuredClone(base);change(world.PlacementPlan);assert.throws(()=>S.layout(world));
 }
});
test('whole diverse worlds retain every source object, landscape member and directed path',()=>{
 for(const name of ['inland','coastal']){
  const world=read(worlds+'/'+name+'/world'),before=JSON.stringify(world),view=S.layout(world);
  assert.equal(view.boxes.length,61);assert.equal(view.routes.length,114);
  assert.equal(world.PlacementPlan.FootprintAnchorResults.length,7);
  const kinds=k=>world.Nodes.filter(n=>n.Kind===k).length;
  assert.deepEqual(['compound','structure','ground-area','equipment','environment-feature'].map(kinds),[7,13,9,13,18]);
  assert.equal(JSON.stringify(world),before);
 }
});
test('fresh export embeds the current viewer and the exact generated document',()=>{
 for(const name of ['inland','coastal']){
  const p=worlds+'/'+name+'/world',world=read(p);
  assert.equal(fs.readFileSync(p+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const data=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(p+'/schematic.html','utf8'));
  assert.deepEqual(JSON.parse(data[1]),world);
 }
});
