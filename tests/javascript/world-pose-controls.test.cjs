const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_POSE_PACKET||'generated/pose-controls-cli-final';
const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
const S=require(path.resolve('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
const load=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-metadata.json'),'utf8'));
for(const name of ['controlled','larger-source','absolute-child','stage-child'])test(name+': exact authored poses and complete sources reach inspection',()=>{
 const w=load(name),before=JSON.stringify(w),view=S.layout(w);
 assert.equal(fs.readFileSync(path.join(root,name,'schematic.js'),'utf8'),source);
 assert.equal(view.boxes.length,w.Nodes.length);assert.equal(JSON.stringify(w),before);
 for(const pose of w.PlacementPlan.PoseResults){
  const observations=view.suitability.get(pose.Definition.NodeId);assert.ok(observations.some(o=>JSON.stringify(o.Placement?.Pose)===JSON.stringify(pose)));
  const site=w.AccessPlan.Sites.find(s=>s.NodeId===pose.Definition.NodeId);
  assert.ok(observations.some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
 }
 if(name==='controlled'||name==='larger-source'){assert.equal(w.Nodes.length,21);assert.equal(w.AccessPlan.Sites.length,20);assert.equal(w.RoutePlan.Routes.length,15);}
});
for(const [name,edit] of [
 ['missing pose receipt',w=>{delete w.PlacementPlan.PoseResults;}],
 ['unbound receipt',w=>{delete w.PlacementPlan.Definition.PoseControls;}],
 ['unknown controlled node',w=>{w.PlacementPlan.Definition.PoseControls[0].NodeId='missing';}],
 ['duplicate controlled node',w=>{w.PlacementPlan.Definition.PoseControls[1].NodeId=w.PlacementPlan.Definition.PoseControls[0].NodeId;}],
 ['invalid coordinate frame',w=>{w.PlacementPlan.Definition.PoseControls[0].Frame='Local';}],
 ['moved resolved pose',w=>{w.PlacementPlan.PoseResults[0].WorldPosition.X+=.25;}],
 ['mismatched orientation',w=>{w.PlacementPlan.PoseResults[0].RotationDegrees+=10;}],
 ['mismatched definition',w=>{w.PlacementPlan.PoseResults[0].Definition.SourceRuleId='invented';}],
 ['nonfinite requested position',w=>{w.PlacementPlan.Definition.PoseControls[0].Position.X=NaN;}],
 ['wrong stage owner',w=>{w.PlacementPlan.PoseResults[0].StageId='missing';}]
])test('refuses '+name,()=>{const w=load('controlled');edit(w);assert.throws(()=>S.layout(w));});
