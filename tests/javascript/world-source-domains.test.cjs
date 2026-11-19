const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SOURCE_DOMAIN_ROOT||'generated/source-domain-qualified-native';
const read=n=>JSON.parse(fs.readFileSync(path.join(root,n,'world-metadata.json'),'utf8'));
for(const name of ['base','rotated','more','source-edit','session','reservation']){
 test(`${name}: exact required content, domain provenance and source geometry`,()=>{
  const w=read(name),before=JSON.stringify(w),p=S.layout(w);assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,w.Nodes.length);
  assert.deepEqual(p.populationDomains,w.PlacementPlan.PopulationDomains||[]);
  for(const t of p.populationDomains)for(const id of t.NodeIds){const site=w.AccessPlan.Sites.find(s=>s.NodeId===id);assert.ok(site);assert.ok(p.suitability.get(id).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));}
  if(name!=='reservation')assert.deepEqual(w.RoutePlan.Routes,w.PlacementPlan.Definition.SourceWorld.RoutePlan.Routes);
 });
}
test('source domain omissions, source substitutions, provenance and member edits refuse inspection',()=>{
 const mutations=[w=>w.PlacementPlan.PopulationDomains=null,w=>w.PlacementPlan.PopulationDomains.pop(),
  w=>w.PlacementPlan.PopulationDomains.push(w.PlacementPlan.PopulationDomains[0]),w=>w.PlacementPlan.PopulationDomains[0].Domain.SourceIds=['forged'],
  w=>w.PlacementPlan.PopulationDomains[0].Domain.SourceNodeId='missing',w=>w.PlacementPlan.PopulationDomains[0].Domain.SourceGeometrySha256='bad',
  w=>w.PlacementPlan.PopulationDomains[0].Domain.Shape.Area++,w=>w.PlacementPlan.PopulationDomains[0].Domain.Shape.Cells[0].Vertices[0].X++,
  w=>w.PlacementPlan.PopulationDomains[0].Domain.WorldPositions[0].X++,w=>w.PlacementPlan.PopulationDomains[0].NodeIds[0]='missing',
  w=>w.PlacementPlan.PopulationDomains[0].Domain.Definition.Selector='missing',w=>w.PlacementPlan.PopulationDomains[0].Purpose='forged',
  w=>w.PlacementPlan.PopulationDomains[0].Domain.Shape.BoundaryEdges[0].From.X=Infinity];
 for(const change of mutations){const w=read('base');change(w);assert.throws(()=>S.layout(w),/population source domain/);}
});
test('feature edge orientation and area strata remain linked to actual source members',()=>{
 const mutations=[w=>w.PlacementPlan.PopulationDomains.find(t=>t.Domain.ResolvedBands).Domain.ResolvedBands.DirectionDegrees++,
  w=>w.PlacementPlan.PopulationDomains.find(t=>t.Domain.Area).Domain.Area.Strata[0].Position.X++,
  w=>w.PlacementPlan.PopulationDomains.find(t=>t.Domain.Area).Domain.Area.Area++,
  w=>w.PlacementPlan.PopulationOrientations[0].TangentDegrees[0]++,w=>w.PlacementPlan.PopulationOrientations[0].Rotations[0]++];
 for(const change of mutations){const w=read('rotated');change(w);assert.throws(()=>S.layout(w),/population (source domain|orientation)/);}
});
test('source count and source-size controls retain all required program members',()=>{
 const base=read('base'),more=read('more'),edit=read('source-edit');
 assert.equal(base.Nodes.length,15);assert.equal(more.Nodes.length,28);assert.equal(edit.Nodes.length,15);
 assert.equal(more.PlacementPlan.ProgramInstances.length,3);assert.equal(more.PlacementPlan.PopulationDomains.reduce((s,t)=>s+t.NodeIds.length,0),24);
 const area=w=>w.GroundCoverPlan.Cells.filter(c=>c.MaterialId==='deck').reduce((sum,c)=>sum+c.Area,0);
 assert.ok(Math.abs(area(base)-160)<1e-7);assert.ok(Math.abs(area(edit)-176)<1e-7);
 assert.deepEqual(base.PlacementPlan.TemplateDefinition.Programs,more.PlacementPlan.TemplateDefinition.Programs.map(p=>({...p,Bindings:p.Bindings.map(b=>b.PartId==='plants'?{...b,Population:{...b.Population,Domain:{...b.Population.Domain,Bands:{...b.Population.Domain.Bands,Bands:b.Population.Domain.Bands.Bands.map(row=>({...row,Count:4}))}}}}:b)})));
});
