const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_PROGRAM_DEMAND||'generated/program-demand-native-r2';
for(const name of ['world','enlarged']){
 const world=JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
 test(`${name}: complete saved programs and measured demand bind to current schematic`,()=>{
  const plan=R.layout(world),proof=R.ownershipDerivation(world.Definition);
  assert.equal(plan.boxes.length,15);assert.equal(proof.ProgramDemands.length,3);
  const alpha=proof.ProgramDemands.find(d=>d.RegionId==='alpha');assert.equal(alpha.EnvelopeArea,name==='world'?120:180);
  assert.equal(alpha.RequiredArea,name==='world'?240:360);assert.equal(alpha.Analysis.Nodes.length,5);assert.equal(alpha.Sources.length,1);
 });
 test(`${name}: missing or altered source demand, analysis, membership and bounds refuse`,()=>{
  for(const change of [
   w=>w.Definition.OwnershipPlan.ProgramDemands.pop(),
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].EnvelopeArea++,
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].RequiredArea++,
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Weight++,
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].RequiredClearance++,
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Nodes[0].Position=[0,0],
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Nodes.pop(),
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Dependencies.pop(),
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Analysis.Connections[0].Kind='forged',
   w=>w.Definition.OwnershipPlan.ProgramDemands[0].Sources[0].Source.NodeId='missing',
   w=>w.Definition.OwnershipPlan.Source.Regions[0].Capacity.MaximumCoverage=1,
  ]){const w=structuredClone(world);change(w);assert.throws(()=>R.layout(w),/Invalid regional/);}
 });
}
