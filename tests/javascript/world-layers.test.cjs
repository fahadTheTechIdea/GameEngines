const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const Schematic = require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_LAYER_PACKET||'generated/layered-exploration';
const world = JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('fresh schema 2 packet resolves every declared membership exactly once', () => {
  assert.equal(world.SchemaVersion, 2);
  assert.deepEqual(world.LayerPlan.Layers.map(l => l.Definition.Id), ['regions', 'encounters', 'traversal']);
  for (const [source, key] of [[world.Nodes, 'NodeIds'], [world.Connections, 'ConnectionIds']]) {
    const assigned = world.LayerPlan.Layers.flatMap(l => l[key]);
    assert.equal(new Set(assigned).size, assigned.length);
    assert.deepEqual(assigned.toSorted(), source.map(n => n.Id).toSorted());
    for (const layer of world.LayerPlan.Layers) {
      assert.deepEqual(layer[key], source.filter(n => n.LayerId === layer.Definition.Id).map(n => n.Id).toSorted());
    }
  }
  assert.deepEqual(world.LayerPlan.UnassignedNodeIds, []);
  assert.deepEqual(world.LayerPlan.UnassignedConnectionIds, []);
});

test('semantic dependencies and physical floors remain distinct', () => {
  const positions = new Map(world.LayerPlan.Layers.map((l,i) => [l.Definition.Id,i]));
  for (const {Definition:d} of world.LayerPlan.Layers) {
    for (const prerequisite of d.DependsOn) assert.ok(positions.get(prerequisite) < positions.get(d.Id));
    assert.ok(d.Purpose && d.SourceRuleId);
  }
  assert.deepEqual(world.Space.Axes, ['x','y','z']);
  for (let floor=0; floor<2; floor++) for (let room=0; room<6; room++) {
    const node=world.Nodes.find(n => n.Id===`dungeon/floors-${floor}/rooms-${room}`);
    assert.equal(node.LayerId,'encounters');
    assert.deepEqual(node.Position,[Math.min(room*12,36),Math.max(0,room*12-36),floor===0 ? 0 : -8*floor]);
  }
  assert.ok(!world.Nodes.some(n => ['city','district','parcel','block'].includes(n.Kind)));
});

test('saved review exposes layer scope and preserves exact embedded schematic metadata', () => {
  const review=fs.readFileSync(`${folder}/review.html`,'utf8');
  assert.ok(review.includes('Semantic layers') && review.includes('Without a declared layer: 0 nodes and 0 links'));
  const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');
  // This check covers packaged data, not browser interactions or physical-map rendering.
  const embedded=html.match(/<script[^>]*id="world-data"[^>]*>([\s\S]*?)<\/script>/);
  assert.ok(embedded,'JSON data script is present');
  assert.deepEqual(JSON.parse(embedded[1]),world);
});

test('semantic inspection resolves exact memberships without changing world data',()=>{
  const before=JSON.stringify(world),index=Schematic.semanticLayers(world);
  for(const node of world.Nodes)assert.equal(index.nodes.get(node.Id),node.LayerId);
  for(const link of world.Connections)assert.equal(index.connections.get(link.Id),link.LayerId);
  assert.equal(index.entries.size,3);assert.equal(JSON.stringify(world),before);
  for(const mutate of [
    w=>w.LayerPlan.Layers[0].NodeIds.push(w.LayerPlan.Layers[0].NodeIds[0]),
    w=>w.LayerPlan.UnassignedNodeIds.push(w.Nodes[0].Id),
    w=>w.Nodes[0].LayerId='missing',
    w=>w.LayerPlan.Layers[0].Definition.DependsOn.push(w.LayerPlan.Layers[2].Definition.Id),
    w=>w.LayerPlan.Layers[0].Definition.RemainingScopes.push('Unresolved content'),
    w=>w.LayerPlan.Layers[0].ConnectionIds.push(w.Connections[0].Id),
    w=>w.LayerPlan=null
  ]){const bad=structuredClone(world);mutate(bad);assert.throws(()=>Schematic.semanticLayers(bad),/Semantic layer/);}
});

test('palette is an isolated versioned presentation contract with safe declared colors',()=>{
  const index=Schematic.semanticLayers(world),before=JSON.stringify(world),settings={Version:1,LayerColors:{encounters:'#A012EF'}};
  const palette=Schematic.palette(settings,index.entries);assert.deepEqual(palette,{Version:1,LayerColors:{encounters:'#a012ef'}});
  settings.LayerColors.encounters='#ffffff';assert.equal(palette.LayerColors.encounters,'#a012ef');assert.equal(JSON.stringify(world),before);
  for(const bad of [{Version:2,LayerColors:{}},{Version:1,LayerColors:{missing:'#123456'}},{Version:1,LayerColors:{encounters:'url(https://example.invalid)'}},
    {Version:1,LayerColors:{encounters:'#abc'}},{Version:1,LayerColors:[]},{Version:1,LayerColors:{},Extra:true},null])assert.throws(()=>Schematic.palette(bad,index.entries),/Palette/);
  // Catalog-valid special IDs remain data, including object prototype names.
  const special=new Map([['__proto__',{}],['null',{}],[':layer',{}]]),data=JSON.parse('{"Version":1,"LayerColors":{"__proto__":"#123456","null":"#abcdef",":layer":"#654321"}}');
  const safe=Schematic.palette(data,special);assert.equal(Object.getPrototypeOf(safe.LayerColors),Object.prototype);assert.equal(Object.hasOwn(safe.LayerColors,'__proto__'),true);
  assert.equal(safe.LayerColors.__proto__,'#123456');assert.equal(Object.prototype.polluted,undefined);
});
