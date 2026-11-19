const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGIONAL_INSPECTION_NATIVE||'generated/regional-inspection-native';
const scale=process.env.WORLD_REGIONAL_INSPECTION_SCALE||'generated/regional-inspection-scale';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const base=read(path.join(root,'portable/world-regions.json'));
test('combined view retains each source plan, whole seam geometry, complete identities and exact saved paths',()=>{
 const before=JSON.stringify(base),plan=R.layout(base);
 assert.equal(plan.boxes.length,10);assert.equal(new Set(plan.boxes.map(b=>b.id)).size,10);
 for(const v of plan.views){
  assert.strictEqual(v.packet,base.Regions[v.index]);assert.deepEqual(v.plan,S.layout(v.packet.World));
  for(const box of v.plan.boxes){const display=plan.boxes.find(b=>b.id===R.address(v.packet.RegionId,box.id));
   assert.strictEqual(display.node,box.node);assert.strictEqual(display.sourceBox,box);assert.equal(display.x,box.x);assert.equal(display.y,box.y);
   assert.strictEqual(display.physicalAccess,box.physicalAccess);assert.strictEqual(display.focusBounds,box.focusBounds);
  }
 }
 const m=plan.members.get(JSON.stringify(['west','Node','region-west/compounds-0']));assert.deepEqual(m.RegionIds,['east','west']);
 assert.equal(plan.terrainScale.minimum,Math.min(...plan.views.map(v=>v.plan.heightField.minimum)));
 assert.equal(plan.terrainScale.maximum,Math.max(...plan.views.map(v=>v.plan.heightField.maximum)));
 assert.equal(JSON.stringify(base),before);
});
test('same local template IDs stay independent and richer content retains every member',()=>{
 const repeated=R.layout(read(path.join(root,'repeated/world-regions.json'))),rich=R.layout(read(path.join(root,'richer/world-regions.json')));
 assert.equal(repeated.boxes.length,10);assert.equal(new Set(repeated.boxes.map(b=>b.id)).size,10);
 assert.equal(repeated.boxes.filter(b=>b.node.Id==='region-west/compounds-0').length,2);assert.equal(rich.boxes.length,11);
 assert.notEqual(R.address('a/b','c'),R.address('a','b/c'));
});
test('complete 4/8/16-region packets retain every source, directed journey and original field window',()=>{
 for(const count of [4,8,16]){
  const source=read(path.join(scale,`world-${count}.json`)),before=JSON.stringify(source),p=R.layout(source);
  assert.equal(p.boxes.length,count*5);assert.equal(p.boxes.filter(b=>b.physicalAccess).length,count*3);
  assert.equal(source.Connections.reduce((n,r)=>n+1+Number(!!r.Reverse),0),(count-1)*2);
  for(const v of p.views)assert.strictEqual(v.plan.heightField.source,v.packet.World.Surface.TerrainCapturePlan.Window);
  assert.equal(JSON.stringify(source),before);
 }
});
test('reader rejects incomplete ownership, ambiguous identities and changed regional endpoint bindings',()=>{
 const reject=change=>{const w=structuredClone(base);change(w);assert.throws(()=>R.layout(w),/Invalid regional schematic/);};
 reject(w=>w.Members.splice(w.Members.findIndex(m=>m.Kind==='Node'),1));
 reject(w=>w.Members.push(w.Members[0]));reject(w=>w.Regions.push(w.Regions[0]));
 reject(w=>w.Members[0].RegionIds=['missing']);reject(w=>w.Definition.Regions[0].GenerationDomain.Vertices[0].X++);
 reject(w=>w.Connections[0].From.X++);reject(w=>w.Connections[0].Forward.Path.Points[0].Y++);
 reject(w=>w.Connections[0].Forward.Cost.Distance++);reject(w=>w.Connections[0].Definition.Profile.Radius=0);
 reject(w=>w.Connections[0].Reverse=null);reject(w=>w.Connections[0].Definition.OwnerRegionId='missing');
 reject(w=>w.DefinitionSha256='invalid');
});
test('reader refuses differing samples on the same shared terrain lattice',()=>{
 const w=structuredClone(base),a=w.Regions[0].World.Surface.TerrainCapturePlan.Window,b=w.Regions[1].World.Surface.TerrainCapturePlan.Window;
 const col=Math.max(a.FirstColumn,b.FirstColumn),row=Math.max(a.FirstRow,b.FirstRow);
 b.Heights[(row-b.FirstRow)*b.Columns+col-b.FirstColumn]+=.25;
 assert.throws(()=>R.layout(w),/changed shared terrain sample/);
});
test('current publisher embeds complete world input and current resources, keeping per-region packets',()=>{
 const dir=path.join(root,'portable'),html=fs.readFileSync(path.join(dir,'schematic.html'),'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),base);
 assert.match(html,/<script src="regional-schematic.js"><\/script>/);
 assert.equal(fs.readFileSync(path.join(dir,'regional-schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js','utf8'));
 for(let i=0;i<base.Regions.length;i++)assert.deepEqual(read(path.join(dir,`region-${String(i).padStart(4,'0')}/world-metadata.json`)),base.Regions[i].World);
});
