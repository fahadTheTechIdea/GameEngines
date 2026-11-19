const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_COMPONENT_METADATA;
if(!root)throw new Error('WORLD_COMPONENT_METADATA must identify the current native component publication');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6,`${a} != ${b}`);
for(const name of ['logical','angle-0','angle-30','angle-90'])test(`${name}: exact catalog roles, required parent/parts and current saved inspection`,()=>{
 const w=read(`${name}/world/world-metadata.json`),c=read(`${name}/catalog.json`),before=JSON.stringify(w),view=Schematic.layout(w);
 assert.equal(w.Nodes.length,name==='logical'?7:4);assert.equal(view.boxes.length,w.Nodes.length);
 assert.equal(w.Nodes.filter(n=>n.Component?.Role==='StaticAssembly').length,name==='logical'?2:1);
 assert.equal(w.Nodes.filter(n=>n.Component?.Role==='MovingPart').length,name==='logical'?4:2);
 const ids=new Set(w.Nodes.map(n=>n.Id));
 for(const n of w.Nodes){
  assert.deepEqual(n.Component,c.Archetypes.find(a=>a.Id===n.ArchetypeId).Component);
  if(n.Component?.Role==='MovingPart'){assert.ok(ids.has(n.ParentId));assert.notEqual(n.ParentId,n.Id);}
 }
 if(name==='logical'){assert.equal(w.Space.Axes.length,0);assert.ok(w.Nodes.every(n=>n.Position===null));}
 else assert.equal(w.AccessPlan.Sites.length,3);
 const html=fs.readFileSync(path.join(root,name,'world/schematic.html'),'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
 assert.equal(fs.readFileSync(path.join(root,name,'world/schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 assert.equal(JSON.stringify(w),before);
});
for(const angle of [0,30,90])test(`${angle}: component classification preserves exact rotated physical source size`,()=>{
 const w=read(`angle-${angle}/world/world-metadata.json`),catalog=read(`angle-${angle}/catalog.json`),r=angle*Math.PI/180,c=Math.cos(r),s=Math.sin(r);
 for(const site of w.AccessPlan.Sites){
  const n=w.Nodes.find(n=>n.Id===site.NodeId),source=catalog.Archetypes.find(a=>a.Id===n.ArchetypeId).PhysicalFootprint.Polygon.Vertices;
  assert.equal(site.Footprint.Vertices.length,source.length);
  for(let i=0;i<source.length;i++){near(site.Footprint.Vertices[i].X,n.Position[0]+source[i].X*c-source[i].Y*s);near(site.Footprint.Vertices[i].Y,n.Position[1]+source[i].X*s+source[i].Y*c);}
 }
 assert.equal(w.PlacementPlan.SiteFeatures.length,4);
 assert.equal(w.Nodes.filter(n=>n.Kind==='fixture').length,2);
});
test('catalog role edit updates all instances and retains complete source/checkpoint history',()=>{
 const initial=read('logical/world/world-metadata.json'),w=read('edited/world-metadata.json'),cp=read('edited/generation-session.json');
 assert.equal(w.Nodes.length,initial.Nodes.length);assert.equal(w.Nodes.filter(n=>n.Component?.Role==='MovingPart').length,0);
 assert.equal(w.Nodes.filter(n=>n.Component?.Role==='StaticAssembly').length,6);assert.equal(cp.Edits.length,1);
 assert.equal(cp.Edits[0].Request.Id,'change-role');assert.equal(cp.InitialCatalog.Archetypes.find(a=>a.Id==='part').Component.Role,'MovingPart');
 assert.notEqual(w.CatalogSha256,initial.CatalogSha256);
 assert.deepEqual(w.Nodes.map(n=>({...n,Component:undefined})),initial.Nodes.map(n=>({...n,Component:undefined})));
 const tampered=read('missing-role/world-metadata.json');
 assert.equal(tampered.Nodes.filter(n=>n.Component?.Role==='MovingPart').length,3);
 assert.equal(initial.Nodes.filter(n=>n.Component?.Role==='MovingPart').length,4);
});
