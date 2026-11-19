const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.IRREGULAR_SITES_ROOT,rectangleBaseline=process.env.IRREGULAR_RECTANGLE_BASELINE,terrainRoot=process.env.IRREGULAR_TERRAIN_ROOT;
if(!root||!rectangleBaseline||!terrainRoot)throw new Error('IRREGULAR_SITES_ROOT,IRREGULAR_RECTANGLE_BASELINE and IRREGULAR_TERRAIN_ROOT must identify explicit publications/immutable regression control');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
test('actual irregular plots retain complete physical hierarchy and source polygons in saved inspection',()=>{
 for(const name of ['angle-0','angle-30','angle-90','angle-180','edited']){
  const world=read(path.join(root,name,'world-metadata.json')),before=JSON.stringify(world),view=S.layout(world);
  assert.equal(view.boxes.length,4);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,3);
  for(const box of view.boxes.filter(b=>b.physicalAccess))assert.deepEqual(box.physicalAccess,world.AccessPlan.Sites.find(s=>s.NodeId===box.node.Id));
  assert.deepEqual(view.world.PlacementPlan.SiteFeatures,world.PlacementPlan.SiteFeatures);assert.equal(world.PlacementPlan.SiteFeatures.length,4);
  assert.equal(world.Nodes.filter(n=>n.Kind==='fixture').length,2);assert.equal(JSON.stringify(world),before);
 }
});
test('complete packets embed the exact actual world and current inspection code',()=>{
 for(const name of ['angle-0','angle-30','angle-90','angle-180','edited']){
  const folder=path.join(root,name),html=fs.readFileSync(path.join(folder,'schematic.html'),'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),read(path.join(folder,'world-metadata.json')));
  assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
test('prior/current rectangular worlds are exact and all invalid irregular requests leave no publication',()=>{
 assert.deepEqual(read(path.join(rectangleBaseline,'world-metadata.json')),read(path.join(root,'current-rectangle/world-metadata.json')));
 const runs=read(root+'-runs.json');
 for(const name of ['concave','extended','narrow-access','false-bounds','oversized-child']){
  const run=runs.find(r=>r.Id==='refuse-'+name);assert.ok(run);assert.equal(run.ExitCode,1);assert.equal(run.OutputAbsent,true);
  assert.equal(fs.existsSync(path.join(root,'refused-'+name)),false);
 }
});
test('an excluded point outside the actual plot retains complete inspection geometry',()=>{
 const world=read(path.join(terrainRoot,'outside-gap/world-metadata.json')),view=S.layout(world),base=read(path.join(root,'angle-30/world-metadata.json'));
 assert.equal(view.boxes.length,4);assert.deepEqual(world.PlacementPlan.SiteFeatures,base.PlacementPlan.SiteFeatures);
 assert.equal(fs.existsSync(path.join(terrainRoot,'inside-yard')),false);
});
