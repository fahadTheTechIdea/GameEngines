const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_OFF_CENTER_SURROUNDINGS;
if(!root)throw Error('WORLD_OFF_CENTER_SURROUNDINGS is required');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));
const close=(a,b)=>assert.ok(Math.abs(a-b)<1e-7,`${a} != ${b}`);
const world=read('off-center-world.json');
test('walkway follows the authored off-center entrance after rotation',()=>{
 assert.equal(S.layout(world).boxes.length,2);
 const f=world.PlacementPlan.SiteFeatures.find(f=>f.Kind==='AccessWalkway');
 const p=world.AccessPlan.Sites[0].Ports[0],angle=Math.PI/6,c=Math.cos(angle),s=Math.sin(angle);
 const rotate=([x,y])=>({X:50+x*c-y*s,Y:50+x*s+y*c});
 const expected=[[-1.75,-10],[-.25,-10],[-.25,-6],[-1.75,-6]].map(rotate);
 f.Boundary.Vertices.forEach((v,i)=>{close(v.X,expected[i].X);close(v.Y,expected[i].Y);});
 const door=rotate([-1,-6]),approach=rotate([-1,-10]);
 close(p.BoundaryPosition.X,door.X);close(p.BoundaryPosition.Y,door.Y);
 close(p.ApproachPosition.X,approach.X);close(p.ApproachPosition.Y,approach.Y);
 assert.equal(p.Width,1.5);assert.equal(f.EntrancePortId,p.Id);
 const forged=structuredClone(world);forged.PlacementPlan.SiteFeatures.find(f=>f.Kind==='AccessWalkway').Boundary.Vertices[0].X++;
 assert.throws(()=>S.layout(forged),/Invalid/);
});
test('each repeated and nested source retains complete off-center surroundings',()=>{
 for(const [file,nodes] of [['repeated-world.json',3],['nested-world.json',4]]){
  const data=read(file);assert.equal(S.layout(data).boxes.length,nodes);
  const sites=data.PlacementPlan.SiteFeatures.filter(f=>f.Kind==='AccessWalkway');assert.equal(sites.length,2);
  for(const feature of sites){const site=data.AccessPlan.Sites.find(s=>s.NodeId===feature.NodeId);assert.ok(site);
   assert.equal(site.Ports[0].Id,feature.EntrancePortId);}
 }
});
if(fs.existsSync(path.join(root,'side-world.json')))test('side entrance and its entire walkway rotate together',()=>{
 const data=read('side-world.json');assert.equal(S.layout(data).boxes.length,2);
 const f=data.PlacementPlan.SiteFeatures.find(f=>f.Kind==='AccessWalkway'),p=data.AccessPlan.Sites[0].Ports[0];
 const a=Math.PI/6,c=Math.cos(a),s=Math.sin(a),rotate=([x,y])=>({X:50+x*c-y*s,Y:50+x*s+y*c});
 const expected=[[9,-3.75],[9,-2.25],[5,-2.25],[5,-3.75]].map(rotate);
 f.Boundary.Vertices.forEach((v,i)=>{close(v.X,expected[i].X);close(v.Y,expected[i].Y);});
 const door=rotate([5,-3]),approach=rotate([9,-3]);close(p.BoundaryPosition.X,door.X);close(p.BoundaryPosition.Y,door.Y);
 close(p.ApproachPosition.X,approach.X);close(p.ApproachPosition.Y,approach.Y);
});
