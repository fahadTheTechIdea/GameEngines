const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const read=(name)=>JSON.parse(fs.readFileSync(`generated/world-programs/${name}/world-metadata.json`));
const near=(actual,expected)=>{assert.equal(actual.length,expected.length);actual.forEach((v,i)=>assert.ok(Math.abs(v-expected[i])<1e-7));};
test('nested floor and room coordinates preserve the authored bent route and vertical floor offset',()=>{
 const w=read('dungeon');assert.equal(w.Nodes.length,15);assert.equal(w.Connections.length,12);
 for(let floor=0;floor<2;floor++)for(let room=0;room<6;room++){
  const d=room*12;near(w.Nodes.find(n=>n.Id===`dungeon/floors-${floor}/rooms-${room}`).Position,[Math.min(36,d),Math.max(0,d-36),-8*floor]);
 }
 assert.ok(w.Connections.some(c=>c.FromId==='dungeon/floors-0/rooms-5'&&c.ToId==='dungeon/floors-1/rooms-0'));
});
test('side-scrolling sections follow a path bend instead of repeated blocks',()=>{
 const w=read('platformer');assert.equal(w.Nodes.length,17);
 for(let level=0;level<4;level++){
  near(w.Nodes.find(n=>n.Id===`platformer/levels-${level}/sections-0`).Position,[level*200,0]);
  near(w.Nodes.find(n=>n.Id===`platformer/levels-${level}/sections-1`).Position,[level*200+30,10]);
  near(w.Nodes.find(n=>n.Id===`platformer/levels-${level}/sections-2`).Position,[level*200+50,30]);
 }
 assert.ok(w.Connections.every(c=>!c.Bidirectional));
});
test('3D system parts lie on the selected x/z plane around their own parent',()=>{
 const w=read('space');assert.equal(w.Nodes.length,9);
 for(let system=0;system<2;system++){
  const parent=w.Nodes.find(n=>n.Id===`space/systems-${system}`).Position;
  for(let planet=0;planet<3;planet++){
   const angle=planet*2*Math.PI/3;
   near(w.Nodes.find(n=>n.Id===`space/systems-${system}/planets-${planet}`).Position,[parent[0]+50*Math.cos(angle),parent[1],parent[2]+50*Math.sin(angle)]);
  }
 }
});
test('all exported programs keep hierarchy and endpoint integrity without urban prerequisites',()=>{
 for(const name of ['dungeon','platformer','space']){
  const w=read(name),ids=new Set(w.Nodes.map(n=>n.Id));
  assert.equal(ids.size,w.Nodes.length);assert.equal(w.Nodes.filter(n=>n.ParentId===null).length,1);
  for(const n of w.Nodes){assert.ok(n.ParentId===null||ids.has(n.ParentId));assert.ok(!['block','parcel','district'].includes(n.Kind));assert.ok(n.Position.every(Number.isFinite));}
  for(const c of w.Connections){assert.ok(ids.has(c.FromId));assert.ok(ids.has(c.ToId));}
  assert.equal(w.MapGeometry,null); // Coordinate programs do not claim physical terrain or room geometry.
 }
});
