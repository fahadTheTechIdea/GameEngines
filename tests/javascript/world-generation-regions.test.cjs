const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_GENERATION_REGIONS_PACKET||'generated/regional-content-verified-cli-whole';
const world=JSON.parse(fs.readFileSync(path.join(root,'world-regions.json'),'utf8'));
test('each published region preserves its complete original-coordinate metadata and exact saved samples',()=>{
 let nodes=0,paths=0,vertices=0;
 for(let i=0;i<world.Regions.length;i++){
  const r=world.Regions[i],dir=path.join(root,`region-${i.toString().padStart(4,'0')}`),w=JSON.parse(fs.readFileSync(path.join(dir,'world-metadata.json'),'utf8'));
  assert.deepEqual(w,r.World);const before=JSON.stringify(w),plan=S.layout(w),window=w.Surface.TerrainCapturePlan.Window;
  assert.equal(plan.boxes.length,5);nodes+=plan.boxes.length;
  assert.deepEqual(plan.boundary,w.Surface.Boundary.Vertices.map(p=>({x:p.X,y:p.Y})));
  assert.deepEqual(plan.heightField.source,window);
  assert.equal(fs.readFileSync(path.join(dir,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const html=fs.readFileSync(path.join(dir,'schematic.html'),'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
  for(const entry of w.RoutePlan.Routes)for(const route of [entry.Forward,entry.Reverse]){
   assert.deepEqual(plan.routes.find(r=>r.id===route.Id).points,route.Path.Points.map(p=>({x:p.X,y:p.Y})));paths++;
  }
  for(const t of S.heightTriangles(plan.heightField))for(const p of t.points){
   const f=window.Source,col=Math.round((p.x-f.Origin.X)/f.StepX),row=Math.round((p.y-f.Origin.Y)/f.StepY);
   assert.equal(p.x,f.Origin.X+col*f.StepX);assert.equal(p.y,f.Origin.Y+row*f.StepY);
   assert.equal(p.height,window.Heights[(row-window.FirstRow)*window.Columns+col-window.FirstColumn]);vertices++;
  }
  assert.equal(JSON.stringify(w),before);
 }
 assert.equal(nodes,10);assert.equal(paths,4);assert.ok(vertices>10000);
});
test('whole and reversed-order loaded assembly retain byte-identical complete world data',()=>{
 assert.deepEqual(fs.readFileSync(path.join(root,'world-regions.json')),fs.readFileSync(path.join(process.env.WORLD_GENERATION_REGIONS_ASSEMBLED_PACKET||'generated/regional-content-verified-cli-assembled','world-regions.json')));
 assert.equal(world.Members.length,new Set(world.Members.map(m=>JSON.stringify([m.OwnerRegionId,m.Kind,m.Id]))).size);
 const compound=world.Members.find(m=>m.OwnerRegionId==='west'&&m.Id==='region-west/compounds-0');
 assert.deepEqual(compound.RegionIds,['east','west']);
 const route=world.Connections[0];assert.ok(route.Forward.Path.Points.length>=2&&route.Reverse.Path.Points.length>=2);
 const index=fs.readFileSync(path.join(root,'index.html'),'utf8');assert.match(index,/region-0000\/schematic\.html/);assert.match(index,/region-0001\/schematic\.html/);
 // This scope qualifies complete child packets and canonical assembly. Combined
 // rendering is checked separately in world-regional-schematic.test.cjs/browser.
});
