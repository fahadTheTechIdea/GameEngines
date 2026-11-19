const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const prefix=process.env.WORLD_PROVISIONAL_RELATIONS_PREFIX||'generated/provisional-relations-cli-';
const load=name=>JSON.parse(fs.readFileSync(prefix+name+'/world-metadata.json','utf8'));
test('provisional relation examples present the complete saved physical program',()=>{
 for(const name of ['single','focused','backtrack','nested']){
  const w=load(name),before=JSON.stringify(w),nested=name==='nested';
  assert.equal(S.layout(w).boxes.length,nested?9:4);assert.equal(w.AccessPlan.Sites.length,nested?8:3);
  if(!nested){
   const position=i=>w.Nodes.find(n=>n.Id===w.Id+'/sites-'+i).Position;
   const first=position(0),second=position(1),child=position(2);
   assert.ok(Math.abs(Math.hypot(child[0]-first[0],child[1]-first[1])-5)<1e-12);
   assert.ok(Math.abs(Math.hypot(child[0]-second[0],child[1]-second[1])-5)<1e-12);
   if(name==='backtrack')assert.notDeepEqual(first,[0,0]);else assert.deepEqual(child,[3,4]);
   assert.match(w.PlacementPlan.Placement.Sites.find(s=>s.DemandId===(name==='backtrack'?'z-first':'a-child')).CandidateSource,/:provisional$/);
  }else{
   assert.equal(w.PlacementPlan.ProgramInstances.length,2);
   for(let i=0;i<2;i++)assert.deepEqual(w.Nodes.find(n=>n.Id===w.Id+'/containers-'+i+'/child-0').Position,[20*i+3,4]);
  }
  assert.equal(JSON.stringify(w),before);
 }
});
test('provisional exports use the common saved-metadata viewer',()=>{
 assert.equal(fs.readFileSync(prefix+'nested/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
