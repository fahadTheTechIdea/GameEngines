const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const prefix=process.env.WORLD_RELATION_CANDIDATES_PREFIX||'generated/relation-candidates-cli-';
const load=name=>JSON.parse(fs.readFileSync(prefix+name+'/world-metadata.json','utf8'));
test('saved analytic placements present both complete catalog members',()=>{
 for(const [name,x,y] of [['analytic',3,4],['changed',4,3]]){
  const w=load(name),before=JSON.stringify(w),layout=S.layout(w);
  assert.equal(layout.boxes.length,3);assert.equal(w.AccessPlan.Sites.length,2);
  assert.deepEqual(w.Nodes.filter(n=>n.ParentId!=null).map(n=>n.Position).sort((a,b)=>a[1]-b[1]),[[x,-y],[x,y]]);
  for(const site of w.PlacementPlan.Placement.Sites)assert.match(site.CandidateSource,/relation:.*:intersection:/);
  assert.equal(JSON.stringify(w),before);
 }
});
test('exported JavaScript remains the common engine-metadata presentation',()=>{
 assert.equal(fs.readFileSync(prefix+'analytic/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
