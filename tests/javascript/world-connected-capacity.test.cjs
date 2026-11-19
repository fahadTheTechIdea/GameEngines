const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_STUDY_PACKET||'generated/connected-capacity-interiors-final';
const source=path.resolve('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'),S=require(source);
const load=name=>JSON.parse(fs.readFileSync(path.join(root,'cases','case-'+name,'world-metadata.json'),'utf8'));
for(const shape of ['rectangle','hexagon'])for(const extra of [0,1])test(`${shape}: complete 24-room program with ${extra} extra connection`,()=>{
 const w=load(`${shape}-24-extra${extra}`),before=JSON.stringify(w),view=S.layout(w),assembly=w.InteriorPlan.Results[0];
 assert.equal(view.boxes.length,49);assert.equal(w.AccessPlan.Sites.length,48);assert.equal(assembly.Openings.length,23+extra);
 assert.ok(assembly.Walls.length>128&&assembly.Walls.length<=256);
 assert.deepEqual(view.interiorAssemblies,[assembly]);assert.equal(JSON.stringify(w),before);
 for(const route of assembly.Routes)for(const direction of [route.Forward,route.Reverse]){
  const rendered=view.routes.find(r=>r.id===direction.Id);assert.ok(rendered);assert.deepEqual(rendered.points,direction.Path.Points.map(p=>({x:p.X,y:p.Y})));
 }
});
for(const [name,edit] of [
 ['missing grammar',w=>{w.PlacementPlan.Placement.Request.Growth=null;}],
 ['empty forged grammar',w=>{w.PlacementPlan.Placement.Request.Growth={};}],
 ['unlinked required members',w=>{w.PlacementPlan.Placement.Request.Growth.Links=[];}],
 ['unknown root',w=>{w.PlacementPlan.Placement.Request.Growth.RootDemandId='missing';}],
 ['unknown link endpoint',w=>{w.PlacementPlan.Placement.Request.Growth.Links[0].FromId='missing';}],
 ['duplicate link',w=>{const g=w.PlacementPlan.Placement.Request.Growth;g.Links.push(g.Links[0]);}],
 ['invalid direction',w=>{w.PlacementPlan.Placement.Request.Growth.DirectionAnglesDegrees[0]=360;}],
 ['unsatisfied link distance',w=>{w.PlacementPlan.Placement.Request.Growth.MaximumConnectionDistance=.1;}],
 ['prescribed growth conflict',w=>{w.PlacementPlan.Placement.Request.Execution='Prescribed';}],
 ['wall over capacity',w=>{w.InteriorPlan.Results[0].Walls=Array.from({length:257},()=>w.InteriorPlan.Results[0].Walls[0]);}]
])test('refuses '+name,()=>{const w=load('rectangle-24-extra0');edit(w);assert.throws(()=>S.layout(w));});
