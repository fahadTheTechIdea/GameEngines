const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_TERRAIN_CAPTURE_PACKET||'generated/metadata-capture-event-identity-packet';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const mode of ['baseline','edited'])test(`${mode}: saved terrain capture preserves every source, path and original lattice index`,()=>{
 const world=read(`${mode}/world-metadata.json`),before=JSON.stringify(world),plan=S.layout(world),w=world.Surface.TerrainCapturePlan.Window,f=w.Source;
 assert.equal(plan.boxes.length,24);assert.equal(world.AccessPlan.Sites.length,21);assert.equal(plan.heightField.values.length,1584);
 assert.equal(plan.heightField.firstRow,1);assert.deepEqual(plan.heightField.source,w);
 for(const t of S.heightTriangles(plan.heightField))for(const p of t.points){
  const col=Math.round((p.x-f.Origin.X)/f.StepX),row=Math.round((p.y-f.Origin.Y)/f.StepY);
  assert.equal(p.x,f.Origin.X+col*f.StepX);assert.equal(p.y,f.Origin.Y+row*f.StepY);
  assert.equal(p.height,w.Heights[(row-w.FirstRow)*w.Columns+col-w.FirstColumn]);
  const expected=f.BaseElevation+f.GradientX*(col*f.StepX)+f.GradientY*(row*f.StepY);
  assert.ok(Math.abs(p.height-expected)<1e-12);
 }
 let directions=0;for(const n of world.NetworkPlan.Networks)for(const e of n.Plan.Edges)for(const r of [e.Forward,e.Reverse]){
  assert.deepEqual(plan.routes.find(p=>p.id===r.Id).points,r.Path.Points.map(p=>({x:p.X,y:p.Y})));directions++;
 }
 assert.equal(directions,12);assert.equal(JSON.stringify(world),before);
 assert.equal(fs.readFileSync(path.join(root,mode,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const html=fs.readFileSync(path.join(root,mode,'schematic.html'),'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
});
test('neighboring capture displays globally indexed overlapping samples without rebasing',()=>{
 const surface=read('neighbor-surface.json'),field=S.surfaceHeightField(surface),w=surface.TerrainCapturePlan.Window,f=w.Source;
 assert.ok(field.firstColumn>0);assert.equal(field.origin.x,f.Origin.X+w.FirstColumn*f.StepX);
 const original=read('baseline/world-metadata.json').Surface.TerrainCapturePlan.Window;let shared=0;
 for(const t of S.heightTriangles(field))for(const p of t.points){
  const col=Math.round((p.x-f.Origin.X)/f.StepX),row=Math.round((p.y-f.Origin.Y)/f.StepY);
  if(col>=original.FirstColumn&&col<original.FirstColumn+original.Columns&&row>=original.FirstRow&&row<original.FirstRow+original.Rows){
   assert.equal(p.height,original.Heights[(row-original.FirstRow)*original.Columns+col-original.FirstColumn]);shared++;
  }
 }
 assert.ok(shared>0);
});
test('capture pairing, identities, dimensions, work, absent samples and nonfinite values refuse',()=>{
 const world=read('baseline/world-metadata.json');
 for(const change of [s=>delete s.TerrainCapture,s=>delete s.TerrainCapturePlan,s=>s.TerrainCapturePlan.Window.Heights.pop(),
  s=>s.TerrainCapturePlan.Window.FirstRow++,s=>s.TerrainCapturePlan.Window.Work++,s=>s.TerrainCapturePlan.Window.SourceRecipeSha256='0'.repeat(64),
  s=>s.TerrainCapturePlan.Window.Heights[0]=Infinity,s=>s.TerrainCapturePlan.Window=null,s=>s.TerrainCapturePlan.Version=2]){
  const bad=structuredClone(world);change(bad.Surface);assert.throws(()=>S.layout(bad));
 }
 // Presentation validates structure and preserves samples; C# source replay is
 // the authenticity authority for finite tampering and source/build changes.
});
