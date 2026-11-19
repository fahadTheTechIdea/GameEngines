const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/source-surroundings-identity';
const read=p=>JSON.parse(fs.readFileSync(p));
const world=read(`${folder}/world-metadata.json`);
test('world site polygons retain exact C# geometry and source dimensions',()=>{
  const p=Schematic.layout(world),entries=p.suitability.get('surroundings-world/sites-0');
  assert.equal(p.boxes.length,2);assert.equal(entries[0].Placement.SiteFeatures.length,2);
  assert.deepEqual(entries[0].Placement.SiteFeatures,world.PlacementPlan.SiteFeatures);
  const garden=entries[0].Placement.SiteFeatures.find(f=>f.Kind==='Garden').Boundary.Vertices;
  assert.ok(Math.abs(Math.hypot(garden[1].X-garden[0].X,garden[1].Y-garden[0].Y)-11.5)<1e-8);
});
test('missing, duplicate, moved and forged provenance refuses display',()=>{
  for(const edit of [w=>w.PlacementPlan.SiteFeatures.pop(),w=>w.PlacementPlan.SiteFeatures.push(w.PlacementPlan.SiteFeatures[0]),
    w=>w.PlacementPlan.SiteFeatures[0].Boundary.Vertices[0].X++,w=>w.PlacementPlan.SiteFeatures[0].SourceRuleId='forged',
    w=>w.PlacementPlan.Footprints[0].SiteFeatures=null,w=>w.PlacementPlan.Definition.Bindings[0].SiteLayout=null]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('optional surroundings do not change earlier placement fixtures',()=>{
  for(const [packet,count] of [['clearance-world-reviewed',3],['capacity-world-final',10],['placement-feedback-final',4]])
    assert.equal(Schematic.layout(read(`generated/${packet}/world-metadata.json`)).boxes.length,count);
});
test('packet contains current renderer and exact saved engine data',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
