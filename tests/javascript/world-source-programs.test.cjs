const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_SOURCE_PROGRAM_ROOT||'generated/source-program-final-native';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const name of ['source-programs','source-allocation','consumer-programs','source-region'])test(name+': qualified complete source choices and downstream geometry',()=>{
 const w=read(name+'/world-metadata.json'),a=read(name+'-analysis.json'),before=JSON.stringify(w),source=w.PlacementPlan.Definition.SourceWorld;
 S.layout(w);R.sourceProgramComposition(a.SourceProgram,source);R.terrainAnalysis(a,w);R.regionAnalysis(a,w);
 assert.equal(JSON.stringify(w),before);assert.deepEqual(a.SourceProgram.QualifiedComposition,source.ProgramCompositionPlan);
 assert.equal(source.ProgramCompositionPlan.Attempts[0].Accepted,false);assert.equal(source.ProgramCompositionPlan.Attempts.at(-1).Accepted,true);
 for(const allocation of source.ProgramMixPlan?.Allocations??[])assert.equal(S.inheritedProgramMix(w,w.ProgramMixPlan.Allocations.find(v=>v.Key===allocation.Key)),true);
 for(const node of source.Nodes)assert.deepEqual(w.Nodes.find(n=>n.Id===node.Id),node);
 if(name==='consumer-programs')assert.deepEqual(w.ProgramCompositionPlan.Attempts[0].Selections.map(v=>v.Request.PartId),['tokens']);
 else assert.equal(w.ProgramCompositionPlan,null);
 if(name==='source-region'){assert.equal(S.inheritedProgramRegion(w),true);assert.equal(w.RegionPlan.Request.ExcludedAssignments.length,1);}
 else assert.equal(w.TerrainDemandPlan.Results[0].Count,name==='source-allocation'?2:3);
});
test('upstream exclusions cannot be asserted as independent downstream decisions',()=>{
 for(const mutate of [w=>w.ProgramMixPlan.Allocations[0].Cost++,w=>delete w.PlacementPlan.Definition.SourceWorld.ProgramMixPlan,
  w=>w.ProgramMixPlan.Allocations[0].Request.ExcludedCounts.pop()]){
  const w=read('source-programs/world-metadata.json');mutate(w);assert.throws(()=>S.layout(w));
 }
 const w=read('consumer-programs/world-metadata.json');w.ProgramCompositionPlan.Attempts[0].Selections.unshift(w.ProgramMixPlan.Allocations[0]);
 assert.throws(()=>S.layout(w));
});
test('selected source component needs its exact inherited receipt',()=>{
 for(const mutate of [w=>w.RegionPlan.Request.ExcludedAssignments.pop(),w=>w.PlacementPlan.Definition.SourceWorld.RegionPlan.Regions[0].Area++]){
  const w=read('source-region/world-metadata.json');mutate(w);assert.throws(()=>S.layout(w));
 }
});
test('original upstream composition analysis must retain failed choices and accepted mixes',()=>{
 const w=read('source-programs/world-metadata.json'),source=w.PlacementPlan.Definition.SourceWorld;
 for(const mutate of [a=>delete a.SourceProgram.QualifiedComposition,a=>a.SourceProgram.QualifiedComposition.Attempts[0].Accepted=true,
  a=>a.SourceProgram.QualifiedComposition.Attempts.at(-1).Selections[0].Cost++,a=>a.SourceProgram.QualifiedComposition.SourceCatalog.Id+='-changed']){
  const a=read('source-programs-analysis.json');mutate(a);assert.throws(()=>R.sourceProgramComposition(a.SourceProgram,source));
 }
});
test('accepted source habitat and complete downstream quantity remain bound',()=>{
 for(const mutate of [a=>a.TerrainAnalysis.Source.LocalLayouts.Allocations[0].Shape.Area++,a=>a.TerrainAnalysis.Plan.Results[0].Count++,
  a=>a.TerrainAnalysis.SourceAdaptation.Attempts[0].Accepted=true]){
  const a=read('source-programs-analysis.json'),w=read('source-programs/world-metadata.json');mutate(a);assert.throws(()=>R.terrainAnalysis(a,w));
 }
});
