const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const roots=['generated/domain-lifecycle-final-api','generated/domain-lifecycle-sdk','generated/domain-lifecycle-cli'];
const current=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
for(const root of roots)for(const stage of ['baseline','edited'])test(root+'/'+stage+': exact composed-world inspectors',()=>{
 const folder=path.join(root,stage),world=JSON.parse(fs.readFileSync(path.join(folder,'world-domains.json')));
 assert.equal(world.Domains.reduce((n,d)=>n+d.World.Nodes.length,0),21);
 assert.ok(fs.readFileSync(path.join(folder,'index.html'),'utf8').includes('Qualified journeys'));
 for(let i=0;i<world.Domains.length;i++){
  const child=path.join(folder,`domain-${String(i).padStart(4,'0')}`),data=world.Domains[i].World;
  assert.equal(fs.readFileSync(path.join(child,'schematic.js'),'utf8'),current);
  const S=require(path.resolve(child,'schematic.js')),before=JSON.stringify(data),view=S.layout(data);
  assert.equal(JSON.stringify(data),before);
  assert.deepEqual(new Set(view.boxes.map(b=>b.node.Id)),new Set(data.Nodes.map(n=>n.Id)));
  for(const site of data.AccessPlan?.Sites||[])assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
  const html=fs.readFileSync(path.join(child,'schematic.html'),'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),data);
  const navigation=JSON.parse(/<script id="domain-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]);
  assert.equal(S.domainNavigation(navigation,data,`http://localhost/fixture/domain-${String(i).padStart(4,'0')}/schematic.html`).CurrentDomainId,world.Domains[i].Id);
 }
});
