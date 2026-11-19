const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SPATIAL_ANALYSIS_ROOT||'generated/spatial-analysis-final-native';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const name of ['region-demand','second-component','adapted-22','adapted-21_5'])test(name+': complete source/content geometry and analysis',()=>{
 const world=read(name+'/world-metadata.json'),analysis=read(name+'-analysis.json'),before=JSON.stringify(world);
 S.layout(world);R.regionAnalysis(analysis,world);R.terrainAnalysis(analysis,world);assert.equal(JSON.stringify(world),before);
 if(analysis.RegionAnalysis){assert.deepEqual(analysis.RegionAnalysis.Plan,world.RegionPlan);}
 if(analysis.TerrainAnalysis){const t=analysis.TerrainAnalysis;assert.equal(t.SourceAdaptation.Attempts.length,2);assert.equal(t.SourceAdaptation.Attempts[0].Accepted,false);
  assert.equal(t.SourceAllocationWork,t.SourceAdaptation.Work);assert.equal(world.TerrainDemandPlan.Results[0].Count,name==='adapted-22'?3:2);
  assert.equal(world.AccessPlan.Sites.length,name==='adapted-22'?5:4);assert.equal(world.RoutePlan.Routes.length,1);}
});
test('selected region/demand analysis cannot drift from final geometry',()=>{
 for(const mutate of [a=>a.RegionAnalysis.Plan.Regions[0].Area++,a=>a.RegionAnalysis.PreparationWork=0,a=>a.RegionAnalysis.DemandPlan.Results[0].RequiredArea++]){
  const world=read('region-demand/world-metadata.json'),a=read('region-demand-analysis.json');mutate(a);assert.throws(()=>R.regionAnalysis(a,world));
 }
});
test('source adaptation/count/work provenance remains mandatory',()=>{
 for(const mutate of [a=>delete a.TerrainAnalysis.SourceAdaptation,a=>a.TerrainAnalysis.SourceAllocationWork++,a=>a.TerrainAnalysis.SourceAdaptation.Attempts[0].Accepted=true,
  a=>a.TerrainAnalysis.Source.LocalLayouts.Allocations[0].Shape.Area++,a=>a.TerrainAnalysis.Plan.Results[0].Count++]){
  const world=read('adapted-22/world-metadata.json'),a=read('adapted-22-analysis.json');mutate(a);assert.throws(()=>R.terrainAnalysis(a,world));
 }
});
test('failed preferred region analysis retains finite ranked selections and actual work',()=>{
 const item=read('observations.json'),recipe=read('region-demand-recipe.json');
 const choice={RejectedAnalyses:item.PreferredAnalysisRejections};R.analysisFailures(choice,recipe.ProgramComposition);
 assert.equal(choice.RejectedAnalyses.length,1);assert.ok(choice.RejectedAnalyses[0].Work>0);assert.deepEqual(choice.RejectedAnalyses[0].ActualRanks,[0]);
 for(const mutate of [c=>c.RejectedAnalyses[0].Work=0,c=>c.RejectedAnalyses[0].RegionRequested=false,c=>c.RejectedAnalyses[0].ActualRanks=[999]]){
  const c=structuredClone(choice);mutate(c);assert.throws(()=>R.analysisFailures(c,recipe.ProgramComposition));
 }
});
