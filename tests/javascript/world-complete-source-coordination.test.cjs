const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),crypto=require('node:crypto');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_COMPLETE_SOURCE_PACKET||'generated/complete-source-coordination-cli-route';
const load=()=>JSON.parse(fs.readFileSync(folder+'/world-metadata.json','utf8'));
test('whole source-arrangement receipt presents selected route content without mutating data',()=>{
 const w=load(),before=JSON.stringify(w),p=S.populationCoordination(w),source=w.PlacementPlan.Definition.SourceWorld;
 assert.equal(p.Version,3);assert.equal(p.Attempts.length,2);assert.equal(S.layout(w).boxes.length,4);
 const guard=p.Attempts[0].AddedNogood;assert.equal(guard.Assignments.length,0);assert.match(guard.WholeAssignmentSha256,/^[0-9a-f]{64}$/);
 assert.deepEqual(source.Nodes.find(n=>n.Id.endsWith('sites-0')).Position,[10,5]);
 assert.notDeepEqual(source.Nodes.find(n=>n.Id.endsWith('sites-1')).Position,[7,5]);
 const digest=nodes=>crypto.createHash('sha256').update(JSON.stringify(nodes)).digest('hex');
 const bindings=[...source.PlacementPlan.Definition.Bindings,...(source.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].map(b=>b.NodeId);
 const poses=source.Nodes.filter(n=>bindings.includes(n.Id)).sort((a,b)=>a.Id<b.Id?-1:1).map(n=>({NodeId:n.Id,Position:{X:n.Position[0],Y:n.Position[1]}}));
 assert.notEqual(digest(poses),guard.WholeAssignmentSha256);
 assert.equal(digest([{NodeId:w.Id+'/sites-0',Position:{X:10,Y:5}},{NodeId:w.Id+'/sites-1',Position:{X:7,Y:5}}]),guard.WholeAssignmentSha256);
 assert.deepEqual(w.RoutePlan.Routes,source.RoutePlan.Routes);assert.equal(JSON.stringify(w),before);
});
test('malformed and obsolete arrangement receipts refuse presentation',()=>{
 for(const change of [w=>w.PopulationCoordinationPlan.Version=2,w=>w.PopulationCoordinationPlan=null,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.WholeAssignmentSha256='invalid',
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments.push({NodeId:w.Id+'/sites-0',Position:{X:10,Y:5}}),
  w=>w.PlacementPlan.Definition.SourceWorld.PlacementPlan.Definition.Nogoods=[]]){
  const w=load();change(w);assert.throws(()=>S.layout(w),/population coordination/);
 }
});
test('exported viewer is the current saved-metadata consumer',()=>{
 assert.equal(fs.readFileSync(folder+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
