const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const folder = 'generated/irregular-surface';
const world = JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
const recipe = JSON.parse(fs.readFileSync(`${folder}/recipe.json`));

test('saved domain preserves concavity and declared field rather than a bounding rectangle', () => {
  assert.equal(world.SchemaVersion,2);
  assert.deepEqual(world.Surface,recipe.Surface);
  assert.deepEqual(world.Surface.Boundary.Vertices,[{X:0,Y:0},{X:120,Y:0},{X:120,Y:40},{X:70,Y:40},{X:70,Y:100},{X:0,Y:100}]);
  assert.equal(world.Surface.Patches[0].GradientX,.1);
  assert.equal(world.Surface.Patches[0].GradientY,.2);
  assert.equal(world.Surface.Unit,world.Space.Unit);
});

test('actual positioned content remains explicit and does not turn logical links into roads', () => {
  const positions=world.Nodes.filter(n=>n.Kind==='destination').map(n=>n.Position);
  assert.deepEqual(positions,[[10,10],[30,10],[50,10],[50,30]]);
  assert.equal(world.Connections.length,3);
  assert.ok(world.Connections.every(c=>c.Kind==='travel-intent'));
  assert.equal(world.MapGeometry,null);
  assert.equal(world.Coverage,'PartialScope');
  assert.ok(world.RemainingScopes.some(s=>s.includes('route generation')));
});

test('summary and packaged schematic retain the same surface facts', () => {
  const summary=JSON.parse(fs.readFileSync(`${folder}/summary.json`));
  assert.equal(summary.SurfaceId,'hill');
  assert.equal(summary.TerrainPatches,1);
  const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');
  const embedded=html.match(/<script[^>]*id="world-data"[^>]*>([\s\S]*?)<\/script>/);
  assert.deepEqual(JSON.parse(embedded[1]).Surface,world.Surface);
});
