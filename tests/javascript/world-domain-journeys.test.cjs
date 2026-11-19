const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const cases=process.env.WORLD_DOMAIN_JOURNEY_CASES?JSON.parse(process.env.WORLD_DOMAIN_JOURNEY_CASES):[{folder:'generated/domain-journeys'}];
for(const {folder,nodes=21,localNodes=7,localNodeCounts=null,forward=11,return:expectedReturn=11,transitionSteps=2} of cases){
const world=JSON.parse(fs.readFileSync(`${folder}/world-domains.json`)),journey=world.Journeys[0];
function continuous(from,to,steps){
 let domain=from.Definition.DomainId,position=from.Position,transitions=0,local=0;
 for(const step of steps){
  assert.equal(step.DomainId,domain);
  if(step.Kind==='transition'){
   const t=world.Transitions.find(t=>t.Definition.Id===step.SourceId),a=step.Reverse?t.To:t.From,b=step.Reverse?t.From:t.To;
   assert.equal(a.Definition.DomainId,domain);assert.deepEqual(position,a.Position);assert.equal(step.ToDomainId,b.Definition.DomainId);
   domain=b.Definition.DomainId;position=b.Position;transitions++;
  }else{
   assert.equal(step.Kind,'route-segment');const w=world.Domains.find(d=>d.Id===domain).World;
   const routes=[...(w.RoutePlan?.Routes||[]),...(w.InteriorPlan?.Results||[]).flatMap(r=>r.Routes),...(w.NetworkPlan?.Networks||[]).flatMap(n=>n.Plan.Edges)];
   const found=routes.map(r=>step.Reverse?r.Reverse:r.Forward).filter(r=>r?.Id===step.SourceId);assert.equal(found.length,1);
   const points=found[0].Path.Points,a=points[step.SegmentIndex],b=points[step.SegmentIndex+1];
   assert.deepEqual(position,[a.X,a.Y]);position=[b.X,b.Y];local++;
  }
 }
 assert.equal(domain,to.Definition.DomainId);assert.deepEqual(position,to.Position);assert.equal(transitions,transitionSteps);assert.ok(local>2);
}
test(folder+': independent source-segment continuity through all three domains in both directions',()=>{
 assert.equal(world.Domains.reduce((n,d)=>n+d.World.Nodes.length,0),nodes);assert.equal(journey.Forward.length,forward);assert.equal(journey.Return.length,expectedReturn);
 continuous(journey.From,journey.To,journey.Forward);continuous(journey.To,journey.From,journey.Return);
});
test(folder+': missing segment or transition cannot masquerade as a continuous journey',()=>{
 assert.throws(()=>continuous(journey.From,journey.To,journey.Forward.slice(1)));
 assert.throws(()=>continuous(journey.From,journey.To,journey.Forward.filter(s=>s.Kind!=='transition')));
 const changed=structuredClone(journey.Forward);changed[0].SegmentIndex+=1;assert.throws(()=>continuous(journey.From,journey.To,changed));
});
test(folder+': all local maps remain exact and root review includes explicit journey proof',()=>{
 for(let i=0;i<world.Domains.length;i++){
  const child=`${folder}/domain-${String(i).padStart(4,'0')}`,data=JSON.parse(fs.readFileSync(`${child}/world-metadata.json`));
  assert.deepEqual(data,world.Domains[i].World);assert.equal(Schematic.layout(data).boxes.length,localNodeCounts?.[world.Domains[i].Id]??localNodes);
  assert.equal(fs.readFileSync(`${child}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const html=fs.readFileSync(`${child}/schematic.html`,'utf8');
  const navigation=JSON.parse(/<script id="domain-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]);
  const url=`http://localhost/fixture/domain-${String(i).padStart(4,'0')}/schematic.html`;
  assert.equal(Schematic.domainNavigation(navigation,data,url).CurrentDomainId,world.Domains[i].Id);
  const sameWorld=navigation.Entries.find(e=>e.Id!==navigation.CurrentDomainId&&e.WorldId===data.Id&&e.RecipeSha256===data.RecipeSha256&&e.CatalogSha256===data.CatalogSha256);
  if(sameWorld){const wrong=structuredClone(navigation);wrong.CurrentDomainId=sameWorld.Id;assert.throws(()=>Schematic.domainNavigation(wrong,data,url),/Domain navigation/);}
 }
 assert.ok(fs.readFileSync(`${folder}/index.html`,'utf8').includes('Qualified journeys'));
});

}
