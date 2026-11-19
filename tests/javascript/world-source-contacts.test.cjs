const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_SOURCE_CONTACT_PACKET||'generated/source-contact-semantic/source-world/world';
const load=()=>JSON.parse(fs.readFileSync(folder+'/world-metadata.json','utf8'));
test('the viewer retains all six source compounds, nested members and actual routes',()=>{
 const w=load(),before=JSON.stringify(w),view=S.layout(w);
 assert.equal(view.boxes.length,29);assert.equal(w.AccessPlan.Sites.length,28);
 assert.equal(w.Nodes.filter(n=>n.Kind==='compound').length,6);
 assert.equal(w.RoutePlan.Routes.length,19);assert.equal(w.Nodes.filter(n=>n.ArchetypeId==='research').length,2);
 assert.equal(JSON.stringify(w),before);
 assert.ok(w.PlacementPlan.Placement.Sites.some(s=>s.CandidateSource.includes(':source-contact:')));
});
test('the published viewer uses the current JavaScript source and actual stored world',()=>{
 assert.equal(fs.readFileSync(folder+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const html=fs.readFileSync(folder+'/schematic.html','utf8');
 assert.ok(html.includes('world-metadata.json'));assert.ok(html.includes('schematic.js'));
});
