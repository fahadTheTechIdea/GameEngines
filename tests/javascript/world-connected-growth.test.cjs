const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const cases=['inland','inlet','larger-content'];
const packetRoot=process.env.CONNECTED_GROWTH_PACKET||'generated/connected-growth-qualified';
const read=name=>JSON.parse(fs.readFileSync(`${packetRoot}/${name}/world-metadata.json`,'utf8'));

for(const name of cases){
 test(`${name}: complete physical program reaches the schematic without inferred geometry`,()=>{
  const world=read(name),view=Schematic.layout(world);
  assert.equal(world.Nodes.length,21);assert.equal(world.AccessPlan.Sites.length,20);
  const physical=view.boxes.filter(b=>b.physicalAccess);
  assert.equal(physical.length,20);
  for(const site of world.AccessPlan.Sites)assert.deepEqual(physical.find(b=>b.id===site.NodeId).physicalAccess,site);
  assert.deepEqual(view.boundary,world.Surface.Boundary.Vertices.map(p=>({x:p.X,y:p.Y})));
  assert.equal(view.coordinateSpace,'engine-surface');
 });
 test(`${name}: every generated route direction retains exact engine coordinates and width`,()=>{
  const world=read(name),view=Schematic.layout(world);
  assert.equal(view.routes.length,30);
  for(const route of world.RoutePlan.Routes)for(const [direction,label] of [['Forward','forward'],['Reverse','reverse']]){
   const actual=view.routes.find(r=>r.id===route.ConnectionId+'/'+label);
   assert.deepEqual(actual.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.equal(actual.width,.3);
  }
 });
 test(`${name}: packet contains the current renderer, full source world and connected-growth evidence`,()=>{
  const folder=`${packetRoot}/${name}`,world=read(name);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(world.PlacementPlan.Placement.Request.Anchors.length,1);
  assert.ok(world.PlacementPlan.Placement.Sites.every(s=>s.CandidateSource.startsWith('growth:')));
 });
}
test('counterfactual changes physical composition while retaining topology and all content',()=>{
 const dry=read('inland'),wet=read('inlet'),larger=read('larger-content');
 assert.deepEqual(dry.TopologyPlan,wet.TopologyPlan);
 assert.notDeepEqual(dry.Nodes.map(n=>n.Position),wet.Nodes.map(n=>n.Position));
 assert.deepEqual(dry.Nodes.map(n=>n.Id),wet.Nodes.map(n=>n.Id));
 const yard=id=>id.Nodes.find(n=>n.Id==='connected-wilds/bases-0/yard-0').Position;
 assert.notDeepEqual(yard(dry),yard(larger));
 assert.ok(Schematic.layout(wet).terrain.some(p=>p.kind==='water'));
});

// Optional technical figure, using the exact consumer geometry above. This is
// not a browser screenshot or production art. Sharp is only needed for PNG output.
if(process.argv.includes('--render'))(async()=>{
 const sharp=require('sharp'),views=cases.slice(0,2).map(name=>({name,view:Schematic.layout(read(name))}));
 const sites=views.flatMap(p=>p.view.boxes.filter(b=>b.physicalAccess).flatMap(b=>b.physicalAccess.Footprint.Vertices));
 const minX=Math.min(...sites.map(p=>p.X))-8,minY=Math.min(...sites.map(p=>p.Y))-8;
 const maxX=Math.max(...sites.map(p=>p.X))+8,maxY=Math.max(...sites.map(p=>p.Y))+8;
 const colors={compound:'#d8d5bb',structure:'#96754f','ground-area':'#a9bd90',equipment:'#504438'};
 const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
 const points=vertices=>vertices.map(p=>`${p.x??p.X},${p.y??p.Y}`).join(' ');
 const panels=views.map(({name,view},i)=>{
  let body=`<polygon points="${points(view.boundary)}" fill="#edf0e2" stroke="#78906a" stroke-width=".2"/>`;
  for(const terrain of view.terrain.filter(p=>p.kind==='water'))body+=`<polygon points="${points(terrain.points)}" fill="#83b2c8"/>`;
  for(const box of view.boxes.filter(b=>b.physicalAccess).sort((a,b)=>a.depth-b.depth))body+=`<polygon points="${points(box.physicalAccess.Footprint.Vertices)}" fill="${colors[box.node.Kind]||'#b6b6a6'}" stroke="#615c4d" stroke-width=".12"/>`;
  for(const route of view.routes.filter(r=>!r.reverse))body+=`<polyline points="${points(route.points)}" fill="none" stroke="#c6a25f" stroke-width="${route.width}"/>`;
  for(const box of view.boxes.filter(b=>b.node.Kind==='compound'))body+=`<text x="${box.node.Position[0]}" y="${box.node.Position[1]+3.2}" text-anchor="middle" font-size="1.1" fill="#31392e">${escape(box.id.split('/').at(-1))}</text>`;
  return `<g transform="translate(${i*720},0)"><text x="20" y="32" font-size="20" fill="#253a31">${escape(name)} — exact engine geometry</text><svg x="15" y="50" width="690" height="600" viewBox="${minX} ${minY} ${maxX-minX} ${maxY-minY}">${body}</svg></g>`;
 }).join('');
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="710"><rect width="1440" height="710" fill="#faf9f5"/>${panels}<text x="20" y="685" font-size="15" fill="#45584b">Five complete bases · twenty source-sized footprints · fifteen paths · technical diagram, not production artwork</text></svg>`;
 const folder='generated/connected-growth-review';fs.mkdirSync(folder,{recursive:true});
 fs.writeFileSync(path.join(folder,'comparison.svg'),svg);
 await sharp(Buffer.from(svg)).png().toFile(path.join(folder,'comparison.png'));
 console.log('Saved technical geometry comparison; browser UI acceptance remains NOT RUN.');
})().catch(error=>{console.error(error);process.exitCode=1;});
