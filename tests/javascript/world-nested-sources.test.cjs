const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_NESTED_SOURCE_ROOT||'generated/nested-source-final-native';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
for(const name of ['source-only','population','allocation-edit','mixed-source','terrain-rotation'])test(name+': complete nested source habitat and consumer programs',()=>{
 const w=read(name),before=JSON.stringify(w),p=w.Definition.OwnershipPlan,view=R.layout(w);
 assert.equal(JSON.stringify(w),before);assert.equal(view.views.length,2);
 assert.equal(p.JointPlan.Attempts[0].InputChoices.every(c=>c.RegionSelectionDeferred),true);
 assert.equal(p.JointPlan.Attempts.at(-1).OutputChoices.every(c=>!c.RegionSelectionDeferred),true);
 for(const packet of w.Regions){
  const world=packet.World,source=world.PlacementPlan.Definition.SourceWorld,analysis=p.ProgramDemands.find(d=>d.RegionId===packet.RegionId).Analysis;
  assert.equal(source.Nodes.length,11);assert.equal(source.AccessPlan.Sites.length,9);
  assert.equal(source.RegionPlan.Regions.length,3);assert.equal(source.LocalLayoutPlan.Allocations.length,3);
  assert.deepEqual(source.RegionPlan,world.RegionPlan);assert.deepEqual(source.LocalLayoutPlan,world.LocalLayoutPlan);
  R.sourceProgramComposition(analysis.SourceProgram,source);
  assert.deepEqual(source.RegionPlan.Request.Boundary,w.Definition.Regions.find(r=>r.Id===packet.RegionId).Ownership);
  if(world.TerrainDemandPlan)assert.equal(world.TerrainDemandPlan.Results[0].Count,Math.floor(source.LocalLayoutPlan.Allocations.find(a=>a.Id==='alpha-site').Shape.Area/800));
  else{assert.deepEqual(world.ProgramCompositionPlan.Definition.RequiredRanks,[]);assert.equal(world.ProgramCompositionPlan.Selections,0);}
 }
});
test('source context, framed layouts and accepted geometry cannot drift',()=>{
 for(const mutate of [w=>w.Definition.OwnershipPlan.Source.Regions[0].SpatialContext=null,
  w=>w.Definition.OwnershipPlan.Source.Regions[0].Recipe.PopulationSource.Context.UsePlacementBoundary=false,
  w=>w.Definition.OwnershipPlan.Source.Regions[0].Recipe.PopulationSource.Recipe.Regions.Boundary=w.Definition.Surface.Boundary,
  w=>w.Definition.OwnershipPlan.Source.Regions[1].Recipe.PopulationSource.Recipe.LocalLayouts.Layouts[0].RegionFrame=null,
  w=>w.Regions[0].World.LocalLayoutPlan.Allocations[0].Shape.Area++,
  w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.RegionSelectionDeferred=true]){
  const w=read('source-only');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('zero consumer choices require an actual inherited source composition',()=>{
 for(const mutate of [w=>w.PlacementPlan.Definition.SourceWorld.ProgramCompositionPlan=null,w=>w.ProgramCompositionPlan.SourceRecipe.PopulationSource.Context.UsePlacementBoundary=false,
  w=>w.ProgramCompositionPlan.Attempts[0].Selections.push(w.PlacementPlan.Definition.SourceWorld.ProgramMixPlan?.Allocations?.[0]??{})]){
  const w=read('source-only').Regions[0].World;mutate(w);assert.throws(()=>S.programComposition(w));
 }
});
test('source qualification and accepted habitat equations remain mandatory',()=>{
 for(const mutate of [w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.SourceProgram.QualifiedComposition=null,
  w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.TerrainAnalysis.Plan.Results[0].Count++,
  w=>w.Regions[0].World.RegionPlan.Request.Boundary=w.Definition.Surface.Boundary]){
  const w=read('population');mutate(w);assert.throws(()=>R.layout(w));
 }
});
test('declared longest boundary edge must match the actual source geometry',()=>{
 const w=read('population').Regions[0].World;w.PlacementPlan.PopulationBoundaries[0].Boundary.Pattern.StartEdgeIndex++;
 assert.throws(()=>S.layout(w));
});
