const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_TERRAIN_PACKET||'generated/landform-terrain';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('landform packet preserves exact engine samples and domain without adding physical layout',()=>{
  const plan=Schematic.layout(world),f=world.Surface.HeightField;
  assert.deepEqual(plan.heightField.source,f);
  assert.deepEqual(plan.boundary,world.Surface.Boundary.Vertices.map(v=>({x:v.X,y:v.Y})));
  assert.equal(plan.heightField.source.Heights.length,1089);
  assert.equal(plan.routes.length,0);assert.equal(plan.corridors.length,0);
  const triangles=[...Schematic.heightTriangles(plan.heightField)];
  assert.equal(triangles.length,2048);
  for(const triangle of triangles)for(const p of triangle.points){
    const x=(p.x-f.Origin.X)/f.StepX,y=(p.y-f.Origin.Y)/f.StepY;
    assert.equal(p.height,f.Heights[y*f.Columns+x]);
  }
});
test('presentation triangle height and slope agree with an independent analytic plane',()=>{
  const f={...world.Surface.HeightField,Origin:{X:-10,Y:20},StepX:2,StepY:3,Columns:3,Rows:4,
    Heights:Array.from({length:12},(_,i)=>7+.25*(i%3)*2-.5*Math.floor(i/3)*3)};
  const triangles=[...Schematic.heightTriangles(Schematic.heightField(f,'meters'))];
  for(const t of triangles){
    const cx=t.points.reduce((s,p)=>s+p.x,0)/3,cy=t.points.reduce((s,p)=>s+p.y,0)/3;
    assert.ok(Math.abs(t.elevation-(7+.25*(cx+10)-.5*(cy-20)))<1e-10);
    assert.ok(Math.abs(t.slope-Math.hypot(.25,.5))<1e-10);
  }
});
test('malformed and missing generated fields reject; export packages current presentation and layer control',()=>{
  for(const change of [w=>w.Surface.HeightField.Heights.pop(),w=>w.Surface.HeightField.StepX=0,
    w=>w.Surface.HeightField.Heights[0]=Infinity,w=>w.Surface.HeightField=null,w=>w.Surface.HeightField.Columns=514]){
    const bad=structuredClone(world);change(bad);assert.throws(()=>Schematic.layout(bad));
  }
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');assert.match(html,/id="terrain-field-view"/);
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
});
