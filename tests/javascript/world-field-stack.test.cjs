const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_FIELD_STACK_ROOT||'generated/s02-field-inspection-20261102';
const load=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`));

test('complete saved field stack retains definitions, independent units and every input sample',()=>{
 for(const name of ['wind','retained-water','environment']){
  const world=load(name),before=JSON.stringify(world),fields=S.fieldStack(world),definition=world.EnvironmentPlan.Definition;
  assert.equal(fields.length,definition.Fields.length+(definition.SampledFields?.length??0));
  for(const source of definition.SampledFields??[]){
   const entry=fields.find(f=>f.Key==='sampled:'+source.Id),{Values,...metadata}=source;
   assert.deepEqual(entry.Definition,metadata);assert.equal(entry.SourceValues.Count,Values.length);
   assert.equal(entry.SourceValues.Minimum,Math.min(...Values));assert.equal(entry.SourceValues.Maximum,Math.max(...Values));
   for(let row=0;row<source.Rows;row++)for(let column=0;column<source.Columns;column++){
    const index=row*source.Columns+column,sample=S.fieldSample(world,entry.Key,index);
    assert.deepEqual(sample,{Index:index,Column:column,Row:row,
     Position:{X:source.Origin.X+column*source.StepX,Y:source.Origin.Y+row*source.StepY},
     CoordinateUnit:world.Surface.Unit,Value:Values[index],ValueUnit:source.Unit});
   }
   assert.equal(entry.Bounds.MaximumX,source.Origin.X+(source.Columns-1)*source.StepX);
   assert.equal(entry.Bounds.MaximumY,source.Origin.Y+(source.Rows-1)*source.StepY);
   assert.throws(()=>S.fieldSample(world,entry.Key,-1));assert.throws(()=>S.fieldSample(world,entry.Key,Values.length));
  }
  for(const definitionField of definition.Fields){
   const field=fields.find(f=>f.Key==='derived:'+definitionField.Id);assert.deepEqual(field.Definition,definitionField);
   const values=world.EnvironmentPlan.Observations.flatMap(e=>Object.hasOwn(e.Result.Observation.Values,definitionField.Id)?[e.Result.Observation.Values[definitionField.Id]]:[]);
   assert.equal(field.SavedPointValues.Count,values.length);
   assert.equal(field.SavedPointValues.Minimum,values.length?Math.min(...values):null);
   assert.equal(field.SavedPointValues.Maximum,values.length?Math.max(...values):null);
   for(const observation of field.Observations){
    const actual=world.EnvironmentPlan.Observations.find(e=>e.NodeId===observation.NodeId&&e.RuleId===observation.RuleId);
    assert.equal(observation.Value,actual.Result.Observation.Values[definitionField.Id]);
    assert.deepEqual(observation.Position,actual.Result.Observation.Surface.Position);
   }
  }
  S.layout(world);assert.equal(JSON.stringify(world),before);
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 }
});

test('unused declarations, zero and unavailable observations stay distinct and detached',()=>{
 const world=load('wind');world.EnvironmentPlan.Definition.Fields.push({Id:'unused',Purpose:'Declared but not sampled',SourceRuleId:'test:unused',Unit:'score',Offset:0,Terms:[]});
 const field=S.fieldStack(world).find(f=>f.Key==='derived:unused');
 assert.deepEqual(field.SavedPointValues,{Count:0,Minimum:null,Maximum:null,Shown:0});assert.deepEqual(field.Observations,[]);
 field.Definition.Purpose='presentation mutation';assert.equal(world.EnvironmentPlan.Definition.Fields.at(-1).Purpose,'Declared but not sampled');
 assert.deepEqual(S.fieldStack({}),[]);assert.throws(()=>S.fieldSample(world,'derived:unused',0));
});

test('CLI packages field controls beside the map and embeds the complete unchanged world',()=>{
 for(const name of ['wind','retained-water','environment']){
  const html=fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'),start=html.indexOf('<aside>'),end=html.indexOf('</aside>');
  assert.ok(start<html.indexOf('id="field-tools"')&&html.indexOf('id="field-tools"')<end);
  assert.match(html,/\[hidden\]\{display:none!important\}/);
  const embedded=JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]);
  assert.deepEqual(embedded,load(name));
 }
});

test('source arrays and selection refuse corruption rather than show fabricated values',()=>{
 const base=load('wind');assert.ok(base.EnvironmentPlan.Definition.SampledFields.length>0);
 for(const edit of [s=>s.Values.pop(),s=>s.Values[0]=NaN,s=>s.StepX=0,s=>s.Columns=514,s=>s.Origin.X=Infinity]){
  const world=structuredClone(base);edit(world.EnvironmentPlan.Definition.SampledFields[0]);assert.throws(()=>S.fieldStack(world));
 }
 const key='sampled:'+base.EnvironmentPlan.Definition.SampledFields[0].Id;
 for(const index of [NaN,Infinity,.5,-1])assert.throws(()=>S.fieldSample(base,key,index));
 const field=S.fieldStack(base).find(f=>f.Key===key);field.Definition.Origin.X=999;
 assert.notEqual(base.EnvironmentPlan.Definition.SampledFields[0].Origin.X,999);
});
