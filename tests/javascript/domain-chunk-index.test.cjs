const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
function read(root,name){return JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));}
function check(root,composed){
  if(!root)throw new Error('Domain chunk output root is required');
  const bytes=fs.readFileSync(path.join(root,'world.json')),world=JSON.parse(bytes),selection=read(root,'selection.json');
  assert.equal(selection.SourceSha256,crypto.createHash('sha256').update(bytes).digest('hex'));
  const domains=composed?world.Domains.Domains:world.Domains,transitions=composed?world.Domains.Transitions:world.Transitions;
  assert.deepEqual(selection.SharedTransitions,transitions);
  for(const item of selection.Domains){
    const source=domains.find(d=>d.Id===item.DomainId).World,seen=new Set();
    if(source.Surface){
      assert.equal(item.Surface.Id,source.Surface.Id);assert.deepEqual(item.Surface.Patches,source.Surface.Patches);
      assert.equal(item.Terrain.length,item.ChunkIds.length);
      const capture=source.Surface.TerrainCapturePlan?.Window;
      if(capture)assert.deepEqual(item.Surface.AvailableSamples,{FirstColumn:capture.FirstColumn,FirstRow:capture.FirstRow,Columns:capture.Columns,Rows:capture.Rows});
      const full=source.Surface.HeightField;
      if(full)assert.deepEqual(item.Surface.AvailableSamples,{FirstColumn:0,FirstRow:0,Columns:full.Columns,Rows:full.Rows});
      for(const terrain of item.Terrain)if(terrain.Window&&capture){
        const w=terrain.Window;
        for(let y=0;y<w.Rows;y++)for(let x=0;x<w.Columns;x++){
          const col=w.FirstColumn+x,row=w.FirstRow+y;
          assert.equal(w.Heights[y*w.Columns+x],capture.Heights[(row-capture.FirstRow)*capture.Columns+col-capture.FirstColumn]);
        }
      }
      for(const terrain of item.Terrain)if(terrain.Window&&full){
        const w=terrain.Window;for(let y=0;y<w.Rows;y++)for(let x=0;x<w.Columns;x++)
          assert.equal(w.Heights[y*w.Columns+x],full.Heights[(w.FirstRow+y)*full.Columns+w.FirstColumn+x]);
      }
    }else assert.equal(item.Surface,null);
    for(const member of item.Geometry){
      const key=member.Member.Kind+':'+member.Member.Id;assert.ok(!seen.has(key));seen.add(key);
      assert.ok(member.Member.ChunkIds.some(id=>item.ChunkIds.includes(id)));
      if(member.Node){
        const node=source.Nodes.find(n=>n.Id===member.Member.Id);assert.deepEqual(member.Node,node);
        assert.deepEqual(member.Connections,source.Connections.filter(c=>c.FromId===node.Id||c.ToId===node.Id));
      }
    }
    for(const node of item.SharedNodes)assert.deepEqual(node,source.Nodes.find(n=>n.Id===node.Id));
    for(const link of item.SharedConnections)assert.deepEqual(link,source.Connections.find(c=>c.Id===link.Id));
  }
  return {world,selection};
}
test('overlapping-coordinate floors retain separate domain identity and logical nodes',()=>{
  const {selection}=check(process.env.DOMAIN_CHUNK_WORLD,false),lower=selection.Domains.find(d=>d.DomainId==='lower'),upper=selection.Domains.find(d=>d.DomainId==='upper');
  assert.deepEqual(lower.Geometry,upper.Geometry);assert.notEqual(lower.DomainId,upper.DomainId);
  const logical=selection.Domains.find(d=>d.DomainId==='progression');assert.ok(logical.SharedNodes.length>0&&logical.SharedConnections.length>0);
  assert.ok(selection.SharedTransitions.some(t=>t.Definition.Id==='state'));assert.ok(selection.SharedProgression.Results.length>0);
});
test('partial regional selection retains complete regional routes and explicit portal',()=>{
  const {world,selection}=check(process.env.REGIONAL_DOMAIN_CHUNK_WORLD,true);
  assert.deepEqual(selection.SharedRegionalRoutes,world.RegionalRoutes);assert.ok(selection.SharedRegionalRoutes[0].Forward.Path.Points.length>1);
  assert.ok(selection.Domains.find(d=>d.DomainId==='west').Terrain.every(t=>t.Window?.Heights.length>0));
  assert.ok(selection.SharedTransitions.some(t=>t.Definition.Id==='portal'));assert.equal(selection.SharedJourneys.length,1);
});
