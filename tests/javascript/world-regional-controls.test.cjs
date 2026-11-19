const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGIONAL_CONTROLS_ROOT||'generated/regional-controls-native-qualified';
const pinRoot=process.env.WORLD_REGION_PINS_ROOT||'generated/regional-controls-pins-qualified';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
test('automatic and pinned intent candidates retain exact positions and clearance',()=>{
 const p=read(path.join(pinRoot,'intent-plan.json')),seeds=S.regionIntentSeeds(p);
 assert.deepEqual(seeds.find(s=>s.Id==='chosen').Position,{X:75,Y:20});
 assert.equal(p.IntentPlan.Candidates.find(c=>c.IntentId==='chosen'&&c.Eligible).Anchor.Clearance,20);
 assert.ok(p.IntentPlan.Candidates.some(c=>c.Rejection==='required-position-outside'&&c.Anchor.Clearance<0));
 for(const mutate of [p=>p.Request.Intents[1].RequiredPosition.X++,p=>p.IntentPlan.Selections[1].Seed.Position.Y++,
  p=>p.IntentPlan.Candidates.find(c=>c.Rejection==='required-position-outside').Eligible=true,
  p=>p.IntentPlan.Candidates.find(c=>c.Eligible&&c.IntentId==='chosen').Anchor.Clearance=-1]){
  const changed=structuredClone(p);mutate(changed);assert.throws(()=>S.regionIntentSeeds(changed),/Invalid region intent/);
 }
});
for(const name of ['initial','changed','other-region','rollback'])test(`${name}: complete pinned world remains inspectable`,()=>{
 const w=read(path.join(root,name,'world-regions.json')),view=R.layout(w);
 assert.equal(view.boxes.length,10);assert.equal(w.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),6);
 const source=w.Definition.OwnershipPlan.Source.Regions.find(r=>r.Id==='arrival'),r=w.Definition.Regions.find(r=>r.Id==='arrival');
 assert.deepEqual(r.Recipe.Origin,[source.PinnedAnchor.Position.X,source.PinnedAnchor.Position.Y]);
 assert.ok(w.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
 for(const mutate of [w=>w.Definition.OwnershipPlan.Source.Regions[0].PinnedAnchor.Position.X++,
  w=>w.Definition.OwnershipPlan.Anchors[0].Plan.Request.Intents[0].RequiredPosition.Y++,
  w=>delete w.Definition.OwnershipPlan.Source.Regions[0].PinnedAnchor]){
  const changed=structuredClone(w);mutate(changed);assert.throws(()=>R.layout(changed),/Invalid region/);
 }
});
test('actual terrain moves the unlocked location and retains every locked source pose',()=>{
 const before=read(path.join(root,'initial','world-regions.json')),after=read(path.join(root,'changed','world-regions.json'));
 const unlocked=read(path.join(root,'unlocked-changed','world-regions.json')),region=w=>w.Definition.Regions.find(r=>r.Id==='arrival');
 assert.deepEqual(region(before).Recipe.Origin,region(after).Recipe.Origin);assert.notDeepEqual(region(before).Recipe.Origin,region(unlocked).Recipe.Origin);
 assert.deepEqual(before.Regions[0].World.Nodes,after.Regions[0].World.Nodes);
 const checkpoint=read(path.join(root,'changed','generation-session.json'));
 assert.equal(checkpoint.Protections.Inputs.length,3);assert.equal(checkpoint.Protections.Anchors.length,1);
 assert.deepEqual(checkpoint.InitialReport.Protections,checkpoint.Protections);
 assert.ok(checkpoint.Edits.every(e=>JSON.stringify(e.Report.Protections)===JSON.stringify(checkpoint.Protections)));
});
