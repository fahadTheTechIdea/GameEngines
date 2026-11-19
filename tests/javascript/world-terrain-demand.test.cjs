const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.TERRAIN_DEMAND_PACKET||'generated/terrain-demand-reviewed';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-7*Math.max(1,Math.abs(b)),`${a} != ${b}`);
for(const [name,scale,expected,nodes] of [['baseline',1,[100,117,2],248],['richer-density',1.1,[110,129,2],270]]){
 test(`${name}: measured demand expands every source-sized required member`,()=>{
  const world=read(name);assert.equal(Schematic.layout(world).boxes.length,nodes);assert.equal(world.Nodes.length,nodes);
  const ids=['lowland','highland','woodland'],mass=[75240547/15000,108930607659/18496000,25806/125],area=[4039/2,445455/272,68];
  for(let i=0;i<3;i++){
   const result=world.TerrainDemandPlan.Results.find(r=>r.RuleId===ids[i]),rule=world.TerrainDemandPlan.Definition.Rules.find(r=>r.Id===ids[i]);
   near(result.Measure.Area,area[i]);near(result.Measure.Mass,mass[i]*scale);assert.equal(result.Count,expected[i]);
   assert.equal(result.Count,Math.floor(mass[i]*scale/rule.AmountPerMember));
   const members=world.Nodes.filter(n=>n.TerrainDemandRuleId===rule.Id);assert.equal(members.length,expected[i]);
   const trace=world.PlacementPlan.PopulationAreas.find(t=>t.RegionId===ids[i]);assert.equal(trace.NodeIds.length,expected[i]);
   assert.deepEqual(new Set(trace.NodeIds),new Set(members.map(n=>n.Id)));near(trace.Plan.Mass,result.Measure.Mass);
  }
  const source=world.PlacementPlan.Definition.SourceWorld;
  for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
  for(const route of source.RoutePlan.Routes)assert.deepEqual(world.RoutePlan.Routes.find(r=>r.ConnectionId===route.ConnectionId),route);
  const catalog=JSON.parse(fs.readFileSync(`examples/metadata/terrain-demand/${name}/catalog.json`,'utf8'));
  const rootType=catalog.Archetypes.find(a=>a.Id===world.Nodes.find(n=>n.ParentId==null).ArchetypeId);
  for(const part of rootType.Parts)assert.ok(world.Nodes.filter(n=>n.ParentId===world.Id&&n.Id.startsWith(`${world.Id}/${part.Id}-`)).length>=part.Count);
  assert.equal(world.AccessPlan.Sites.length,nodes-1);assert.equal(world.RoutePlan.Routes.length,19);assert.equal(world.PlacementPlan.ProgramInstances.length,14);
 });
}
test('density counterfactual changes counts while source compounds and routes remain exact',()=>{
 const a=read('baseline').PlacementPlan.Definition.SourceWorld,b=read('richer-density').PlacementPlan.Definition.SourceWorld;
 for(const key of ['Nodes','RoutePlan','RegionPlan','LocalLayoutPlan','Surface'])assert.deepEqual(a[key],b[key]);
});
test('missing or altered demand/member provenance refuses the display packet',()=>{
 const world=read('baseline');
 for(const change of [w=>w.TerrainDemandPlan=null,w=>w.TerrainDemandPlan.Results[0].Count++,
  w=>w.TerrainDemandPlan.Results[0].Measure.RegionId='missing',w=>w.TerrainDemandPlan.Work++,
  w=>w.TerrainDemandPlan.SourceRecipeSha256='stale',w=>w.TerrainDemandPlan.Definition.Rules[0].AmountPerMember*=2,
  w=>w.Nodes.find(n=>n.TerrainDemandRuleId!=null).TerrainDemandRuleId='foreign',
  w=>w.TerrainDemandPlan.Definition.Rules[0].PartId='missing']){
   const copy=structuredClone(world);change(copy);assert.throws(()=>Schematic.layout(copy),/terrain demand witness/);
 }
});
test('both packets embed exact saved data and the current renderer',()=>{
 for(const name of ['baseline','richer-density']){
  assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),read(name));
 }
});
test('similar part-name prefixes remain separate member groups',()=>{
 const world=read('baseline'),parent=world.Nodes.find(n=>n.ParentId==null);
 const sibling={...parent,Id:world.Id+'/lowland-features-extra-0',ParentId:world.Id,TerrainDemandRuleId:null};
 world.Nodes.push(sibling);assert.doesNotThrow(()=>Schematic.terrainDemands(world,new Map(world.Nodes.map(n=>[n.Id,n]))));
});
