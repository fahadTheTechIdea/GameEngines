const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_TERRAIN_PROGRAM_ANALYSIS_ROOT||'generated/terrain-program-analysis-final-native';
const read=(name,file)=>JSON.parse(fs.readFileSync(path.join(root,name,file),'utf8'));
for(const name of ['baseline','richer','larger','context','mini','protected'])test(`${name}: real measured habitat binds complete actual consumer`,()=>{
 const a=read(name,'program-analysis.json'),w=read(name,'world-metadata.json'),before=JSON.stringify([a,w]);
 R.terrainAnalysis(a,w);const p=S.layout(w);assert.equal(p.boxes.length,w.Nodes.length);assert.equal(JSON.stringify([a,w]),before);
 assert.deepEqual(a.Nodes.map(n=>({...n,Position:null})),w.Nodes.map(n=>({...n,Position:null})));
 assert.deepEqual({...a.TerrainAnalysis.Plan,SourceRecipeSha256:w.TerrainDemandPlan.SourceRecipeSha256},w.TerrainDemandPlan);
});
test('changed habitat, count, work and source identity cannot bind',()=>{
 for(const mutate of [a=>a.TerrainAnalysis=null,a=>a.TerrainAnalysis.HabitatSha256='forged',a=>a.TerrainAnalysis.PreparationWork=a.Work+1,
  a=>a.TerrainAnalysis.Source.RecipeSha256='0'.repeat(64),a=>a.TerrainAnalysis.Source.Environment.Fields[0].Offset++,
  a=>a.TerrainAnalysis.Source.Regions.Regions[0].Area++,a=>a.TerrainAnalysis.Plan.Results[0].Count++]){
  const a=read('baseline','program-analysis.json'),w=read('baseline','world-metadata.json');mutate(a);assert.throws(()=>R.terrainAnalysis(a,w),/terrain analysis binding/);
 }
});
test('physical source-pose revision keeps a distinct recipe identity and exact habitat',()=>{
 const a=read('baseline','program-analysis.json'),w=read('baseline','world-metadata.json'),source=w.PlacementPlan.Definition.SourceWorld;
 assert.notEqual(a.TerrainAnalysis.Source.RecipeSha256,source.RecipeSha256);assert.ok(w.PopulationCoordinationPlan.Attempts.length>1);
 assert.equal(a.SourceProgram.RecipeSha256,a.TerrainAnalysis.Source.RecipeSha256);assert.equal(w.TerrainDemandPlan.SourceRecipeSha256,source.RecipeSha256);
 R.terrainAnalysis(a,w);
});
test('bound-context repair accounts for work before attempts without changing its cap',()=>{
 const original=read('context','world-metadata.json'),p=original.PopulationCoordinationPlan;
 assert.equal(p.SourceBindingWork,24);assert.equal(p.Attempts.reduce((sum,a)=>sum+a.Work,0)+p.SourceBindingWork,p.Work);S.populationCoordination(original);
 for(const edit of [p=>delete p.SourceBindingWork,p=>p.SourceBindingWork=-1,p=>p.SourceBindingWork++,p=>p.SourceRecipe.PopulationSource.Context=null]){
  const w=structuredClone(original);edit(w.PopulationCoordinationPlan);assert.throws(()=>S.populationCoordination(w),/population coordination/);
 }
});
