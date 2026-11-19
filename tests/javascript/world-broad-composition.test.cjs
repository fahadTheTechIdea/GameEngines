const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
const roots=process.env.WORLD_COMPOSITION_ROOTS?JSON.parse(process.env.WORLD_COMPOSITION_ROOTS):['generated/broad-composition-qualified-api','generated/broad-composition-sdk','generated/broad-composition-cli'];
const packets=process.env.WORLD_COMPOSITION_PACKETS?JSON.parse(process.env.WORLD_COMPOSITION_PACKETS):roots.flatMap(root=>['baseline','edited'].map(name=>[root,name]));
for(const [root,name,expectedNodes=91,expectedPhysical=86] of packets)test(root+'/'+name+': complete terrain/local/logical data reaches saved viewers',()=>{
 const folder=path.join(root,name),world=JSON.parse(fs.readFileSync(path.join(folder,'world-domains.json')));
 assert.equal(world.Domains.reduce((s,d)=>s+d.World.Nodes.length,0),expectedNodes);assert.equal(world.Domains.reduce((s,d)=>s+(d.World.AccessPlan?.Sites.length||0),0),expectedPhysical);
 for(const transition of world.Transitions)for(const endpoint of [transition.From,transition.To])if(endpoint.Definition.InteriorOpening){
  const w=world.Domains.find(d=>d.Id===endpoint.Definition.DomainId).World,selector=endpoint.Definition.InteriorOpening;
  const candidates=w.Connections.filter(c=>c.Kind===selector.ConnectionKind&&(c.FromId===endpoint.Definition.NodeId||c.ToId===endpoint.Definition.NodeId)).sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
  assert.equal(endpoint.ResolvedInteriorConnectionId,candidates[selector.ConnectionIndex].Id);
  const opening=w.InteriorPlan.Results.flatMap(r=>r.Openings).find(o=>o.ConnectionId===endpoint.ResolvedInteriorConnectionId);
  const p=opening.FromId===endpoint.Definition.NodeId?opening.FromApproach:opening.ToApproach;
  assert.deepEqual(endpoint.Position,[p.X,p.Y]);assert.equal(endpoint.AvailableWidth,opening.Width);
 }
 for(let i=0;i<world.Domains.length;i++){
  const child=path.join(folder,`domain-${String(i).padStart(4,'0')}`),w=world.Domains[i].World,before=JSON.stringify(w),S=require(path.resolve(child,'schematic.js'));
  assert.equal(fs.readFileSync(path.join(child,'schematic.js'),'utf8'),source);const view=S.layout(w);assert.equal(JSON.stringify(w),before);assert.equal(view.boxes.length,w.Nodes.length);
  for(const site of w.AccessPlan?.Sites||[])assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
  if(world.Domains[i].Id==='progression'){assert.equal(w.Surface,null);assert.equal(w.Nodes.length,3);}
  const html=fs.readFileSync(path.join(child,'schematic.html'),'utf8');assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
  const nav=JSON.parse(/<script id="domain-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]);assert.equal(S.domainNavigation(nav,w,`http://localhost/fixture/domain-${String(i).padStart(4,'0')}/schematic.html`).CurrentDomainId,world.Domains[i].Id);
 }
});
