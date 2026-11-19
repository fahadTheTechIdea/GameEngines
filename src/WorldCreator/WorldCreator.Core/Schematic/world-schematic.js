/* Dependency-free schematic presentation. Engine geometry is authoritative when supplied. */

class WorldSchematic {
    // A region scene uses the same renderer and its original metadata. Only its
    // controls and SVG definition IDs are scoped; there is no combined fake world.
    static renderScene(world, svg, controls, namespace, terrainScale) {
        const viewer=Object.create(WorldSchematic.prototype);
        Object.assign(viewer,{svg,controls,namespace,terrainScale,sceneOnly:true,events:[],selected:null,
            details:document.createElement('section'),status:document.createElement('p')});
        viewer.load(world);
        return viewer;
    }
    control(selector) { return (this.controls||document).querySelector(selector); }
    controlsAll(selector) { return (this.controls||document).querySelectorAll(selector); }
    svgId(id) { return this.namespace?`${this.namespace}-${id}`:id; }
    worldId() { return this.plan.world.Id; }
    exportProvenance() { return {RecipeSha256:this.plan.world.RecipeSha256,CatalogSha256:this.plan.world.CatalogSha256}; }
    static async readStored(source,options={}) {
        if(!source||source.StorageFormat===undefined)return source;
        const fail=()=>{throw new Error('World data is missing, changed or outside its storage limits. Regenerate the export.');};
        const keys=(value,names)=>value&&typeof value==='object'&&!Array.isArray(value)&&Object.keys(value).sort().join(',')===names.slice().sort().join(',');
        const hash=value=>typeof value==='string'&&/^[0-9a-f]{64}$/.test(value);
        const partBytes=1048576,maximum=268435456;
        if(!keys(source,['StorageFormat','Version','DocumentSha256','DocumentBytes','Parts'])||source.StorageFormat!=='worldcreator-json-chunks'||source.Version!==1||
            !hash(source.DocumentSha256)||!Number.isSafeInteger(source.DocumentBytes)||source.DocumentBytes<=8388608||source.DocumentBytes>maximum||
            !Array.isArray(source.Parts)||source.Parts.length!==Math.ceil(source.DocumentBytes/partBytes)||source.Parts.length>256)fail();
        for(let i=0;i<source.Parts.length;i++){
            const part=source.Parts[i],expected=Math.min(partBytes,source.DocumentBytes-i*partBytes);
            if(!keys(part,['Sha256','Bytes'])||!hash(part.Sha256)||part.Bytes!==expected)fail();
        }
        const cancelled=()=>{if(options.signal?.aborted)throw new DOMException('World data loading cancelled.','AbortError');};
        const digest=options.sha256||(async bytes=>{
            if(!globalThis.crypto?.subtle)throw new Error('Secure checksum support is required to load this world.');
            return [...new Uint8Array(await globalThis.crypto.subtle.digest('SHA-256',bytes))].map(v=>v.toString(16).padStart(2,'0')).join('');
        });
        const readPart=options.readPart||(async part=>{
            const url=new URL(`_worlddata/${part.Sha256}.bin`,document.baseURI);
            if(url.origin!==location.origin)fail();
            const response=await fetch(url,{signal:options.signal,credentials:'same-origin'});
            if(!response.ok||!response.body)fail();
            const reader=response.body.getReader(),bytes=new Uint8Array(part.Bytes);let offset=0;
            try{
                while(true){cancelled();const next=await reader.read();if(next.done)break;
                    if(next.value.byteLength>bytes.length-offset)fail();bytes.set(next.value,offset);offset+=next.value.byteLength;}
            }catch(error){await reader.cancel().catch(()=>{});throw error;}finally{reader.releaseLock();}
            if(offset!==part.Bytes)fail();return bytes;
        });
        cancelled();const bytes=new Uint8Array(source.DocumentBytes);let offset=0;
        for(const part of source.Parts){
            cancelled();const data=await readPart(part);cancelled();
            if(!(data instanceof Uint8Array)||data.byteLength!==part.Bytes||await digest(data)!==part.Sha256)fail();
            bytes.set(data,offset);offset+=data.length;
        }
        cancelled();if(offset!==source.DocumentBytes||await digest(bytes)!==source.DocumentSha256)fail();
        const value=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
        if(value?.StorageFormat!==undefined)fail();return value;
    }

    static regionDemand(world) {
        const p=world.RegionDemandPlan,fail=()=>{throw new Error('Invalid region source demand. Regenerate with the engine.');};
        if(p==null){if(world.RegionDemandId!=null||world.ProgramCompositionPlan?.SourceRecipe?.RegionDemands!=null)fail();return null;}
        const d=p.Definition,equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),finite=n=>Number.isFinite(n)&&n>0;
        const text=s=>typeof s==='string'&&s.trim().length>0,near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        if(p.Version!==1||d?.Version!==1||world.RegionDemandId!==d.Id||![d.Id,d.Purpose,d.SourceRuleId].every(text)||
            !Number.isSafeInteger(d.MaximumWork)||d.MaximumWork<1||d.MaximumWork>20000000||!Number.isSafeInteger(p.Work)||p.Work<1||p.Work>d.MaximumWork||
            !Array.isArray(d.Rules)||d.Rules.length<1||d.Rules.length>32||!Array.isArray(p.Results)||p.Results.length!==d.Rules.length||
            !Array.isArray(p.SourceRequest?.Intents)||!p.DerivedRequest||!world.RegionPlan||!world.PlacementPlan)fail();
        const derived=structuredClone(p.SourceRequest),ids=new Set(),regions=new Set(),sources=new Set();
        for(let i=0;i<d.Rules.length;i++){
            const rule=d.Rules[i],result=p.Results[i],intent=derived.Intents.find(v=>v.Id===rule?.RegionId);
            if(!rule||![rule.Id,rule.Purpose,rule.SourceRuleId,rule.RegionId].every(text)||ids.has(rule.Id)||regions.has(rule.RegionId)||
                !finite(rule.MaximumCoverage)||rule.MaximumCoverage>1||!intent||!equal(result?.Rule,rule)||!Array.isArray(result.Sources)||!result.Sources.length)fail();
            ids.add(rule.Id);regions.add(rule.RegionId);let total=0,radius=0;
            const expected=world.PlacementPlan.Definition.Request.Demands.filter(v=>v.RegionId===rule.RegionId).map(v=>world.PlacementPlan.Definition.Bindings.find(b=>b.DemandId===v.Id)?.NodeId).sort();
            if(!equal(result.Sources.map(s=>s.Source?.NodeId),expected))fail();
            for(const measured of result.Sources){
                const source=measured?.Source,actual=world.PlacementPlan.Footprints.find(s=>s.NodeId===source?.NodeId),envelope=source?.Reservation||source?.Polygon;
                if(!source||sources.has(source.NodeId)||!equal(source,actual)||source.Unit!==world.Space.Unit||!Array.isArray(envelope?.Vertices)||envelope.Vertices.length<3||envelope.Vertices.length>64)fail();
                sources.add(source.NodeId);const vertices=envelope.Vertices,o=vertices[0];let twice=0,anchorRadius=0;
                for(let n=0;n<vertices.length;n++){
                    const a=vertices[n],b=vertices[(n+1)%vertices.length];
                    if(![a.X,a.Y].every(Number.isFinite))fail();twice+=(a.X-o.X)*(b.Y-o.Y)-(a.Y-o.Y)*(b.X-o.X);anchorRadius=Math.max(anchorRadius,Math.hypot(a.X,a.Y));
                }
                const area=Math.abs(twice)/2;
                if(!finite(area)||!near(measured.EnvelopeArea,area)||!near(measured.AnchorRadius,anchorRadius))fail();total+=area;radius=Math.max(radius,anchorRadius);
            }
            if(!near(result.EnvelopeArea,total)||!near(result.RequiredArea,total/rule.MaximumCoverage)||!near(result.RequiredClearance,radius))fail();
            intent.MinimumArea=Math.max(intent.MinimumArea,result.RequiredArea);intent.MinimumClearance=Math.max(intent.MinimumClearance,result.RequiredClearance);
            const actual=world.RegionPlan.Request.Intents?.find(v=>v.Id===rule.RegionId);
            if(!actual||actual.MinimumArea!==intent.MinimumArea||actual.MinimumClearance!==intent.MinimumClearance)fail();
        }
        if(!equal(derived,p.DerivedRequest))fail();return p;
    }
    static regionIntentSeeds(plan,failedQuery=false) {
        const r=plan.Request,intents=r?.Intents||[],p=plan.IntentPlan;
        const fail=()=>{throw new Error('Invalid region intent witness. Regenerate with the engine.');};
        if(!intents.length){if(failedQuery||p!=null||r.IntentPartition!=null||r.RequiredAssignment!=null)fail();return [];}
        const integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max;
        const point=v=>v&&Number.isFinite(v.X)&&Number.isFinite(v.Y);
        const excluded=r.ExcludedAssignments;
        if(r.RequiredAssignment!=null&&(!Array.isArray(r.RequiredAssignment)||r.RequiredAssignment.length!==intents.length||r.RequiredAssignment.some(c=>!integer(c,0,4095))))fail();
        if(excluded!=null&&(!Array.isArray(excluded)||excluded.length>64||excluded.some(v=>!Array.isArray(v)||v.length!==intents.length||v.some(c=>!integer(c,0,4095)))||
            new Set(excluded.map(v=>JSON.stringify(v))).size!==excluded.length))fail();
        if(!p||!integer(intents.length,1,32)||!Number.isFinite(r.AnchorPrecision)||r.AnchorPrecision<.000001||r.AnchorPrecision>100000||
            !integer(r.MaximumAnchorCells,1,1000000)||!integer(r.MaximumIntentCandidates,1,4096)||!integer(r.MaximumIntentStates,1,1000000)||
            !Array.isArray(p.Candidates)||!integer(p.Candidates.length,failedQuery?0:1,r.MaximumIntentCandidates)||!Array.isArray(p.Selections)||p.Selections.length!==(failedQuery?0:intents.length)||
            !integer(p.AnchorCells,failedQuery?0:1,r.MaximumAnchorCells)||!integer(p.SearchStates,failedQuery?1:intents.length+1,r.MaximumIntentStates))fail();
        const ids=new Map(intents.map(i=>[i?.Id,i])),references=new Set(),anchors=new Map(),selected=new Set(),components=new Set();
        if(ids.size!==intents.length||[...ids.keys()].some(id=>typeof id!=='string'||(r.Seeds||[]).some(s=>s.Id===id)))fail();
        if(intents.some(i=>i.RequiredPosition!=null&&(!point(i.RequiredPosition)||Math.abs(i.RequiredPosition.X)>1e7||Math.abs(i.RequiredPosition.Y)>1e7)))fail();
        if(intents.some(i=>i.AllocationWeight!=null&&(!Number.isFinite(i.AllocationWeight)||i.AllocationWeight<=0||i.AllocationWeight>4e14||r.IntentPartition==null)))fail();
        for(const c of p.Candidates){
            const intent=ids.get(c?.IntentId),key=JSON.stringify([c?.IntentId,c?.Component]);
            if(!intent||!integer(c.Component,0,4095)||references.has(key)||!Number.isFinite(c.Area)||c.Area<=0||typeof c.Eligible!=='boolean'||
                (c.Eligible?c.Rejection!=null:!['minimum-area','protected-ownership','minimum-clearance','required-position-outside'].includes(c.Rejection)))fail();
            references.add(key);
            if(c.Anchor!=null){
                const a=c.Anchor;
                const key=JSON.stringify([c.Component,intent.RequiredPosition??null]),pinned=intent.RequiredPosition!=null;
                if(!point(a.Position)||!Number.isFinite(a.Clearance)||(!pinned&&a.Clearance<=0)||!Number.isFinite(a.UpperBound)||a.UpperBound<a.Clearance||a.UpperBound-a.Clearance>r.AnchorPrecision+1e-8||
                    pinned&&(JSON.stringify(a.Position)!==JSON.stringify(intent.RequiredPosition)||a.UpperBound!==a.Clearance||a.Evaluations!==1)||
                    !integer(a.Evaluations,1,r.MaximumAnchorCells)||anchors.has(key)&&JSON.stringify(anchors.get(key))!==JSON.stringify(a))fail();
                anchors.set(key,a);
            }
            if(c.Rejection==='required-position-outside'&&(intent.RequiredPosition==null||!c.Anchor||c.Anchor.Clearance>0))fail();
            if(c.Eligible&&(!c.Anchor||c.Anchor.Clearance<=0||c.Area<intent.MinimumArea||c.Anchor.Clearance<intent.MinimumClearance))fail();
        }
        if([...anchors.values()].reduce((sum,a)=>sum+a.Evaluations,0)!==p.AnchorCells)fail();
        // Failed one-intent query receipts are distinct from normal world seeds.
        // A saved failure never supplies a fabricated accepted selection.
        if(failedQuery){
            if(intents.length!==1||r.Seeds?.length||r.IntentPartition!=null||r.IntentRelations?.length||r.RequiredAssignment!=null||r.ExcludedAssignments?.length||p.Candidates.some(c=>c.Eligible))fail();
            return [];
        }
        const partitionSeeds=WorldSchematic.regionPartitionSeeds(plan);
        for(const s of p.Selections){
            const i=ids.get(s?.IntentId),c=p.Candidates.find(c=>c.IntentId===s?.IntentId&&c.Component===s?.Component),seed=s?.Seed;
            if(!i||!c?.Eligible||selected.has(i.Id)||!partitionSeeds&&components.has(s.Component)||!seed||seed.Id!==i.Id||seed.Purpose!==i.Purpose||seed.SourceRuleId!==i.SourceRuleId||seed.MinimumArea!==i.MinimumArea||
                JSON.stringify(seed.Position)!==JSON.stringify(partitionSeeds?.get(i.Id)||c.Anchor.Position)||JSON.stringify(seed.Profile)!==JSON.stringify(i.Profile||r.Profile))fail();
            selected.add(i.Id);components.add(s.Component);
        }
        // C# replays component topology, distance bounds and assignment; this consumes saved witnesses.
        const vector=intents.map(i=>p.Selections.find(s=>s.IntentId===i.Id).Component);
        if(r.RequiredAssignment!=null&&JSON.stringify(r.RequiredAssignment)!==JSON.stringify(vector))fail();
        if(excluded?.some(v=>JSON.stringify(v)===JSON.stringify(vector)))fail();
        return p.Selections.map(s=>s.Seed);
    }
    static regionPartitionSeeds(plan) {
        const request=plan.Request,proof=plan.IntentPlan,policy=request.IntentPartition;
        const fail=()=>{throw new Error('Invalid region partition witness. Regenerate with the engine.');};
        if(policy==null){if(proof?.Partitions!=null||proof?.PartitionAttempts!=null)fail();return null;}
        const integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max,point=p=>p&&[p.X,p.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=1e7);
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),intents=request.Intents||[],byId=new Map(intents.map(i=>[i.Id,i]));
        if(!policy.Pattern||!integer(policy.MaximumAssignments,1,4096)||!Array.isArray(proof?.Partitions)||!integer(proof.Partitions.length,1,intents.length)||
            !Array.isArray(proof.PartitionAttempts)||!integer(proof.PartitionAttempts.length,1,policy.MaximumAssignments))fail();
        const expectedPattern={...policy.Pattern,Interior:policy.Pattern.Interior??{Precision:request.AnchorPrecision,MaximumCells:request.MaximumAnchorCells,MinimumClearance:0}};
        const seeds=new Map(),components=new Set();
        for(const part of proof.Partitions){
            const area=part?.AreaPlan;
            if(!part||!integer(part.Component,0,4095)||components.has(part.Component)||!Array.isArray(part.IntentIds)||!integer(part.IntentIds.length,1,intents.length)||
                !area||area.SchemaVersion!==1||area.RegionId!==`component-${part.Component}`||!equal(area.Pattern,expectedPattern)||
                !Number.isFinite(area.Area)||area.Area<=0||!Number.isFinite(area.Mass)||area.Mass<=0||!integer(area.Work,1,request.MaximumWork)||
                !Array.isArray(area.Strata)||area.Strata.length!==part.IntentIds.length||
                !equal(area.RelativeWeights,part.IntentIds.map(id=>byId.get(id)?.AllocationWeight??byId.get(id)?.MinimumArea)))fail();
            components.add(part.Component);let total=0,mass=0,cells=0;
            const weight=area.RelativeWeights.reduce((sum,w)=>sum+w,0);
            for(let i=0;i<part.IntentIds.length;i++){
                const id=part.IntentIds[i],intent=byId.get(id),s=area.Strata[i],region=plan.Regions.find(r=>r.Id===id),selection=proof.Selections.find(p=>p.IntentId===id);
                const interior=s?.Interior;
                if(intent?.RequiredPosition!=null&&(!equal(s?.Position,intent.RequiredPosition)||!(interior?.Clearance>0)||interior?.UpperBound!==interior?.Clearance||interior?.Evaluations!==1))fail();
                if(!intent||seeds.has(id)||selection?.Component!==part.Component||!s||s.Id!==`${area.RegionId}/area-${String(i).padStart(5,'0')}`||
                    !point(s.Position)||!Number.isFinite(s.Area)||s.Area<intent.MinimumArea||!Number.isFinite(s.Mass)||s.Mass<=0||
                    !interior||!point(interior.Position)||!equal(s.Position,interior.Position)||!Number.isFinite(interior.Clearance)||
                    interior.Clearance<Math.max(intent.MinimumClearance,expectedPattern.Interior.MinimumClearance)||!Number.isFinite(interior.UpperBound)||
                    interior.UpperBound<interior.Clearance||interior.UpperBound-interior.Clearance>expectedPattern.Interior.Precision+1e-8||
                    !integer(interior.Evaluations,1,expectedPattern.Interior.MaximumCells)||!Array.isArray(s.Cells)||!s.Cells.length||
                    !equal(region?.Cells,s.Cells)||!equal(region?.BoundaryEdges,s.BoundaryEdges)||region.Area!==s.Area||
                    Math.abs(s.Mass-area.Mass*(intent.AllocationWeight??intent.MinimumArea)/weight)>area.Mass*1e-7)fail();
                for(const cell of s.Cells)if(!Array.isArray(cell?.Vertices)||!integer(cell.Vertices.length,3,64)||!cell.Vertices.every(point))fail();
                cells+=s.Cells.length;total+=s.Area;mass+=s.Mass;seeds.set(id,s.Position);
            }
            if(cells>area.Pattern.MaximumCells||Math.abs(total-area.Area)>Math.max(1,area.Area)*1e-8||Math.abs(mass-area.Mass)>area.Mass*1e-8)fail();
        }
        if(seeds.size!==intents.length)fail();
        for(let i=0;i<proof.PartitionAttempts.length;i++){
            const a=proof.PartitionAttempts[i],last=i===proof.PartitionAttempts.length-1;
            if(!a||a.Ordinal!==i+1||a.Accepted!==last||!Array.isArray(a.Components)||a.Components.length!==intents.length||a.Components.some(c=>!integer(c,0,4095))||
                !Array.isArray(a.Partitions)||a.Partitions.length>intents.length||(last?a.Rejection!=null:typeof a.Rejection!=='string'||!a.Rejection.length))fail();
            if(last&&(!equal(a.Partitions,proof.Partitions)||!equal(a.Components,intents.map(intent=>proof.Selections.find(s=>s.IntentId===intent.Id)?.Component))))fail();
        }
        return seeds;
    }
    static populationCoordination(world) {
        const source=world.PlacementPlan?.Definition?.SourceWorld,guards=source?.PlacementPlan?.Definition?.Nogoods||[],p=world.PopulationCoordinationPlan;
        const fail=()=>{throw new Error('Invalid population coordination witness. Regenerate with the engine.');};
        if(p==null){if(guards.some(n=>n.ConsumerRecipeSha256===world.RecipeSha256)||(source?.PlacementPlan?.Placement?.Request?.Nogoods||[]).some(n=>n.ConsumerRecipeSha256===world.RecipeSha256))fail();return null;}
        const integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max,hash=v=>typeof v==='string'&&/^[0-9a-f]{64}$/.test(v);
        if(!source||p.Version!==3||!p.SourceRecipe?.PopulationSource||!p.SourceCatalog||p.SourceRecipe.Id!==world.Id||p.SourceRecipe.CatalogId!==p.SourceCatalog.Id||
            !integer(p.MaximumAttempts,1,256)||!integer(p.MaximumWork,1,200000000)||p.MaximumAttempts!==p.SourceRecipe.Placement?.MaximumFeasibilityAttempts||
            p.MaximumWork!==p.SourceRecipe.Placement?.MaximumWork||!integer(p.Work,1,p.MaximumWork)||!hash(p.AreaDependenciesSha256)||
            !Array.isArray(p.Dependencies)||!Array.isArray(p.Attempts)||!integer(p.Attempts.length,2,p.MaximumAttempts))fail();
        const bindingWork=p.SourceBindingWork??0;
        if(!integer(bindingWork,0,p.MaximumWork)||bindingWork>0&&p.SourceRecipe.PopulationSource.Context==null)fail();
        let work=bindingWork;const added=[];
        for(let i=0;i<p.Attempts.length;i++){
            const a=p.Attempts[i],accepted=i===p.Attempts.length-1;
            if(!a||a.Ordinal!==i+1||a.Accepted!==accepted||!hash(a.SourceRecipeSha256)||!hash(a.SourceWorldSha256)||!integer(a.Work,1,p.MaximumWork))fail();
            work+=a.Work;
            if(accepted){if(a.Rejection!=null||a.AddedNogood!=null||a.SourceRecipeSha256!==source.RecipeSha256)fail();continue;}
            const n=a.AddedNogood;
            if(typeof a.Rejection!=='string'||!a.Rejection.length||!n||typeof n.Id!=='string'||!n.Id.length||typeof n.Purpose!=='string'||!n.Purpose.length||
                typeof n.SourceRuleId!=='string'||!n.SourceRuleId.length||n.ConsumerRecipeSha256!==world.RecipeSha256||!Array.isArray(n.Assignments)||(n.WholeAssignmentSha256!=null?(!hash(n.WholeAssignmentSha256)||n.Assignments.length!==0):!integer(n.Assignments.length,1,16))||
                new Set(n.Assignments.map(a=>a?.NodeId)).size!==n.Assignments.length)fail();
            let unchanged=true;
            for(const pose of n.Assignments){
                const node=source.Nodes.find(n=>n.Id===pose?.NodeId);
                if(!node||!Array.isArray(node.Position)||node.Position.length!==2||!pose.Position||!Number.isFinite(pose.Position.X)||!Number.isFinite(pose.Position.Y)||Math.abs(pose.Position.X)>1e7||Math.abs(pose.Position.Y)>1e7)fail();
                if(node.Position[0]!==pose.Position.X||node.Position[1]!==pose.Position.Y)unchanged=false;
            }
            if((n.WholeAssignmentSha256==null&&unchanged)||!guards.some(g=>JSON.stringify(g)===JSON.stringify(n)))fail();
            added.push(n.Id);
        }
        if(work!==p.Work||new Set(added).size!==added.length)fail();
        // C# authenticates input hashes and replays rejected and selected attempts.
        return p;
    }
    static inheritedProgramMix(world,allocation) {
        const source=world.PlacementPlan?.Definition?.SourceWorld?.ProgramMixPlan?.Allocations?.find(a=>a.Key===allocation.Key);
        return source!=null&&JSON.stringify(source)===JSON.stringify(allocation);
    }
    static inheritedProgramRegion(world) {
        const source=world.PlacementPlan?.Definition?.SourceWorld?.RegionPlan;
        return source!=null&&JSON.stringify(source)===JSON.stringify(world.RegionPlan);
    }
    static programComposition(world) {
        const plan=world.ProgramCompositionPlan,fail=()=>{throw new Error('Invalid program composition witness. Regenerate with the engine.');};
        const upstream=world.PlacementPlan?.Definition?.SourceWorld;
        if((upstream?.ProgramMixPlan?.Allocations??[]).some(source=>!world.ProgramMixPlan?.Allocations?.some(a=>JSON.stringify(a)===JSON.stringify(source)))||
            upstream?.ProgramCompositionPlan?.Attempts?.at(-1)?.RegionSelection!=null&&!this.inheritedProgramRegion(world))fail();
        if(plan==null){if(world.ProgramCompositionId!=null||(world.ProgramMixPlan?.Allocations||[]).some(a=>a.Request?.ExcludedCounts?.length&&!this.inheritedProgramMix(world,a))||
            world.RegionPlan?.Request?.ExcludedAssignments?.length&&!this.inheritedProgramRegion(world))fail();return null;}
        const d=plan.Definition,integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max;
        const inherited=plan.SourceRecipe?.PopulationSource?.Context?.UsePlacementBoundary===true&&upstream?.ProgramCompositionPlan!=null;
        if(plan.Version!==1||!d||world.ProgramCompositionId!==d.Id||!integer(d.MaximumAttempts,1,64)||!integer(d.MaximumAlternativesPerMix,1,64)||!integer(d.MaximumSelections,1,16384)||!integer(d.MaximumMixWork,1,20000000)||!integer(d.MaximumCacheBytes,1,8388608)||!integer(d.MaximumReceiptBytes,1,8388608)||!integer(d.MaximumRegionAlternatives,1,64)||!integer(d.MaximumRegionWork,1,200000000)||
            !plan.SourceRecipe||!plan.SourceCatalog||JSON.stringify(d)!==JSON.stringify(plan.SourceRecipe.ProgramComposition)||!Array.isArray(plan.Dependencies)||
            !Array.isArray(plan.Attempts)||!integer(plan.Attempts.length,1,d.MaximumAttempts)||!integer(plan.MixWork,0,d.MaximumMixWork)||!integer(plan.RegionWork,0,d.MaximumRegionWork)||!inherited&&plan.MixWork+plan.RegionWork<=0||!integer(plan.Selections,inherited?0:1,d.MaximumSelections))fail();
        if(inherited)this.programComposition(upstream);
        let selections=0;
        if(d.RequiredRanks!=null&&(!Array.isArray(d.RequiredRanks)||d.RequiredRanks.length<(inherited?0:1)||d.RequiredRanks.length>4096||
            d.RequiredRanks.some(r=>!integer(r,0,63))||plan.Attempts.length!==1||JSON.stringify(plan.Attempts[0].Prefix)!==JSON.stringify(d.RequiredRanks)))fail();
        for(let i=0;i<plan.Attempts.length;i++){
            const a=plan.Attempts[i];
            if(!a||a.Attempt!==i+1||!Array.isArray(a.Prefix)||a.Prefix.some(r=>!integer(r,0,Math.max(d.MaximumAlternativesPerMix,d.MaximumRegionAlternatives)-1))||!Array.isArray(a.Selections)||
                a.Accepted!==(i===plan.Attempts.length-1)||(a.Accepted?a.Rejection!=null:typeof a.Rejection!=='string'||!a.Rejection.length))fail();
            if(plan.SourceRecipe.RegionDemands!=null){
                if(typeof a.RegionRequested!=='boolean'||a.RegionSelection!=null&&!a.RegionRequested||a.Rejection==='WORLD_REGION_INTENT_UNSATISFIED'&&!a.RegionRequested)fail();
            }else if(a.RegionRequested!=null)fail();
            selections+=a.Selections.length+(a.RegionRequested??(a.RegionSelection!=null)?1:0);
            if(a.Prefix.slice(0,a.Selections.length).some(r=>r>=d.MaximumAlternativesPerMix)||a.RegionSelection!=null&&(!plan.SourceRecipe.Regions?.Intents?.length||plan.RegionWork<=0||(a.Prefix[a.Selections.length]||0)>=d.MaximumRegionAlternatives))fail();
            if(new Set(a.Selections.map(s=>s.Key)).size!==a.Selections.length)fail();
        }
        if(selections>plan.Selections)fail();
        const accepted=plan.Attempts[plan.Attempts.length-1],selected=accepted.Selections,actual=(world.ProgramMixPlan?.Allocations||[]).filter(a=>!this.inheritedProgramMix(world,a));
        if(d.RequiredRanks!=null&&d.RequiredRanks.length!==selected.length+(accepted.RegionSelection!=null?1:0))fail();
        if(selected.length!==actual.length||selected.some(s=>JSON.stringify(s)!==JSON.stringify(actual.find(a=>a.Key===s.Key)))||
            plan.SourceRecipe.Regions?.Intents?.length&&!this.inheritedProgramRegion(world)&&(accepted.RegionSelection==null||JSON.stringify(accepted.RegionSelection)!==JSON.stringify(world.RegionPlan)))fail();
        // C# replays every attempt and dependency; the viewer consumes the saved receipt.
        return plan;
    }
    static programMixes(world,nodes) {
        const plan=world.ProgramMixPlan,marked=[...nodes.values()].filter(n=>n.ProgramMixKey!=null);
        const fail=()=>{throw new Error('Invalid program mix witness. Regenerate with the engine.');};
        if(plan==null){if(marked.length)fail();return [];}
        if(plan.Version!==1||!Array.isArray(plan.Allocations)||plan.Allocations.length<1||plan.Allocations.length>4096||!Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>20000000)fail();
        const keys=new Set(),members=new Set();let work=nodes.size;
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-9*Math.max(1,Math.abs(a),Math.abs(b));
        for(const a of plan.Allocations){
            const r=a?.Request,rule=r?.Rule;
            if(!r||!rule||a.Key!==r.ParentNodeId+'/program-mix/'+r.PartId||keys.has(a.Key)||nodes.get(r.ParentNodeId)?.ArchetypeId!==r.ParentArchetypeId||
                !Array.isArray(rule.Offers)||!Array.isArray(r.Supplies)||!Array.isArray(a.Choices)||!Array.isArray(a.Balances)||
                a.Choices.length<r.MinimumCount||a.Choices.length>rule.MaximumCount||!Number.isSafeInteger(a.SearchStates)||a.SearchStates<1||a.SearchStates>rule.MaximumSearchStates||
                r.ExactCount!=null&&(!Number.isSafeInteger(r.ExactCount)||r.ExactCount<0||r.ExactCount>128||r.ExactCount!==r.MinimumCount||a.Choices.length!==r.ExactCount)||
                !Number.isSafeInteger(a.Work)||a.Work<1||a.Work>rule.MaximumWork)fail();
            keys.add(a.Key);let cost=0;const counts=new Map(),used=new Set();
            for(let i=0;i<a.Choices.length;i++){
                const c=a.Choices[i],n=nodes.get(c?.NodeId),s=r.Supplies.find(s=>s.ArchetypeId===c?.ArchetypeId);
                if(!c||!s||members.has(c.NodeId)||c.NodeId!==r.ParentNodeId+'/'+r.PartId+'-'+i||!n||n.ParentId!==r.ParentNodeId||n.ArchetypeId!==c.ArchetypeId||n.Kind!==s.Kind||n.ProgramMixKey!==a.Key)fail();
                members.add(c.NodeId);counts.set(c.ArchetypeId,(counts.get(c.ArchetypeId)||0)+1);
            }
            const prefix=r.ParentNodeId+'/'+r.PartId+'-';
            for(const n of nodes.values())if(n.ParentId===r.ParentNodeId&&n.Id.startsWith(prefix)&&/^\d+$/.test(n.Id.slice(prefix.length))&&!members.has(n.Id))fail();
            for(const o of rule.Offers){const count=counts.get(o.ArchetypeId)||0;if(used.has(o.ArchetypeId)||count<o.MinimumCount||count>o.MaximumCount)fail();used.add(o.ArchetypeId);cost+=count*o.Cost;}
            if([...counts.keys()].some(id=>!used.has(id))||!near(cost,a.Cost)||cost>rule.MaximumCost+1e-9)fail();
            const ids=new Set([...rule.Demands.map(d=>d.CapacityId),...r.Supplies.flatMap(s=>[...s.Provides,...s.Inputs].map(c=>c.Id))]);
            if(a.Balances.length!==ids.size||new Set(a.Balances.map(b=>b.CapacityId)).size!==ids.size)fail();
            for(const id of ids){
                const b=a.Balances.find(b=>b.CapacityId===id),d=rule.Demands.find(d=>d.CapacityId===id);let provided=0,consumed=0;
                if(!b)fail();
                for(const s of r.Supplies){const count=counts.get(s.ArchetypeId)||0;for(const c of s.Provides.filter(c=>c.Id===id)){if(c.Unit!==b.Unit)fail();provided+=count*c.Quantity;}for(const c of s.Inputs.filter(c=>c.Id===id)){if(c.Unit!==b.Unit)fail();consumed+=count*c.Quantity;}}
                const required=d?(d.QuantityParameter==null?d.Quantity:r.Parameters[d.QuantityParameter]):0;
                if(d&&d.Unit!==b.Unit||!near(b.Provided,provided)||!near(b.Consumed,consumed)||!near(b.Required,required)||provided-consumed+1e-9<required)fail();
            }
            work+=a.Work+nodes.size+a.Choices.length;
        }
        if(work!==plan.Work||marked.some(n=>!members.has(n.Id)))fail();
        // Optimization and source replay belong to C#; this checks saved balances and membership.
        return plan.Allocations;
    }

    static layout(world) {

        if (!world || world.SchemaVersion !== 2 || typeof world.Id !== 'string' || !Array.isArray(world.RemainingScopes) ||

            world.RemainingScopes.some(s => typeof s !== 'string') || !Array.isArray(world.Nodes) || !Array.isArray(world.Connections) ||

            world.Nodes.length === 0 || world.Nodes.length > 5000 || world.Connections.length > 20000)

            throw new Error('Schematic requires 1–5000 nodes and at most 20000 connections.');

        const nodes = new Map(), children = new Map();

        for (const node of world.Nodes) {

            if (!node || typeof node.Id !== 'string' || nodes.has(node.Id) || typeof node.Kind !== 'string')

                throw new Error('Invalid or duplicate schematic node.');

            nodes.set(node.Id, node); children.set(node.Id, []);

        }

        WorldSchematic.programComposition(world);
        WorldSchematic.populationCoordination(world);
        WorldSchematic.programMixes(world,nodes);
        const roots = [];

        for (const node of world.Nodes) {

            if (node.ParentId == null) roots.push(node);

            else {

                if (!nodes.has(node.ParentId)) throw new Error('Schematic parent is missing.');

                children.get(node.ParentId).push(node);

            }

        }

        if (roots.length !== 1) throw new Error('Schematic needs one root.');

        WorldSchematic.capacityAllocations(world,nodes,children);
        WorldSchematic.terrainDemands(world,nodes);
        WorldSchematic.routeCandidateAudits(world);
        WorldSchematic.routeGuidance(world);

        const linkIds = new Set();

        for (const link of world.Connections) {

            if (!link || typeof link.Id !== 'string' || linkIds.has(link.Id) || !nodes.has(link.FromId) || !nodes.has(link.ToId))

                throw new Error('Invalid schematic connection.');

            linkIds.add(link.Id);

        }

        WorldSchematic.serviceAllocations(world,nodes);

        WorldSchematic.variation(world,nodes);
        WorldSchematic.contextEvaluations(world,nodes);
        WorldSchematic.progression(world,nodes);

        if(world.CorridorPlan!=null&&world.Surface==null)throw new Error('Physical corridors require a surface.');

        if(world.EnvironmentPlan!=null&&world.Surface==null)throw new Error('Environmental point checks require a surface.');
        if(world.WaterPlan!=null&&(!world.Surface||!world.DrainagePlan))throw new Error('Generated water requires drainage and an effective surface.');

        if (world.Surface != null) return WorldSchematic.surfaceLayout(world,nodes,children,roots[0]);

        if (world.MapGeometry != null) {

            const map = world.MapGeometry, depths = new Map(), active = new Set();

            const visit = (node, depth) => {

                if (depth > 32 || active.has(node.Id)) throw new Error('Invalid spatial hierarchy.');

                active.add(node.Id); depths.set(node.Id, depth);

                children.get(node.Id).forEach(child => visit(child, depth + 1));

            };

            visit(roots[0], 0);

            if (depths.size !== nodes.size || map.Unit !== 'meters' || ![map.Width,map.Height].every(v => Number.isFinite(v) && v > 0) ||

                !Array.isArray(map.Boxes) || map.Boxes.length !== nodes.size || !Array.isArray(map.Roads) || map.Roads.length > 40000)

                throw new Error('Invalid engine map geometry.');

            const seen = new Set();

            const boxes = map.Boxes.map(b => {

                if (!b || !nodes.has(b.NodeId) || seen.has(b.NodeId) || ![b.X,b.Y,b.Width,b.Height].every(Number.isFinite) ||

                    b.Width <= 0 || b.Height <= 0 || b.X < -1e-6 || b.Y < -1e-6 || b.X+b.Width > map.Width+1e-6 || b.Y+b.Height > map.Height+1e-6)

                    throw new Error('Invalid engine map box.');

                seen.add(b.NodeId);

                return { id:b.NodeId,node:nodes.get(b.NodeId),x:b.X,y:b.Y,width:b.Width,height:b.Height,

                    depth:depths.get(b.NodeId),container:children.get(b.NodeId).length > 0 };

            });

            const corridors = map.Roads.map(r => {

                if (!r || ![r.X1,r.Y1,r.X2,r.Y2,r.Width].every(Number.isFinite) || r.Width <= 0 ||

                    r.X1 < 0 || r.Y1 < 0 || r.X2 < 0 || r.Y2 < 0 || r.X1 > map.Width || r.X2 > map.Width || r.Y1 > map.Height || r.Y2 > map.Height)

                    throw new Error('Invalid engine map road.');

                return { x1:r.X1,y1:r.Y1,x2:r.X2,y2:r.Y2,width:r.Width,owner:r.Id };

            });

            if (map.Polygons != null) {

                if (!Array.isArray(map.Polygons) || map.Polygons.length > boxes.length) throw new Error('Invalid engine polygons.');

                const byId = new Map(boxes.map(b => [b.id,b])), seenPolygons = new Set();

                let vertexCount = 0;

                for (const polygon of map.Polygons) {

                    const box = polygon && byId.get(polygon.NodeId), points = polygon?.Vertices;

                    if (!box || seenPolygons.has(polygon.NodeId) || !Array.isArray(points) || points.length < 3 || points.length > 1024 ||

                        (vertexCount += points.length) > 40000) throw new Error('Invalid engine polygon.');

                    seenPolygons.add(polygon.NodeId);

                    for (let i=0; i<points.length; i++) {

                        const a=points[i],b=points[(i+1)%points.length];

                        if (!a || !b || ![a.X,a.Y].every(Number.isFinite) || a.X < box.x-1e-6 || a.Y < box.y-1e-6 ||

                            a.X > box.x+box.width+1e-6 || a.Y > box.y+box.height+1e-6 ||

                            Math.hypot(a.X-b.X,a.Y-b.Y) <= 1e-8) throw new Error('Invalid engine polygon vertex.');

                    }

                    const area=points.reduce((sum,a,i)=>{const b=points[(i+1)%points.length];return sum+a.X*b.Y-b.X*a.Y;},0)/2;

                    if (!Number.isFinite(area) || Math.abs(area)<=1e-8) throw new Error('Invalid engine polygon area.');

                    const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);

                    const on=(a,b,p)=>Math.abs(cross(a,b,p))<=1e-8 && p.X>=Math.min(a.X,b.X)-1e-8 && p.X<=Math.max(a.X,b.X)+1e-8 && p.Y>=Math.min(a.Y,b.Y)-1e-8 && p.Y<=Math.max(a.Y,b.Y)+1e-8;

                    for (let i=0;i<points.length;i++) for(let j=i+1;j<points.length;j++) {

                        if(j===i+1 || i===0&&j===points.length-1) continue;

                        const a=points[i],b=points[(i+1)%points.length],c=points[j],d=points[(j+1)%points.length];

                        if (on(a,b,c)||on(a,b,d)||on(c,d,a)||on(c,d,b)||

                            cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0) throw new Error('Self-intersecting engine polygon.');

                    }

                    box.polygon=points.map(p=>({x:p.X,y:p.Y}));

                }

            }

            return {boxes,corridors,width:map.Width,height:map.Height,world,coordinateSpace:'engine-world-meters'};

        }

        const visited = new Set(), sizes = new Map();

        const hint = node => node.Properties?.schematic ?? {};

        const number = (value, fallback, minimum, maximum) => {

            const result = value ?? fallback;

            if (!Number.isFinite(result) || result < minimum || result > maximum) throw new Error('Invalid schematic dimensions.');

            return result;

        };

        const measure = (node, depth) => {

            if (depth > 32 || visited.has(node.Id)) throw new Error('Schematic hierarchy is cyclic or too deep.');

            visited.add(node.Id);

            const h = hint(node), nested = children.get(node.Id);

            const padding = number(h.padding, depth === 0 ? 40 : 18, 8, 160);

            const gap = number(h.gap, depth === 0 ? 56 : 26, 8, 160);

            const header = number(h.header, depth === 0 ? 44 : 36, 24, 100);

            const columns = number(h.columns, Math.ceil(Math.sqrt(nested.length || 1)), 1, 100);

            if (!Number.isInteger(columns)) throw new Error('Schematic columns must be whole numbers.');

            let size;

            if (!nested.length) size = { width: number(h.width, 120, 48, 600), height: number(h.height, 78, 36, 600) };

            else {

                const measures = nested.map(child => measure(child, depth + 1));

                const cols = Math.min(columns, nested.length), rows = Math.ceil(nested.length / cols);

                const cellWidth = Math.max(...measures.map(s => s.width)), cellHeight = Math.max(...measures.map(s => s.height));

                size = { width: padding * 2 + cols * cellWidth + (cols - 1) * gap,

                    height: padding * 2 + header + rows * cellHeight + (rows - 1) * gap,

                    padding, gap, header, cols, rows, cellWidth, cellHeight };

            }

            sizes.set(node.Id, size); return size;

        };

        const root = roots[0], size = measure(root, 0);

        if (visited.size !== nodes.size) throw new Error('Schematic contains unreachable or cyclic nodes.');

        const boxes = [], corridors = [];

        const place = (node, x, y, depth) => {

            const s = sizes.get(node.Id), nested = children.get(node.Id), h = hint(node);

            const box = { id: node.Id, node, x, y, width: s.width, height: s.height, depth, container: nested.length > 0 };

            boxes.push(box);

            if (nested.length && h.corridors === true) {

                const left = x + s.padding, top = y + s.padding + s.header;

                const right = x + s.width - s.padding, bottom = y + s.height - s.padding;

                for (let c = 1; c < s.cols; c++) {

                    const cx = left + c * s.cellWidth + (c - 0.5) * s.gap;

                    corridors.push({ x1: cx, y1: top, x2: cx, y2: bottom, width: s.gap * 0.55, owner: node.Id });

                }

                for (let r = 1; r < s.rows; r++) {

                    const cy = top + r * s.cellHeight + (r - 0.5) * s.gap;

                    corridors.push({ x1: left, y1: cy, x2: right, y2: cy, width: s.gap * 0.55, owner: node.Id });

                }

            }

            nested.forEach((child, i) => {

                const childSize = sizes.get(child.Id);

                place(child, x + s.padding + (i % s.cols) * (s.cellWidth + s.gap) + (s.cellWidth - childSize.width) / 2,

                    y + s.padding + s.header + Math.floor(i / s.cols) * (s.cellHeight + s.gap) + (s.cellHeight - childSize.height) / 2, depth + 1);

            });

        };

        place(root, 24, 24, 0);

        return { boxes, corridors, width: size.width + 48, height: size.height + 48, world };

    }



    static surfaceLayout(world,nodes,children,root) {

        const surface=world.Surface, seen=new Set(), depths=new Map();

        const visit=(node,depth)=>{

            if(depth>32||seen.has(node.Id))throw new Error('Invalid surface hierarchy.');

            seen.add(node.Id);depths.set(node.Id,depth);children.get(node.Id).forEach(n=>visit(n,depth+1));

        };

        visit(root,0);

        if(seen.size!==nodes.size || surface.SchemaVersion!==1 || surface.Unit!==world.Space?.Unit ||

            world.Space.Axes?.length!==2 || !Array.isArray(surface.Patches) || surface.Patches.length>1024 ||

            !Array.isArray(surface.Exclusions) || surface.Exclusions.length>256)throw new Error('Invalid surface frame or budget.');

        let vertices=0;

        const polygon=p=>{

            if(!p || !Array.isArray(p.Vertices) || p.Vertices.length<3 || p.Vertices.length>1024 ||

                (vertices+=p.Vertices.length)>16384 || p.Vertices.some(v=>!v||![v.X,v.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=10000000)))

                throw new Error('Invalid surface polygon.');

            return p.Vertices.map(v=>({x:v.X,y:v.Y}));

        };

        const water=WorldSchematic.waterWitness(world);
        const boundary=polygon(surface.Boundary), terrain=surface.Patches.map(p=>{

            if(!p || typeof p.Kind!=='string' || typeof p.Id!=='string' || !Number.isInteger(p.Priority))throw new Error('Invalid terrain patch.');

            return {id:p.Id,kind:p.Kind,priority:p.Priority,points:polygon(p.Boundary),purpose:p.Purpose,water:water?.byPatch.get(p.Id)};

        }).sort((a,b)=>a.priority-b.priority||a.id.localeCompare(b.id));

        const exclusions=surface.Exclusions.map(e=>({id:e.Id,purpose:e.Purpose,points:polygon(e.Boundary)}));

        const minX=Math.min(...boundary.map(p=>p.x)),minY=Math.min(...boundary.map(p=>p.y));

        const width=Math.max(...boundary.map(p=>p.x))-minX,height=Math.max(...boundary.map(p=>p.y))-minY;

        if(width<=0||height<=0)throw new Error('Empty surface extent.');

        const marker=Math.min(width,height)*.014, boxes=[];

        for(const node of world.Nodes) {

            if(node.Position==null)continue;

            if(!Array.isArray(node.Position)||node.Position.length!==2||!node.Position.every(Number.isFinite))throw new Error('Invalid destination position.');

            const [x,y]=node.Position;

            boxes.push({id:node.Id,node,x:x-marker,y:y-marker,width:marker*2,height:marker*2,marker:true,

                depth:depths.get(node.Id),container:false});

        }

        const access=WorldSchematic.physicalAccess(world,nodes);
        const groundUses=WorldSchematic.groundUses(world,nodes,access);

        const endpoint=(id,portId)=>{

            const site=access.get(id);

            if(portId==null){if(site?.Occupancy==='Solid')throw new Error('Solid endpoint needs an entrance.');return nodes.get(id)?.Position;}

            const port=site?.Ports.find(p=>p.Id===portId);if(!port)throw new Error('Missing route entrance.');

            return [port.ApproachPosition.X,port.ApproachPosition.Y];

        };

        const routes=[],rules=new Map(),connectionMap=new Map(world.Connections.map(c=>[c.Id,c]));

        const routePlan={Rules:[],Routes:[]};
        const interiorAssemblies=WorldSchematic.interiorAssemblies(world,nodes);
        for(const assembly of interiorAssemblies){
            const rule=world.InteriorPlan.Definition.Rules.find(r=>r.Id===assembly.RuleId);
            for(const route of assembly.Routes){
                const link=connectionMap.get(route.ConnectionId),opening=assembly.Openings.find(o=>o.ConnectionId===route.ConnectionId);
                const id=`interior/${rule.Id}/${route.ConnectionId}`,port=opening.Entrance?.OutsidePortId;
                routePlan.Rules.push({Id:id,ConnectionKind:link?.Kind,Profile:rule.Profile,
                    InspectionKind:'InteriorOpening',InspectionRule:rule,InspectionOpening:opening,
                    FromPortId:assembly.RoomNodeIds.includes(link?.FromId)?null:port,ToPortId:assembly.RoomNodeIds.includes(link?.ToId)?null:port});
                routePlan.Routes.push({...route,RuleId:id});
            }
        }

        if(world.RoutePlan!=null){

            if(!Array.isArray(world.RoutePlan.Rules)||world.RoutePlan.Rules.length>16||!Array.isArray(world.RoutePlan.Routes)||world.RoutePlan.Routes.reduce((count,r)=>count+1+(r?.Reverse?1:0),0)>128)

                throw new Error('Invalid route plan.');

            routePlan.Rules.push(...world.RoutePlan.Rules);routePlan.Routes.push(...world.RoutePlan.Routes);

        }

        if(world.NetworkPlan!=null){

            const network=world.NetworkPlan;

            if(!Array.isArray(network.Rules)||network.Rules.length>4||!Array.isArray(network.Networks)||network.Networks.length!==network.Rules.length)

                throw new Error('Invalid network plan.');

            const networkRules=new Map(network.Rules.map(r=>[r.Id,r])),seenNetworks=new Set();

            if(networkRules.size!==network.Rules.length)throw new Error('Duplicate network rule.');

            for(const n of network.Networks){

                const rule=networkRules.get(n.RuleId),plan=n.Plan;

                if(!rule||seenNetworks.has(n.RuleId)||plan?.Status!=='Found'||!Array.isArray(plan.Edges)||plan.Edges.length>496)

                    throw new Error('Invalid generated network.');

                seenNetworks.add(n.RuleId);

                const restrictions=plan.Options?.ForbiddenConnections;
                if(JSON.stringify(restrictions??null)!==JSON.stringify(rule.Options?.ForbiddenConnections??null))throw new Error('Invalid network restrictions.');
                if(restrictions!=null){
                    if(!Array.isArray(restrictions)||restrictions.length>496||!Array.isArray(plan.Destinations)||!Array.isArray(plan.Attempts))throw new Error('Invalid network restrictions.');
                    const ids=new Set(plan.Destinations.map(d=>d.Id)),keys=new Set(),pair=(a,b)=>JSON.stringify([a,b].sort());
                    const required=new Set((plan.Options.RequiredConnections||[]).map(p=>pair(p.FromId,p.ToId)));
                    for(const f of restrictions){
                        const key=pair(f?.FromId,f?.ToId);
                        if(!ids.has(f?.FromId)||!ids.has(f?.ToId)||f.FromId===f.ToId||keys.has(key)||required.has(key))throw new Error('Invalid network restrictions.');
                        keys.add(key);
                    }
                    if([...plan.Attempts,...plan.Edges].some(e=>keys.has(pair(e.Link?.FromId,e.Link?.ToId))))throw new Error('Forbidden network connection.');
                }

                const ruleId=`network/${rule.Id}`;

                routePlan.Rules.push({Id:ruleId,ConnectionKind:rule.ConnectionKind,Profile:plan.Options?.Profile,FromPortId:rule.PortId,ToPortId:rule.PortId,
                    InspectionKind:'DestinationNetwork',InspectionRule:rule,InspectionOptions:plan.Options,InspectionAnalysis:plan.Analysis});

                for(const edge of plan.Edges)routePlan.Routes.push({ConnectionId:edge.Link?.Id,RuleId:ruleId,Forward:edge.Forward,Reverse:edge.Reverse});

            }

        }

        if(routePlan.Rules.length){

            for(const rule of routePlan.Rules){

                if(!rule||rules.has(rule.Id)||!Number.isFinite(rule.Profile?.Radius)||rule.Profile.Radius<=0)throw new Error('Invalid route rule.');

                rules.set(rule.Id,rule);

            }

            const routed=new Set();

            for(const entry of routePlan.Routes){

                const rule=rules.get(entry.RuleId),link=connectionMap.get(entry.ConnectionId);

                if(!rule||!link||routed.has(link.Id)||link.Kind!==rule.ConnectionKind||(!link.Bidirectional)!==(entry.Reverse==null))throw new Error('Invalid routed connection.');

                routed.add(link.Id);

                for(const [result,reverse] of [[entry.Forward,false],[entry.Reverse,true]]){

                    if(reverse&&result==null)continue;

                    const points=result?.Path?.Points,from=endpoint(reverse?link.ToId:link.FromId,reverse?rule.ToPortId:rule.FromPortId),to=endpoint(reverse?link.FromId:link.ToId,reverse?rule.FromPortId:rule.ToPortId);

                    if(result?.Status!=='Found'||result.SurfaceId!==surface.Id||result.ProfileId!==rule.Profile.Id||

                        !Array.isArray(points)||points.length<2||points.length>1024||points.some(p=>!p||![p.X,p.Y].every(Number.isFinite))||

                        !from||!to||points[0].X!==from[0]||points[0].Y!==from[1]||points.at(-1).X!==to[0]||points.at(-1).Y!==to[1])

                        throw new Error('Invalid route geometry.');

                    routes.push({id:result.Id,connectionId:link.Id,purpose:result.Purpose,sourceRuleId:result.SourceRuleId,
                        ...(result.Cost?.Guidance?{guidanceCost:result.Cost.Guidance}:{}),
                        inspection:{Kind:rule.InspectionKind||'AuthoredConnection',Connection:link,Rule:rule.InspectionRule||rule,
                            Profile:rule.Profile,FromPortId:rule.FromPortId??null,ToPortId:rule.ToPortId??null,
                            ...(rule.InspectionOptions?{NetworkOptions:rule.InspectionOptions,NetworkAnalysis:rule.InspectionAnalysis}:{}),
                            ...(rule.InspectionOpening?{Opening:rule.InspectionOpening}:{}),
                            ...(entry.Interface?{Interface:entry.Interface}:{}),Result:result},

                        points:points.map(p=>({x:p.X,y:p.Y})),width:rule.Profile.Radius*2,reverse});

                }

            }

        }

        const heightField=WorldSchematic.surfaceHeightField(surface);

        if(surface.Terrain!=null&&!heightField&&!surface.TerrainCapturePlan)throw new Error('Generated terrain field missing. Regenerate with the engine.');

        const suitability=WorldSchematic.environmentObservations(world,nodes);

        for(const [nodeId,entries] of WorldSchematic.placementObservations(world,nodes)){

            if(!suitability.has(nodeId))suitability.set(nodeId,[]);suitability.get(nodeId).push(...entries);

        }

        for(const box of boxes){

            box.physicalAccess=access.get(box.id);
            box.groundUse=groundUses.get(box.id);

            const entries=suitability.get(box.id)||[],vertices=entries.flatMap(e=>e.Footprint?.Footprint.Vertices||[]);

            if(vertices.length){

                const x=Math.min(...vertices.map(p=>p.X)),y=Math.min(...vertices.map(p=>p.Y));

                box.focusBounds={x,y,width:Math.max(...vertices.map(p=>p.X))-x,height:Math.max(...vertices.map(p=>p.Y))-y};

                box.catalogFootprint=entries.some(e=>e.Placement?.FootprintSource?.SourceId?.startsWith('archetype:')||e.Placement?.FootprintSource?.SourceId?.startsWith('structure:'));

            }

        }

        let viewMinX=minX,viewMinY=minY,viewMaxX=minX+width,viewMaxY=minY+height;

        for(const entries of suitability.values())for(const entry of entries)for(const p of entry.Footprint?.Footprint.Vertices||[]){

            viewMinX=Math.min(viewMinX,p.X);viewMinY=Math.min(viewMinY,p.Y);viewMaxX=Math.max(viewMaxX,p.X);viewMaxY=Math.max(viewMaxY,p.Y);

        }

        const margin=Math.max(viewMaxX-viewMinX,viewMaxY-viewMinY)*.04;

        const feasibility=WorldSchematic.placementFeedback(world);

        const physicalCorridors=WorldSchematic.physicalCorridors(world,routes);

        const generatedRegions=WorldSchematic.generatedRegions(world);

        const localLayouts=WorldSchematic.localLayouts(world);
        const interfaceRoutes=WorldSchematic.interfaceRoutes(world,localLayouts);
        const layoutAdaptation=WorldSchematic.layoutAdaptation(world);
        const populationContours=WorldSchematic.populationContours(world);
        const populationAreas=WorldSchematic.populationAreas(world);
        const populationBoundaries=WorldSchematic.populationBoundaries(world);
        const populationDomains=WorldSchematic.populationDomains(world);
        WorldSchematic.spiralPopulation(world);
        const populationOrientations=WorldSchematic.populationOrientations(world);
        const groundCover=WorldSchematic.groundCover(world);

        return {world,boxes,corridors:[],physicalCorridors,generatedRegions,localLayouts,interfaceRoutes,layoutAdaptation,populationContours,populationAreas,populationBoundaries,populationDomains,populationOrientations,groundCover,interiorAssemblies,terrain,water,exclusions,boundary,routes,heightField,suitability,feasibility,protections:world.ContextPlan?.Protections||[],unit:surface.Unit,

            minX:viewMinX-margin,minY:viewMinY-margin,width:viewMaxX-viewMinX+margin*2,height:viewMaxY-viewMinY+margin*2,coordinateSpace:'engine-surface',marker};

    }

    static groundCover(world) {
        const p=world.GroundCoverPlan,fail=()=>{throw new Error('Invalid ground cover. Regenerate with the engine.');};
        if(p==null){if(world.GroundCoverId!=null)fail();return null;}
        const d=p.Definition,text=s=>typeof s==='string'&&s.length>0&&s.length<=4096,hash=s=>typeof s==='string'&&/^[0-9a-f]{64}$/.test(s);
        const area=shape=>{const v=shape.Vertices,o=v[0];let a=0;for(let i=1;i+1<v.length;i++)a+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(a)/2;};
        const polygon=s=>s&&Array.isArray(s.Vertices)&&s.Vertices.length>=3&&s.Vertices.length<=1024&&s.Vertices.every(p=>Number.isFinite(p.X)&&Number.isFinite(p.Y)&&Math.abs(p.X)<=1e7&&Math.abs(p.Y)<=1e7);
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        if(p.Version!==1||d?.Version!==1||world.GroundCoverId!==d.Id||!text(d.Id)||!text(d.Purpose)||!text(d.SourceRuleId)||typeof d.RequireComplete!=='boolean'||!hash(p.ContextSha256)||
            !Array.isArray(d.Materials)||!d.Materials.length||d.Materials.length>128||!Array.isArray(d.Rules)||!d.Rules.length||d.Rules.length>256||
            !Number.isSafeInteger(d.MaximumSources)||d.MaximumSources<1||d.MaximumSources>1024||!Number.isSafeInteger(d.MaximumCells)||d.MaximumCells<1||d.MaximumCells>16384||
            !Number.isSafeInteger(d.MaximumWork)||d.MaximumWork<1||d.MaximumWork>200000000||!Number.isSafeInteger(p.Work)||p.Work<1||p.Work>d.MaximumWork||
            !Array.isArray(p.Sources)||p.Sources.length>d.MaximumSources||!Array.isArray(p.Matches)||p.Matches.length!==d.Rules.length||
            !Array.isArray(p.Cells)||!p.Cells.length||p.Cells.length>d.MaximumCells||!polygon(world.Surface?.Boundary))fail();
        const kinds=['Terrain','Footprint','Reservation','SiteFeature','Region','Allocation','Corridor'],materials=new Map(),rules=new Map(),sources=new Map(),nodes=new Map(world.Nodes.map(n=>[n.Id,n]));
        for(const m of d.Materials){if(!text(m?.Id)||materials.has(m.Id)||!text(m.Kind)||!text(m.Purpose)||!text(m.SourceRuleId))fail();materials.set(m.Id,m);}
        for(const r of d.Rules){if(!text(r?.Id)||rules.has(r.Id)||!text(r.Purpose)||!text(r.SourceRuleId)||!kinds.includes(r.Source)||!text(r.Selector)||!materials.has(r.MaterialId)||!Number.isSafeInteger(r.Priority)||Math.abs(r.Priority)>1000000||!Number.isSafeInteger(r.MinimumMatches)||r.MinimumMatches<0||r.MinimumMatches>100000)fail();rules.set(r.Id,r);}
        for(const s of p.Sources){
            const r=rules.get(s?.RuleId),n=s?.NodeId==null?null:nodes.get(s.NodeId);
            if(!r||!/^ground-[0-9a-f]{64}$/.test(s.Id)||sources.has(s.Id)||s.Kind!==r.Source||s.MaterialId!==r.MaterialId||s.Priority!==r.Priority||!text(s.SourceId)||!text(s.SourceRuleId)||!polygon(s.Boundary)||
                (s.NodeId!=null&&(!n||s.ParentNodeId!==n.ParentId))||s.NodeId==null&&s.ParentNodeId!=null)fail();sources.set(s.Id,s);
        }
        const matched=new Set();for(const m of p.Matches){const r=rules.get(m?.RuleId);if(!r||matched.has(m.RuleId)||!Number.isSafeInteger(m.Count)||m.Count<r.MinimumMatches||m.Count!==p.Sources.filter(s=>s.RuleId===m.RuleId).length)fail();matched.add(m.RuleId);}
        let covered=0,excluded=0,unresolved=0;const ids=new Set();
        for(const c of p.Cells){
            if(!/^cell-[0-9a-f]{64}$/.test(c?.Id)||ids.has(c.Id)||!polygon(c.Boundary)||!Number.isFinite(c.Area)||c.Area<=0||!near(c.Area,area(c.Boundary))||!Array.isArray(c.SourceIds)||c.SourceIds.length>p.Sources.length||new Set(c.SourceIds).size!==c.SourceIds.length)fail();ids.add(c.Id);
            if(c.Status==='Covered'){
                if(!materials.has(c.MaterialId)||!c.SourceIds.length||c.SourceIds.some(id=>sources.get(id)?.MaterialId!==c.MaterialId)||c.Diagnostic!=null)fail();covered+=c.Area;
            }else if(c.Status==='Excluded'){
                if(c.MaterialId!=null||c.SourceIds.length||c.Diagnostic!=null||!text(c.TerrainSourceId))fail();excluded+=c.Area;
            }else if(c.Status==='Unresolved'){
                if(d.RequireComplete||c.MaterialId!=null||c.SourceIds.length||!text(c.Diagnostic))fail();unresolved+=c.Area;
            }else fail();
        }
        if(!near(covered,p.CoveredArea)||!near(excluded,p.ExcludedArea)||!near(unresolved,p.UnresolvedArea)||!near(covered+excluded+unresolved,p.DomainArea)||!near(p.DomainArea,area(world.Surface.Boundary)))fail();
        return p;
    }

    showGroundCell(cell) {
        const plan=this.plan.groundCover,material=plan.Definition.Materials.find(m=>m.Id===cell.MaterialId);this.details.replaceChildren();
        const title=document.createElement('h2');title.textContent=material?material.Kind:cell.Status;this.details.append(title);
        const p=document.createElement('p');p.textContent=`${material?.Purpose??cell.Diagnostic??'Excluded source terrain'}. ${cell.Area} ${this.plan.unit}². This is a ground-material geometry cell; it adds no objects or physical barriers.`;this.details.append(p);
        const details=document.createElement('pre');details.textContent=JSON.stringify({Cell:cell,Sources:plan.Sources.filter(s=>cell.SourceIds.includes(s.Id)),Material:material},null,2);this.details.append(details);
    }
    static groundCoverBatches(plan) {
        const batches=new Map();for(const cell of plan.Cells){const key=JSON.stringify([cell.Status,cell.MaterialId]);if(!batches.has(key))batches.set(key,[]);batches.get(key).push(cell);}
        return [...batches.values()];
    }



    static routeRoundingWitness(result,fail) {
        const attempts=result?.RoundingAttempts;
        if(attempts!=null){
            if(!Array.isArray(attempts)||attempts.length<1||attempts.length>9||
                attempts.some((a,i)=>!Number.isFinite(a?.MaximumDeviation)||a.MaximumDeviation<.000001||a.MaximumDeviation>100000||
                    (i>0&&a.MaximumDeviation>=attempts[i-1].MaximumDeviation)||!['Found','TraceRejected'].includes(a.Status)||
                    (i<attempts.length-1&&a.Status!=='TraceRejected')||!Number.isSafeInteger(a.EdgeChecks)||a.EdgeChecks<0||
                    !Number.isSafeInteger(a.Work)||a.Work<0)||attempts.at(-1).Status!==result.Status||
                attempts.reduce((n,a)=>n+a.EdgeChecks,0)>result.EdgeChecks||attempts.reduce((n,a)=>n+a.Work,0)>result.Work)fail();
        }
        const w=result?.RoundingWitness,rounded=typeof result?.SearchModel==='string'&&result.SearchModel.endsWith('/rounded-polyline-v1');
        if(w==null){if(rounded)fail();return;}
        const points=w.SourcePath?.Points,c=w.SourceCost;
        if(!rounded||!Array.isArray(points)||points.length<3||points.length>1024||points.some(p=>!Number.isFinite(p?.X)||!Number.isFinite(p?.Y))||
            !c||['Distance','Terrain','Ascent','Descent'].some(k=>!Number.isFinite(c[k])||c[k]<0)||
            ['ReusedDistance','NewPassage','Guidance'].some(k=>c[k]!=null&&(!Number.isFinite(c[k])||c[k]<0)))fail();
        let distance=0;for(let i=1;i<points.length;i++){const d=Math.hypot(points[i].X-points[i-1].X,points[i].Y-points[i-1].Y);if(d<1e-8)fail();distance+=d;}
        if(Math.abs(distance-c.Distance)>1e-7*Math.max(1,distance,c.Distance))fail();
        if(result.Status==='Found'){
            const output=result.Path?.Points;if(!Array.isArray(output)||output.length<2||
                Math.hypot(output[0].X-points[0].X,output[0].Y-points[0].Y)>1e-7||
                Math.hypot(output.at(-1).X-points.at(-1).X,output.at(-1).Y-points.at(-1).Y)>1e-7)fail();
        }else if(result.Status!=='TraceRejected'||result.Path!=null||result.Cost!=null)fail();
    }
    static routeCandidateAudits(world) {
        const fail=()=>{throw new Error('Invalid route candidate audit. Regenerate with the engine.');};
        const routed=[...(world.RoutePlan?.Routes||[]),...(world.InteriorPlan?.Results||[]).flatMap(r=>r.Routes||[])];
        const directions=routed.flatMap(r=>[r.Forward,r.Reverse]).concat((world.NetworkPlan?.Networks||[]).flatMap(n=>(n.Plan?.Edges||[]).flatMap(e=>[e.Forward,e.Reverse]))).filter(Boolean);
        if(directions.length>16384)fail();
        for(const result of directions){
            this.routeRoundingWitness(result,fail);
            const a=result.CandidateAudit;if(a==null)continue;
            const integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max;
            if(!integer(a.Proposals,2,262146)||!integer(a.Retained,2,1024)||a.Retained!==result.CandidateCount||
                !integer(a.ProbeCandidates,0,66)||!integer(a.BoundPruned,0,262144)||!integer(a.GeometryRejected,0,262144)||!integer(a.Unexamined,0,262144)||
                a.Proposals!==a.Retained+a.BoundPruned+a.GeometryRejected+a.Unexamined||typeof a.CompleteWinningSet!=='boolean'||
                a.CompleteWinningSet&&a.Unexamined!==0||!a.CompleteWinningSet&&a.BoundPruned!==0||
                a.UpperBound!=null&&(!Number.isFinite(a.UpperBound)||a.UpperBound<=0)||a.BoundPruned>0&&a.UpperBound==null)fail();
            if(a.UpperBound!=null&&result.Status==='Found'){
                const cost=result.RoundingWitness?.SourceCost??result.Cost,total=cost?.Terrain+cost?.Ascent+cost?.Descent+(cost?.NewPassage||0)+(cost?.Guidance||0);
                if(!Number.isFinite(total)||total>a.UpperBound+1e-8*Math.max(1,a.UpperBound))fail();
            }
        }
        // The engine replays the graph and bound proof; the viewer checks saved arithmetic only.
    }
    static routeGuidance(world) {
        const fail=()=>{throw new Error('Invalid regional route guidance. Regenerate with the engine.');};
        const profiles=[...(world.RoutePlan?.Rules||[]).map(r=>r.Profile),...(world.NetworkPlan?.Rules||[]).map(r=>r.Options?.Profile),
            ...(world.InteriorPlan?.Definition?.Rules||[]).map(r=>r.Profile)].filter(Boolean);
        if(profiles.length>256)fail();
        const records=[],seen=new Set(),text=v=>typeof v==='string'&&v.trim().length>0&&v.length<=2048;
        for(const profile of profiles){
            if(profile.Guidance==null)continue;
            if(!world.Surface?.Boundary||!Array.isArray(profile.Guidance)||profile.Guidance.length<1||profile.Guidance.length>16)fail();
            const ids=new Set();
            for(const rule of profile.Guidance){
                if(!rule||typeof rule.Id!=='string'||!/^[a-zA-Z0-9._:-]{1,160}$/.test(rule.Id)||ids.has(rule.Id)||!text(rule.Purpose)||!text(rule.SourceRuleId)||
                    !Number.isFinite(rule.CostPerMeter)||rule.CostPerMeter<=0||rule.CostPerMeter>1e6||
                    !Array.isArray(rule.Boundary?.Vertices)||rule.Boundary.Vertices.length<3||rule.Boundary.Vertices.length>64||
                    rule.Boundary.Vertices.some(p=>!Number.isFinite(p?.X)||!Number.isFinite(p?.Y)||Math.abs(p.X)>1e7||Math.abs(p.Y)>1e7)||
                    !Array.isArray(rule.Axes)||rule.Axes.length<1||rule.Axes.length>4||rule.Axes.some(a=>!Number.isFinite(a?.X)||!Number.isFinite(a?.Y)||Math.abs(Math.hypot(a.X,a.Y)-1)>1e-9))fail();
                ids.add(rule.Id);
                for(let i=0;i<rule.Axes.length;i++)for(let j=0;j<i;j++)if(Math.abs(rule.Axes[i].X*rule.Axes[j].Y-rule.Axes[i].Y*rule.Axes[j].X)<1e-8)fail();
                const key=JSON.stringify([profile.Id,rule]);if(seen.has(key))continue;seen.add(key);records.push({profileId:profile.Id,rule});
            }
        }
        const directions=[...(world.RoutePlan?.Routes||[]),...(world.InteriorPlan?.Results||[]).flatMap(r=>r.Routes||[])].flatMap(r=>[r.Forward,r.Reverse])
            .concat((world.NetworkPlan?.Networks||[]).flatMap(n=>(n.Plan?.Edges||[]).flatMap(e=>[e.Forward,e.Reverse]))).filter(Boolean);
        for(const direction of directions)if(direction.Cost?.Guidance!==undefined&&(!Number.isFinite(direction.Cost.Guidance)||direction.Cost.Guidance<0||direction.Cost.Guidance>0&&!records.length))fail();
        return records;
    }
    static terrainDemands(world,nodes) {
        const plan=world.TerrainDemandPlan,marked=[...nodes.values()].filter(n=>n.TerrainDemandRuleId!=null);
        const fail=()=>{throw new Error('Invalid terrain demand witness. Regenerate with the engine.');};
        if(plan==null){if(marked.length)fail();return;}
        const source=world.PlacementPlan?.Definition?.SourceWorld,definition=plan.Definition;
        const text=v=>typeof v==='string'&&v.trim().length>0;
        if(!source||!definition||!Array.isArray(definition.Rules)||definition.Rules.length<1||definition.Rules.length>32||
            !Number.isSafeInteger(definition.MaximumWork)||definition.MaximumWork<1||definition.MaximumWork>200000000||
            !Array.isArray(plan.Results)||plan.Results.length!==definition.Rules.length||!Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>definition.MaximumWork||
            plan.SourceRecipeSha256!==source.RecipeSha256||plan.SourceCatalogSha256!==source.CatalogSha256)fail();
        const seen=new Set(),bindings=new Set(),members=new Set();let work=definition.Rules.length;
        for(let i=0;i<definition.Rules.length;i++){
            const rule=definition.Rules[i],result=plan.Results[i],measure=result?.Measure,key=JSON.stringify([rule?.ParentNodeId,rule?.PartId]);
            if(!rule||!text(rule.Id)||seen.has(rule.Id)||!text(rule.Purpose)||!text(rule.SourceRuleId)||!nodes.has(rule.ParentNodeId)||!text(rule.PartId)||bindings.has(key)||
                !Number.isFinite(rule.AmountPerMember)||rule.AmountPerMember<=0||!Number.isSafeInteger(rule.MaximumCount)||rule.MaximumCount<1||rule.MaximumCount>1024||
                !Number.isSafeInteger(rule.MaximumCells)||rule.MaximumCells<1||rule.MaximumCells>16384||
                !result||result.RuleId!==rule.Id||!Number.isSafeInteger(result.Count)||result.Count<1||result.Count>rule.MaximumCount||
                !measure||measure.RegionId!==(rule.AllocationId??rule.RegionId)||measure.DensityFieldId!==rule.DensityFieldId||
                !Number.isFinite(measure.Area)||measure.Area<=0||!Number.isFinite(measure.Mass)||measure.Mass<=0||
                !Number.isSafeInteger(measure.Work)||measure.Work<1||Math.floor(measure.Mass/rule.AmountPerMember)!==result.Count)fail();
            const region=source.RegionPlan?.Regions.find(r=>r.Id===rule.RegionId);
            const shape=rule.AllocationId==null?region:source.LocalLayoutPlan?.Allocations.find(a=>a.Id===rule.AllocationId&&a.RegionId===rule.RegionId)?.Shape;
            if(!region||!shape||Math.abs(shape.Area-measure.Area)>1e-7*Math.max(1,shape.Area)||
                rule.DensityFieldId!=null&&!source.EnvironmentPlan?.Definition?.Fields.some(f=>f.Id===rule.DensityFieldId))fail();
            const prefix=rule.ParentNodeId+'/'+rule.PartId+'-',children=[...nodes.values()].filter(n=>n.ParentId===rule.ParentNodeId&&n.Id.startsWith(prefix)&&/^[0-9]+$/.test(n.Id.slice(prefix.length))&&Number(n.Id.slice(prefix.length))<=2147483647);
            const mix=world.ProgramMixPlan?.Allocations.find(a=>a.Request.ParentNodeId===rule.ParentNodeId&&a.Request.PartId===rule.PartId);
            if(mix&&mix.Request.ExactCount!==result.Count)fail();
            const capacity=world.CapacityPlan?.Allocations.find(a=>a.Request.ParentNodeId===rule.ParentNodeId&&a.Request.PartId===rule.PartId);
            if(capacity&&capacity.Request.ExactCount!==result.Count)fail();
            if(children.length!==result.Count)fail();
            for(let index=0;index<result.Count;index++){
                const node=nodes.get(prefix+index);if(!node||node.ParentId!==rule.ParentNodeId||node.TerrainDemandRuleId!==rule.Id||members.has(node.Id))fail();members.add(node.Id);
            }
            seen.add(rule.Id);bindings.add(key);work+=measure.Work;
        }
        if(work!==plan.Work||marked.length!==members.size||marked.some(n=>!members.has(n.Id)))fail();
        // C# owns integration, catalog minimum checks and physical placement. This validates saved count/member provenance only.
    }
    static interiorWitness(search,proof,shape,position,fail) {
        const point=p=>p&&[p.X,p.Y].every(v=>Number.isFinite(v)&&Math.abs(v)<=1e7);
        const q=search,p=proof?.Position;
        if(!Number.isFinite(q.Precision)||q.Precision<.000001||q.Precision>100000||
            !Number.isInteger(q.MaximumCells)||q.MaximumCells<1||q.MaximumCells>1000000||!Number.isFinite(q.MinimumClearance)||q.MinimumClearance<0||q.MinimumClearance>1e7||
            !point(p)||!Number.isFinite(proof.Clearance)||proof.Clearance<=0||proof.Clearance<q.MinimumClearance||
            !Number.isFinite(proof.UpperBound)||proof.UpperBound<proof.Clearance||proof.UpperBound-proof.Clearance>q.Precision+1e-8||
            !Number.isInteger(proof.Evaluations)||proof.Evaluations<1||proof.Evaluations>q.MaximumCells||JSON.stringify(position)!==JSON.stringify(p))fail();
        const inside=shape.Cells.some(c=>{let sign=0;const v=c.Vertices;
            for(let i=0;i<v.length;i++){const a=v[i],b=v[(i+1)%v.length],cross=(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
                if(Math.abs(cross)>1e-8){const current=Math.sign(cross);if(sign&&sign!==current)return false;sign=current;}}
            return true;});
        if(!inside)fail();let clearance=Infinity;
        for(const e of shape.BoundaryEdges){const a=e.From,b=e.To,dx=b.X-a.X,dy=b.Y-a.Y;
            const t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));
            clearance=Math.min(clearance,Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy));}
        if(Math.abs(clearance-proof.Clearance)>1e-9*Math.max(1,clearance,proof.Clearance))fail();
        // The engine replays the search; this checks saved geometry and proof arithmetic.
    }
    static areaPartitionPolicy(search,fail) {
        if(search==null)return;
        const axes=search.AxisOffsetsDegrees,bend=search.Bend??0,alternates=search.AlternateBends,positions=search.AlternateBendOffsets;
        if(!Array.isArray(axes)||axes.length<1||axes.length>16||axes.some(a=>!Number.isFinite(a)||Math.abs(a)>180)||
            !Number.isInteger(search.MaximumAttempts)||search.MaximumAttempts<1||search.MaximumAttempts>4096||
            (search.Bend!==undefined&&(!Number.isFinite(search.Bend)||Math.abs(search.Bend)>1))||
            (search.BendOffset!==undefined&&(!Number.isFinite(search.BendOffset)||Math.abs(search.BendOffset)>1e7||
                search.BendOffset!==0&&bend===0&&(alternates==null||Array.isArray(alternates)&&alternates.every(b=>b===0))))||
            (positions!=null&&(!Array.isArray(positions)||positions.length<1||positions.length>8||
                positions.some(p=>!Number.isFinite(p)||Math.abs(p)>1e7||p===(search.BendOffset??0))||new Set(positions).size!==positions.length||
                bend===0&&(alternates==null||Array.isArray(alternates)&&alternates.every(b=>b===0))))||
            (alternates!=null&&(!Array.isArray(alternates)||alternates.length<1||alternates.length>8||
                alternates.some(b=>!Number.isFinite(b)||Math.abs(b)>1||b===bend)||new Set(alternates).size!==alternates.length))||
            (search.Seed!==undefined&&search.Seed!==null&&(typeof search.Seed!=="string"||!search.Seed.trim()||search.Seed.length>256)))fail();
        for(let i=0;i<axes.length;i++)for(let j=0;j<i;j++){const d=Math.abs(axes[i]-axes[j])%180;if(Math.min(d,180-d)<1e-9)fail();}
    }
    static areaPartitionWitness(plan,fail) {
        const search=plan.Pattern?.PartitionSearch,attempts=plan.PartitionAttempts;
        if(search==null){if(attempts!=null)fail();return;}
        WorldSchematic.areaPartitionPolicy(search,fail);
        const strata=plan.Strata,n=strata.length,selected=new Map(),valid=new Set();
        const key=(f,m,d)=>`${f}/${m}/${d}`;
        function Keys(first,members,depth){if(members<2)return;valid.add(key(first,members,depth));const left=Math.floor(members/2);Keys(first,left,depth+1);Keys(first+left,members-left,depth+1);}
        Keys(0,n,0);
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));
        const point=p=>p&&[p.X,p.Y].every(v=>Number.isFinite(v)&&Math.abs(v)<=1e7);
        if(!Array.isArray(attempts)||attempts.length>search.MaximumAttempts)fail();
        attempts.forEach((a,i)=>{
            if(!a||a.Ordinal!==i+1||!Number.isInteger(a.FirstMember)||!Number.isInteger(a.Members)||!Number.isInteger(a.Depth)||
                !valid.has(key(a.FirstMember,a.Members,a.Depth))||!search.AxisOffsetsDegrees.includes(a.AxisOffsetDegrees)||
                (search.AlternateBends==null?a.Bend!=null:!Number.isFinite(a.Bend)||![search.Bend??0,...search.AlternateBends].includes(a.Bend))||
                (search.AlternateBendOffsets==null?a.BendOffset!=null:!Number.isFinite(a.BendOffset)||
                    ![search.BendOffset??0,...search.AlternateBendOffsets].includes(a.BendOffset)||
                    (a.Bend??search.Bend??0)===0&&a.BendOffset!==(search.BendOffset??0))||
                !Number.isFinite(a.AngleDegrees)||Math.abs(a.AngleDegrees)>180||!point(a.Origin)||!Number.isFinite(a.Cut)||
                ![a.TargetMass,a.LeftMass,a.RightMass].every(v=>Number.isFinite(v)&&v>0)||!near(a.TargetMass,a.LeftMass)||
                !Number.isSafeInteger(a.Work)||a.Work<0||a.Work>plan.Work||!['Rejected','Superseded','Selected'].includes(a.Status))fail();
            if(a.Status==='Selected'){
                const k=key(a.FirstMember,a.Members,a.Depth);if(a.Rejection!=null||selected.has(k))fail();selected.set(k,a);
            }else if(typeof a.Rejection!=='string'||!a.Rejection.length||a.Status==='Superseded'&&a.Rejection!=='WORLD_AREA_ANCESTOR_RECONSIDERED')fail();
        });
        if(selected.size!==Math.max(0,n-1))fail();
        function Tree(first,members,depth){
            if(members<2)return;const a=selected.get(key(first,members,depth));if(!a)fail();
            const left=Math.floor(members/2),lm=strata.slice(first,first+left).reduce((sum,s)=>sum+s.Mass,0),rm=strata.slice(first+left,first+members).reduce((sum,s)=>sum+s.Mass,0);
            if(!near(lm,a.LeftMass)||!near(rm,a.RightMass))fail();
            const angle=a.AngleDegrees*Math.PI/180,dx=Math.cos(angle),dy=Math.sin(angle),tolerance=1e-7*Math.max(1,Math.abs(a.Cut));
            for(let j=first;j<first+members;j++)for(const cell of strata[j].Cells)for(const p of cell.Vertices){
                const x=p.X-a.Origin.X,y=p.Y-a.Origin.Y,c=x*dx+y*dy-(a.Bend??search.Bend??0)*Math.abs(-x*dy+y*dx-(a.BendOffset??search.BendOffset??0));
                if(j<first+left?c>a.Cut+tolerance:c<a.Cut-tolerance)fail();
            }
            Tree(first,left,depth+1);Tree(first+left,members-left,depth+1);
        }
        Tree(0,n,0);
        // The engine replays every rejected candidate. This checks the selected tree and saved arithmetic.
    }
    static populationAreas(world) {
        const placement=world.PlacementPlan,definition=placement?.Definition,traces=placement?.PopulationAreas||[];
        const fail=()=>{throw new Error('Invalid population area witness. Regenerate with the engine.');};
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y)&&Math.abs(p.X)<=1e7&&Math.abs(p.Y)<=1e7;
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));
        const area=p=>{const v=p?.Vertices;if(!Array.isArray(v)||v.length<3||v.length>128||v.some(x=>!point(x)))fail();let sum=0;const o=v[0];for(let i=1;i+1<v.length;i++)sum+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return sum/2;};
        const stages=definition?[{Id:definition.Request.Id,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        const declared=stages.flatMap(s=>s.Bindings.filter(b=>b.Population?.Region?.Area).map(b=>({stage:s.Id,binding:b})));
        if(!Array.isArray(traces)||traces.length>5000)fail();
        const nodes=new Map(world.Nodes.map(n=>[n.Id,n])),seen=new Set(),ids=new Set();let cells=0,vertices=0;
        for(const trace of traces){
            const key=trace.StageId+'/'+trace.DemandId,plan=trace.Plan;
            if(ids.has(key)||!Array.isArray(trace.NodeIds)||!trace.NodeIds.length||!plan)fail();ids.add(key);
            const source=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===trace.NodeIds[0]),grammar=source?.binding.Population,region=grammar?.Region;
            if(!region?.Area||trace.RegionId!==region.RegionId||trace.AllocationId!==region.AllocationId||trace.Purpose!==grammar.Purpose||trace.SourceRuleId!==grammar.SourceRuleId||
                plan.SchemaVersion!==1||plan.RegionId!==(trace.AllocationId??trace.RegionId)||JSON.stringify(plan.Pattern)!==JSON.stringify(region.Area)||
                !Number.isFinite(plan.Area)||plan.Area<=0||!Number.isFinite(plan.Mass)||plan.Mass<=0||!Number.isSafeInteger(plan.Work)||plan.Work<0||plan.Work>definition.MaximumWork||
                !Array.isArray(plan.Strata)||plan.Strata.length!==trace.NodeIds.length)fail();
            let totalArea=0,totalMass=0;const strata=new Set();
            plan.Strata.forEach((stratum,index)=>{
                const id=trace.NodeIds[index],node=nodes.get(id),member=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===id);
                if(typeof stratum.Id!=='string'||strata.has(stratum.Id)||!point(stratum.Position)||seen.has(id)||!node||!member||JSON.stringify(member.binding.Population)!==JSON.stringify(grammar)||
                    node.Position?.length!==2||!near(node.Position[0],stratum.Position.X)||!near(node.Position[1],stratum.Position.Y)||
                    !Number.isFinite(stratum.Area)||stratum.Area<=0||!Number.isFinite(stratum.Mass)||stratum.Mass<=0||!near(stratum.Mass,plan.Mass/plan.Strata.length)||
                    !Array.isArray(stratum.Cells)||!stratum.Cells.length||!Array.isArray(stratum.BoundaryEdges)||!stratum.BoundaryEdges.length||stratum.BoundaryEdges.length>32768||
                    stratum.BoundaryEdges.some(e=>!point(e.From)||!point(e.To)||e.From.X===e.To.X&&e.From.Y===e.To.Y))fail();
                let measured=0;for(const cell of stratum.Cells){const a=area(cell);if(a<=0||(cells+=1)>32768||(vertices+=cell.Vertices.length)>131072)fail();measured+=a;}
                if(plan.Pattern.Interior==null){if(stratum.Interior!=null||stratum.MassCentroid!=null)fail();}
                else{if(!point(stratum.MassCentroid))fail();WorldSchematic.interiorWitness(plan.Pattern.Interior,stratum.Interior,stratum,stratum.Position,fail);}
                if(!near(measured,stratum.Area))fail();totalArea+=stratum.Area;totalMass+=stratum.Mass;strata.add(stratum.Id);seen.add(id);
            });
            if(!near(totalArea,plan.Area)||!near(totalMass,plan.Mass))fail();
            WorldSchematic.areaPartitionWitness(plan,fail);
        }
        if(seen.size!==declared.length)fail();return traces;
    }

    // Inspect saved engine decisions and their actual source polygons. Native
    // replay remains the authority for sampling, suitability and whole-body fit.
    static populationDomains(world) {
        const placement=world.PlacementPlan,definition=placement?.Definition,traces=placement?.PopulationDomains||[];
        const fail=()=>{throw new Error('Invalid population source domain. Regenerate with the engine.');};
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y)&&Math.abs(p.X)<=1e7&&Math.abs(p.Y)<=1e7;
        const stages=definition?[{Id:definition.Request.Id,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        const declared=stages.flatMap(s=>s.Bindings.filter(b=>b.Population?.Domain).map(b=>({stage:s,binding:b})));
        const nodes=new Map(world.Nodes.map(n=>[n.Id,n])),source=definition?.SourceWorld,seen=new Set(),groups=new Set();let cells=0,vertices=0;
        if(!Array.isArray(traces)||traces.length>5000)fail();
        for(const trace of traces){
            const d=trace?.Domain,p=d?.Definition,shape=d?.Shape;
            if(!trace||!Array.isArray(trace.NodeIds)||!trace.NodeIds.length||groups.has(trace.StageId+'/'+trace.DemandId)||!source||
                !p||!['SiteFeature','GroundMaterial'].includes(p.Source)||typeof p.Selector!=='string'||!p.Selector.trim()||
                !Number.isInteger(p.SourceIndex)||p.SourceIndex<0||p.SourceIndex>1023||!Array.isArray(d.SourceIds)||!d.SourceIds.length||
                typeof d.SourceGeometrySha256!=='string'||!/^[a-f0-9]{64}$/.test(d.SourceGeometrySha256)||
                !shape||shape.Id!=='source-domain'||!point(shape.Seed?.Position)||!Array.isArray(shape.Cells)||!shape.Cells.length||shape.Cells.length>4096||
                !Array.isArray(shape.BoundaryEdges)||!shape.BoundaryEdges.length||shape.BoundaryEdges.length>32768||shape.BoundaryEdges.some(e=>!point(e.From)||!point(e.To))||
                !Array.isArray(d.WorldPositions)||d.WorldPositions.length!==trace.NodeIds.length||d.WorldPositions.some(x=>!point(x)))fail();
            groups.add(trace.StageId+'/'+trace.DemandId);
            const first=declared.find(v=>v.stage.Id===trace.StageId&&v.binding.NodeId===trace.NodeIds[0]),g=first?.binding.Population;
            if(!g||!equal(g.Domain,p)||g.Purpose!==trace.Purpose||g.SourceRuleId!==trace.SourceRuleId||!!p.Bands===!!p.Area)fail();
            const owner=p.NodeId??first.stage.ParentNodeId??(p.Source==='SiteFeature'?source.Id:null);
            if((d.SourceNodeId??null)!==owner||owner!=null&&!source.Nodes.some(n=>n.Id===owner))fail();
            let selected,polygons;
            if(p.Source==='SiteFeature'){
                const features=(source.PlacementPlan?.SiteFeatures||[]).filter(f=>f.NodeId===owner&&f.Kind===p.Selector).sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
                if(p.SourceIndex>=features.length)fail();selected=[features[p.SourceIndex]];polygons=[selected[0].Boundary];
            }else{
                const cover=source.GroundCoverPlan;
                if(!cover||!cover.Definition.Materials.some(m=>m.Id===p.Selector)||p.SourceIndex!==0||p.DirectionEdgeIndex!=null)fail();
                const owners=new Set(cover.Sources.filter(s=>s.NodeId===owner).map(s=>s.Id));
                selected=cover.Cells.filter(c=>c.Status==='Covered'&&c.MaterialId===p.Selector&&(owner==null||c.SourceIds.some(id=>owners.has(id)))).sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
                polygons=selected.map(c=>c.Boundary);
            }
            if(!equal(d.SourceIds,selected.map(s=>s.Id))||!equal(shape.Cells,polygons))fail();
            let measured=0;
            for(const c of shape.Cells){
                if(!Array.isArray(c?.Vertices)||c.Vertices.length<3||c.Vertices.length>64||c.Vertices.some(x=>!point(x))||(cells+=1)>32768||(vertices+=c.Vertices.length)>131072)fail();
                const v=c.Vertices,o=v[0];let a=0;for(let i=1;i+1<v.length;i++)a+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);
                if(!Number.isFinite(a)||a===0)fail();measured+=Math.abs(a)/2;
            }
            if(!near(measured,shape.Area)||!near(shape.Seed.MinimumArea,shape.Area))fail();
            if(p.Bands){
                if(d.Area!=null||p.Profile!=null||!d.ResolvedBands||!equal(d.ResolvedBands.Bands,p.Bands.Bands)||!Number.isFinite(p.Bands.DirectionDegrees))fail();
                let direction=p.Bands.DirectionDegrees;
                if(p.DirectionEdgeIndex!=null){
                    const v=polygons[0].Vertices,i=p.DirectionEdgeIndex;if(!Number.isInteger(i)||i<0||i>=v.length)fail();
                    direction=((direction+Math.atan2(v[(i+1)%v.length].Y-v[i].Y,v[(i+1)%v.length].X-v[i].X)*180/Math.PI)%360+360)%360;
                }
                if(!near(direction,d.ResolvedBands.DirectionDegrees)||p.Bands.Bands.reduce((sum,b)=>sum+b.Count,0)!==trace.NodeIds.length)fail();
            }else{
                const area=d.Area;
                if(d.ResolvedBands!=null||!p.Profile||p.DirectionEdgeIndex!=null||area?.SchemaVersion!==1||area.RegionId!==shape.Id||!equal(area.Pattern,p.Area)||
                    !near(area.Area,shape.Area)||!Array.isArray(area.Strata)||area.Strata.length!==trace.NodeIds.length||!Number.isSafeInteger(area.Work)||area.Work<0||area.Work>definition.MaximumWork||
                    !near(area.Strata.reduce((sum,s)=>sum+s.Area,0),area.Area)||!near(area.Strata.reduce((sum,s)=>sum+s.Mass,0),area.Mass))fail();
                for(let i=0;i<area.Strata.length;i++){
                    const s=area.Strata[i];if(!equal(s.Position,d.WorldPositions[i]))fail();
                    if(p.Area.Interior!=null)WorldSchematic.interiorWitness(p.Area.Interior,s.Interior,s,s.Position,fail);
                }
                WorldSchematic.areaPartitionWitness(area,fail);
            }
            for(let i=0;i<trace.NodeIds.length;i++){
                const id=trace.NodeIds[i],n=nodes.get(id),member=declared.find(v=>v.stage.Id===trace.StageId&&v.binding.NodeId===id),position=d.WorldPositions[i];
                if(seen.has(id)||!member||!equal(member.binding.Population,g)||n?.Position?.length!==2||!near(n.Position[0],position.X)||!near(n.Position[1],position.Y))fail();seen.add(id);
            }
        }
        if(seen.size!==declared.length)fail();return traces;
    }

    static spiralSamples(p,count,fail) {
        if(!p||!Array.isArray(p.Center)||p.Center.length!==2||p.Center.some(v=>!Number.isFinite(v))||
            !Number.isFinite(p.StartRadius)||p.StartRadius<0||!Number.isFinite(p.RadiusStep)||(p.RadiusStep===0&&p.StartRadius===0)||
            !Number.isFinite(p.AngleStepDegrees)||p.AngleStepDegrees===0||Math.abs(p.AngleStepDegrees)>360||
            !Number.isFinite(p.StartDegrees)||(p.AxisRatio!=null&&(!Number.isFinite(p.AxisRatio)||p.AxisRatio<.000001||p.AxisRatio>1000000))||
            (p.OrientationDegrees!=null&&!Number.isFinite(p.OrientationDegrees))||![0,1].includes(p.AxisU)||![0,1].includes(p.AxisV)||p.AxisU===p.AxisV||
            !Number.isSafeInteger(count)||count<1||count>1024)fail();
        const result=[],rotation=((p.OrientationDegrees??0)%360)*Math.PI/180,cr=Math.cos(rotation),sr=Math.sin(rotation),ratio=p.AxisRatio??1;
        for(let i=0;i<count;i++){
            const radius=p.StartRadius+i*p.RadiusStep,angle=((p.StartDegrees%360+i*p.AngleStepDegrees)%360)*Math.PI/180;
            if(!Number.isFinite(radius)||radius<0)fail();
            const u=radius*Math.cos(angle),v=radius*Math.sin(angle)*ratio,xy=[...p.Center];xy[p.AxisU]+=u*cr-v*sr;xy[p.AxisV]+=u*sr+v*cr;
            if(xy.some(v=>!Number.isFinite(v)||Math.abs(v)>1e7))fail();
            const scale=Math.max(Math.abs(p.RadiusStep),radius),radial=p.RadiusStep/scale,angular=radius/scale*(p.AngleStepDegrees*Math.PI/180);
            const du=radial*Math.cos(angle)-angular*Math.sin(angle),dv=(radial*Math.sin(angle)+angular*Math.cos(angle))*ratio;
            const ru=du*cr-dv*sr,rv=du*sr+dv*cr;
            result.push({xy,heading:Math.atan2(p.AxisU===1?ru:rv,p.AxisU===0?ru:rv)*180/Math.PI});
        }
        return result;
    }

    static populationAdaptationTemplate(plan) {
        const template=plan?.TemplateDefinition,adaptation=plan?.PopulationAdaptation;
        const fail=()=>{throw new Error('Invalid population adaptation witness. Regenerate with the engine.');};
        if(!adaptation){if(template?.PopulationAdaptation)fail();return template;}
        const policy=adaptation.Policy,attempts=adaptation.Attempts,additional=policy?.AdditionalScopes;
        if(!template||adaptation.Version!==1||JSON.stringify(template.PopulationAdaptation)!==JSON.stringify(policy)||
            !Number.isSafeInteger(policy?.MaximumWork)||policy.MaximumWork<1||policy.MaximumWork>200000000||
            (additional!=null&&(!Array.isArray(additional)||additional.length<1||additional.length>3))||!Array.isArray(attempts)||attempts.length<1)fail();
        const scopes=[{Target:policy.Target,Translation:policy.Translation,Rotation:policy.Rotation,Rigid:policy.Rigid},...(additional||[])],scopeIds=new Set(),options=[];
        let total=1;
        for(const scope of scopes){
            const target=scope?.Target,translation=scope?.Translation,rotation=scope?.Rotation,rigid=scope?.Rigid,rule=translation??rotation??rigid,key=JSON.stringify([target?.Kind,target?.OwnerId??null,target?.DemandId]);
            if(!target||!['Root','NestedStage','Program'].includes(target.Kind)||typeof target.DemandId!=='string'||!target.DemandId||
                (target.Kind==='Root'?target.OwnerId!=null:typeof target.OwnerId!=='string'||!target.OwnerId)||scopeIds.has(key)||
                [translation,rotation,rigid].filter(x=>x!=null).length!==1||typeof rule?.Purpose!=='string'||!rule.Purpose.trim()||
                typeof rule.SourceRuleId!=='string'||!rule.SourceRuleId.trim())fail();
            scopeIds.add(key);
            const validMeta=r=>typeof r?.Purpose==='string'&&!!r.Purpose.trim()&&typeof r.SourceRuleId==='string'&&!!r.SourceRuleId.trim();
            const translations=r=>{
                const vectors=r?.StepVectors,seen=new Set(),items=[];
                if(!validMeta(r)||!Array.isArray(vectors)||vectors.length<1||vectors.length>15||!Number.isInteger(r.MaximumSteps)||r.MaximumSteps<1||r.MaximumSteps>15||vectors.length*r.MaximumSteps>15)fail();
                for(let step=1;step<=r.MaximumSteps;step++)for(let i=0;i<vectors.length;i++){
                    const v=vectors[i],x=v?.X*step,y=v?.Y*step,point=JSON.stringify([x,y]);
                    if(!Number.isFinite(x)||!Number.isFinite(y)||Math.abs(x)>1e7||Math.abs(y)>1e7||(x===0&&y===0)||seen.has(point))fail();
                    seen.add(point);items.push({id:`translation-${step}-${i+1}`,x,y});
                }
                return items;
            };
            const rotations=r=>{
                const p=r?.Pivot,angles=r?.AnglesDegrees,seen=new Set(),items=[];
                if(!validMeta(r)||!Number.isFinite(p?.X)||!Number.isFinite(p?.Y)||Math.abs(p.X)>1e7||Math.abs(p.Y)>1e7||!Array.isArray(angles)||angles.length<1||angles.length>15)fail();
                for(let i=0;i<angles.length;i++){
                    const angle=angles[i],canonical=((angle%360)+360)%360;
                    if(!Number.isFinite(angle)||Math.abs(angle)>360||canonical===0||seen.has(canonical))fail();
                    seen.add(canonical);items.push({id:`rotation-${i+1}`,angle,pivot:p});
                }
                return items;
            };
            let candidates;
            if(translation)candidates=translations(translation);
            else if(rotation)candidates=rotations(rotation);
            else{
                const turns=[null,...rotations(rigid.Rotation)],moves=[null,...translations(rigid.Translation)],pairs=[];
                if(turns.length*moves.length-1>15)fail();
                for(let r=0;r<turns.length;r++)for(let t=0;t<moves.length;t++)if(r||t)pairs.push({r,t});
                pairs.sort((a,b)=>(a.r+a.t)-(b.r+b.t));
                candidates=pairs.map(({r,t})=>({...(turns[r]||{}),...(moves[t]||{}),id:`rigid-r${r}-t${t}`}));
            }
            const offsets=[{id:null,x:0,y:0},...candidates];
            total*=offsets.length;if(total>64)fail();options.push(offsets);
        }
        const combinations=[];
        const combine=(i,values)=>{if(i===options.length){combinations.push([...values]);return;}for(let j=0;j<options[i].length;j++){values[i]=j;combine(i+1,values);}};
        combine(0,[]);combinations.sort((a,b)=>a.reduce((s,n)=>s+n,0)-b.reduce((s,n)=>s+n,0));
        if(attempts.length>combinations.length)fail();
        let work=attempts.length;
        for(let i=0;i<attempts.length;i++){
            const a=attempts[i],last=i===attempts.length-1,combination=combinations[i];
            if(!a||a.ChoiceId!==options[0][combination[0]].id||a.Accepted!==last||
                (last?a.Rejection!=null:typeof a.Rejection!=='string'||!a.Rejection.startsWith('METADATA_PLACEMENT_UNRESOLVED:'))||
                !Number.isSafeInteger(a.Work)||a.Work<0)fail();
            if(scopes.length===1){if(a.Scopes!=null)fail();}
            else if(!Array.isArray(a.Scopes)||a.Scopes.length!==scopes.length||a.Scopes.some((s,j)=>
                JSON.stringify(s?.Target)!==JSON.stringify(scopes[j].Target)||s?.ChoiceId!==options[j][combination[j]].id))fail();
            work+=a.Work;
        }
        if(!Number.isSafeInteger(work)||adaptation.Work!==work||work>policy.MaximumWork)fail();
        const selected=structuredClone(template),winning=combinations[attempts.length-1];
        const definition=plan.Definition,stages=definition?[{Id:definition.Request?.Id,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        for(let scopeIndex=0;scopeIndex<scopes.length;scopeIndex++){
        const target=scopes[scopeIndex].Target;let owners;
        if(target.Kind==='Root')owners=[selected];
        else owners=(target.Kind==='NestedStage'?selected.NestedStages||[]:selected.Programs||[]).filter(x=>x?.Id===target.OwnerId);
        if(owners.length!==1)fail();
        const bindings=(owners[0].Bindings||[]).filter(b=>b?.DemandId===target.DemandId);
        if(bindings.length!==1||!bindings[0].Population)fail();
        const g=bindings[0].Population,kinds=['Spiral','Path','Boundary','Sockets','Route','Region','GeneratedBoundary','Domain'].filter(k=>g[k]!=null);
        if(kinds.length!==1||!['Spiral','Path','Boundary','Sockets'].includes(kinds[0]))fail();
        const delta=options[scopeIndex][winning[scopeIndex]];
        const move=p=>{let x,y;if(delta.angle!=null){const radians=delta.angle*Math.PI/180,cos=Math.cos(radians),sin=Math.sin(radians),dx=p?.X-delta.pivot.X,dy=p?.Y-delta.pivot.Y;
            x=delta.pivot.X+dx*cos-dy*sin+(delta.x??0);y=delta.pivot.Y+dx*sin+dy*cos+(delta.y??0);}else{x=p?.X+(delta.x??0);y=p?.Y+(delta.y??0);}
            if(!Number.isFinite(x)||!Number.isFinite(y)||Math.abs(x)>1e7||Math.abs(y)>1e7)fail();return {X:x,Y:y};};
        if(g.Spiral){if(!Array.isArray(g.Spiral.Center)||g.Spiral.Center.length!==2)fail();const p=move({X:g.Spiral.Center[0],Y:g.Spiral.Center[1]});g.Spiral.Center=[p.X,p.Y];
            if(delta.angle!=null){if(!Number.isFinite(g.Spiral.StartDegrees)||!((g.Spiral.AxisU===0&&g.Spiral.AxisV===1)||(g.Spiral.AxisU===1&&g.Spiral.AxisV===0)))fail();
                const turn=g.Spiral.AxisU===0?delta.angle:-delta.angle;
                if(g.Spiral.AxisRatio==null&&g.Spiral.OrientationDegrees==null)g.Spiral.StartDegrees=g.Spiral.StartDegrees%360+turn;
                else g.Spiral.OrientationDegrees=(g.Spiral.OrientationDegrees??0)%360+turn;}}
        else if(g.Path){if(!Array.isArray(g.Path.Path?.Points))fail();g.Path.Path.Points=g.Path.Path.Points.map(move);}
        else if(g.Boundary){if(!Array.isArray(g.Boundary.Boundary?.Vertices))fail();g.Boundary.Boundary.Vertices=g.Boundary.Boundary.Vertices.map(move);}
        else {if(!Array.isArray(g.Sockets))fail();g.Sockets=g.Sockets.map(s=>({...s,Position:move(s.Position)}));}
        let contexts;
        if(target.Kind==='Root')contexts=[{id:definition?.Request?.Id,parent:bindings[0].NodeId}];
        else if(target.Kind==='NestedStage')contexts=[{id:target.OwnerId,parent:bindings[0].NodeId}];
        else {
            if(!Array.isArray(plan.ProgramInstances))fail();
            contexts=plan.ProgramInstances.filter(i=>i?.ProgramId===target.OwnerId).map(i=>({id:i.StageId,parent:i.ParentNodeId}));
        }
        const close=(a,b)=>{
            if(typeof a==='number'||typeof b==='number')return typeof a==='number'&&typeof b==='number'&&Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7;
            if(a===null||b===null||typeof a!=='object'||typeof b!=='object')return a===b;
            if(Array.isArray(a)!==Array.isArray(b))return false;
            const ak=Object.keys(a).sort(),bk=Object.keys(b).sort();return JSON.stringify(ak)===JSON.stringify(bk)&&ak.every(k=>close(a[k],b[k]));
        };
        let canonical=null;
        for(const context of contexts){
            const found=stages.filter(s=>s.Id===context.id);if(found.length!==1||!Array.isArray(found[0].Bindings))fail();
            const binding=bindings[0],part=binding.PartId;
            const compiled=found[0].Bindings.filter(b=>{
                if(part==null)return b?.DemandId===target.DemandId&&b.NodeId===binding.NodeId;
                const demandPrefix=target.DemandId+'-',nodePrefix=context.parent+'/'+part+'-';
                return b?.DemandId?.startsWith(demandPrefix)&&/^[0-9]+$/.test(b.DemandId.slice(demandPrefix.length))&&
                    b.NodeId?.startsWith(nodePrefix)&&/^[0-9]+$/.test(b.NodeId.slice(nodePrefix.length));
            });
            if(!compiled.length||compiled.some(b=>!close(b.Population,g)||JSON.stringify(b.Population)!==JSON.stringify(compiled[0].Population)||
                (canonical!=null&&JSON.stringify(b.Population)!==JSON.stringify(canonical))))fail();
            canonical=compiled[0].Population;
        }
        if(canonical!=null)bindings[0].Population=structuredClone(canonical);
        }
        return selected;
    }

    static spiralPopulation(world) {
        const plan=world.PlacementPlan,definition=plan?.Definition,template=WorldSchematic.populationAdaptationTemplate(plan);
        const fail=()=>{throw new Error('Invalid spiral population witness. Regenerate with the engine.');};
        const stages=definition?[{Id:definition.Request.Id,ParentNodeId:null,Bindings:definition.Bindings,Request:definition.Request},...(definition.NestedStages||[])]:[];
        const compiled=stages.flatMap(s=>(s.Bindings||[]).filter(b=>b.Population?.Spiral!=null).map(b=>({stage:s,binding:b})));
        const original=template?[{Id:template.Request.Id,ParentNodeId:null,Bindings:template.Bindings},...(template.NestedStages||[])]:[];
        for(const instance of plan?.ProgramInstances||[]){
            const program=(template?.Programs||[]).find(p=>p.Id===instance.ProgramId);
            if(program)original.push({Id:instance.StageId,ParentNodeId:instance.ParentNodeId,
                Bindings:program.Bindings.map(b=>({...b,NodeId:instance.ParentNodeId}))});
        }
        const declared=original.flatMap(s=>(s.Bindings||[]).filter(b=>b.Population?.Spiral!=null).map(b=>({stage:s,binding:b})));
        if(!compiled.length&&!declared.length)return;
        if(!template||!definition||compiled.length>100000||declared.length>100000)fail();
        const nodes=new Map(world.Nodes.map(n=>[n.Id,n])),children=new Map(),seen=new Set(),stageById=new Map(),bindings=new Map(),anchors=new Map();
        for(const stage of stages){
            if(stageById.has(stage.Id))fail();stageById.set(stage.Id,stage);
            for(const b of stage.Bindings||[]){const key=JSON.stringify([stage.Id,b.DemandId,b.NodeId]);if(bindings.has(key))fail();bindings.set(key,b);}
            for(const a of stage.Request?.Anchors||[]){const key=JSON.stringify([stage.Id,a.Id]);if(anchors.has(key))fail();anchors.set(key,a);}
        }
        for(const node of world.Nodes)if(node.ParentId!=null){if(!children.has(node.ParentId))children.set(node.ParentId,[]);children.get(node.ParentId).push(node);}
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7;
        for(const item of declared){
            const {stage,binding}=item,actual=stageById.get(stage.Id);
            if(!actual||actual.ParentNodeId!==stage.ParentNodeId)fail();
            let members;
            if(binding.PartId!=null){
                const prefix=binding.NodeId+'/'+binding.PartId+'-';
                members=(children.get(binding.NodeId)||[]).filter(n=>n.Id.startsWith(prefix)&&/^[0-9]+$/.test(n.Id.slice(prefix.length)))
                    .sort((a,b)=>Number(a.Id.slice(prefix.length))-Number(b.Id.slice(prefix.length)));
                if(!members.length)fail();
            }else{
                const node=nodes.get(binding.NodeId);if(!node)fail();members=[node];
            }
            const samples=WorldSchematic.spiralSamples(binding.Population.Spiral,members.length,fail);
            const parent=stage.ParentNodeId==null?null:nodes.get(stage.ParentNodeId),origin=parent?.Position??[0,0];
            if(stage.ParentNodeId!=null&&(!parent||!Array.isArray(parent.Position)||parent.Position.length<2))fail();
            for(let i=0;i<members.length;i++){
                const node=members[i],demand=binding.PartId==null?binding.DemandId:binding.DemandId+'-'+i;
                const found=bindings.get(JSON.stringify([stage.Id,demand,node.Id])),key=JSON.stringify([stage.Id,node.Id]);
                const anchor=anchors.get(JSON.stringify([stage.Id,'population-'+demand]));
                if(seen.has(key)||!found||JSON.stringify(found.Population)!==JSON.stringify(binding.Population)||
                    !Array.isArray(node.Position)||node.Position.length<2||!anchor||
                    !near(anchor.Position.X,samples[i].xy[0])||!near(anchor.Position.Y,samples[i].xy[1])||
                    !near(node.Position[0],samples[i].xy[0]+origin[0])||!near(node.Position[1],samples[i].xy[1]+origin[1]))fail();
                seen.add(key);
            }
        }
        if(compiled.length!==seen.size||compiled.some(({stage,binding})=>!seen.has(JSON.stringify([stage.Id,binding.NodeId]))))fail();
    }

    static populationOrientations(world) {
        const plan=world.PlacementPlan,definition=plan?.Definition,traces=plan?.PopulationOrientations||[];
        const fail=()=>{throw new Error('Invalid population orientation witness. Regenerate with the engine.');};
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7;
        const norm=a=>((a+180)%360+360)%360-180;
        const stages=definition?[{Id:definition.Request.Id,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        const declared=stages.flatMap(s=>s.Bindings.filter(b=>b.Population?.Facing!=null).map(b=>({stage:s.Id,binding:b})));
        if(!Array.isArray(traces)||traces.length>100000)fail();
        const key=(stage,id)=>JSON.stringify([stage,id]),members=new Map(declared.map(d=>[key(d.stage,d.binding.NodeId),d]));
        if(members.size!==declared.length)fail();
        const sourceIds=new Set((definition?.SourceWorld?.Nodes||[]).map(n=>n.Id));
        const nodesById=new Map(world.Nodes.map(n=>[n.Id,n]));
        const boundaries=new Map((plan?.PopulationBoundaries||[]).map(t=>[key(t.StageId,t.NodeIds[0]),t]));
        const seen=new Set(),groups=new Set();
        for(const trace of traces){
            if(!trace||!Array.isArray(trace.NodeIds)||!trace.NodeIds.length||groups.has(trace.StageId+'/'+trace.DemandId)||
                !Array.isArray(trace.TangentDegrees)||trace.TangentDegrees.length!==trace.NodeIds.length||
                !Array.isArray(trace.Rotations)||trace.Rotations.length!==trace.NodeIds.length||
                !Number.isFinite(trace.RotationOffsetDegrees)||Math.abs(trace.RotationOffsetDegrees)>360)fail();
            groups.add(trace.StageId+'/'+trace.DemandId);
            const first=members.get(key(trace.StageId,trace.NodeIds[0])),g=first?.binding.Population;
            if(!g||g.Facing!==trace.Facing||trace.Purpose!==g.Purpose||trace.SourceRuleId!==g.SourceRuleId||g.Sockets)fail();
            if(g.Spiral){
                const p=g.Spiral,offset={Along:0,Against:180,Left:90,Right:-90}[trace.Facing];
                if(!Number.isFinite(offset)||trace.InsideNormalDegrees!=null)fail();
                const samples=WorldSchematic.spiralSamples(p,trace.NodeIds.length,fail);
                const stage=stages.find(s=>s.Id===trace.StageId),parent=stage?.ParentNodeId==null?null:nodesById.get(stage.ParentNodeId);
                if(stage?.ParentNodeId!=null&&(!parent||!Array.isArray(parent.Position)||parent.Position.length<2))fail();
                const origin=parent?.Position??[0,0];
                for(let i=0;i<trace.NodeIds.length;i++){
                    const id=trace.NodeIds[i],member=members.get(key(trace.StageId,id)),node=nodesById.get(id);
                    if(!node||!Array.isArray(node.Position)||node.Position.length<2)fail();
                    const {xy,heading}=samples[i],rotation=norm(heading+offset+trace.RotationOffsetDegrees);
                    if(seen.has(id)||JSON.stringify(member?.binding.Population)!==JSON.stringify(g)||
                        !near(node.Position[0],xy[0]+origin[0])||!near(node.Position[1],xy[1]+origin[1])||
                        !near(norm(heading-trace.TangentDegrees[i]),0)||!near(norm(rotation-trace.Rotations[i]),0)||
                        !near(norm(rotation-member.binding.RotationDegrees),0)||sourceIds.has(id))fail();
                    seen.add(id);
                }
                continue;
            }
            if(g.Region){
                const offset={Along:0,Against:180,Left:90,Right:-90}[trace.Facing],headings=[];
                if(g.Region.Area||!Number.isFinite(offset)||trace.InsideNormalDegrees!=null)fail();
                if(g.Region.Pattern){
                    const heading=g.Region.Pattern.DirectionDegrees;
                    if(g.Region.Contour||!Number.isFinite(heading)||Math.abs(heading)>360)fail();
                    for(let i=0;i<trace.NodeIds.length;i++)headings.push(heading);
                }else if(g.Region.Contour){
                    const witness=(plan.PopulationContours||[]).find(t=>t.StageId===trace.StageId&&t.DemandId===trace.DemandId),requests=g.Region.Contour.Contours;
                    if(!witness||JSON.stringify(witness.NodeIds)!==JSON.stringify(trace.NodeIds)||!Array.isArray(requests)||witness.Contours.length!==requests.length)fail();
                    for(let j=0;j<requests.length;j++){
                        const request=requests[j],contour=witness.Contours[j],points=contour?.Path?.Points;
                        if(request.Id!==contour?.Id||!Array.isArray(points)||points.length<2||points.length>4096||!Number.isInteger(request.Count)||request.Count<1||
                            !Number.isFinite(request.StartInset)||request.StartInset<0||!Number.isFinite(request.Spacing)||request.Spacing<=0)fail();
                        const lengths=points.slice(1).map((p,i)=>Math.hypot(p.X-points[i].X,p.Y-points[i].Y));
                        if(lengths.some(v=>!Number.isFinite(v)||v<=1e-9))fail();
                        let segment=0,preceding=0;const total=lengths.reduce((a,b)=>a+b,0);
                        for(let i=0;i<request.Count;i++){
                            const distance=request.StartInset+i*request.Spacing;if(distance>total+1e-9)fail();
                            while(segment<lengths.length-1&&distance>preceding+lengths[segment])preceding+=lengths[segment++];
                            const a=points[segment],b=points[segment+1];headings.push(Math.atan2(b.Y-a.Y,b.X-a.X)*180/Math.PI);
                        }
                    }
                }else fail();
                if(headings.length!==trace.NodeIds.length)fail();
                for(let i=0;i<trace.NodeIds.length;i++){
                    const id=trace.NodeIds[i],member=members.get(key(trace.StageId,id)),heading=headings[i],rotation=norm(heading+offset+trace.RotationOffsetDegrees);
                    if(seen.has(id)||JSON.stringify(member?.binding.Population)!==JSON.stringify(g)||!near(heading,trace.TangentDegrees[i])||!near(rotation,trace.Rotations[i])||
                        !near(rotation,member.binding.RotationDegrees)||sourceIds.has(id))fail();seen.add(id);
                }
                continue;
            }
            if(g.Domain){
                const domain=(plan.PopulationDomains||[]).find(d=>d.StageId===trace.StageId&&d.DemandId===trace.DemandId),heading=domain?.Domain.ResolvedBands?.DirectionDegrees;
                const offset={Along:0,Against:180,Left:90,Right:-90}[trace.Facing];
                if(!g.Domain.Bands||g.Domain.Area||!Number.isFinite(heading)||!Number.isFinite(offset)||trace.InsideNormalDegrees!=null||!domain||JSON.stringify(domain.NodeIds)!==JSON.stringify(trace.NodeIds))fail();
                for(let i=0;i<trace.NodeIds.length;i++){
                    const id=trace.NodeIds[i],member=members.get(key(trace.StageId,id)),rotation=norm(heading+offset+trace.RotationOffsetDegrees);
                    if(seen.has(id)||JSON.stringify(member?.binding.Population)!==JSON.stringify(g)||!near(heading,trace.TangentDegrees[i])||!near(rotation,trace.Rotations[i])||
                        !near(rotation,member.binding.RotationDegrees)||sourceIds.has(id))fail();seen.add(id);
                }
                continue;
            }
            let points,pattern,inside=null;
            if(g.Path){pattern=g.Path;points=pattern.Path?.Points;}
            else if(g.Boundary||g.GeneratedBoundary){
                const witness=boundaries.get(key(trace.StageId,trace.NodeIds[0]));
                if(g.GeneratedBoundary&&JSON.stringify(witness?.NodeIds)!==JSON.stringify(trace.NodeIds))fail();
                const b=g.Boundary??witness?.Boundary?.Pattern;
                const vertices=b?.Boundary?.Vertices;
                if(!Array.isArray(vertices)||vertices.length<3||vertices.length>256||!Number.isInteger(b.StartEdgeIndex)||b.StartEdgeIndex<0||b.StartEdgeIndex>=vertices.length)fail();
                points=vertices.map((_,i)=>vertices[(b.StartEdgeIndex+i)%vertices.length]);points.push(points[0]);pattern=b;
                const area=vertices.reduce((s,a,i)=>{const z=vertices[(i+1)%vertices.length];return s+a.X*z.Y-a.Y*z.X;},0);
                if(!Number.isFinite(area)||area===0)fail();inside=Math.sign(area)*90;
                if(g.GeneratedBoundary&&witness.Boundary.Hole)inside=-inside;
            }else if(g.Route){
                pattern=g.Route;const source=definition.SourceWorld;
                let connection=pattern.ConnectionId,reverse=pattern.Reverse;
                if(pattern.Parent!=null){
                    const selector=pattern.Parent,owner=stages.find(s=>s.Id===trace.StageId)?.ParentNodeId;
                    if(connection!=null||!owner||!source?.Nodes.some(n=>n.Id===owner)||typeof selector.ConnectionKind!=='string'||!/^[A-Za-z0-9_.:-]{1,160}$/.test(selector.ConnectionKind)||
                        !Number.isInteger(selector.ConnectionIndex)||selector.ConnectionIndex<0||selector.ConnectionIndex>127)fail();
                    const links=source.Connections.filter(c=>c.Kind===selector.ConnectionKind&&(c.FromId===owner||c.ToId===owner))
                        .sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
                    if(new Set(links.map(c=>c.Id)).size!==links.length||selector.ConnectionIndex>=links.length)fail();
                    const selected=links[selector.ConnectionIndex];connection=selected.Id;reverse=reverse!==(selected.ToId===owner);
                }else if(connection==null)fail();
                const routes=[...(source?.RoutePlan?.Routes||[]),...(source?.InteriorPlan?.Results||[]).flatMap(r=>r.Routes),...(source?.NetworkPlan?.Networks||[]).flatMap(n=>n.Plan.Edges.map(e=>({...e,ConnectionId:e.Link.Id})))].filter(r=>r.ConnectionId===connection);
                if(routes.length!==1)fail();points=(reverse?routes[0].Reverse:routes[0].Forward)?.Path?.Points;
            }else fail();
            if(!Array.isArray(points)||points.length<2||points.length>4096||points.some(p=>!p||!Number.isFinite(p.X)||!Number.isFinite(p.Y))||
                !Number.isFinite(pattern.StartDistance)||pattern.StartDistance<0||!Number.isFinite(pattern.Spacing)||pattern.Spacing<=0||
                (inside==null?trace.InsideNormalDegrees!=null:!near(inside,trace.InsideNormalDegrees)))fail();
            const offset={Along:0,Against:180,Left:90,Right:-90,Inside:inside,Outside:inside==null?null:inside+180}[trace.Facing];
            if(!Number.isFinite(offset))fail();
            const lengths=points.slice(1).map((p,i)=>Math.hypot(p.X-points[i].X,p.Y-points[i].Y));
            if(lengths.some(v=>!Number.isFinite(v)||v<=1e-9))fail();
            const total=lengths.reduce((a,b)=>a+b,0);let segment=0,preceding=0;
            for(let i=0;i<trace.NodeIds.length;i++){
                const id=trace.NodeIds[i],member=members.get(key(trace.StageId,id)),distance=pattern.StartDistance+i*pattern.Spacing;
                if(seen.has(id)||JSON.stringify(member?.binding.Population)!==JSON.stringify(g)||distance>total+1e-9)fail();seen.add(id);
                while(segment<lengths.length-1&&distance>preceding+lengths[segment])preceding+=lengths[segment++];
                const a=points[segment],b=points[segment+1],heading=Math.atan2(b.Y-a.Y,b.X-a.X)*180/Math.PI,rotation=norm(heading+offset+trace.RotationOffsetDegrees);
                if(!near(heading,trace.TangentDegrees[i])||!near(rotation,trace.Rotations[i])||!near(rotation,member.binding.RotationDegrees)||
                    sourceIds.has(id))fail();
            }
        }
        if(seen.size!==declared.length)fail();return traces;
    }

    static populationBoundaries(world) {
        const plan=world.PlacementPlan,definition=plan?.Definition,traces=plan?.PopulationBoundaries||[];
        const fail=()=>{throw new Error('Invalid generated boundary witness. Regenerate with the engine.');};
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y),near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7;
        const stages=definition?[{Id:definition.Request.Id,ParentNodeId:null,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        const declared=stages.flatMap(s=>s.Bindings.filter(b=>b.Population?.GeneratedBoundary).map(b=>({stage:s.Id,binding:b})));
        const nodes=new Map(world.Nodes.map(n=>[n.Id,n])),seen=new Set(),owners=new Set();
        if(!Array.isArray(traces)||traces.length>2048)fail();
        for(const trace of traces){
            if(!trace||!Array.isArray(trace.NodeIds)||!trace.NodeIds.length||owners.has(trace.StageId+'/'+trace.DemandId))fail();
            owners.add(trace.StageId+'/'+trace.DemandId);
            const member=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===trace.NodeIds[0]),grammar=member?.binding.Population,request=grammar?.GeneratedBoundary;
            const result=trace.Boundary,p=result?.Pattern,vertices=p?.Boundary?.Vertices;
            if(!request||trace.Purpose!==grammar.Purpose||trace.SourceRuleId!==grammar.SourceRuleId||!result||
                !/^[0-9a-f]{64}$/.test(result.SourceGeometrySha256)||!Number.isSafeInteger(result.LoopCount)||result.LoopCount<1||
                !Number.isSafeInteger(result.LoopIndex)||result.LoopIndex<0||result.LoopIndex>=result.LoopCount||
                result.LoopIndex!==(request.LoopIndex??0)||request.LoopIndex==null&&result.LoopCount!==1||typeof result.Hole!=='boolean'||
                !Array.isArray(vertices)||vertices.length<3||vertices.length>256||vertices.some(v=>!point(v))||
                !Array.isArray(result.Positions)||result.Positions.length!==trace.NodeIds.length||result.Positions.some(v=>!point(v))||
                !['Inside','Outside'].includes(request.Side)||!['Footprint','Reservation','Region','Allocation'].includes(request.Source)||
                p.Side!==(result.Hole?(request.Side==='Inside'?'Outside':'Inside'):request.Side)||
                p.Spacing!==request.Spacing||p.StartDistance!==request.StartDistance||request.UseLongestEdge!==true&&p.StartEdgeIndex!==request.StartEdgeIndex||
                p.InwardOffset!==request.Offset||p.SpanLength!==request.SpanLength||!Number.isFinite(p.Spacing)||p.Spacing<=0||
                !Number.isFinite(p.InwardOffset)||p.InwardOffset<0||!Number.isFinite(p.StartDistance)||p.StartDistance<0||
                !Number.isSafeInteger(p.StartEdgeIndex)||p.StartEdgeIndex<0||p.StartEdgeIndex>=vertices.length)fail();
            if(request.UseLongestEdge!=null&&typeof request.UseLongestEdge!=='boolean'||request.UseLongestEdge===true&&request.StartEdgeIndex!==0)fail();
            if(request.UseLongestEdge===true){
                let selected=0,length=-1;vertices.forEach((a,i)=>{const b=vertices[(i+1)%vertices.length],candidate=Math.hypot(b.X-a.X,b.Y-a.Y);if(candidate>length){length=candidate;selected=i;}});
                if(p.StartEdgeIndex!==selected)fail();
            }
            const physical=request.Source==='Footprint'||request.Source==='Reservation';
            if(physical?(typeof result.SourceNodeId!=='string'||!definition.SourceWorld?.Nodes.some(n=>n.Id===result.SourceNodeId)||result.RegionId!=null||result.AllocationId!=null):
                (result.SourceNodeId!=null||result.RegionId!==request.RegionId||result.AllocationId!==request.AllocationId))fail();
            if(physical){
                const owner=stages.find(s=>s.Id===trace.StageId)?.ParentNodeId??definition.SourceWorld.Id;
                const expected=request.NodeId??(request.PartId==null?owner:owner+'/'+request.PartId+'-'+request.PartIndex);
                if(result.SourceNodeId!==expected)fail();
            }else if(!definition.SourceWorld?.RegionPlan?.Regions.some(r=>r.Id===request.RegionId)||
                request.Source==='Allocation'&&!definition.SourceWorld.LocalLayoutPlan?.Allocations.some(a=>a.Id===request.AllocationId&&a.RegionId===request.RegionId))fail();
            const ordered=vertices.map((_,i)=>vertices[(p.StartEdgeIndex+i)%vertices.length]);ordered.push(ordered[0]);
            const lengths=ordered.slice(1).map((b,i)=>Math.hypot(b.X-ordered[i].X,b.Y-ordered[i].Y));
            if(lengths.some(v=>!Number.isFinite(v)||v<=1e-9))fail();
            const total=lengths.reduce((a,b)=>a+b,0),span=p.SpanLength??total-p.StartDistance;
            const area=vertices.reduce((s,a,i)=>{const b=vertices[(i+1)%vertices.length];return s+a.X*b.Y-a.Y*b.X;},0);
            if(!(area>0)||!Number.isFinite(span)||span<0||p.StartDistance+span>total+1e-7)fail();
            let segment=0,preceding=0;
            for(let i=0;i<trace.NodeIds.length;i++){
                const id=trace.NodeIds[i],node=nodes.get(id),binding=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===id),position=result.Positions[i];
                if(seen.has(id)||!node||JSON.stringify(binding?.binding.Population)!==JSON.stringify(grammar))fail();seen.add(id);
                const distance=p.StartDistance+i*p.Spacing;
                if(distance>=total-1e-9||distance>p.StartDistance+span+1e-7)fail();
                while(segment<lengths.length-1&&distance>preceding+lengths[segment])preceding+=lengths[segment++];
                const a=ordered[segment],b=ordered[segment+1],t=Math.max(0,Math.min(1,(distance-preceding)/lengths[segment]));
                const offset=p.InwardOffset*(p.Side==='Inside'?1:-1),x=a.X*(1-t)+b.X*t-(b.Y-a.Y)/lengths[segment]*offset,
                    y=a.Y*(1-t)+b.Y*t+(b.X-a.X)/lengths[segment]*offset;
                if(!near(position.X,x)||!near(position.Y,y)||node.Position?.length!==2||!near(node.Position[0],x)||!near(node.Position[1],y))fail();
            }
        }
        if(seen.size!==declared.length)fail();return traces;
    }

    static populationContours(world) {
        const plan=world.PlacementPlan,definition=plan?.Definition,traces=plan?.PopulationContours||[];
        const fail=()=>{throw new Error('Invalid population contour witness. Regenerate with the engine.');};
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y);
        const onPath=(p,points)=>points.slice(1).some((b,i)=>{
            const a=points[i],dx=b.X-a.X,dy=b.Y-a.Y,den=dx*dx+dy*dy;if(!den)return false;
            const t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/den));
            return Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy)<=1e-7;
        });
        const stages=definition?[{Id:definition.Request.Id,Bindings:definition.Bindings},...(definition.NestedStages||[])]:[];
        const declared=stages.flatMap(s=>s.Bindings.filter(b=>b.Population?.Region?.Contour).map(b=>({stage:s.Id,binding:b})));
        if(!Array.isArray(traces)||traces.length>2048)fail();
        const nodes=new Map(world.Nodes.map(n=>[n.Id,n])),seen=new Set(),ids=new Set();
        for(const trace of traces){
            const key=trace.StageId+'/'+trace.DemandId;
            if(ids.has(key)||!Array.isArray(trace.NodeIds)||!trace.NodeIds.length||!Array.isArray(trace.Contours)||!trace.Contours.length)fail();ids.add(key);
            const source=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===trace.NodeIds[0]);
            const grammar=source?.binding.Population,region=grammar?.Region,pattern=region?.Contour;
            if(!pattern||trace.RegionId!==region.RegionId||trace.AllocationId!==region.AllocationId||trace.FieldId!==pattern.FieldId||
                trace.Purpose!==grammar.Purpose||trace.SourceRuleId!==grammar.SourceRuleId||trace.Contours.length!==pattern.Contours.length)fail();
            let index=0;
            for(let i=0;i<trace.Contours.length;i++){
                const actual=trace.Contours[i],request=pattern.Contours[i],points=actual.Path?.Points;
                if(actual.Id!==request.Id||!Array.isArray(points)||points.length<2||points.length>4096||points.some(p=>!point(p))||
                    typeof actual.Closed!=='boolean'||actual.Closed!==(points[0].X===points.at(-1).X&&points[0].Y===points.at(-1).Y)||
                    !Number.isFinite(actual.AnchorDistance)||actual.AnchorDistance<0||actual.AnchorDistance>request.MaximumAnchorDistance+1e-8||
                    !Array.isArray(actual.Positions)||actual.Positions.length!==request.Count)fail();
                for(const position of actual.Positions){
                    const id=trace.NodeIds[index++],node=nodes.get(id),member=declared.find(d=>d.stage===trace.StageId&&d.binding.NodeId===id);
                    if(!point(position)||!onPath(position,points)||seen.has(id)||!node||!member||JSON.stringify(member.binding.Population)!==JSON.stringify(grammar)||
                        node.Position?.length!==2||Math.abs(node.Position[0]-position.X)>1e-7||Math.abs(node.Position[1]-position.Y)>1e-7)fail();seen.add(id);
                }
            }
            if(index!==trace.NodeIds.length)fail();
        }
        if(seen.size!==declared.length)fail();
        return traces;
    }

    static layoutAdaptation(world) {
        const plan=world.LayoutAdaptationPlan;if(plan==null)return null;
        const fail=()=>{throw new Error('Invalid local repartition plan. Regenerate with the engine.');};
        const policy=plan.Policy,original=plan.OriginalLayouts,actual=world.LocalLayoutPlan?.Definition;
        const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b),copy=v=>JSON.parse(JSON.stringify(v));
        const order=(a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0;
        if(plan.Version!==1||!policy||!Number.isInteger(policy.MaximumAttempts)||policy.MaximumAttempts<1||policy.MaximumAttempts>64||
            !Number.isSafeInteger(policy.MaximumWork)||policy.MaximumWork<1||policy.MaximumWork>200000000||
            !Array.isArray(plan.Attempts)||!plan.Attempts.length||plan.Attempts.length>policy.MaximumAttempts||
            !Number.isSafeInteger(plan.Work)||plan.Work<0||plan.Work>policy.MaximumWork||!actual||!Array.isArray(original?.Layouts)||
            original.Layouts.length!==actual.Layouts.length||!Array.isArray(original.Interfaces)||!plan.OriginalPlacement||
            (policy.AdditionalScopes!=null&&(!Array.isArray(policy.AdditionalScopes)||policy.AdditionalScopes.length<1||policy.AdditionalScopes.length>3)))fail();
        const scopes=[policy,...(policy.AdditionalScopes||[])],scopeIds=new Set();
        function choices(scope){
            if(!scope||scopeIds.has(scope.LayoutId)||typeof scope.Purpose!=='string'||!scope.Purpose.trim()||
                typeof scope.SourceRuleId!=='string'||!scope.SourceRuleId.trim()||!Array.isArray(scope.Dials)||scope.Dials.length>3)fail();
            scopeIds.add(scope.LayoutId);
            const owner=original.Layouts.find(l=>l.Id===scope.LayoutId),alternatives=scope.Alternatives??[];
            if(!owner||!Array.isArray(owner.Programs)||owner.Programs.some(p=>!p)||!Array.isArray(alternatives)||alternatives.length>15||
                (scope.Alternatives!=null?alternatives.length<1||scope.Dials.length!==0:scope.Dials.length<1))fail();
            const dials=[...scope.Dials].sort(order),ids=new Set(),targets=new Set();let combinations=1;
            for(const d of dials){
                if(!d||typeof d.Id!=='string'||!d.Id.length||ids.has(d.Id)||!Number.isFinite(d.Step)||d.Step<=0||d.Step>1e14||
                    !Number.isInteger(d.MinimumSteps)||d.MinimumSteps< -8||d.MinimumSteps>0||!Number.isInteger(d.MaximumSteps)||d.MaximumSteps<0||d.MaximumSteps>8||d.MinimumSteps===d.MaximumSteps)fail();
                ids.add(d.Id);const target=JSON.stringify([d.Kind,d.ProgramId??null,d.CutId??null]);if(targets.has(target))fail();targets.add(target);
                combinations*=d.MaximumSteps-d.MinimumSteps+1;if(combinations>64)fail();let value;
                if(d.Kind==='PowerInfluence'){
                    const program=owner.Programs.find(p=>p.Id===d.ProgramId);if(d.CutId!=null||owner.Strategy!=='PowerDiagram'||!program)fail();value=program.Influence;
                }else if(d.Kind==='AreaRotation'){
                    if(owner.Strategy!=='AreaBalanced'||!owner.AreaPattern||d.ProgramId!=null||d.CutId!=null)fail();value=owner.AreaPattern.AxisRotationDegrees;
                }else if(d.Kind==='PartitionOffset'){
                    if(owner.Strategy!=='BinaryPartition'||d.ProgramId!=null||typeof d.CutId!=='string'||!d.CutId.length)fail();
                    WorldSchematic.partitionPaths(owner,fail);const cut=owner.Partition.Cuts.find(c=>c.Id===d.CutId);if(!cut)fail();
                    const angle=cut.NormalDegrees*Math.PI/180;
                    for(const n of [d.MinimumSteps,d.MaximumSteps]){
                        const distance=n*d.Step,x=cut.Origin.X+Math.cos(angle)*distance,y=cut.Origin.Y+Math.sin(angle)*distance;
                        if(!Number.isFinite(x)||!Number.isFinite(y)||Math.abs(x)>1e7||Math.abs(y)>1e7)fail();
                    }
                    continue;
                }else if(['GridWidth','GridHeight','GridRotation'].includes(d.Kind)){
                    if(d.CutId!=null||owner.Strategy!=='OrientedGrid'||!owner.Grid||d.ProgramId!=null)fail();value=owner.Grid[d.Kind==='GridWidth'?'CellWidth':d.Kind==='GridHeight'?'CellHeight':'RotationDegrees'];
                }else fail();
                const min=value+d.MinimumSteps*d.Step,max=value+d.MaximumSteps*d.Step;
                if(!Number.isFinite(min)||!Number.isFinite(max)||(d.Kind==='PowerInfluence'?Math.max(Math.abs(min),Math.abs(max))>1e14:
                    d.Kind==='GridRotation'?min< -360||max>360:d.Kind==='AreaRotation'?min< -180||max>180:min<=1e-6||max>1e7))fail();
            }
            const contract=l=>JSON.stringify({...l,Strategy:'PowerDiagram',Grid:null,Partition:null,AreaPattern:null,
                ProgramIntents:l.ProgramIntents==null?null:[...l.ProgramIntents].sort(order),Programs:[...l.Programs].sort(order).map(p=>({...p,Influence:0,Column:null,Row:null}))});
            const alternativeIds=new Set(),owners=[{Id:null,Layout:owner}];
            for(const alternative of alternatives){
                const layout=alternative?.Layout;
                if(!alternative||typeof alternative.Id!=='string'||!alternative.Id.length||alternativeIds.has(alternative.Id)||
                    typeof alternative.Purpose!=='string'||!alternative.Purpose.trim()||typeof alternative.SourceRuleId!=='string'||!alternative.SourceRuleId.trim()||
                    !layout||!Array.isArray(layout.Programs)||layout.Programs.some(p=>!p)||contract(layout)!==contract(owner)||!['OrientedGrid','PowerDiagram','BinaryPartition','AnchorBisection','AreaBalanced'].includes(layout.Strategy))fail();
                WorldSchematic.areaProgramIntents(layout,world.EnvironmentPlan?.Definition?.Fields??[],fail);
                WorldSchematic.partitionPaths(layout,fail);
                alternativeIds.add(alternative.Id);
                if(layout.Strategy==='OrientedGrid'){
                    const g=layout.Grid;if(!g||![g.Origin?.X,g.Origin?.Y,g.RotationDegrees,g.CellWidth,g.CellHeight].every(Number.isFinite)||
                        Math.abs(g.Origin.X)>1e7||Math.abs(g.Origin.Y)>1e7||Math.abs(g.RotationDegrees)>360||g.CellWidth<=1e-6||g.CellWidth>1e7||g.CellHeight<=1e-6||g.CellHeight>1e7)fail();
                    const slots=new Set();for(const p of layout.Programs){const slot=JSON.stringify([p.Column,p.Row]);
                        if(!Number.isInteger(p.Column)||!Number.isInteger(p.Row)||Math.abs(p.Column)>1024||Math.abs(p.Row)>1024||slots.has(slot)||p.Influence!==0)fail();slots.add(slot);}
                }else if(layout.Grid!=null||layout.Programs.some(p=>p.Column!=null||p.Row!=null||!Number.isFinite(p.Influence)||Math.abs(p.Influence)>1e14))fail();
                owners.push(alternative);
            }
            const steps=[];
            function enumerate(i,values){if(i===dials.length){steps.push([...values]);return;}
                for(let n=dials[i].MinimumSteps;n<=dials[i].MaximumSteps;n++){values[i]=n;enumerate(i+1,values);}}
            enumerate(0,[]);steps.sort((a,b)=>a.reduce((s,v)=>s+Math.abs(v),0)-b.reduce((s,v)=>s+Math.abs(v),0));
            const result=[];
            for(let rank=0;rank<owners.length;rank++)for(const values of steps){
                const layout=copy(owners[rank].Layout),settings={};let distance=rank;
                for(let i=0;i<dials.length;i++){
                    const d=dials[i],delta=d.Step*values[i];settings[d.Id]=values[i];distance+=Math.abs(values[i]);
                    if(d.Kind==='PartitionOffset'){
                        const cut=layout.Partition.Cuts.find(c=>c.Id===d.CutId),angle=cut.NormalDegrees*Math.PI/180;
                        cut.Origin={X:cut.Origin.X+Math.cos(angle)*delta,Y:cut.Origin.Y+Math.sin(angle)*delta};
                    }else if(d.Kind==='AreaRotation')layout.AreaPattern.AxisRotationDegrees+=delta;
                    else if(d.Kind==='PowerInfluence')layout.Programs.find(p=>p.Id===d.ProgramId).Influence+=delta;
                    else layout.Grid[d.Kind==='GridWidth'?'CellWidth':d.Kind==='GridHeight'?'CellHeight':'RotationDegrees']+=delta;
                }
                result.push({layout,Steps:settings,AlternativeId:owners[rank].Id,Distance:distance});
            }
            return result;
        }
        const prepared=scopes.map(choices);let count=1;for(const list of prepared){count*=list.length;if(count>64)fail();}
        const joint=[];
        function combine(i,selected){if(i===prepared.length){joint.push([...selected]);return;}
            for(const choice of prepared[i]){selected[i]=choice;combine(i+1,selected);}}
        combine(0,[]);joint.sort((a,b)=>a.reduce((s,c)=>s+c.Distance,0)-b.reduce((s,c)=>s+c.Distance,0));
        if(plan.Attempts.length>joint.length)fail();
        for(let i=0;i<plan.Attempts.length;i++){
            const a=plan.Attempts[i],last=i===plan.Attempts.length-1,expected=joint[i];
            if(a?.Ordinal!==i+1||a.Accepted!==last||!Number.isSafeInteger(a.Work)||a.Work<0||typeof a.LayoutSha256!=='string'||!/^[a-f0-9]{64}$/.test(a.LayoutSha256)||
                (last?a.Rejection!=null:typeof a.Rejection!=='string'||!a.Rejection.length)||!same(a.Steps,expected[0].Steps)||(a.AlternativeId??null)!==expected[0].AlternativeId)fail();
            if(scopes.length===1){if(a.ScopeSettings!=null)fail();}
            else if(!Array.isArray(a.ScopeSettings)||a.ScopeSettings.length!==scopes.length||a.ScopeSettings.some((s,j)=>s?.LayoutId!==scopes[j].LayoutId||
                !same(s.Steps,expected[j].Steps)||(s.AlternativeId??null)!==expected[j].AlternativeId))fail();
        }
        const selected=new Map(joint[plan.Attempts.length-1].map(c=>[c.layout.Id,c.layout]));
        for(const source of original.Layouts){
            const expected=copy(selected.get(source.Id)??source),found=actual.Layouts.find(l=>l.Id===source.Id);if(!found)fail();expected.Programs.sort(order);
            if(expected.ProgramIntents!=null)expected.ProgramIntents.sort(order);
            if(!same(expected,found))fail();
        }
        if(!same([...original.Interfaces].sort(order),actual.Interfaces))fail();return plan;
    }

    static interfaceRoutes(world,layouts) {
        const fail=()=>{throw new Error('Invalid physical interface route. Regenerate with the engine.');};
        const point=p=>p&&[p.X,p.Y].every(Number.isFinite);
        const equal=(a,b)=>point(a)&&point(b)&&a.X===b.X&&a.Y===b.Y;
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));
        const on=(p,span)=>point(p)&&point(span?.From)&&point(span?.To)&&
            near(Math.hypot(p.X-span.From.X,p.Y-span.From.Y)+Math.hypot(p.X-span.To.X,p.Y-span.To.Y),Math.hypot(span.To.X-span.From.X,span.To.Y-span.From.Y));
        const result=[],bindings=new Map();
        for(const rule of world.RoutePlan?.Rules||[])if(rule.Interfaces!=null){
            if(!Array.isArray(rule.Interfaces)||rule.Interfaces.length<1||rule.Interfaces.length>16)fail();
            for(const binding of rule.Interfaces){
                if(!binding||bindings.has(binding.ConnectionId)||!Number.isInteger(binding.MaximumCrossingCandidates)||binding.MaximumCrossingCandidates<1||binding.MaximumCrossingCandidates>16)fail();
                bindings.set(binding.ConnectionId,{binding,rule});
            }
        }
        for(const route of world.RoutePlan?.Routes||[]){
            const selected=bindings.get(route.ConnectionId),item=route.Interface;
            if(!selected){if(item!=null)fail();continue;}
            bindings.delete(route.ConnectionId);
            const {binding,rule}=selected,source=binding.Source??'LocalAllocation';
            if(!['LocalAllocation','RegionRelation'].includes(source))fail();
            let required;
            if(source==='RegionRelation'){
                const relation=world.RegionPlan?.IntentPlan?.Relations?.find(r=>r.Requirement?.Id===binding.InterfaceId);
                if(!relation?.SharedSpan||!Array.isArray(relation.SharedSegments)||!relation.SharedSegments.length||!Number.isFinite(relation.Requirement.MinimumSharedSpan)||relation.Requirement.MinimumSharedSpan<=0)fail();
                required={Id:relation.Requirement.Id,Requirement:relation.Requirement,Segments:relation.SharedSegments};
            }else required=layouts?.Interfaces.find(i=>i.Id===binding.InterfaceId);
            if(!item||!required||route.RuleId!==rule.Id||item.InterfaceId!==required.Id||
                ![required.Requirement.FromId,required.Requirement.ToId].includes(item.FromDomainId)||
                ![required.Requirement.FromId,required.Requirement.ToId].includes(item.ToDomainId)||item.FromDomainId===item.ToDomainId||
                !required.Segments.some(s=>equal(s.From,item.Span?.From)&&equal(s.To,item.Span?.To))||!on(item.Center,item.Span)||
                !Array.isArray(item.Attempts)||item.Attempts.length<1||item.Attempts.length>binding.MaximumCrossingCandidates)fail();
            for(let i=0;i<item.Attempts.length;i++){
                const a=item.Attempts[i],last=i===item.Attempts.length-1;
                if(a?.Ordinal!==i+1||!point(a.Center)||!Number.isSafeInteger(a.Work)||a.Work<0||a.Accepted!==last||
                    (last?(a.Rejection!=null||!equal(a.Center,item.Center)):typeof a.Rejection!=='string'||!a.Rejection.length))fail();
            }
            for(const [combined,journey] of [[route.Forward,item.Forward],[route.Reverse,item.Reverse]]){
                if(combined==null){if(journey!=null)fail();continue;}
                if(combined.SearchModel!=='domain-interface-v1'||!journey)fail();
                const parts=[journey.FromLeg,journey.Crossing,journey.ToLeg];
                for(let i=0;i<parts.length;i++){
                    const p=parts[i],suffix=['/from','/crossing','/to'][i];
                    if(p?.Status!=='Found'||p.Id!==combined.Id+suffix||p.SurfaceId!==combined.SurfaceId||p.ProfileId!==combined.ProfileId||
                        !Array.isArray(p.Path?.Points)||p.Path.Points.length<2||p.Path.Points.length>256||!p.Path.Points.every(point))fail();
                }
                if(!equal(parts[0].Path.Points.at(-1),parts[1].Path.Points[0])||!equal(parts[1].Path.Points.at(-1),parts[2].Path.Points[0]))fail();
                const crossing=parts[1].Path.Points,a=crossing[0],b=crossing.at(-1),span=item.Span;
                if(crossing.length!==2||!near((a.X+b.X)/2,item.Center.X)||!near((a.Y+b.Y)/2,item.Center.Y)||
                    !near((b.X-a.X)*(span.To.X-span.From.X)+(b.Y-a.Y)*(span.To.Y-span.From.Y),0))fail();
                const joined=[...parts[0].Path.Points,...crossing.slice(1),...parts[2].Path.Points.slice(1)];
                if(joined.length!==combined.Path?.Points.length||joined.some((p,i)=>!equal(p,combined.Path.Points[i])))fail();
                const join=journey.JoinCost;
                if(join!=null&&(typeof join!=='object'||Array.isArray(join)||Object.keys(join).sort().join(',')!=='Ascent,Descent'||
                    !Number.isFinite(join.Ascent)||!Number.isFinite(join.Descent)||join.Ascent<0||join.Descent<0||join.Ascent+join.Descent<=0))fail();
                for(const key of ['Distance','Terrain','Ascent','Descent','ReusedDistance','NewPassage'])
                    if(!near(combined.Cost?.[key],parts.reduce((sum,p)=>sum+p.Cost?.[key],0)+(key==='Ascent'||key==='Descent'?join?.[key]||0:0)))fail();
                if(!near(combined.Cost?.Guidance||0,parts.reduce((sum,p)=>sum+(p.Cost?.Guidance||0),0)))fail();
            }
            result.push({connectionId:route.ConnectionId,width:rule.Profile.Radius*2,...item});
        }
        if(bindings.size)fail();
        return result;
    }

    static interiorAssemblies(world,nodes) {
        const plan=world.InteriorPlan;
        const fail=()=>{throw new Error('Invalid generated interior. Regenerate with the engine.');};
        if(plan==null){if(world.PlacementPlan?.Feedback?.Interiors!=null)fail();return [];}
        const definition=plan.Definition,point=p=>p&&[p.X,p.Y].every(Number.isFinite);
        if(!Array.isArray(definition?.Rules)||definition.Rules.length<1||definition.Rules.length>16||
            !Number.isSafeInteger(plan.Work)||plan.Work<0||plan.Work>definition.MaximumWork||!Array.isArray(plan.Results)||plan.Results.length!==definition.Rules.length)fail();
        const rules=new Map(definition.Rules.map(r=>[r.Id,r])),owners=new Set(),ruleIds=new Set(),connections=new Map(world.Connections.map(c=>[c.Id,c]));
        if(rules.size!==definition.Rules.length)fail();
        for(const result of plan.Results){
            const rule=rules.get(result.RuleId);
            if(!rule||ruleIds.has(rule.Id)||!Array.isArray(result.RoomNodeIds)||result.RoomNodeIds.length<2||result.RoomNodeIds.length>32||
                !Array.isArray(result.Walls)||result.Walls.length>256||!Array.isArray(result.Openings)||result.Openings.length<1||result.Openings.length>64||
                !Array.isArray(result.Routes)||result.Routes.length!==result.Openings.length||!Array.isArray(rule.ConnectionKinds)||!Array.isArray(rule.RoomKinds))fail();
            ruleIds.add(rule.Id);const rooms=new Set(result.RoomNodeIds),walls=new Set(),openings=new Set();
            for(const id of result.RoomNodeIds){const node=nodes.get(id);if(!node||owners.has(id)||node.ParentId!==rule.ScopeNodeId||!rule.RoomKinds.includes(node.Kind))fail();owners.add(id);}
            for(const wall of result.Walls){
                const points=wall.Footprint?.Vertices;
                if(!rooms.has(wall.RoomNodeId)||typeof wall.Id!=='string'||walls.has(wall.Id)||!Number.isInteger(wall.EdgeIndex)||wall.EdgeIndex<0||
                    !point(wall.From)||!point(wall.To)||!Array.isArray(points)||points.length<3||points.length>16||!points.every(point))fail();
                walls.add(wall.Id);
            }
            for(const opening of result.Openings){
                const link=connections.get(opening.ConnectionId);
                const entrance=opening.Entrance,declared=(rule.Entrances||[]).find(e=>e.ConnectionId===opening.ConnectionId);
                if(!link||openings.has(link.Id)||link.FromId!==opening.FromId||link.ToId!==opening.ToId||
                    (entrance?(rooms.has(link.FromId)===rooms.has(link.ToId)||!declared||JSON.stringify(entrance)!==JSON.stringify(declared)):
                        (!rooms.has(link.FromId)||!rooms.has(link.ToId)||!rule.ConnectionKinds.includes(link.Kind)))||
                    ![opening.From,opening.To,opening.FromApproach,opening.ToApproach].every(point)||
                    opening.Width!==rule.OpeningWidth||Math.abs(Math.hypot(opening.To.X-opening.From.X,opening.To.Y-opening.From.Y)-opening.Width)>1e-7)fail();
                openings.add(link.Id);
            }
            const routed=new Set();
            for(const route of result.Routes){if(route.RuleId!==rule.Id||!openings.has(route.ConnectionId)||routed.has(route.ConnectionId))fail();routed.add(route.ConnectionId);}
        }
        return plan.Results;
    }

    static generatedRegions(world) {
        WorldSchematic.regionDemand(world);
        const plan=world.RegionPlan;

        const fail=()=>{throw new Error('Invalid generated region plan. Regenerate with the engine.');};

        if(plan==null){
            if(world.PlacementPlan?.TemplateDefinition?.RegionAnchors?.length)fail();

            for(const stage of [world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)])

                if(stage?.Request?.Demands?.some(d=>d.RegionId!=null))fail();

            return null;

        }

        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y)&&Math.abs(p.X)<=1e7&&Math.abs(p.Y)<=1e7;

        const polygon=p=>p&&Array.isArray(p.Vertices)&&p.Vertices.length>=3&&p.Vertices.length<=512&&p.Vertices.every(point);

        const area=p=>{const o=p.Vertices[0];let sum=0;for(let i=1;i+1<p.Vertices.length;i++)sum+=(p.Vertices[i].X-o.X)*(p.Vertices[i+1].Y-o.Y)-(p.Vertices[i].Y-o.Y)*(p.Vertices[i+1].X-o.X);return sum/2;};

        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=Math.max(1,Math.abs(a),Math.abs(b))*1e-9;

        const request=plan.Request;
        if(request?.ExcludedAssignments?.length&&!world.ProgramCompositionPlan&&!this.inheritedProgramRegion(world))fail();

        const derived=WorldSchematic.regionIntentSeeds(plan),allSeeds=[...(request?.Seeds||[]),...derived];
        if(!request||!polygon(request.Boundary)||!world.EnvironmentPlan||!Array.isArray(request.Seeds)||allSeeds.length<1||allSeeds.length>32||

            !Array.isArray(plan.Regions)||plan.Regions.length!==allSeeds.length||!Number.isInteger(request.MaximumCells)||request.MaximumCells<1||request.MaximumCells>4096||

            ![plan.BoundaryArea,plan.SuitableArea,plan.UnclaimedSuitableArea,plan.IneligibleArea].every(v=>Number.isFinite(v)&&v>=0)||

            !near(plan.BoundaryArea,Math.abs(area(request.Boundary)))||!near(plan.BoundaryArea,plan.SuitableArea+plan.IneligibleArea))fail();

        const seeds=new Map(allSeeds.map(s=>[s?.Id,s]));if(seeds.size!==allSeeds.length)fail();

        const ids=new Set();let total=0,cells=0;

        for(const region of plan.Regions){

            const seed=seeds.get(region?.Id);

            if(!seed||ids.has(region.Id)||!point(seed.Position)||!Number.isFinite(seed.MinimumArea)||seed.MinimumArea<=0||

                JSON.stringify(seed)!==JSON.stringify(region.Seed)||!Array.isArray(region.Cells)||!region.Cells.length||

                (cells+=region.Cells.length)>request.MaximumCells||region.Cells.some(p=>!polygon(p)||area(p)<=0)||

                !Number.isFinite(region.Area)||region.Area<seed.MinimumArea||!near(region.Area,region.Cells.reduce((n,p)=>n+area(p),0))||

                !Array.isArray(region.BoundaryEdges)||!region.BoundaryEdges.length||region.BoundaryEdges.length>32768||

                region.BoundaryEdges.some(e=>!point(e?.From)||!point(e?.To)||(e.From.X===e.To.X&&e.From.Y===e.To.Y)))fail();

            ids.add(region.Id);total+=region.Area;

        }

        if(!near(total+plan.UnclaimedSuitableArea,plan.SuitableArea))fail();
        const relations=request.IntentRelations,relationProofs=plan.IntentPlan?.Relations;
        if(relations==null){if(relationProofs!=null)fail();}
        else{
            if(!Array.isArray(relations)||relations.length<1||relations.length>128||!request.Intents?.length||
                !Array.isArray(relationProofs)||relationProofs.length!==relations.length)fail();
            const seenRelations=new Set();
            const covers=(region,span)=>{
                const a=span.From,b=span.To,dx=b.X-a.X,dy=b.Y-a.Y,length=Math.hypot(dx,dy),pieces=[];
                for(const edge of region.BoundaryEdges){
                    const cross=p=>Math.abs(dx*(p.Y-a.Y)-dy*(p.X-a.X))/length;
                    if(cross(edge.From)>1e-7||cross(edge.To)>1e-7)continue;
                    const along=p=>((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(length*length);
                    const x=along(edge.From),y=along(edge.To),lo=Math.max(0,Math.min(x,y)),hi=Math.min(1,Math.max(x,y));
                    if(hi>=lo)pieces.push([lo,hi]);
                }
                pieces.sort((x,y)=>x[0]-y[0]);let end=0;
                for(const [lo,hi] of pieces){if(lo>end+1e-7/length)return false;end=Math.max(end,hi);if(end>=1-1e-7/length)return true;}
                return false;
            };
            for(let i=0;i<relations.length;i++){
                const r=relations[i],proof=relationProofs[i],from=seeds.get(r?.FromId)?.Position,to=seeds.get(r?.ToId)?.Position;
                if(!r||typeof r.Id!=='string'||!r.Id.length||seenRelations.has(r.Id)||r.FromId===r.ToId||!point(from)||!point(to)||
                    typeof r.Purpose!=='string'||!r.Purpose.trim()||typeof r.SourceRuleId!=='string'||!r.SourceRuleId.trim()||
                    !Number.isFinite(r.MinimumDistance)||!Number.isFinite(r.MaximumDistance)||r.MinimumDistance<0||r.MaximumDistance<r.MinimumDistance||r.MaximumDistance>30000000||
                    JSON.stringify(proof?.Requirement)!==JSON.stringify(r)||JSON.stringify(proof?.From)!==JSON.stringify(from)||JSON.stringify(proof?.To)!==JSON.stringify(to)||
                    !Number.isFinite(proof?.Distance)||proof.Distance<r.MinimumDistance||proof.Distance>r.MaximumDistance||!near(proof.Distance,Math.hypot(from.X-to.X,from.Y-to.Y)))fail();
                if(r.MinimumSharedSpan!=null){
                    const span=proof.SharedSpan,a=plan.Regions.find(x=>x.Id===r.FromId),b=plan.Regions.find(x=>x.Id===r.ToId);
                    if(!request.IntentPartition||!Number.isFinite(r.MinimumSharedSpan)||r.MinimumSharedSpan<=0||r.MinimumSharedSpan>30000000||
                        !a||!b||!point(span?.From)||!point(span?.To)||Math.hypot(span.To.X-span.From.X,span.To.Y-span.From.Y)<r.MinimumSharedSpan||
                        !Array.isArray(proof.SharedSegments)||proof.SharedSegments.length<1||proof.SharedSegments.length>32768||
                        !proof.SharedSegments.some(e=>JSON.stringify(e)===JSON.stringify(span))||
                        proof.SharedSegments.some(e=>!point(e?.From)||!point(e?.To)||Math.hypot(e.To.X-e.From.X,e.To.Y-e.From.Y)<=0||!covers(a,e)||!covers(b,e)))fail();
                }else if(proof.SharedSpan!=null||proof.SharedSegments!=null)fail();
                seenRelations.add(r.Id);
            }
        }
        const bindings=world.PlacementPlan?.TemplateDefinition?.RegionAnchors||[],bound=new Set();
        for(const b of bindings){
            const seed=seeds.get(b?.RegionId),anchor=world.PlacementPlan?.Definition?.Request?.Anchors?.find(a=>a.Id===b?.AnchorId);
            if(!seed||!anchor||bound.has(b.AnchorId)||JSON.stringify(seed.Position)!==JSON.stringify(anchor.Position)||
                world.PlacementPlan.TemplateDefinition.Request.Anchors.some(a=>a.Id===b.AnchorId)||world.PlacementPlan.Definition.RegionAnchors!=null)fail();
            bound.add(b.AnchorId);
        }

        for(const stage of [world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)])

            for(const demand of stage?.Request?.Demands||[])if(demand.RegionId!=null&&!ids.has(demand.RegionId))fail();

        return plan;

    }



    static areaProgramIntents(layout,fields,fail) {
        if(layout.Strategy!=='AreaBalanced'){if(layout.ProgramIntents!=null||layout.AreaPattern!=null)fail();return [];}
        const intents=layout.ProgramIntents,p=layout.AreaPattern,text=v=>typeof v==='string'&&v.trim().length>0;
        if(!Array.isArray(layout.Programs)||layout.Programs.length||layout.RegionFrame!=null||layout.Grid!=null||layout.Partition!=null||
            !Array.isArray(intents)||intents.length<1||intents.length>32||new Set(intents.map(i=>i?.Id)).size!==intents.length||
            intents.some(i=>!i||!text(i.Id)||!text(i.Purpose)||!text(i.SourceRuleId)||!Number.isFinite(i.MinimumArea)||i.MinimumArea<=0||i.MinimumArea>4e14||
                i.AllocationWeight!=null&&(!Number.isFinite(i.AllocationWeight)||i.AllocationWeight<=0||i.AllocationWeight>4e14))||
            !p||!Number.isFinite(p.AxisRotationDegrees)||Math.abs(p.AxisRotationDegrees)>180||!Number.isInteger(p.MaximumCells)||p.MaximumCells<1||p.MaximumCells>16384||
            !Number.isInteger(p.MaximumDepth)||p.MaximumDepth<1||p.MaximumDepth>32||p.DensityFieldId!=null&&!fields.some(f=>f.Id===p.DensityFieldId))fail();
        WorldSchematic.areaPartitionPolicy(p.PartitionSearch,fail);
        return [...intents].sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
    }

    static partitionPaths(layout,fail) {
        if(layout.Strategy==='AnchorBisection'&&(layout.Grid!=null||layout.Partition!=null||layout.Programs.some(p=>p.Influence!==0||p.Column!=null||p.Row!=null)))fail();
        if(layout.Strategy!=='BinaryPartition'){if(layout.Partition!=null)fail();return null;}
        const partition=layout.Partition,leaves=new Set(layout.Programs.map(p=>p.Id)),cuts=new Map(),seen=new Set(),paths=new Map();
        const id=v=>typeof v==='string'&&v.length>0;
        if(!partition||!id(partition.RootId)||!Array.isArray(partition.Cuts)||partition.Cuts.length!==leaves.size-1||leaves.size>32||
            layout.Grid!=null||layout.Programs.some(p=>p.Influence!==0||p.Column!=null||p.Row!=null))fail();
        for(const c of partition.Cuts){
            if(!c||!id(c.Id)||leaves.has(c.Id)||cuts.has(c.Id)||!c.Purpose?.trim()||!c.SourceRuleId?.trim()||
                ![c.Origin?.X,c.Origin?.Y,c.NormalDegrees].every(Number.isFinite)||Math.abs(c.Origin.X)>1e7||Math.abs(c.Origin.Y)>1e7||
                Math.abs(c.NormalDegrees)>360||!id(c.NegativeId)||!id(c.PositiveId)||c.NegativeId===c.PositiveId)fail();
            cuts.set(c.Id,c);
        }
        const visit=(id,path)=>{if(seen.has(id))fail();seen.add(id);if(leaves.has(id)){paths.set(id,path);return;}
            const cut=cuts.get(id);if(!cut)fail();visit(cut.NegativeId,[...path,[cut,-1]]);visit(cut.PositiveId,[...path,[cut,1]]);};
        visit(partition.RootId,[]);if(paths.size!==leaves.size||seen.size!==leaves.size+cuts.size)fail();return paths;
    }

    static localLayouts(world) {

        const plan=world.LocalLayoutPlan,fail=()=>{throw new Error('Invalid local layout plan. Regenerate with the engine.');};

        const demands=[world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)].flatMap(s=>s?.Request?.Demands||[]);

        if(plan==null){if(demands.some(d=>d.AllocationId!=null)||world.PlacementPlan?.TemplateDefinition?.AllocationAnchors?.length)fail();return null;}

        const list=(a,max)=>Array.isArray(a)&&a.length<=max;

        const point=p=>p&&[p.X,p.Y].every(v=>Number.isFinite(v)&&Math.abs(v)<=1e7);

        const edge=e=>e&&point(e.From)&&point(e.To)&&Math.hypot(e.To.X-e.From.X,e.To.Y-e.From.Y)>1e-8;

        const area=p=>{const o=p.Vertices[0];let sum=0;for(let i=1;i+1<p.Vertices.length;i++)sum+=(p.Vertices[i].X-o.X)*(p.Vertices[i+1].Y-o.Y)-(p.Vertices[i].Y-o.Y)*(p.Vertices[i+1].X-o.X);return sum/2;};

        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=Math.max(1,Math.abs(a),Math.abs(b))*1e-9;

        const def=plan.Definition;

        if(!world.RegionPlan||!def||!list(def.Layouts,32)||!def.Layouts.length||!list(def.Interfaces,128)||!list(plan.Allocations,128)||

            !list(plan.Areas,32)||plan.Areas.length!==def.Layouts.length||!list(plan.Interfaces,128)||plan.Interfaces.length!==def.Interfaces.length||

            !Number.isInteger(def.MaximumCells)||def.MaximumCells<1||def.MaximumCells>16384||!/^[a-f0-9]{64}$/.test(plan.RegionPlanSha256))fail();

        const regions=new Map(world.RegionPlan.Regions.map(r=>[r.Id,r])),layouts=new Map(),programs=new Map(),allocations=new Map(),generated=new Map(),areaPlans=new Map();
        if(!list(plan.GeneratedAreas??[],32))fail();
        for(const proof of plan.GeneratedAreas??[]){
            if(!proof||areaPlans.has(proof.LayoutId)||def.Layouts.find(l=>l?.Id===proof.LayoutId)?.Strategy!=='AreaBalanced')fail();areaPlans.set(proof.LayoutId,proof.AreaPlan);
        }
        if(!list(plan.GeneratedPartitions??[],32))fail();
        for(const proof of plan.GeneratedPartitions??[]){
            if(!proof||generated.has(proof.LayoutId)||def.Layouts.find(l=>l?.Id===proof.LayoutId)?.Strategy!=='AnchorBisection')fail();
            generated.set(proof.LayoutId,proof.Partition);
        }
        const effective=layout=>{
            if(layout.Strategy!=='AnchorBisection')return layout;
            const partition=generated.get(layout.Id);if(!partition)fail();
            return {...layout,Strategy:'BinaryPartition',Partition:partition};
        };

        for(const layout of def.Layouts){

            if(!layout||layouts.has(layout.Id)||!regions.has(layout.RegionId)||!['OrientedGrid','PowerDiagram','BinaryPartition','AnchorBisection','AreaBalanced'].includes(layout.Strategy)||!list(layout.Programs,32)||
                (layout.Strategy==='AreaBalanced'?layout.Programs.length!==0:!layout.Programs.length))fail();
            const intents=this.areaProgramIntents(layout,world.EnvironmentPlan?.Definition?.Fields??[],fail);
            this.partitionPaths(layout,fail);
            this.partitionPaths(effective(layout),fail);

            layouts.set(layout.Id,layout);

            const frame=layout.RegionFrame,seed=regions.get(layout.RegionId)?.Seed?.Position;
            if(frame!=null&&(!frame||typeof frame.Purpose!=='string'||!frame.Purpose.trim()||typeof frame.SourceRuleId!=='string'||!frame.SourceRuleId.trim()||!point(seed)))fail();
            if(layout.Strategy==='AreaBalanced'){
                const a=areaPlans.get(layout.Id),close=(x,y)=>Number.isFinite(x)&&Number.isFinite(y)&&Math.abs(x-y)<=1e-7*Math.max(1,Math.abs(x),Math.abs(y));
                if(!a||a.SchemaVersion!==1||a.RegionId!==(layout.ParentAllocationId??layout.RegionId)||JSON.stringify(a.Pattern)!==JSON.stringify(layout.AreaPattern)||
                    !Number.isFinite(a.Area)||a.Area<=0||!Number.isFinite(a.Mass)||a.Mass<=0||!Number.isSafeInteger(a.Work)||a.Work<1||a.Work>plan.Work||
                    !list(a.Strata,32)||a.Strata.length!==intents.length||!list(a.RelativeWeights,32)||a.RelativeWeights.length!==intents.length||a.RelativeWeights.some((w,i)=>w!==(intents[i].AllocationWeight??intents[i].MinimumArea)))fail();
                const totalWeight=intents.reduce((s,i)=>s+(i.AllocationWeight??i.MinimumArea),0);
                const seen=new Set();let totalArea=0,totalMass=0;
                a.Strata.forEach((s,i)=>{
                    const intent=intents[i];
                    if(!s||typeof s.Id!=='string'||seen.has(s.Id)||!point(s.Position)||!Number.isFinite(s.Area)||s.Area<intent.MinimumArea||
                        !Number.isFinite(s.Mass)||s.Mass<=0||!close(s.Mass,a.Mass*((intent.AllocationWeight??intent.MinimumArea)/totalWeight))||programs.has(intent.Id))fail();
                    if(a.Pattern.Interior==null){if(s.Interior!=null||s.MassCentroid!=null)fail();}
                    else{if(!point(s.MassCentroid))fail();WorldSchematic.interiorWitness(a.Pattern.Interior,s.Interior,s,s.Position,fail);}
                    seen.add(s.Id);totalArea+=s.Area;totalMass+=s.Mass;
                    const program={Id:intent.Id,Purpose:intent.Purpose,SourceRuleId:intent.SourceRuleId,Anchor:s.Position,MinimumArea:intent.MinimumArea,Influence:0,Column:null,Row:null,AllowDisconnected:false};
                    programs.set(intent.Id,{program,layout,areaStratum:s});
                });
                WorldSchematic.areaPartitionWitness(a,fail);
                if(!close(totalArea,a.Area)||!close(totalMass,a.Mass))fail();
            }
            for(const p of layout.Programs){
                if(!p||programs.has(p.Id)||!point(p.Anchor)||!Number.isFinite(p.MinimumArea)||p.MinimumArea<=0)fail();
                const program=frame==null?p:{...p,Anchor:{X:p.Anchor.X+seed.X,Y:p.Anchor.Y+seed.Y}};
                if(!point(program.Anchor))fail();programs.set(p.Id,{program,layout});
            }

        }

        if(plan.Allocations.length!==programs.size)fail();let cells=0;

        for(const allocation of plan.Allocations){

            const entry=programs.get(allocation?.Id),shape=allocation?.Shape;

            if(!entry||allocations.has(allocation.Id)||allocation.LayoutId!==entry.layout.Id||allocation.RegionId!==entry.layout.RegionId||

                JSON.stringify(allocation.Program)!==JSON.stringify(entry.program)||!shape||shape.Id!==allocation.Id||shape.Seed?.Id!==allocation.Id||

                !point(shape.Seed.Position)||shape.Seed.Position.X!==entry.program.Anchor.X||shape.Seed.Position.Y!==entry.program.Anchor.Y||

                shape.Seed.MinimumArea!==entry.program.MinimumArea||!list(shape.Cells,def.MaximumCells)||!shape.Cells.length||

                (cells+=shape.Cells.length)>def.MaximumCells||shape.Cells.some(p=>!list(p?.Vertices,512)||p.Vertices.length<3||!p.Vertices.every(point)||area(p)<=0)||

                !near(shape.Area,shape.Cells.reduce((s,p)=>s+area(p),0))||shape.Area<entry.program.MinimumArea||

                !list(shape.BoundaryEdges,32768)||!shape.BoundaryEdges.length||!shape.BoundaryEdges.every(edge))fail();

            if(entry.areaStratum&&(JSON.stringify(shape.Cells)!==JSON.stringify(entry.areaStratum.Cells)||JSON.stringify(shape.BoundaryEdges)!==JSON.stringify(entry.areaStratum.BoundaryEdges)||!near(shape.Area,entry.areaStratum.Area)))fail();
            const paths=this.partitionPaths(effective(entry.layout),fail),seed=regions.get(entry.layout.RegionId).Seed.Position;
            if(paths)for(const [cut,sign] of paths.get(allocation.Id)){
                const angle=cut.NormalDegrees*Math.PI/180,ox=cut.Origin.X+(entry.layout.RegionFrame?seed.X:0),oy=cut.Origin.Y+(entry.layout.RegionFrame?seed.Y:0);
                if(shape.Cells.some(c=>c.Vertices.some(p=>sign*(Math.cos(angle)*(p.X-ox)+Math.sin(angle)*(p.Y-oy)) < -1e-7)))fail();
            }
            allocations.set(allocation.Id,allocation);

        }

        const areaIds=new Set(),scopeOwners=new Set();
        for(const layout of layouts.values()){
            const ownerKey=JSON.stringify([layout.RegionId,layout.ParentAllocationId??null]);
            if(scopeOwners.has(ownerKey))fail();scopeOwners.add(ownerKey);
            const visited=new Set();let current=layout;
            while(current.ParentAllocationId!=null){
                if(typeof current.ParentAllocationId!=='string'||visited.has(current.Id))fail();visited.add(current.Id);
                const parent=allocations.get(current.ParentAllocationId);
                if(!parent||parent.RegionId!==current.RegionId)fail();
                current=layouts.get(parent.LayoutId);if(!current)fail();
            }
        }

        for(const a of plan.Areas){

            const layout=layouts.get(a?.LayoutId),region=layout?.ParentAllocationId!=null?allocations.get(layout.ParentAllocationId)?.Shape:regions.get(a?.RegionId);

            if(!layout||!region||areaIds.has(a.LayoutId)||layout.RegionId!==a.RegionId||(a.ParentAllocationId??null)!==(layout.ParentAllocationId??null)||!near(a.RegionArea,region.Area)||

                ![a.AllocatedArea,a.ResidualArea].every(v=>Number.isFinite(v)&&v>=0)||!near(a.AllocatedArea+a.ResidualArea,a.RegionArea)||

                !near(a.AllocatedArea,plan.Allocations.filter(x=>x.LayoutId===a.LayoutId).reduce((s,x)=>s+x.Shape.Area,0))||

                (a.ResidualArea>Math.max(1,a.RegionArea)*1e-10&&typeof a.ResidualPurpose!=='string'))fail();

            areaIds.add(a.LayoutId);

        }

        const requirements=new Map(def.Interfaces.map(i=>[i.Id,i])),seen=new Set();if(requirements.size!==def.Interfaces.length)fail();

        for(const item of plan.Interfaces){

            const requirement=requirements.get(item?.Id),span=item?.SelectedSpan;

            if(!requirement||seen.has(item.Id)||!allocations.has(requirement.FromId)||!allocations.has(requirement.ToId)||requirement.FromId===requirement.ToId||

                JSON.stringify(requirement)!==JSON.stringify(item.Requirement)||!list(item.Segments,32768)||!item.Segments.length||!item.Segments.every(edge)||

                !edge(span)||!item.Segments.some(e=>JSON.stringify(e)===JSON.stringify(span))||

                !Number.isFinite(requirement.MinimumSpan)||requirement.MinimumSpan<=0||Math.hypot(span.To.X-span.From.X,span.To.Y-span.From.Y)<requirement.MinimumSpan||

                !point(item.Position)||!near(item.Position.X,(span.From.X+span.To.X)/2)||!near(item.Position.Y,(span.From.Y+span.To.Y)/2))fail();

            seen.add(item.Id);

        }

        const anchorBindings=world.PlacementPlan?.TemplateDefinition?.AllocationAnchors||[],boundAnchors=new Set();
        const anchorResults=world.PlacementPlan?.AllocationAnchorResults??[],resultIds=new Set();
        const footprintResults=world.PlacementPlan?.FootprintAnchorResults??[],footprintIds=new Set();
        if(!Array.isArray(footprintResults)||footprintResults.length>32)fail();
        for(const r of footprintResults)if(!r||footprintIds.has(r.AnchorId))fail();else footprintIds.add(r.AnchorId);
        if(!Array.isArray(anchorBindings)||anchorBindings.length>32||!Array.isArray(anchorResults)||anchorResults.length>32||world.PlacementPlan?.Definition?.AllocationAnchors!=null)fail();
        for(const result of anchorResults)if(!result||resultIds.has(result.AnchorId))fail();else resultIds.add(result.AnchorId);
        let interiorCount=0,footprintCount=0;
        for(const b of anchorBindings){
            const allocation=allocations.get(b?.AllocationId),anchor=world.PlacementPlan?.Definition?.Request?.Anchors?.find(a=>a.Id===b?.AnchorId);
            if(!allocation||!anchor||boundAnchors.has(b.AnchorId)||
                world.PlacementPlan.TemplateDefinition.Request.Anchors.some(a=>a.Id===b.AnchorId)||
                (world.PlacementPlan.TemplateDefinition.RegionAnchors||[]).some(a=>a.AnchorId===b.AnchorId)||
                typeof b.Purpose!=='string'||!b.Purpose.trim()||typeof b.SourceRuleId!=='string'||!b.SourceRuleId.trim())fail();
            const result=anchorResults.find(r=>r.AnchorId===b.AnchorId);
            if(b.Footprint!=null){
                footprintCount++;if(b.Interior!=null||result)fail();
                const fit=footprintResults.find(r=>r.AnchorId===b.AnchorId);
                WorldSchematic.footprintAnchorWitness(b,fit,allocation,anchor,world,fail);
            }
            else if(b.Interior==null){if(result||JSON.stringify(allocation.Program.Anchor)!==JSON.stringify(anchor.Position))fail();}
            else{
                interiorCount++;if(!result||result.AllocationId!==b.AllocationId)fail();
                WorldSchematic.interiorWitness(b.Interior,result.Interior,allocation.Shape,anchor.Position,fail);
            }
            boundAnchors.add(b.AnchorId);
        }
        if(anchorResults.length!==interiorCount||footprintResults.length!==footprintCount)fail();
        for(const demand of demands)if(demand.AllocationId!=null&&allocations.get(demand.AllocationId)?.RegionId!==demand.RegionId)fail();

        return plan;

    }



    static footprintAnchorWitness(binding,result,allocation,anchor,world,fail) {
        const spec=binding.Footprint,q=spec?.Search,p=result?.Plan;
        const clearance=q?.MinimumClearance===undefined?0:q.MinimumClearance;
        const point=v=>v&&[v.X,v.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=1e7);
        const integer=(n,lo,hi)=>Number.isSafeInteger(n)&&n>=lo&&n<=hi;
        const demand=world.PlacementPlan?.Placement?.Request?.Demands?.find(d=>d.Id===spec?.DemandId);
        if(!demand||demand.AllocationId!==binding.AllocationId||result?.AllocationId!==binding.AllocationId||result.DemandId!==spec.DemandId||
            !q||!integer(q.MaximumCandidates,1,16384)||!integer(q.MaximumContacts,1,16384)||!integer(q.MaximumWork,1,200000000)||!Number.isFinite(clearance)||clearance<0||clearance>1e7||
            !point(p?.Preferred)||!point(p.Position)||JSON.stringify(p.Preferred)!==JSON.stringify(allocation.Program.Anchor)||JSON.stringify(p.Position)!==JSON.stringify(anchor.Position)||
            !integer(p.Contacts,0,q.MaximumContacts)||!integer(p.Candidates,1,q.MaximumCandidates)||!integer(p.Evaluations,1,p.Candidates+1)||
            !integer(p.Work,1,q.MaximumWork)||typeof p.CandidateSource!=='string'||!p.CandidateSource.length||!/^[a-f0-9]{64}$/.test(p.FootprintSha256))fail();
        const source=demand.Reservation??demand.Footprint;
        if(!Array.isArray(source?.Vertices)||source.Vertices.length<3||source.Vertices.length>64||!source.Vertices.every(point))fail();
        const polygon=source.Vertices.map(v=>({X:v.X+p.Position.X,Y:v.Y+p.Position.Y}));
        const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        const on=(v,a,b)=>Math.abs(cross(a,b,v))<=1e-8&&v.X>=Math.min(a.X,b.X)-1e-8&&v.X<=Math.max(a.X,b.X)+1e-8&&v.Y>=Math.min(a.Y,b.Y)-1e-8&&v.Y<=Math.max(a.Y,b.Y)+1e-8;
        const inside=(v,vertices,boundary=true)=>{
            let yes=false;
            for(let i=0;i<vertices.length;i++){
                const a=vertices[i],b=vertices[(i+1)%vertices.length];if(on(v,a,b))return boundary;
                if((a.Y>v.Y)!==(b.Y>v.Y)&&v.X<(b.X-a.X)*(v.Y-a.Y)/(b.Y-a.Y)+a.X)yes=!yes;
            }
            return yes;
        };
        const crossesInterior=edge=>{
            const a=edge.From,b=edge.To,dx=b.X-a.X,dy=b.Y-a.Y,ts=[0,1];
            for(let i=0;i<polygon.length;i++){
                const c=polygon[i],d=polygon[(i+1)%polygon.length],ex=d.X-c.X,ey=d.Y-c.Y,den=dx*ey-dy*ex;
                if(Math.abs(den)>1e-12){
                    const t=((c.X-a.X)*ey-(c.Y-a.Y)*ex)/den,u=((c.X-a.X)*dy-(c.Y-a.Y)*dx)/den;
                    if(t>=0&&t<=1&&u>=0&&u<=1)ts.push(t);
                }else for(const v of [c,d])if(on(v,a,b))ts.push(Math.max(0,Math.min(1,((v.X-a.X)*dx+(v.Y-a.Y)*dy)/(dx*dx+dy*dy))));
            }
            ts.sort((a,b)=>a-b);
            for(let i=1;i<ts.length;i++)if(ts[i]-ts[i-1]>1e-12){const t=(ts[i]+ts[i-1])/2;if(inside({X:a.X+t*dx,Y:a.Y+t*dy},polygon,false))return true;}
            return false;
        };
        if(polygon.some(v=>!allocation.Shape.Cells.some(c=>inside(v,c.Vertices)))||allocation.Shape.BoundaryEdges.some(crossesInterior))fail();
        const boundary=world.PlacementPlan.TemplateDefinition.Request.Boundary??world.Surface?.Boundary;
        if(boundary&&(polygon.some(v=>!inside(v,boundary.Vertices))||boundary.Vertices.some((a,i)=>crossesInterior({From:a,To:boundary.Vertices[(i+1)%boundary.Vertices.length]}))))fail();
        if(clearance>0){
            const distance=(v,a,b)=>{const dx=b.X-a.X,dy=b.Y-a.Y,t=Math.max(0,Math.min(1,((v.X-a.X)*dx+(v.Y-a.Y)*dy)/(dx*dx+dy*dy)));return Math.hypot(v.X-a.X-t*dx,v.Y-a.Y-t*dy);};
            const borders=[...allocation.Shape.BoundaryEdges,...(boundary?.Vertices||[]).map((v,i)=>({From:v,To:boundary.Vertices[(i+1)%boundary.Vertices.length]}))];
            for(const edge of borders)for(let i=0;i<polygon.length;i++){
                const a=polygon[i],b=polygon[(i+1)%polygon.length];
                if(Math.min(distance(a,edge.From,edge.To),distance(b,edge.From,edge.To),distance(edge.From,a,b),distance(edge.To,a,b))+1e-8<clearance)fail();
            }
        }
        // C# replays the search and source hash. The viewer checks saved geometry,
        // bounded receipts and anchor identity without generating placements.
    }

    static physicalCorridors(world,routes) {

        const plan=world.CorridorPlan;if(plan==null)return [];

        const fail=()=>{throw new Error('Invalid physical corridor plan. Regenerate with the engine.');};

        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y);

        const same=(a,b)=>a.X===b.X&&a.Y===b.Y;

        const list=(v,max)=>Array.isArray(v)&&v.length<=max;

        const strings=v=>list(v,128)&&v.every(s=>typeof s==='string')&&new Set(v).size===v.length;

        if(!list(plan.Groups,16)||!plan.Groups.length||!list(plan.Definition?.Groups,16)||plan.Definition.Groups.length!==plan.Groups.length)fail();

        const seen=new Set(),kinds=new Set(),routeMap=new Map(routes.map(r=>[r.id,r]));

        for(const group of plan.Groups){

            const d=group?.Definition,declared=plan.Definition.Groups.find(g=>g.Id===d?.Id);

            if(!declared||seen.has(d.Id)||kinds.has(d.Movement)||declared.ConnectionKind!==d.Movement||d.Level!==0||declared.Level!==0||

                d.JunctionPolicy!=='connect-at-grade'||declared.JunctionPolicy!==d.JunctionPolicy||group.GeometryModel!=='surface-circular-at-grade-v1'||

                group.IntersectionTolerance!==1e-8||!list(group.Segments,16384)||!group.Segments.length||!list(group.Junctions,2048)||

                !list(group.Journeys,128)||!group.Journeys.length||!list(group.SharedDependencies,16384))fail();

            seen.add(d.Id);kinds.add(d.Movement);

            const segments=new Map(group.Segments.map(s=>[s.Id,s])),junctions=new Map(group.Junctions.map(j=>[j.Id,j])),journeys=new Map(group.Journeys.map(j=>[j.Id,j]));

            if(segments.size!==group.Segments.length||junctions.size!==group.Junctions.length||journeys.size!==group.Journeys.length)fail();

            for(const s of segments.values())if(typeof s.Id!=='string'||!point(s.From)||!point(s.To)||same(s.From,s.To)||!Number.isFinite(s.Width)||

                s.Width<=0||s.Width!==2*group.Profile?.Radius||!strings(s.ForwardJourneys)||!strings(s.ReverseJourneys)||

                !s.ForwardJourneys.concat(s.ReverseJourneys).length||s.ForwardJourneys.concat(s.ReverseJourneys).some(id=>!journeys.has(id)))fail();

            const transitionKey=t=>JSON.stringify([t.FromSegmentId,t.FromForward,t.ToSegmentId,t.ToForward]);

            for(const j of junctions.values()){

                if(typeof j.Id!=='string'||!point(j.Position)||!list(j.Envelope?.Vertices,32)||j.Envelope.Vertices.length<3||

                    j.Envelope.Vertices.some(p=>!point(p))||!list(j.SegmentIds,2048)||!j.SegmentIds.length||new Set(j.SegmentIds).size!==j.SegmentIds.length||

                    !list(j.Transitions,4096))fail();

                const incoming=[],outgoing=[];

                for(const id of j.SegmentIds){const s=segments.get(id);if(!s)fail();

                    const from=s.FromJunctionId===j.Id,to=s.ToJunctionId===j.Id;

                    if(from===to||!same(j.Position,from?s.From:s.To))fail();

                    if(s.ForwardJourneys.length)(from?outgoing:incoming).push([id,true]);

                    if(s.ReverseJourneys.length)(from?incoming:outgoing).push([id,false]);

                }

                const expected=new Set(incoming.flatMap(a=>outgoing.map(b=>JSON.stringify([...a,...b]))));

                if(expected.size!==j.Transitions.length||new Set(j.Transitions.map(transitionKey)).size!==j.Transitions.length||j.Transitions.some(t=>!expected.has(transitionKey(t))))fail();

            }

            for(const s of segments.values())if(!junctions.get(s.FromJunctionId)?.SegmentIds.includes(s.Id)||!junctions.get(s.ToJunctionId)?.SegmentIds.includes(s.Id))fail();

            const expectedRoutes=routes.filter(r=>world.Connections.find(c=>c.Id===r.connectionId)?.Kind===d.Movement);

            if(expectedRoutes.length!==journeys.size||expectedRoutes.some(r=>!journeys.has(r.id)))fail();

            for(const j of journeys.values()){

                const route=routeMap.get(j.Id);

                if(!route||j.ConnectionId!==route.connectionId||!point(j.From)||!point(j.To)||!same(j.From,{X:route.points[0].x,Y:route.points[0].y})||

                    !same(j.To,{X:route.points.at(-1).x,Y:route.points.at(-1).y})||!list(j.Steps,16384)||!j.Steps.length)fail();

                let cursor=j.From;

                for(const step of j.Steps){const s=segments.get(step.SegmentId);if(!s||typeof step.Forward!=='boolean'||

                    !(step.Forward?s.ForwardJourneys:s.ReverseJourneys).includes(j.Id)||!same(cursor,step.Forward?s.From:s.To))fail();cursor=step.Forward?s.To:s.From;}

                if(!same(cursor,j.To))fail();

            }

            const dependencies=group.Segments.map(s=>({SegmentId:s.Id,ConnectionIds:[...new Set(s.ForwardJourneys.concat(s.ReverseJourneys).map(id=>journeys.get(id).ConnectionId))].sort()})).filter(d=>d.ConnectionIds.length>1);

            if(JSON.stringify(dependencies)!==JSON.stringify(group.SharedDependencies))fail();

            const required=d.RequireSingleSegmentFailureTolerance===undefined?false:d.RequireSingleSegmentFailureTolerance;
            const declaredRequired=declared.RequireSingleSegmentFailureTolerance===undefined?false:declared.RequireSingleSegmentFailureTolerance;
            if(typeof required!=='boolean'||required!==declaredRequired)fail();
            if(!required){if(group.Resilience!=null)fail();}
            else {
                const receipt=group.Resilience;
                if(receipt?.Version!==1||!Number.isSafeInteger(receipt.Work)||receipt.Work<1||receipt.Work>group.Work||!list(receipt.Alternatives,4096))fail();
                const expected=new Set([...journeys.values()].flatMap(j=>[...new Set(j.Steps.map(s=>s.SegmentId))].map(id=>JSON.stringify([j.Id,id]))));
                if(expected.size!==receipt.Alternatives.length)fail();
                let count=0;
                for(const alternative of receipt.Alternatives){
                    const j=journeys.get(alternative?.JourneyId),key=JSON.stringify([alternative?.JourneyId,alternative?.FailedSegmentId]);
                    if(!j||!expected.delete(key)||!list(alternative.Steps,32768)||(count+=alternative.Steps.length)>1000000)fail();
                    let cursor=j.From,previous=null;
                    for(const step of alternative.Steps){
                        const s=segments.get(step?.SegmentId);
                        if(!s||s.Id===alternative.FailedSegmentId||typeof step.Forward!=='boolean'||!(step.Forward?s.ForwardJourneys:s.ReverseJourneys).length||!same(cursor,step.Forward?s.From:s.To))fail();
                        if(previous){
                            const p=segments.get(previous.SegmentId),junction=junctions.get(previous.Forward?p.ToJunctionId:p.FromJunctionId);
                            if(!junction?.Transitions.some(t=>t.FromSegmentId===previous.SegmentId&&t.FromForward===previous.Forward&&t.ToSegmentId===step.SegmentId&&t.ToForward===step.Forward))fail();
                        }
                        cursor=step.Forward?s.To:s.From;previous=step;
                    }
                    if(!same(cursor,j.To))fail();
                }
            }

        }

        return plan.Groups;

    }



    static capacityAllocations(world,nodes,children) {

        const plan=world.CapacityPlan,marked=[...nodes.values()].filter(n=>n.CapacityKey!=null);
        if(plan==null){if(marked.length)throw new Error('Capacity plan required.');return [];}

        if(!plan.Definitions||!Array.isArray(plan.Allocations)||plan.Allocations.length<1||plan.Allocations.length>4096)

            throw new Error('Invalid capacity plan.');

        const groups=new Set(),selected=new Set();

        for(const allocation of plan.Allocations){

            const request=allocation?.Request,rule=request?.Rule,contributions=allocation?.Contributions;

            if(!request||!rule||!Array.isArray(contributions)||!nodes.has(request.ParentNodeId)||typeof request.PartId!=='string'||

                !Number.isSafeInteger(allocation.Count)||allocation.Count<0||allocation.Count>rule.MaximumCount||allocation.Count<request.MinimumCount||

                request.ExactCount!=null&&(!Number.isSafeInteger(request.ExactCount)||request.ExactCount<request.MinimumCount||allocation.Count!==request.ExactCount)||

                !Number.isFinite(request.RequiredQuantity)||request.RequiredQuantity<0||

                !Number.isFinite(allocation.ProvidedQuantity)||

                allocation.ProvidedQuantity+1e-8<request.RequiredQuantity||

                !Array.isArray(allocation.NodeIds)||allocation.NodeIds.length!==allocation.Count)

                throw new Error('Invalid capacity allocation.');

            const group=`${request.ParentNodeId}/${request.PartId}`;

            if(groups.has(group))throw new Error('Duplicate capacity group.');groups.add(group);

            const prefix=group+'-',members=children.get(request.ParentNodeId).filter(n=>n.Id.startsWith(prefix)&&/^\d+$/.test(n.Id.slice(prefix.length)));

            if(members.length!==allocation.Count||allocation.NodeIds.some((id,i)=>id!==prefix+i||!nodes.has(id)||

                nodes.get(id).ParentId!==request.ParentNodeId))

                throw new Error('Capacity membership mismatch.');

            const accounted=new Set(),expectedIds=new Set(allocation.NodeIds),sourceGroups=new Set();let total=0;

            for(const contribution of contributions){
                const source=contribution?.Source, ids=contribution?.NodeIds;
                if(!source||!Array.isArray(ids)||ids.length<1||source.Unit!==rule.Unit||!Number.isFinite(source.Quantity)||source.Quantity<=0||
                    !Number.isFinite(contribution.ProvidedQuantity)||Math.abs(contribution.ProvidedQuantity-source.Quantity*ids.length)>1e-8*Math.max(1,contribution.ProvidedQuantity))
                    throw new Error('Invalid capacity contribution.');
                const sourceGroup=JSON.stringify([contribution.ArchetypeId,contribution.StructureKey]);
                if(sourceGroups.has(sourceGroup))throw new Error('Duplicate capacity contribution.');sourceGroups.add(sourceGroup);
                if(request.Variation!=null){
                    if(!request.Variation.ArchetypeIds?.includes(contribution.ArchetypeId)||!request.CandidateStructureKeys||
                        request.CandidateStructureKeys[contribution.ArchetypeId]!==contribution.StructureKey)throw new Error('Capacity candidate mismatch.');
                }else if(contribution.ArchetypeId!==request.ArchetypeId||contribution.StructureKey!==request.StructureKey)
                    throw new Error('Capacity candidate mismatch.');
                const expected=rule.UseStructureVariant?world.StructureDefinitions?.[contribution.StructureKey]:
                    plan.Definitions[contribution.ArchetypeId]?.find(c=>c.Id===rule.CapacityId);
                if(!expected||source.Quantity!==(rule.UseStructureVariant?expected.Variant.ServiceCapacity:expected.Quantity)||
                    source.Unit!==(rule.UseStructureVariant?expected.Definition.CapacityUnit:expected.Unit)||
                    source.SourceId!==(rule.UseStructureVariant?`structure:${contribution.StructureKey}`:`archetype:${contribution.ArchetypeId}/${rule.CapacityId}`)||
                    source.SourceRuleId!==(rule.UseStructureVariant?`structure:${contribution.StructureKey}`:expected.SourceRuleId))throw new Error('Capacity source mismatch.');
                for(const id of ids){
                    const member=nodes.get(id);
                    if(accounted.has(id)||selected.has(id)||!expectedIds.has(id)||!member||member.ArchetypeId!==contribution.ArchetypeId||member.StructureKey!==contribution.StructureKey||member.CapacityKey!==request.ParentNodeId+'/capacity/'+request.PartId)
                        throw new Error('Capacity contribution membership mismatch.');
                    accounted.add(id);selected.add(id);
                }
                total+=contribution.ProvidedQuantity;
            }
            if(accounted.size!==allocation.Count||Math.abs(total-allocation.ProvidedQuantity)>1e-8*Math.max(1,allocation.ProvidedQuantity))
                throw new Error('Capacity contribution total mismatch.');

        }

        if(marked.some(n=>!selected.has(n.Id)))throw new Error('Unaccounted capacity member.');
        return plan.Allocations;

    }



    static serviceAllocations(world,nodes) {

        const plan=world.ServicePlan;if(plan==null)return;

        const fail=()=>{throw new Error('Invalid service delivery record. Regenerate with the engine.');};

        const positive=n=>Number.isFinite(n)&&n>0, close=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));

        if(!Array.isArray(plan.Sources)||plan.Sources.length>4096||!Array.isArray(plan.Allocations)||plan.Allocations.length>4096||

            !Array.isArray(plan.Definition?.Bindings)||!Array.isArray(plan.Definition?.Links)||!plan.Profiles||!plan.Usage||

            !Number.isSafeInteger(plan.Work)||plan.Work<0)fail();

        const sources=new Map(),limits=new Map(),usage=new Map(),bound=new Set(),links=new Map();

        const graph=new Map(world.Connections.map(c=>[c.Id,c]));

        for(const source of plan.Sources){

            if(!nodes.has(source?.NodeId)||sources.has(source.NodeId)||!plan.Profiles[nodes.get(source.NodeId).ArchetypeId]||

                !Array.isArray(source.Supplies)||!Array.isArray(source.Requirements)||!Array.isArray(source.RemainingRequirements))fail();

            sources.set(source.NodeId,source);

            for(const [items,kind] of [[source.Supplies,'supply'],[source.Requirements,'requirement']]){

                const ids=new Set();

                for(const item of items){if(!item||typeof item.Id!=='string'||ids.has(item.Id)||!positive(item.Quantity)||typeof item.Kind!=='string'||typeof item.Unit!=='string')fail();ids.add(item.Id);

                    if(kind==='supply'){

                        if(item.Area!=null&&(world.Space.Unit!=='meters'||!Array.isArray(item.Area.Vertices)||item.Area.Vertices.length<3||item.Area.Vertices.length>256||

                            item.Area.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y))))fail();

                        limits.set(`source:${source.NodeId}/${item.Id}`,item.Quantity);

                    }}

            }

        }

        for(const link of plan.Definition.Links){

            if(!graph.has(link?.ConnectionId)||links.has(link.ConnectionId)||!positive(link.Capacity))fail();

            links.set(link.ConnectionId,link);limits.set(`link:${link.ConnectionId}`,link.Capacity);

        }

        const capacityDefinitions=plan.Definition.CorridorCapacities||[],segmentLimits=new Map(),capacityGroups=new Map(),routeIds=new Map();

        if(!Array.isArray(capacityDefinitions)||capacityDefinitions.length>16||!Array.isArray(plan.SegmentCapacities||[])||(plan.SegmentCapacities||[]).length>100000)fail();

        const resolvedSegments=new Map();

        for(const c of plan.SegmentCapacities||[]){if(!c||typeof c.SegmentId!=='string'||resolvedSegments.has(c.SegmentId))fail();resolvedSegments.set(c.SegmentId,c);}

        for(const r of world.RoutePlan?.Routes||[])routeIds.set(r.ConnectionId,{Forward:r.Forward.Id,Reverse:r.Reverse?.Id});

        for(const n of world.NetworkPlan?.Networks||[])for(const e of n.Plan.Edges)routeIds.set(e.Link.Id,{Forward:e.Forward.Id,Reverse:e.Reverse.Id});

        const seenGroups=new Set();

        for(const d of capacityDefinitions){

            const group=world.CorridorPlan?.Groups?.find(g=>g.Definition.Id===d?.GroupId);

            if(!group||seenGroups.has(d.GroupId)||!positive(d.CapacityPerSegment)||typeof d.Unit!=='string'||!Array.isArray(group.Segments)||

                group.Segments.length>16384||!Array.isArray(group.Journeys)||group.Journeys.length>128)fail();

            seenGroups.add(d.GroupId);

            for(const s of group.Segments){

                const c=resolvedSegments.get(s.Id);

                if(!c||segmentLimits.has(s.Id)||c.GroupId!==d.GroupId||c.Capacity!==d.CapacityPerSegment||c.Unit!==d.Unit||c.Purpose!==d.Purpose||c.SourceRuleId!==d.SourceRuleId)fail();

                segmentLimits.set(s.Id,c);limits.set(`segment:${s.Id}`,c.Capacity);

            }

            for(const id of new Set(group.Journeys.map(j=>j.ConnectionId))){if(capacityGroups.has(id))fail();capacityGroups.set(id,group);}

        }

        if(segmentLimits.size!==(plan.SegmentCapacities||[]).length)fail();

        for(const allocation of plan.Allocations){

            const requirement=sources.get(allocation?.ConsumerNodeId)?.Requirements.find(r=>r.Id===allocation.RequirementId);

            const supply=sources.get(allocation?.ProviderNodeId)?.Supplies.find(s=>s.Id===allocation.SupplyId);

            const key=JSON.stringify([allocation?.ConsumerNodeId,allocation?.RequirementId]);

            const bindings=plan.Definition.Bindings.filter(b=>b.ConsumerNodeId===allocation.ConsumerNodeId&&b.RequirementId===allocation.RequirementId);

            if(!requirement||!supply||bound.has(key)||bindings.length!==1||bindings[0].ProviderNodeId!==allocation.ProviderNodeId||bindings[0].SupplyId!==allocation.SupplyId||

                allocation.Quantity!==requirement.Quantity||allocation.Kind!==requirement.Kind||allocation.Kind!==supply.Kind||allocation.Unit!==requirement.Unit||allocation.Unit!==supply.Unit||

                allocation.Physical!==requirement.RequirePhysicalRoute||Boolean(allocation.AreaContained)!==Boolean(requirement.SourceContainsSite)||!Array.isArray(allocation.ConnectionIds)||

                (requirement.SourceContainsSite?(allocation.ConnectionIds.length!==0||allocation.Physical||!supply.Area):allocation.ConnectionIds.length===0)||

                JSON.stringify(allocation.ConnectionIds)!==JSON.stringify(bindings[0].ConnectionIds)||

                (allocation.Physical?(!Number.isFinite(allocation.Distance)||allocation.Distance<0||requirement.MaximumDistance!=null&&allocation.Distance>requirement.MaximumDistance+1e-8):allocation.Distance!==null))fail();

            bound.add(key);let current=allocation.ProviderNodeId;const visited=new Set([current]),expectedSegments=new Map();

            const reserve=id=>usage.set(id,(usage.get(id)||0)+allocation.Quantity);

            reserve(`source:${allocation.ProviderNodeId}/${allocation.SupplyId}`);

            for(const id of allocation.ConnectionIds){

                const edge=graph.get(id),link=links.get(id);

                if(!edge||!link||edge.Kind!==requirement.ConnectionKind||link.Unit!==allocation.Unit)fail();

                const forward=current===edge.FromId,group=capacityGroups.get(id);

                if(group){

                    if(!allocation.Physical)fail();

                    const journeyId=routeIds.get(id)?.[forward?'Forward':'Reverse'];

                    const journey=group.Journeys.find(j=>j.Id===journeyId&&j.ConnectionId===id);

                    if(!journey||!Array.isArray(journey.Steps)||journey.Steps.length>16384)fail();

                    for(const step of journey.Steps){const limit=segmentLimits.get(step.SegmentId);if(!limit||limit.GroupId!==group.Definition.Id||limit.Unit!==allocation.Unit)fail();

                        expectedSegments.set(step.SegmentId,(expectedSegments.get(step.SegmentId)||0)+1);}

                }

                if(current===edge.FromId)current=edge.ToId;else if(edge.Bidirectional&&current===edge.ToId)current=edge.FromId;else fail();

                if(visited.has(current))fail();visited.add(current);reserve(`link:${id}`);

            }

            if(!requirement.SourceContainsSite&&current!==allocation.ConsumerNodeId)fail();

            const uses=allocation.SegmentUses||[];

            if(!Array.isArray(uses)||uses.length!==expectedSegments.size||new Set(uses.map(u=>u.SegmentId)).size!==uses.length)fail();

            for(const use of uses){

                const count=expectedSegments.get(use.SegmentId),limit=segmentLimits.get(use.SegmentId);

                if(!limit||use.GroupId!==limit.GroupId||use.Traversals!==count||!Number.isSafeInteger(count)||count<1||!close(use.Quantity,allocation.Quantity*count))fail();

                const id=`segment:${use.SegmentId}`;usage.set(id,(usage.get(id)||0)+use.Quantity);

            }

        }

        if(bound.size!==plan.Definition.Bindings.length)fail();

        for(const source of sources.values())for(const r of source.Requirements)if(!bound.has(JSON.stringify([source.NodeId,r.Id])))fail();

        if(Object.keys(plan.Usage).length!==limits.size)fail();

        for(const [id,limit] of limits){const used=usage.get(id)||0;if(used>limit+1e-8||!close(plan.Usage[id],used))fail();}

        const expected=plan.Allocations.filter(a=>nodes.get(a.ConsumerNodeId)?.StructureKey!=null),evaluations=plan.PolicyEvaluations||[];

        if(!Array.isArray(evaluations)||evaluations.length!==expected.length)fail();

        for(const allocation of expected){

            const checks=evaluations.filter(p=>p.ConsumerNodeId===allocation.ConsumerNodeId&&p.RequirementId===allocation.RequirementId);

            const structure=world.StructureDefinitions?.[nodes.get(allocation.ConsumerNodeId).StructureKey];

            if(checks.length!==1||checks[0].PolicyId!==structure?.Catalog?.PolicyId||checks[0].SourceId!==`${allocation.ProviderNodeId}/${allocation.SupplyId}`)fail();

        }

    }



    static progression(world,nodes) {
        const plan=world.ProgressionPlan;
        if(plan==null)return;
        const fail=()=>{throw new Error('Invalid progression witness. Regenerate with the engine.');};
        const text=v=>typeof v==='string'&&v.trim().length>0;
        const list=(v,min,max)=>Array.isArray(v)&&v.length>=min&&v.length<=max&&v.every(text)&&new Set(v).size===v.length;
        if(plan.Version!==1||!Array.isArray(plan.Rules)||plan.Rules.length<1||plan.Rules.length>32||
            !Array.isArray(plan.Results)||plan.Results.length!==plan.Rules.length||!Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>50000000)fail();
        const links=new Map(world.Connections.map(l=>[l.Id,l])),seen=new Set();let total=0;
        for(let i=0;i<plan.Rules.length;i++){
            const rule=plan.Rules[i],result=plan.Results[i];
            if(!rule||!text(rule.Id)||seen.has(rule.Id)||!text(rule.Purpose)||!text(rule.SourceRuleId)||!nodes.has(rule.ScopeNodeId)||!nodes.has(rule.StartNodeId)||
                !list(rule.GoalNodeIds,1,64)||!list(rule.ConnectionKinds,1,32)||!list(rule.RequiredMilestones,0,32)||
                new Set([...rule.GoalNodeIds,...rule.RequiredMilestones,rule.StartNodeId]).size!==rule.GoalNodeIds.length+rule.RequiredMilestones.length+1||
                !Number.isSafeInteger(rule.MaximumWork)||rule.MaximumWork<1||rule.MaximumWork>10000000||
                !result||result.RuleId!==rule.Id||!Array.isArray(result.Paths)||result.Paths.length!==rule.GoalNodeIds.length||
                !Number.isSafeInteger(result.Work)||result.Work<1||result.Work>rule.MaximumWork)fail();
            seen.add(rule.Id);total+=result.Work;
            const inScope=id=>{for(let depth=0;depth<=32;depth++){if(id===rule.ScopeNodeId)return true;id=nodes.get(id)?.ParentId;if(id==null)return false;}return false;};
            if(![rule.StartNodeId,...rule.GoalNodeIds,...rule.RequiredMilestones].every(id=>nodes.has(id)&&inScope(id)))fail();
            const capabilities=rule.Capabilities,grants=new Map(),gates=new Map();
            if(capabilities!=null){
                if(!list(capabilities.CapabilityIds,1,16)||!list(capabilities.InitialCapabilityIds,0,16)||
                    !Array.isArray(capabilities.Grants)||capabilities.Grants.length>256||!Array.isArray(capabilities.Gates)||capabilities.Gates.length>512||
                    typeof capabilities.RequireReturnToStart!=='boolean'||!Number.isSafeInteger(capabilities.MaximumStates)||capabilities.MaximumStates<1||capabilities.MaximumStates>100000||
                    !Number.isSafeInteger(result.ReachableStates)||result.ReachableStates<1||result.ReachableStates>capabilities.MaximumStates)fail();
                const known=values=>values.every(id=>capabilities.CapabilityIds.includes(id));
                if(!known(capabilities.InitialCapabilityIds))fail();
                for(const grant of capabilities.Grants){
                    if(!grant||!nodes.has(grant.NodeId)||!inScope(grant.NodeId)||grants.has(grant.NodeId)||!list(grant.CapabilityIds,1,16)||!known(grant.CapabilityIds))fail();
                    grants.set(grant.NodeId,grant.CapabilityIds);
                }
                for(const gate of capabilities.Gates){
                    const link=links.get(gate?.ConnectionId);
                    if(!link||!rule.ConnectionKinds.includes(link.Kind)||!inScope(link.FromId)||!inScope(link.ToId)||gates.has(gate.ConnectionId)||!list(gate.RequiredCapabilityIds,1,16)||!known(gate.RequiredCapabilityIds))fail();
                    gates.set(gate.ConnectionId,gate.RequiredCapabilityIds);
                }
                const used=new Set([...capabilities.InitialCapabilityIds,...[...grants.values()].flat(),...[...gates.values()].flat()]);
                if(capabilities.CapabilityIds.some(id=>!used.has(id)))fail();
            }else if(result.ReachableStates!=null)fail();
            for(let j=0;j<result.Paths.length;j++){
                const path=result.Paths[j];
                const sequence=(values,min,max)=>Array.isArray(values)&&values.length>=min&&values.length<=max&&values.every(text);
                if(!path||path.GoalNodeId!==rule.GoalNodeIds[j]||
                    !(capabilities?sequence(path.NodeIds,2,result.ReachableStates):list(path.NodeIds,2,nodes.size))||
                    !(capabilities?sequence(path.ConnectionIds,1,result.ReachableStates-1):list(path.ConnectionIds,1,links.size))||
                    path.ConnectionIds.length!==path.NodeIds.length-1||path.NodeIds[0]!==rule.StartNodeId||path.NodeIds.at(-1)!==path.GoalNodeId||
                    !path.NodeIds.every(id=>nodes.has(id)&&inScope(id)))fail();
                if(capabilities){
                    if(!Array.isArray(path.CapabilityStates)||path.CapabilityStates.length!==path.NodeIds.length)fail();
                    let acquired=new Set(capabilities.InitialCapabilityIds);const states=new Set();
                    for(let k=0;k<path.NodeIds.length;k++){
                        for(const id of grants.get(path.NodeIds[k])||[])acquired.add(id);
                        const saved=path.CapabilityStates[k];
                        if(!list(saved,0,16)||saved.length!==acquired.size||saved.some(id=>!acquired.has(id)))fail();
                        const key=JSON.stringify([path.NodeIds[k],[...acquired].sort()]);if(states.has(key))fail();states.add(key);
                        if(k<path.ConnectionIds.length&&(gates.get(path.ConnectionIds[k])||[]).some(id=>!acquired.has(id)))fail();
                    }
                }else if(path.CapabilityStates!=null)fail();
                let previous=0;
                for(const milestone of rule.RequiredMilestones){const index=path.NodeIds.indexOf(milestone);if(index<=previous)fail();previous=index;}
                for(let k=0;k<path.ConnectionIds.length;k++){
                    const link=links.get(path.ConnectionIds[k]),from=path.NodeIds[k],to=path.NodeIds[k+1];
                    if(!link||!rule.ConnectionKinds.includes(link.Kind)||!(link.FromId===from&&link.ToId===to||link.Bidirectional&&link.FromId===to&&link.ToId===from))fail();
                }
            }
        }
        if(total!==plan.Work)fail();
        // C# owns state exploration, all-path dominance and return safety. This only checks the saved display witness.
    }
    static variation(world,nodes) {
        const plan=world.VariationPlan;
        const fail=()=>{throw new Error('Invalid catalog variation record. Regenerate with the engine.');};
        if(plan==null){if([...nodes.values()].some(n=>n.VariationKey!=null))fail();return;}
        if(plan.Version!==1||!Array.isArray(plan.Allocations)||!plan.Allocations.length||plan.Allocations.length>4096)fail();
        const keys=new Set(),members=new Set(),text=v=>typeof v==='string'&&v.trim().length>0;
        for(const allocation of plan.Allocations){
            const r=allocation?.Request,rule=r?.Rule;
            if(!r||!rule||!text(rule.Id)||!text(rule.Seed)||rule.Seed.length>128||!text(rule.Purpose)||!text(rule.SourceRuleId)||
                !Array.isArray(rule.ArchetypeIds)||!rule.ArchetypeIds.length||rule.ArchetypeIds.length>32||!rule.ArchetypeIds.every(text)||
                new Set(rule.ArchetypeIds).size!==rule.ArchetypeIds.length||!rule.ArchetypeIds.includes(r.BaseArchetypeId)||
                !nodes.has(r.ParentNodeId)||nodes.get(r.ParentNodeId).ArchetypeId!==r.ParentArchetypeId||!text(r.PartId)||!text(r.Kind)||
                !Number.isInteger(r.Count)||r.Count<0||r.Count>100000||allocation.Key!==`${r.ParentNodeId}/variation/${r.PartId}`||keys.has(allocation.Key)||
                !Array.isArray(allocation.Choices)||allocation.Choices.length!==r.Count)fail();
            keys.add(allocation.Key);
            const prefix=`${r.ParentNodeId}/${r.PartId}-`;
            const direct=[...nodes.values()].filter(n=>n.ParentId===r.ParentNodeId&&n.Id.startsWith(prefix)&&/^\d+$/.test(n.Id.slice(prefix.length)));
            if(direct.length!==r.Count)fail();
            for(let i=0;i<r.Count;i++){
                const choice=allocation.Choices[i],node=nodes.get(prefix+i);
                if(!choice||!node||choice.NodeId!==node.Id||node.ArchetypeId!==choice.ArchetypeId||!rule.ArchetypeIds.includes(choice.ArchetypeId)||
                    node.Kind!==r.Kind||node.VariationKey!==allocation.Key||members.has(node.Id)||!/^[a-f0-9]{64}$/.test(choice.ScoreSha256))fail();
                members.add(node.Id);
            }
        }
        if([...nodes.values()].some(n=>n.VariationKey!=null&&!members.has(n.Id)))fail();
        // C# owns deterministic hash replay; the browser checks the saved display receipt.
    }
    static protectedSpaces(world,nodes) {
        const plan=world.ContextPlan,defined=plan?.Definition?.Protections,saved=plan?.Protections;
        const fail=()=>{throw new Error('Invalid protected gameplay space. Regenerate with the engine.');};
        if(defined==null){if(saved!=null)fail();return [];}
        if(!Array.isArray(defined)||defined.length<1||defined.length>64||!Array.isArray(saved)||saved.length!==defined.length)fail();
        const finite=v=>Number.isFinite(v)&&Math.abs(v)<=1e7;
        const point=p=>p&&finite(p.X)&&finite(p.Y);
        const polygon=p=>Array.isArray(p?.Vertices)&&p.Vertices.length>=3&&p.Vertices.length<=256&&p.Vertices.every(point);
        const text=v=>typeof v==='string'&&v.trim().length>0;
        const origin=id=>{const p=nodes.get(id)?.Position;if(!Array.isArray(p)||p.length!==2||!p.every(finite))fail();return {X:p[0],Y:p[1]};};
        const anchor=a=>{if(!a||!point(a.Offset))fail();const p=origin(a.NodeId);return {X:p.X+a.Offset.X,Y:p.Y+a.Offset.Y};};
        const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        const ids=new Set((plan.Definition.Exclusions||[]).map(e=>e.Id));
        for(const rule of defined){
            if(!rule||!text(rule.Id)||ids.has(rule.Id)||!text(rule.Kind)||!text(rule.Purpose)||!text(rule.SourceRuleId)||
                !Array.isArray(rule.Tags)||!rule.Tags.length||rule.Tags.length>64||!rule.Tags.every(text)||new Set(rule.Tags).size!==rule.Tags.length||
                (rule.Area==null)===(rule.Sightline==null)||rule.Sightline!=null&&rule.RelativeToNodeId!=null)fail();
            ids.add(rule.Id);
            const matches=saved.filter(p=>p?.Definition?.Id===rule.Id);if(matches.length!==1)fail();
            const item=matches[0];if(!same(rule,item.Definition)||!polygon(item.Boundary))fail();
            if(rule.Area!=null){
                if(!polygon(rule.Area)||item.Centerline!=null)fail();
                const p=rule.RelativeToNodeId==null?{X:0,Y:0}:origin(rule.RelativeToNodeId);
                const expected=rule.Area.Vertices.map(v=>({X:v.X+p.X,Y:v.Y+p.Y}));
                if(!same(expected,item.Boundary.Vertices))fail();
            }else{
                const line=rule.Sightline;
                if(!Number.isFinite(line.Width)||line.Width<=0||line.Width>1e6)fail();
                const a=anchor(line.From),b=anchor(line.To);
                if(Math.hypot(b.X-a.X,b.Y-a.Y)<=1e-9||!same(item.Centerline?.Points,[a,b]))fail();
            }
        }
        // Display exact saved envelopes. C# owns collision checks and authoritative geometry replay.
        return saved;
    }
    static contextEvaluations(world,nodes) {

        const plan=world.ContextPlan,feedback=world.PlacementPlan?.Feedback?.Context;

        const fail=()=>{throw new Error('Invalid region context record. Regenerate with the engine.');};

        if(plan==null){if(feedback!=null)fail();return;}

        if(world.Space.Unit!=='meters'||world.Space.Axes.length!==2||!world.Surface||!world.PlacementPlan||

            JSON.stringify(plan.Definition)!==JSON.stringify(feedback)||!Array.isArray(plan.Sources)||!Array.isArray(plan.Evaluations)||

            !Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>plan.Definition.MaximumWork)fail();

        const protections=WorldSchematic.protectedSpaces(world,nodes);
        const exclusions=[...(plan.Definition.Exclusions||[]),...protections.map(p=>({Id:p.Definition.Id,Tags:p.Definition.Tags,SourceRuleId:p.Definition.SourceRuleId,Boundary:p.Boundary}))];
        const regions=new Map(),sources=new Map(),expected=[];

        for(const r of plan.Definition.Regions){

            if(!r||typeof r.Id!=='string'||regions.has(r.Id)||typeof r.Use!=='string'||typeof r.SourceRuleId!=='string'||

                !Array.isArray(r.Boundary?.Vertices)||r.Boundary.Vertices.length<3||r.Boundary.Vertices.length>256||

                r.Boundary.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)))fail();

            regions.set(r.Id,r);

        }

        const placed=[...world.PlacementPlan.Definition.Bindings,...(world.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].map(b=>b.NodeId);

        if(plan.Sources.length!==placed.length||plan.Definition.Sites.length!==placed.length)fail();

        for(const source of plan.Sources){

            const node=nodes.get(source.NodeId),binding=plan.Definition.Sites.filter(b=>b.NodeId===source.NodeId),region=regions.get(source.RegionId);

            if(!node||sources.has(source.NodeId)||!placed.includes(source.NodeId)||binding.length!==1||binding[0].RegionId!==source.RegionId||!region||!Array.isArray(source.Tags))fail();

            const registered=node.StructureKey==null?null:world.StructureDefinitions?.[node.StructureKey];

            const tags=registered?.Definition?.SitingProfile?.Tags??binding[0].Tags;

            if(!Array.isArray(tags)||JSON.stringify([...tags].sort())!==JSON.stringify(source.Tags)||

                (registered?(binding[0].Tags!=null||source.PolicyId!==registered.Catalog.PolicyId||!registered.Definition.SitingProfile.AllowedDistricts.includes(region.Use)):source.PolicyId!=null))fail();

            sources.set(source.NodeId,source);

            const zoneIds=new Set();

            for(const zone of exclusions){

                if(!zone||typeof zone.Id!=='string'||zoneIds.has(zone.Id)||typeof zone.SourceRuleId!=='string'||!Array.isArray(zone.Tags)||zone.Tags.length===0||

                    !Array.isArray(zone.Boundary?.Vertices)||zone.Boundary.Vertices.length<3||zone.Boundary.Vertices.length>256||

                    zone.Boundary.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)))fail();

                zoneIds.add(zone.Id);

                if(source.Tags.some(t=>zone.Tags.includes(t)))expected.push({SiteId:source.NodeId,RuleId:`exclusion:${zone.Id}`,RelatedId:zone.Id,Spatial:true,SourceRuleId:zone.SourceRuleId,Distance:null,Minimum:null});

            }

            expected.push({SiteId:source.NodeId,RuleId:'region-use',RelatedId:region.Id,Spatial:false,SourceRuleId:region.SourceRuleId,Distance:null,Minimum:null},

                {SiteId:source.NodeId,RuleId:'region-containment',RelatedId:region.Id,Spatial:true,SourceRuleId:region.SourceRuleId,Distance:null,Minimum:null});

        }

        const ordered=[...sources.values()].sort((a,b)=>a.NodeId<b.NodeId?-1:a.NodeId>b.NodeId?1:0);

        for(let i=0;i<ordered.length;i++)for(let j=0;j<i;j++){

            const a=ordered[i],b=ordered[j];

            for(const rule of plan.Definition.Separations){

                if(!Number.isFinite(rule.MinimumMeters)||rule.MinimumMeters<0)fail();

                if(a.Tags.includes(rule.FirstTag)&&b.Tags.includes(rule.SecondTag)||a.Tags.includes(rule.SecondTag)&&b.Tags.includes(rule.FirstTag))

                    expected.push({SiteId:a.NodeId,RuleId:`separation:${rule.Id}`,RelatedId:b.NodeId,Spatial:true,SourceRuleId:rule.SourceId,Minimum:rule.MinimumMeters});

            }

        }

        if(plan.Evaluations.length!==expected.length)fail();

        for(const e of expected){

            const records=plan.Evaluations.filter(r=>r.SiteId===e.SiteId&&r.RuleId===e.RuleId&&r.RelatedId===e.RelatedId);

            if(records.length!==1)fail();const actual=records[0];

            if(actual.Passed!==true||actual.Spatial!==e.Spatial||actual.SourceRuleId!==e.SourceRuleId||actual.Minimum!==e.Minimum||

                (e.Minimum===null?actual.Distance!==null:!Number.isFinite(actual.Distance)||actual.Distance+1e-8<e.Minimum))fail();

        }

    }



    static placementFeedback(world) {

        const plan=world.PlacementPlan;if(!plan)return null;

        const attempts=plan.FeasibilityAttempts,feedback=plan.Feedback;

        const required=world.AccessPlan!=null||world.RoutePlan!=null||world.NetworkPlan!=null||world.ServicePlan!=null||world.ContextPlan!=null;

        if(!Array.isArray(attempts)||!Number.isSafeInteger(plan.Work)||plan.Work<0||

            !Number.isSafeInteger(plan.Definition?.MaximumFeasibilityAttempts)||plan.Definition.MaximumFeasibilityAttempts<1||

            plan.Definition.MaximumFeasibilityAttempts>256||attempts.length>plan.Definition.MaximumFeasibilityAttempts||

            required!==(feedback!=null)||(!feedback&&attempts.length)||(feedback&&attempts.length===0))

            throw new Error('Invalid placement feasibility record. Regenerate with the engine.');

        let work=0;

        for(const [index,attempt] of attempts.entries()){

            if(!attempt||attempt.Ordinal!==index+1||typeof attempt.CandidateSha256!=='string'||! /^[a-f0-9]{64}$/.test(attempt.CandidateSha256)||

                attempt.Accepted!==(index===attempts.length-1)||!Number.isSafeInteger(attempt.Work)||attempt.Work<0||

                (attempt.Accepted?attempt.Rejection!==null:typeof attempt.Rejection!=='string'||!attempt.Rejection.startsWith('METADATA_')))

                throw new Error('Invalid placement feasibility attempt.');

            work+=attempt.Work;

        }

        if(!Number.isSafeInteger(work)||work>plan.Work)throw new Error('Invalid placement feasibility work.');

        return feedback?{attempts,work,totalWork:plan.Work}:null;

    }



    static groundUses(world,nodes,access) {
        const result=new Map(),sources=world.FootprintDefinitions;
        if(sources==null)return result;
        if(typeof sources!=='object'||Array.isArray(sources))throw new Error('Invalid ground-use declaration.');
        const definitions=new Map(),used=new Set();
        const text=v=>typeof v==='string'&&v.trim().length>0&&v.length<=2048;
        for(const [id,source] of Object.entries(sources)){
            const use=source?.GroundUse;if(use==null)continue;
            if(typeof use!=='object'||Array.isArray(use)||typeof use.Kind!=='string'||!/^[a-zA-Z0-9._:-]{1,160}$/.test(use.Kind)||
                !text(use.Purpose)||!text(use.SourceRuleId)||!Number.isFinite(use.MinimumOpenFraction)||use.MinimumOpenFraction<0||use.MinimumOpenFraction>1||
                source.Unit!==world.Space.Unit)throw new Error('Invalid ground-use declaration.');
            definitions.set(id,{Kind:use.Kind,Purpose:use.Purpose,SourceRuleId:use.SourceRuleId,MinimumOpenFraction:use.MinimumOpenFraction});
        }
        for(const node of nodes.values()){
            const use=definitions.get(node.ArchetypeId);if(!use)continue;
            if(access.get(node.Id)?.Occupancy!=='Reservation')throw new Error('Ground use requires reserved physical source.');
            result.set(node.Id,{...use});used.add(node.ArchetypeId);
        }
        if(used.size!==definitions.size)throw new Error('Ground use requires reserved physical source.');
        return result;
    }

    static physicalAccess(world,nodes) {

        const plan=world.AccessPlan,result=new Map();if(plan==null)return result;

        if(!plan.Definitions||!Array.isArray(plan.Sites)||plan.Sites.length>1024||!world.PlacementPlan)throw new Error('Invalid physical access plan.');

        const placed=new Map(),stored=world.PlacementPlan;

        for(const stage of [{Placement:stored.Placement,Bindings:stored.Definition.Bindings},...stored.NestedStages.map(s=>({...s,Bindings:stored.Definition.NestedStages.find(d=>d.Id===s.Id)?.Bindings}))]){

            if(!Array.isArray(stage.Bindings))throw new Error('Missing physical stage.');

            for(const b of stage.Bindings)placed.set(b.NodeId,{binding:b,site:stage.Placement.Sites.find(s=>s.DemandId===b.DemandId)});

        }

        if(placed.size!==plan.Sites.length)throw new Error('Incomplete occupancy classification.');

        for(const site of plan.Sites){

            const node=nodes.get(site?.NodeId),source=plan.Definitions[node?.ArchetypeId],location=placed.get(site?.NodeId);

            if(!node||!source||!location||result.has(site.NodeId)||!['Reservation','Solid'].includes(site.Occupancy)||site.Occupancy!==source.Occupancy||

                JSON.stringify(site.Footprint)!==JSON.stringify(location.site?.Footprint?.Footprint)||!Array.isArray(site.Ports)||!Array.isArray(source.Ports)||

                site.Ports.length>16||site.Ports.length!==source.Ports.length)throw new Error('Invalid physical occupancy.');

            const ids=new Set(),a=location.binding.RotationDegrees*Math.PI/180,c=Math.cos(a),s=Math.sin(a),center=location.site.Position;

            for(const port of site.Ports){

                const p=source.Ports.find(p=>p.Id===port?.Id);

                if(!p||ids.has(port.Id)||!Number.isFinite(port.Width)||port.Width<=0||port.Width!==p.Width)throw new Error('Invalid access port.');

                ids.add(port.Id);

                for(const key of ['BoundaryPosition','ApproachPosition']){

                    const point=port[key],local=p[key];

                    if(!point||!local||![point.X,point.Y,local.X,local.Y].every(Number.isFinite)||

                        Math.abs(point.X-(local.X*c-local.Y*s+center.X))>1e-8||Math.abs(point.Y-(local.X*s+local.Y*c+center.Y))>1e-8)throw new Error('Moved access port.');

                }

            }

            result.set(site.NodeId,site);

        }

        return result;

    }



    static placementObservations(world,nodes) {

        const stored=world.PlacementPlan;if(stored==null)return new Map();

        const declared=stored.Definition?.NestedStages||[],nested=stored.NestedStages;

        if(!Array.isArray(nested)||nested.length>128||!Array.isArray(declared)||declared.length!==nested.length||!world.EnvironmentPlan)

            throw new Error('Invalid placement stages.');

        const definitions=new Map(declared.map(s=>[s.Id,s]));

        if(definitions.size!==declared.length)throw new Error('Duplicate placement stages.');

        const stages=[{Id:stored.Definition.Request.Id,ParentNodeId:null,Placement:stored.Placement,Bindings:stored.Definition.Bindings,Footprints:stored.Footprints}];

        for(const stage of nested){

            const definition=definitions.get(stage.Id);

            if(!definition||stage.ParentNodeId!==definition.ParentNodeId)throw new Error('Invalid placement parent.');

            definitions.delete(stage.Id);stages.push({...stage,Bindings:definition.Bindings});

        }

        const byNode=new Map(),features=stored.SiteFeatures??[],featureIds=new Set();

        if(!Array.isArray(features)||features.length>68112)throw new Error('Invalid site surroundings.');

        for(const feature of features)if(!feature||typeof feature.Id!=='string'||featureIds.has(feature.Id))throw new Error('Invalid site feature identity.');else featureIds.add(feature.Id);

        for(const stage of stages){

            const plan=stage.Placement,bindings=stage.Bindings,demands=plan?.Request?.Demands,sources=stage.Footprints;
            const execution=plan?.Request?.Execution??'Search',limit=execution==='Prescribed'?1024:plan?.Request?.Growth?128:16;

            if(!['Search','Prescribed'].includes(execution)||plan?.Status!=='Found'||!Array.isArray(bindings)||bindings.length<1||bindings.length>limit||

                !Array.isArray(demands)||demands.length!==bindings.length||!Array.isArray(plan.Sites)||plan.Sites.length!==bindings.length||

                !Array.isArray(plan.Diagnostics)||plan.Diagnostics.length!==bindings.length||!Array.isArray(sources)||sources.length!==bindings.length||

                (stage.ParentNodeId!==null&&!byNode.has(stage.ParentNodeId)))throw new Error('Invalid placement plan.');

            if(plan.Request.Growth!=null){
                const growth=plan.Request.Growth,fail=()=>{throw new Error('Invalid connected placement grammar.');};
                const ids=new Set(demands.map(d=>d.Id)),anchors=new Map((plan.Request.Anchors||[]).map(a=>[a.Id,a.Position]));
                if(execution!=='Search'||typeof growth.Id!=='string'||!growth.Id.trim()||
                    !ids.has(growth.RootDemandId)||!anchors.has(growth.AnchorId)||
                    !Array.isArray(growth.Links)||growth.Links.length>1024||
                    !Array.isArray(growth.DirectionAnglesDegrees)||!growth.DirectionAnglesDegrees.length||growth.DirectionAnglesDegrees.length>32||
                    new Set(growth.DirectionAnglesDegrees).size!==growth.DirectionAnglesDegrees.length||
                    growth.DirectionAnglesDegrees.some(a=>!Number.isFinite(a)||a<0||a>=360)||
                    !Number.isFinite(growth.Gap)||growth.Gap<0||growth.Gap>1e7||!Number.isFinite(growth.MaximumConnectionDistance)||
                    growth.MaximumConnectionDistance<=growth.Gap||growth.MaximumConnectionDistance>1e7)fail();
                const graph=new Map([...ids].map(id=>[id,[]])),links=new Set(),pairs=new Set(),sites=new Map(plan.Sites.map(s=>[s.DemandId,s.Position]));
                for(const link of growth.Links){
                    if(!link||typeof link.Id!=='string'||!link.Id.trim()||links.has(link.Id)||!ids.has(link.FromId)||!ids.has(link.ToId)||link.FromId===link.ToId)fail();
                    links.add(link.Id);const pair=JSON.stringify([link.FromId,link.ToId].sort());if(pairs.has(pair))fail();pairs.add(pair);
                    graph.get(link.FromId).push(link.ToId);graph.get(link.ToId).push(link.FromId);
                    const a=sites.get(link.FromId),b=sites.get(link.ToId);
                    if(!a||!b||Math.hypot(a.X-b.X,a.Y-b.Y)>growth.MaximumConnectionDistance+1e-8)fail();
                }
                const reached=new Set(),pending=[growth.RootDemandId];
                while(pending.length){const id=pending.pop();if(reached.has(id))continue;reached.add(id);pending.push(...graph.get(id));}
                if(growth.Links.some(l=>!reached.has(l.FromId)||!reached.has(l.ToId)))fail();
                const root=sites.get(growth.RootDemandId),anchor=anchors.get(growth.AnchorId);
                if(!root||!anchor||root.X!==anchor.X||root.Y!==anchor.Y)fail();
                for(const id of ids)if(!reached.has(id)){
                    const pins=(plan.Request.Relations||[]).filter(r=>r.FromId===id&&r.Minimum===0&&r.Maximum===0&&r.Weight>0)
                        .map(r=>anchors.get(r.ToId)).filter(Boolean),site=sites.get(id);
                    if(!pins.length||!site||pins.some(p=>p.X!==site.X||p.Y!==site.Y))fail();
                }
            }

            const rules=[],observations=[],reservationRules=[],reservationObservations=[],seen=new Set();

            for(const binding of bindings){

                const demand=demands.find(d=>d.Id===binding.DemandId),site=plan.Sites.find(s=>s.DemandId===binding.DemandId);

                const diagnostic=plan.Diagnostics.find(d=>d.DemandId===binding.DemandId),source=sources.find(s=>s.NodeId===binding.NodeId),node=nodes.get(binding.NodeId);
                if(execution==='Prescribed'){
                    const pins=(plan.Request.Relations||[]).filter(r=>r.FromId===binding.DemandId&&r.Minimum===0&&r.Maximum===0&&r.Weight>0)
                        .map(r=>(plan.Request.Anchors||[]).find(a=>a.Id===r.ToId)).filter(Boolean);
                    if(plan.SearchStates!==1||diagnostic?.Candidates!==1||diagnostic?.Eligible!==1||!pins.length||!site?.Position||
                        pins.some(p=>p.Position.X!==site.Position.X||p.Position.Y!==site.Position.Y))throw new Error('Invalid prescribed placement witness.');
                }

                if(!demand||!site||!node||byNode.has(node.Id)||seen.has(binding.DemandId)||!site.Position||

                    (stage.ParentNodeId!==null&&node.ParentId!==stage.ParentNodeId)||!source||source.Unit!==world.Space.Unit||

                    JSON.stringify(source.Polygon)!==JSON.stringify(demand.Footprint)||typeof source.SourceId!=='string'||

                    JSON.stringify(source.Reservation??null)!==JSON.stringify(demand.Reservation??null)||

                    (demand.Reservation!=null)!==(site.Reservation!=null)||

                    site.Position.X!==site.Point?.Observation?.Surface?.Position?.X||site.Position.Y!==site.Point?.Observation?.Surface?.Position?.Y||

                    typeof site.CandidateSource!=='string'||!diagnostic||!Number.isInteger(diagnostic.Candidates)||!Number.isInteger(diagnostic.Eligible)||

                    diagnostic.Eligible<1||diagnostic.Eligible>diagnostic.Candidates)throw new Error('Invalid placement binding.');

                seen.add(binding.DemandId);

                const sourceFeatures=source.SiteFeatures??[],siteFeatures=features.filter(f=>f.NodeId===binding.NodeId);

                if(!Array.isArray(sourceFeatures)||sourceFeatures.length>129||siteFeatures.length!==sourceFeatures.length||

                    (binding.SiteLayout!=null)!==(source.SiteFeatures!=null))throw new Error('Invalid site feature coverage.');

                for(const feature of sourceFeatures){

                    const placed=siteFeatures.find(f=>f.Id===feature.Id),vertices=feature.Boundary?.Vertices;

                    if(!placed||!featureIds.delete(placed.Id)||feature.NodeId!==binding.NodeId||!Array.isArray(vertices)||vertices.length!==4||

                        !Array.isArray(placed.Boundary?.Vertices)||placed.Boundary.Vertices.length!==4||

                        ['Kind','SourceRuleId','Purpose','EntrancePortId','SpeciesAssetId','PlantSpacingMeters','PlantMarginMeters'].some(k=>placed[k]!==feature[k])||

                        vertices.some((p,i)=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)||placed.Boundary.Vertices[i].X!==p.X+site.Position.X||placed.Boundary.Vertices[i].Y!==p.Y+site.Position.Y))

                        throw new Error('Invalid site feature geometry or provenance.');

                }

                const id=`placement/${stage.Id}/${demand.Id}`;

                rules.push({Id:id,Profile:demand.Profile,Footprint:demand.Footprint,FootprintWorkBudget:plan.Request.MaximumWork,

                    RequireEligible:true,MinimumNodes:1,MaximumNodes:1});

                observations.push({RuleId:id,NodeId:binding.NodeId,Result:site.Point,Footprint:site.Footprint,

                    Placement:{Stage:stage.Id,ParentNodeId:stage.ParentNodeId,RegionId:demand.RegionId,AllocationId:demand.AllocationId,Purpose:demand.Purpose,SourceRuleId:demand.SourceRuleId,CandidateSource:site.CandidateSource,

                        FootprintSource:source,SiteFeatures:siteFeatures,Candidates:diagnostic.Candidates,EligibleCandidates:diagnostic.Eligible,Rejections:diagnostic.Rejections}});

                if(demand.Reservation){

                    const reservationId=`reservation/${stage.Id}/${demand.Id}`;

                    reservationRules.push({...rules.at(-1),Id:reservationId,Footprint:demand.Reservation});

                    reservationObservations.push({...observations.at(-1),RuleId:reservationId,Footprint:site.Reservation,

                        Placement:{...observations.at(-1).Placement,IsReservation:true}});

                }

            }

            for(const [nodeId,entries] of WorldSchematic.environmentObservations({...world,EnvironmentPlan:{Definition:world.EnvironmentPlan.Definition,Rules:rules,Observations:observations}},nodes,limit))byNode.set(nodeId,entries);

            if(reservationRules.length)for(const [nodeId,entries] of WorldSchematic.environmentObservations({...world,EnvironmentPlan:{Definition:world.EnvironmentPlan.Definition,Rules:reservationRules,Observations:reservationObservations}},nodes,limit))byNode.get(nodeId).push(...entries);

        }

        if(featureIds.size)throw new Error('Unbound site features.');

        const controls=stored.Definition.PoseControls,poses=stored.PoseResults;
        if(controls==null){if(poses!=null)throw new Error('Unexpected authored poses.');}
        else{
            const fail=()=>{throw new Error('Invalid authored placement pose.');};
            if(!Array.isArray(controls)||controls.length<1||controls.length>1024||!Array.isArray(poses)||poses.length!==controls.length)fail();
            const ids=new Set(),members=new Set(),seen=new Set(),lookup=new Map();
            for(const stage of stages)for(const binding of stage.Bindings){
                const site=stage.Placement.Sites.find(s=>s.DemandId===binding.DemandId);
                lookup.set(binding.NodeId,{stage,binding,site});
            }
            for(const control of controls){
                if(!control||typeof control.Id!=='string'||!control.Id.trim()||ids.has(control.Id)||members.has(control.NodeId)||
                    typeof control.Purpose!=='string'||!control.Purpose.trim()||typeof control.SourceRuleId!=='string'||!control.SourceRuleId.trim()||
                    !['World','Stage'].includes(control.Frame)||!control.Position||![control.Position.X,control.Position.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=1e7)||
                    control.RotationDegrees!=null&&(!Number.isFinite(control.RotationDegrees)||Math.abs(control.RotationDegrees)>360))fail();
                ids.add(control.Id);members.add(control.NodeId);
                const entry=lookup.get(control.NodeId),pose=poses.find(p=>p?.Definition?.Id===control.Id);if(!entry||!pose||seen.has(control.Id))fail();seen.add(control.Id);
                const {stage,binding,site}=entry,parent=stage.ParentNodeId==null?null:nodes.get(stage.ParentNodeId),offset=control.Frame==='Stage'&&parent?parent.Position:[0,0];
                if(!offset||offset.length!==2||JSON.stringify(pose.Definition)!==JSON.stringify(control)||pose.StageId!==stage.Id||pose.DemandId!==binding.DemandId||
                    pose.RotationDegrees!==binding.RotationDegrees||control.RotationDegrees!=null&&pose.RotationDegrees!==control.RotationDegrees||
                    !pose.WorldPosition||pose.WorldPosition.X!==control.Position.X+offset[0]||pose.WorldPosition.Y!==control.Position.Y+offset[1]||
                    !site||site.Position.X!==pose.WorldPosition.X||site.Position.Y!==pose.WorldPosition.Y)fail();
                for(const observation of byNode.get(control.NodeId)||[])if(observation.Placement&&!observation.Placement.IsReservation)observation.Placement.Pose=pose;
            }
        }

        return byNode;

    }



    // Numeric inspection of saved inputs/results only. No climate derivation,
    // suitability solve or geometry generation runs in the presentation layer.
    static fieldStack(world) {
        const definition=world.EnvironmentPlan?.Definition;if(!definition)return [];
        const fail=()=>{throw new Error('Invalid saved environmental field data.');};
        if(!Array.isArray(definition.Fields)||definition.Fields.length>32||
            !Array.isArray(definition.SampledFields??[])||(definition.SampledFields?.length??0)>8)fail();
        const entries=[],seen=new Set();let total=0;
        for(const source of definition.SampledFields??[]){
            if(!source||typeof source.Id!=='string'||seen.has(source.Id)||!Array.isArray(source.Values)||
                !Number.isInteger(source.Columns)||source.Columns<2||source.Columns>513||
                !Number.isInteger(source.Rows)||source.Rows<2||source.Rows>513||
                source.Values.length!==source.Columns*source.Rows||!Number.isFinite(source.Origin?.X)||!Number.isFinite(source.Origin?.Y)||
                !Number.isFinite(source.StepX)||source.StepX<=0||!Number.isFinite(source.StepY)||source.StepY<=0||
                (total+=source.Values.length)>2105352)fail();
            seen.add(source.Id);let minimum=Infinity,maximum=-Infinity;
            for(const value of source.Values){if(!Number.isFinite(value))fail();minimum=Math.min(minimum,value);maximum=Math.max(maximum,value);}
            const {Values,...metadata}=source;
            const maxX=source.Origin.X+(source.Columns-1)*source.StepX,maxY=source.Origin.Y+(source.Rows-1)*source.StepY;
            if(!Number.isFinite(maxX)||!Number.isFinite(maxY))fail();
            entries.push({Key:'sampled:'+source.Id,Kind:'Sampled input',Definition:structuredClone(metadata),
                Bounds:{MinimumX:source.Origin.X,MinimumY:source.Origin.Y,
                    MaximumX:maxX,MaximumY:maxY},
                SourceValues:{Count:Values.length,Minimum:minimum,Maximum:maximum}});
        }
        seen.clear();
        for(const field of definition.Fields){
            if(!field||typeof field.Id!=='string'||seen.has(field.Id)||!Array.isArray(field.Terms))fail();seen.add(field.Id);
            let count=0,minimum=Infinity,maximum=-Infinity;const observations=[];
            for(const entry of world.EnvironmentPlan.Observations??[]){
                const values=entry.Result?.Observation?.Values;
                if(!values||!Object.hasOwn(values,field.Id))continue;
                const value=values[field.Id];if(!Number.isFinite(value))fail();count++;minimum=Math.min(minimum,value);maximum=Math.max(maximum,value);
                if(observations.length<64)observations.push({NodeId:entry.NodeId,RuleId:entry.RuleId,
                    Position:structuredClone(entry.Result.Observation.Surface.Position),Value:value});
            }
            entries.push({Key:'derived:'+field.Id,Kind:'Derived scalar',Definition:structuredClone(field),
                SavedPointValues:{Count:count,Minimum:count?minimum:null,Maximum:count?maximum:null,Shown:observations.length},
                Observations:observations});
        }
        return entries;
    }

    static fieldSample(world,key,index) {
        const source=world.EnvironmentPlan?.Definition?.SampledFields?.find(f=>'sampled:'+f.Id===key);
        if(!source||!Number.isInteger(index)||index<0||index>=source.Values.length)throw new Error('Select an available sampled field and valid sample index.');
        const column=index%source.Columns,row=Math.floor(index/source.Columns),value=source.Values[index];
        if(!Number.isFinite(value))throw new Error('Invalid saved environmental field data.');
        return {Index:index,Column:column,Row:row,Position:{X:source.Origin.X+column*source.StepX,Y:source.Origin.Y+row*source.StepY},
            CoordinateUnit:world.Surface?.Unit??world.Space.Unit,Value:value,ValueUnit:source.Unit};
    }

    static environmentObservations(world,nodes,maximumRules=16) {

        const byNode=new Map(),plan=world.EnvironmentPlan;if(plan==null)return byNode;

        const fields=plan.Definition?.Fields;

        if(plan.Definition?.SchemaVersion!==1||!Array.isArray(fields)||fields.length<1||fields.length>32||

            !Array.isArray(plan.Rules)||plan.Rules.length>maximumRules||!Array.isArray(plan.Observations)||plan.Observations.length>4096)

            throw new Error('Invalid environmental plan.');

        const fieldIds=new Set(fields.map(f=>f.Id)),rules=new Map(plan.Rules.map(r=>[r.Id,r])),seen=new Set(),counts=new Map();

        if(fieldIds.size!==fields.length||rules.size!==plan.Rules.length)throw new Error('Duplicate environment definitions.');

        for(const entry of plan.Observations){

            const rule=rules.get(entry.RuleId),node=nodes.get(entry.NodeId),result=entry.Result,observation=result?.Observation,position=observation?.Surface?.Position;

            const key=`${entry.RuleId}\n${entry.NodeId}`,values=observation?.Values;

            if(!rule||!node||seen.has(key)||result?.ProfileId!==rule.Profile?.Id||typeof result.Eligible!=='boolean'||

                !Array.isArray(node.Position)||!position||position.X!==node.Position[0]||position.Y!==node.Position[1]||

                !values||typeof values!=='object'||Array.isArray(values)||Object.keys(values).some(k=>!fieldIds.has(k)||!Number.isFinite(values[k]))||

                (result.Eligible?(!Number.isFinite(result.Score)||result.Score<0||result.Score>1):result.Score!==null)||

                !Array.isArray(result.Rejections)||result.Rejections.some(r=>typeof r!=='string')||

                (result.Eligible&&result.Rejections.length!==0)||(!result.Eligible&&result.Rejections.length===0)||

                !Array.isArray(result.Decisions)||result.Decisions.length>64||(rule.RequireEligible&&!result.Eligible))

                throw new Error('Invalid point suitability observation.');

            if(observation.Surface.Status==='Sampled'&&Object.keys(values).length!==fields.length)throw new Error('Incomplete environmental values.');

            const area=entry.Footprint;

            if(rule.Footprint!=null){

                const vertices=area?.Footprint?.Vertices,local=rule.Footprint.Vertices;

                if(!area||area.ProfileId!==result.ProfileId||typeof area.Eligible!=='boolean'||typeof area.CompleteCoverage!=='boolean'||

                    !Array.isArray(vertices)||vertices.length<3||vertices.length>64||!Array.isArray(local)||local.length!==vertices.length||

                    vertices.some((p,i)=>!p||!local[i]||!Number.isFinite(p.X)||!Number.isFinite(p.Y)||Math.abs(p.X)>10000000||Math.abs(p.Y)>10000000||p.X!==node.Position[0]+local[i].X||p.Y!==node.Position[1]+local[i].Y)||

                    !area.Ranges||typeof area.Ranges!=='object'||Array.isArray(area.Ranges)||Object.entries(area.Ranges).some(([id,r])=>!fieldIds.has(id)||!r||!Number.isFinite(r.Minimum)||!Number.isFinite(r.Maximum)||r.Minimum>r.Maximum)||

                    (area.CompleteCoverage&&Object.keys(area.Ranges).length!==fields.length)||

                    !Array.isArray(area.Rejections)||area.Rejections.some(r=>typeof r!=='string')||area.Eligible!==(area.Rejections.length===0)||

                    (area.Eligible&&!area.CompleteCoverage)||!Array.isArray(area.Decisions)||area.Decisions.length!==rule.Profile.Requirements.length||

                    !Number.isSafeInteger(area.Work)||area.Work<1||area.Work>rule.FootprintWorkBudget||

                    !Number.isSafeInteger(area.Regions)||area.Regions<1||!Number.isSafeInteger(area.Pieces)||area.Pieces<0||

                    (rule.RequireEligible&&!area.Eligible))throw new Error('Invalid whole-footprint observation.');

            }else if(area!=null)throw new Error('Undeclared whole-footprint observation.');

            seen.add(key);counts.set(rule.Id,(counts.get(rule.Id)||0)+1);

            if(!byNode.has(node.Id))byNode.set(node.Id,[]);byNode.get(node.Id).push(entry);

        }

        for(const rule of plan.Rules)if((counts.get(rule.Id)||0)<rule.MinimumNodes||(counts.get(rule.Id)||0)>rule.MaximumNodes)

            throw new Error('Point suitability count mismatch.');

        return byNode;

    }



    static waterWitness(world) {
        if(world.WaterPlan==null)return null;
        const p=world.WaterPlan,d=world.DrainagePlan,w=p?.Definition;
        const fail=()=>{throw new Error('Invalid generated-water witness. Regenerate with the engine.');};
        const integer=(v,a,b)=>Number.isSafeInteger(v)&&v>=a&&v<=b;
        const finite=Number.isFinite,point=v=>v&&finite(v.X)&&finite(v.Y);
        const same=(a,b)=>{
            if(a===b)return true;if(a==null||b==null||typeof a!=='object'||typeof b!=='object')return false;
            if(Array.isArray(a)||Array.isArray(b))return Array.isArray(a)&&Array.isArray(b)&&a.length===b.length&&a.every((v,i)=>same(v,b[i]));
            const keys=Object.keys(a).sort(),other=Object.keys(b).sort();return keys.length===other.length&&keys.every((k,i)=>k===other[i]&&same(a[k],b[k]));
        };
        const area=shape=>{
            const v=shape?.Vertices;if(!Array.isArray(v)||v.length<3||v.length>64||v.some(x=>!point(x)))fail();
            const o=v[0];let sum=0;for(let i=1;i+1<v.length;i++)sum+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(sum/2);
        };
        const near=(a,b)=>finite(a)&&finite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        if(p.SchemaVersion!==1||!w||w.SchemaVersion!==1||d?.SchemaVersion!==1||!d.Surface||typeof d.Definition?.RateUnit!=='string'||!Array.isArray(d.Nodes)||d.Nodes.length>263169||!world.Surface||
            typeof w.Id!=='string'||!w.Id||w.Id.length>96||typeof w.Kind!=='string'||!w.Kind||typeof w.SourceRuleId!=='string'||typeof w.Purpose!=='string'||
            !finite(w.MinimumFlow)||w.MinimumFlow<=0||w.MinimumFlow>1e15||!finite(w.BaseWidth)||w.BaseWidth<.001||!finite(w.WidthPerRate)||w.WidthPerRate<0||w.WidthPerRate>10000||
            !finite(w.MaximumWidth)||w.MaximumWidth<w.BaseWidth||w.MaximumWidth>10000||!integer(w.Priority,-2147483648,2147482623)||
            !integer(w.MaximumSegments,1,4096)||!integer(w.MaximumPatches,1,1024)||!integer(w.MaximumWork,1,200000000)||
            !integer(p.Work,1,w.MaximumWork)||!Array.isArray(p.Patches)||p.Patches.length>w.MaximumPatches||
            !Array.isArray(p.Segments)||p.Segments.length>w.MaximumSegments||!Array.isArray(d.Surface.Patches)||d.Surface.Patches.length+p.Patches.length>1024||
            !/^[a-f0-9]{64}$/.test(p.DrainageSha256||''))fail();
        const nodes=new Map(),patches=new Map(),priorities=new Set(),byPatch=new Map(),lakeOwners=new Map();
        for(const n of d.Nodes){if(!integer(n?.SampleIndex,0,263168)||nodes.has(n.SampleIndex)||!finite(n.Outgoing)||n.Outgoing<0)fail();nodes.set(n.SampleIndex,n);}
        const definitions=w.RetainedBasins??[],bodies=p.RetainedBodies??[];
        if(!Array.isArray(definitions)||definitions.length>16||!Array.isArray(bodies)||bodies.length!==definitions.length)fail();
        const declared=new Map();for(const b of definitions){if(!b||typeof b.Id!=='string'||declared.has(b.Id))fail();declared.set(b.Id,b);}
        const seenBodies=new Set();
        for(const b of bodies){
            const def=b?.Definition,region=b?.Region,terminal=d.Definition?.Terminals?.find(t=>t.Id===def?.TerminalId),received=d.Terminals?.find(t=>t.TerminalId===def?.TerminalId);
            if(!def||seenBodies.has(def.Id)||!same(def,declared.get(def.Id))||!finite(def.Level)||Math.abs(def.Level)>1e7||
                !finite(def.MinimumArea)||def.MinimumArea<=0||!finite(def.MinimumReceived)||def.MinimumReceived<0||
                !['RequireTerrainShoreline','ClipToBoundary'].includes(def.ShorelinePolicy)||!finite(def.ShorelineTolerance)||def.ShorelineTolerance<0||def.ShorelineTolerance>.001||
                !Array.isArray(def.InundatableKinds)||!def.InundatableKinds.length||terminal?.Kind!=='RetainedBasin'||!received||b.Received!==received.Received||b.Received<def.MinimumReceived||
                !region||region.Id!==def.Id||region.Seed?.Id!==def.Id||!finite(region.Area)||region.Area<def.MinimumArea||
                !Array.isArray(region.Cells)||!region.Cells.length||!Array.isArray(b.PatchIds)||!Array.isArray(b.BedFaces)||b.PatchIds.length!==region.Cells.length||b.BedFaces.length!==region.Cells.length||
                !finite(b.Capacity)||b.Capacity<=0||!integer(b.UnresolvedShorelineSpans,0,1e7)||!finite(b.MaximumShorelineDeficit)||b.MaximumShorelineDeficit<0||
                b.BoundaryClipped!==(b.UnresolvedShorelineSpans>0||b.MaximumShorelineDeficit>def.ShorelineTolerance)||b.BoundaryClipped&&def.ShorelinePolicy==='RequireTerrainShoreline')fail();
            const sink=nodes.get(terminal.Row*d.Surface.HeightField?.Columns+terminal.Column);
            if(!sink||!same(region.Seed.Position,sink.Position))fail();
            seenBodies.add(def.Id);let capacity=0,totalArea=0;
            for(let i=0;i<region.Cells.length;i++){
                const id=b.PatchIds[i],bed=b.BedFaces[i],a=area(region.Cells[i]);
                if(typeof id!=='string'||lakeOwners.has(id)||!bed||bed.CellIndex!==i||!point(bed.Origin)||!finite(bed.Elevation)||!finite(bed.GradientX)||!finite(bed.GradientY)||
                    !finite(bed.MinimumDepth)||!finite(bed.MaximumDepth)||bed.MinimumDepth< -def.ShorelineTolerance||bed.MaximumDepth<bed.MinimumDepth||
                    !finite(bed.Capacity)||bed.Capacity<=0||!near(bed.Capacity,a*(def.Level-bed.Elevation)))fail();
                capacity+=bed.Capacity;totalArea+=a;lakeOwners.set(id,{body:b,index:i});
            }
            if(!near(capacity,b.Capacity)||!near(totalArea,region.Area))fail();
        }
        for(const patch of p.Patches){
            const owner=lakeOwners.get(patch?.Id),source=owner?.body.Definition??w;
            if(!patch||typeof patch.Id!=='string'||patches.has(patch.Id)||patch.Kind!==w.Kind||patch.Purpose!==source.Purpose||patch.SourceRuleId!==source.SourceRuleId||
                !integer(patch.Priority,w.Priority,w.Priority+p.Patches.length-1)||priorities.has(patch.Priority)||!point(patch.Origin)||
                !finite(patch.Elevation)||!finite(patch.GradientX)||!finite(patch.GradientY)||area(patch.Boundary)<=0)fail();
            if(owner&&(!same(patch.Boundary,owner.body.Region.Cells[owner.index])||!same(patch.Origin,owner.body.BedFaces[owner.index].Origin)||
                patch.Elevation!==source.Level||patch.GradientX!==0||patch.GradientY!==0))fail();
            patches.set(patch.Id,patch);priorities.add(patch.Priority);
        }
        const expected=d.Nodes.filter(n=>n.DownstreamSampleIndex!=null&&n.Outgoing>=w.MinimumFlow),seen=new Set();
        if(expected.length!==p.Segments.length||p.BelowThresholdEdges!==d.Nodes.filter(n=>n.DownstreamSampleIndex!=null&&n.Outgoing<w.MinimumFlow).length)fail();
        for(const s of p.Segments){
            const n=nodes.get(s?.FromSampleIndex),to=nodes.get(s?.ToSampleIndex);
            if(!n||!to||seen.has(n.SampleIndex)||n.DownstreamSampleIndex!==to.SampleIndex||n.Outgoing<w.MinimumFlow||s.Flow!==n.Outgoing||
                !near(s.Width,w.BaseWidth+w.WidthPerRate*n.Outgoing)||s.Width>w.MaximumWidth||!Array.isArray(s.PatchIds)||s.PatchIds.length<1||s.PatchIds.length>1024||
                !finite(s.RetainedArea)||s.RetainedArea<=0||!finite(s.ClippedArea)||s.ClippedArea< -1e-8*Math.max(1,s.RetainedArea))fail();
            seen.add(n.SampleIndex);let retained=0;
            for(const id of s.PatchIds){const patch=patches.get(id);if(!patch||byPatch.has(id))fail();byPatch.set(id,s);retained+=area(patch.Boundary);}
            if(!near(retained,s.RetainedArea)||!near(s.RetainedArea+s.ClippedArea,area(s.Envelope)))fail();
        }
        for(const [id,owner] of lakeOwners){if(!patches.has(id)||byPatch.has(id))fail();byPatch.set(id,owner.body);}
        if(byPatch.size!==patches.size)fail();
        const baseIds=new Set(d.Surface.Patches.map(x=>x.Id));if(p.Patches.some(x=>baseIds.has(x.Id))||d.Surface.Patches.some(b=>b.Priority>=w.Priority))fail();
        const combined=[...d.Surface.Patches,...p.Patches].sort((a,b)=>b.Priority-a.Priority||(a.Id<b.Id?-1:a.Id>b.Id?1:0));
        if(!same({...d.Surface,Patches:combined},world.Surface))fail();
        return {definition:w,segments:p.Segments,bodies,byPatch,rateUnit:d.Definition?.RateUnit};
    }

    static heightGeometry(f,unit) {

        if(!f||f.SchemaVersion!==1||f.Unit!==unit||typeof f.Id!=='string'||typeof f.Kind!=='string'||

            !Number.isInteger(f.Columns)||f.Columns<2||f.Columns>513||!Number.isInteger(f.Rows)||f.Rows<2||f.Rows>513||

            ![f.StepX,f.StepY].every(n=>Number.isFinite(n)&&n>=.001)||!f.Origin||

            ![f.Origin.X,f.Origin.Y,f.Origin.X+(f.Columns-1)*f.StepX,f.Origin.Y+(f.Rows-1)*f.StepY].every(n=>Number.isFinite(n)&&Math.abs(n)<=10000000))

            throw new Error('Invalid height field.');

        return f;
    }
    static fieldView(source,geometry,values,firstColumn,firstRow,columns,rows) {
        if(!Array.isArray(values)||values.length!==columns*rows||values.some(h=>!Number.isFinite(h)||Math.abs(h)>1e7))throw new Error('Invalid height samples.');
        let minimum=Infinity,maximum=-Infinity;

        for(const h of values){minimum=Math.min(minimum,h);maximum=Math.max(maximum,h);}

        const origin={x:geometry.Origin.X+firstColumn*geometry.StepX,y:geometry.Origin.Y+firstRow*geometry.StepY};
        return {source,geometry,values,firstColumn,firstRow,columns,rows,origin,minimum,maximum,
            width:geometry.Origin.X+(firstColumn+columns-1)*geometry.StepX-origin.x,
            height:geometry.Origin.Y+(firstRow+rows-1)*geometry.StepY-origin.y};

    }

    static heightField(f,unit) {
        WorldSchematic.heightGeometry(f,unit);
        return WorldSchematic.fieldView(f,f,f.Heights,0,0,f.Columns,f.Rows);
    }
    static surfaceHeightField(surface) {
        const d=surface.TerrainCapture,p=surface.TerrainCapturePlan;
        const fail=()=>{throw new Error('Invalid terrain capture. Regenerate with the engine.');};
        if(d==null){if(p!=null)fail();return surface.HeightField==null?null:WorldSchematic.heightField(surface.HeightField,surface.Unit);}
        const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b),text=v=>typeof v==='string'&&v.trim().length>0&&v.length<=2048;
        const integer=(v,a,b)=>Number.isSafeInteger(v)&&v>=a&&v<=b;
        const guid=v=>typeof v==='string'&&/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v);
        if(!surface.Terrain||surface.HeightField!=null||!surface.Boundary||d.Version!==1||!text(d.Id)||!text(d.Purpose)||!text(d.SourceRuleId)||
            !p||p.Version!==1||!same(p.Definition,d)||!guid(p.ImplementationId)||!/^[0-9a-f]{64}$/.test(p.SourceRecipeSha256))fail();
        const f=WorldSchematic.heightGeometry(surface.Terrain,surface.Unit),v=d.Domain?.Vertices;
        if(!Array.isArray(v)||!integer(v.length,3,1024)||v.some(p=>![p?.X,p?.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=1e7)))fail();
        const minX=Math.min(...v.map(p=>p.X)),maxX=Math.max(...v.map(p=>p.X)),minY=Math.min(...v.map(p=>p.Y)),maxY=Math.max(...v.map(p=>p.Y));
        if(surface.Boundary.Vertices.some(p=>p.X<minX-1e-8||p.X>maxX+1e-8||p.Y<minY-1e-8||p.Y>maxY+1e-8))fail();
        const outside=maxX<f.Origin.X-1e-8||maxY<f.Origin.Y-1e-8||minX>f.Origin.X+(f.Columns-1)*f.StepX+1e-8||minY>f.Origin.Y+(f.Rows-1)*f.StepY+1e-8;
        if(outside){if(p.Window!=null)fail();return null;}
        const cell=(n,o,s,count)=>Math.max(0,Math.min(count-2,Math.floor((n-o)/s)));
        const firstColumn=Math.max(0,cell(minX,f.Origin.X,f.StepX,f.Columns)-1),firstRow=Math.max(0,cell(minY,f.Origin.Y,f.StepY,f.Rows)-1);
        const columns=Math.min(f.Columns-2,cell(maxX,f.Origin.X,f.StepX,f.Columns)+1)-firstColumn+2;
        const rows=Math.min(f.Rows-2,cell(maxY,f.Origin.Y,f.StepY,f.Rows)+1)-firstRow+2,w=p.Window;
        if(!w||w.Version!==1||w.AlgorithmModel!=='original-lattice-terrain-window-v1'||w.ImplementationId!==p.ImplementationId||
            w.SourceRecipeSha256!==p.SourceRecipeSha256||!same(w.Source,f)||w.FirstColumn!==firstColumn||w.FirstRow!==firstRow||w.Columns!==columns||w.Rows!==rows||
            !same(w.Request,{Version:1,FirstColumn:firstColumn,FirstRow:firstRow,Columns:columns,Rows:rows,HaloCells:0}))fail();
        const cost=1+(f.Operations||[]).reduce((sum,o)=>{const segments=Math.max(1,o.ControlPath.length-1),transform=(o.OrientationDegrees??0)%360!==0||(o.AxisStretch??1)!==1?5:0;return sum+segments+transform+(o.InnerRadius>0?1:0)+(o.DetailProtectionRadius!=null?segments+transform:0);},0)+
            (f.Detail?.Octaves||0)*4+((f.Detail?.OrientationDegrees??0)!==0||(f.Detail?.AxisStretch??1)!==1?5:0);
        if(!integer(w.Work,1,f.MaximumWork)||w.Work!==cost*columns*rows)fail();
        return WorldSchematic.fieldView(w,f,w.Heights,firstColumn,firstRow,columns,rows);
    }



    // Display triangulation of the engine's samples; never runs landform/noise generation.

    static *heightTriangles(field) {

        const f=field.geometry,point=(x,y)=>({x:f.Origin.X+(field.firstColumn+x)*f.StepX,y:f.Origin.Y+(field.firstRow+y)*f.StepY,height:field.values[y*field.columns+x]});

        for(let y=0;y<field.rows-1;y++)for(let x=0;x<field.columns-1;x++){

            const a=point(x,y),b=point(x+1,y),c=point(x,y+1),d=point(x+1,y+1);

            yield {points:[a,b,d],elevation:(a.height+b.height+d.height)/3,slope:Math.hypot((b.height-a.height)/f.StepX,(d.height-b.height)/f.StepY)};

            yield {points:[a,d,c],elevation:(a.height+d.height+c.height)/3,slope:Math.hypot((d.height-c.height)/f.StepX,(c.height-a.height)/f.StepY)};

        }

    }



    static semanticLayers(world) {
        const fail=()=>{throw new Error('Semantic layer membership is missing or changed. Regenerate with the engine.');};
        const plan=world?.LayerPlan;
        if(!plan||!Array.isArray(plan.Layers)||plan.Layers.length>256||!Array.isArray(world.Nodes)||world.Nodes.length>5000||!Array.isArray(world.Connections)||world.Connections.length>20000)fail();
        const entries=new Map(),text=v=>typeof v==='string'&&v.trim().length>0&&v.length<=2048;
        const id=v=>typeof v==='string'&&/^[a-zA-Z0-9._:-]{1,160}$/.test(v);
        for(const entry of plan.Layers){
            const d=entry?.Definition;
            if(!d||!id(d.Id)||!id(d.Kind)||entries.has(d.Id)||!text(d.Purpose)||!text(d.SourceRuleId)||
                !Array.isArray(d.DependsOn)||d.DependsOn.length>256||new Set(d.DependsOn).size!==d.DependsOn.length||d.DependsOn.some(p=>!entries.has(p))||
                !Array.isArray(d.RemainingScopes)||d.RemainingScopes.length>512||d.RemainingScopes.some(s=>!text(s))||
                !['CompleteDeclaredScope','PartialScope'].includes(d.Coverage)||(d.Coverage==='PartialScope')!==(d.RemainingScopes.length>0))fail();
            entries.set(d.Id,entry);
        }
        const members=(items,key,unassigned)=>{
            if(!Array.isArray(unassigned))fail();
            const actual=new Map([...entries.keys(),null].map(k=>[k,new Set()])),index=new Map();
            for(const item of items){
                if(!item)fail();
                const layer=item.LayerId??null;
                if(!text(item.Id)||index.has(item.Id)||!actual.has(layer))fail();
                index.set(item.Id,layer);actual.get(layer).add(item.Id);
            }
            for(const [layer,set] of actual){
                const saved=layer===null?unassigned:entries.get(layer)[key];
                if(!Array.isArray(saved)||saved.length!==set.size||new Set(saved).size!==saved.length||saved.some(i=>!set.has(i)))fail();
            }
            return index;
        };
        return {entries,nodes:members(world.Nodes,'NodeIds',plan.UnassignedNodeIds),connections:members(world.Connections,'ConnectionIds',plan.UnassignedConnectionIds)};
    }

    static palette(settings,layers) {
        const fail=()=>{throw new Error('Palette requires version 1 and colors for declared layer IDs only.');};
        if(!settings||Array.isArray(settings)||Object.keys(settings).sort().join(',')!=='LayerColors,Version'||settings.Version!==1||
            !settings.LayerColors||typeof settings.LayerColors!=='object'||Array.isArray(settings.LayerColors)||Object.keys(settings.LayerColors).length>256)fail();
        const colors={};
        for(const [id,color] of Object.entries(settings.LayerColors)){
            if(!layers.has(id)||typeof color!=='string'||!/^#[0-9a-f]{6}$/i.test(color))fail();
            Object.defineProperty(colors,id,{value:color.toLowerCase(),enumerable:true,writable:true,configurable:true});
        }
        return {Version:1,LayerColors:colors};
    }

    static domainNavigation(navigation,world,pageUrl) {
        if(navigation===null)return null;
        const fail=()=>{throw new Error('Domain navigation is missing, changed or unsafe. Regenerate the collection.');};
        const text=v=>typeof v==='string'&&v.trim().length>0&&v.length<=2048,hash=v=>typeof v==='string'&&/^[0-9a-f]{64}$/.test(v);
        const keys=(v,names)=>v&&typeof v==='object'&&!Array.isArray(v)&&Object.keys(v).sort().join(',')===names.split(',').sort().join(',');
        if(!keys(navigation,'Version,CollectionId,DefinitionSha256,CurrentDomainId,Entries,Transitions')||navigation.Version!==1||
            !text(navigation.CollectionId)||!hash(navigation.DefinitionSha256)||!text(navigation.CurrentDomainId)||
            !Array.isArray(navigation.Entries)||navigation.Entries.length<2||navigation.Entries.length>16||
            !Array.isArray(navigation.Transitions)||navigation.Transitions.length<1||navigation.Transitions.length>128)fail();
        const entries=new Map();
        for(const [i,entry] of navigation.Entries.entries()){
            if(!keys(entry,'Id,Purpose,Page,WorldId,RecipeSha256,CatalogSha256')||!text(entry.Id)||entries.has(entry.Id)||!text(entry.Purpose)||
                !text(entry.WorldId)||!hash(entry.RecipeSha256)||!hash(entry.CatalogSha256)||entry.Page!==`../domain-${String(i).padStart(4,'0')}/schematic.html`)fail();
            entries.set(entry.Id,entry);
        }
        const current=entries.get(navigation.CurrentDomainId);
        if(!current||current.WorldId!==world?.Id||current.RecipeSha256!==world.RecipeSha256||current.CatalogSha256!==world.CatalogSha256)fail();
        try{
            const page=new URL(pageUrl);
            if(!['http:','https:','file:'].includes(page.protocol)||new URL(current.Page,page).pathname!==page.pathname)fail();
        }catch{fail();}
        const seen=new Set(),nodes=new Set(world.Nodes.map(n=>n.Id));
        for(const transition of navigation.Transitions){
            if(!keys(transition,'Id,Kind,Purpose,SourceRuleId,FromDomainId,FromNodeId,ToDomainId,ToNodeId,Bidirectional')||
                !text(transition.Id)||seen.has(transition.Id)||!text(transition.Kind)||!text(transition.Purpose)||!text(transition.SourceRuleId)||
                !entries.has(transition.FromDomainId)||!entries.has(transition.ToDomainId)||!text(transition.FromNodeId)||!text(transition.ToNodeId)||
                typeof transition.Bidirectional!=='boolean'||
                transition.FromDomainId===current.Id&&!nodes.has(transition.FromNodeId)||transition.ToDomainId===current.Id&&!nodes.has(transition.ToNodeId))fail();
            seen.add(transition.Id);
        }
        return structuredClone(navigation);
    }

    constructor(svg, world, details, status, navigation=null) {

        this.svg = svg; this.details = details; this.status = status; this.selected = null; this.events = [];

        this.load(world,navigation);
        this.resizeObserver=new ResizeObserver(()=>this.applyView());this.resizeObserver.observe(svg);

        svg.addEventListener('wheel', event => { event.preventDefault(); this.zoom(event.deltaY < 0 ? 1.15 : 1 / 1.15); }, { passive: false });

        let drag = null;

        svg.addEventListener('pointerdown', event => { if (event.button === 0) { this.dragged = false; drag = [event.clientX, event.clientY]; } });

        svg.addEventListener('pointermove', event => {

            if (!drag) return;

            if (!this.dragged && Math.hypot(event.clientX - drag[0], event.clientY - drag[1]) < 3) return;

            this.dragged = true; svg.setPointerCapture(event.pointerId);

            const bounds = svg.getBoundingClientRect();

            this.view[0] -= (event.clientX - drag[0]) * this.view[2] / bounds.width;

            this.view[1] -= (event.clientY - drag[1]) * this.view[3] / bounds.height;

            drag = [event.clientX, event.clientY]; this.applyView();

        });

        svg.addEventListener('pointerup', () => { drag = null; });

        svg.addEventListener('pointercancel', () => { drag = null; });

        svg.addEventListener('click',event=>{
            if(this.dragged||event.detail===0||!event.target.closest?.('g[data-node-id]'))return;
            const ids=[],seen=new Set();
            for(const element of svg.ownerDocument.elementsFromPoint(event.clientX,event.clientY)){
                const group=element.closest?.('g[data-node-id]');
                if(!group||!svg.contains(group))continue;
                const id=group.getAttribute('data-node-id');
                if(seen.has(id)||!this.selectors.has(id))continue;
                seen.add(id);ids.push(id);
            }
            this.showSourceChoices(ids);
        });

        svg.addEventListener('pointermove', event => {
            const matrix=svg.getScreenCTM();if(!matrix)return;
            const point=new DOMPoint(event.clientX,event.clientY).matrixTransform(matrix.inverse());
            const unit=this.plan.coordinateSpace?.startsWith('engine-')?this.plan.unit||'engine units':'diagram units';
            this.control('#map-coordinates').textContent=`x ${point.x.toFixed(2)}, y ${point.y.toFixed(2)} · ${unit}`;
        });

    }



    element(tag, attrs = {}, text = null, parent = this.svg) {

        const el = document.createElementNS('http://www.w3.org/2000/svg', tag);

        for (const [key, value] of Object.entries(attrs)) el.setAttribute(key,
            key==='id'?this.svgId(String(value)):String(value).replace(/url\(#([^)]+)\)/g,(_,id)=>`url(#${this.svgId(id)})`));

        if (text !== null) el.textContent = text;

        parent.append(el); return el;

    }

    showSourceChoices(ids) {
        if(ids.length<2)return;
        const panel=document.createElement('section');panel.dataset.overlappingSources='true';
        const label=document.createElement('label'),list=document.createElement('select'),open=document.createElement('button');
        const visible=ids.slice(0,128);
        label.textContent=ids.length>visible.length?`Sources at this point (${visible.length} of ${ids.length})`:`Sources at this point (${ids.length})`;
        label.style.display='block';list.setAttribute('aria-label','Sources at this point');
        list.style.width='100%';list.style.display='block';list.style.marginTop='.35rem';
        for(const id of visible){
            const box=this.boxesById.get(id);if(!box)continue;
            const option=document.createElement('option');option.value=id;
            option.textContent=`${box.ownerRegionId?box.ownerRegionId+' / ':''}${box.node.Kind} — ${box.node.Id}`;
            list.append(option);
        }
        if(visible.includes(this.selected?.id))list.value=this.selected.id;
        label.append(list);open.type='button';open.textContent='Inspect chosen source';open.style.marginTop='.4rem';
        open.addEventListener('click',()=>{
            this.dragged=false;this.selectors.get(list.value)?.();this.showSourceChoices(ids);
        });
        panel.append(label,open);const heading=this.details.querySelector('h2');
        if(heading)heading.after(panel);else this.details.prepend(panel);
    }



    load(world,navigation=null) {

        const domainNavigation=WorldSchematic.domainNavigation(navigation,world,this.svg.ownerDocument.URL);
        const plan = WorldSchematic.layout(world); // Validate before replacing a working diagram.
        const semantic=WorldSchematic.semanticLayers(world);
        const fields=WorldSchematic.fieldStack(world);

        this.routeGuidanceRecords=WorldSchematic.routeGuidance(world);
        this.domainNavigation=domainNavigation;this.semantic=semantic;this.semanticFilter='';this.paletteSettings={Version:1,LayerColors:{}};
        this.plan = plan; this.selected = null; this.selectors = new Map(); this.labelNodes = new Map(); this.markerNodes = []; this.focusRootId = null; this.svg.replaceChildren();

        const boxesById = new Map(plan.boxes.map(box=>[box.id,box]));

        this.boxesById=boxesById;

        const childrenById = new Map();

        for(const box of plan.boxes){const parent=box.node.ParentId;if(!childrenById.has(parent))childrenById.set(parent,[]);childrenById.get(parent).push(box);}

        this.details.replaceChildren();

        const prompt = document.createElement('p'); prompt.textContent = 'Select a region, site or constituent structure to inspect its purpose and metadata.';

        this.details.append(prompt);

        this.showFieldStack(fields);

        this.showPlacementFeedback();

        this.showCapacity();
        this.showProgression();
        this.showRouteGuidance();

        this.element('title', {}, `${world.Id}: world schematic`);

        this.element('desc', {}, `Use Tab and Enter to inspect locations, or the fit and zoom controls. ${plan.coordinateSpace?'Engine world coordinates.':'Layout uses diagram units.'}`);

        const defs = this.element('defs');

        const marker = this.element('marker', { id: 'arrow', markerWidth: 8, markerHeight: 8, refX: 7, refY: 4, orient: 'auto' }, null, defs);

        this.element('path', { d: 'M0 0 L8 4 L0 8 Z', fill: '#176891' }, null, marker);

        this.groups = {};

        for (const name of ['terrain', 'ground', 'regions', 'population', 'roads', 'sites', 'links', 'labels']) this.groups[name] = this.element('g', { 'data-layer': name });

        const legend=this.control('.key');

        if(legend)legend.replaceChildren();

        const key=(text,color)=>{if(legend){const label=document.createElement('span');label.textContent=text;label.style.setProperty('--color',color);legend.append(label);}};

        for(const {profileId,rule} of this.routeGuidanceRecords){
            const shape=this.element('polygon',{points:rule.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',stroke:'#805d9b',
                'stroke-width':1.5,'stroke-dasharray':'6 4','vector-effect':'non-scaling-stroke','data-route-guidance':`${profileId}/${rule.Id}`,
                'data-guidance-rate':rule.CostPerMeter,'data-source-rule':rule.SourceRuleId},null,this.groups.regions);
            this.element('title',{},`${rule.Purpose}\n${rule.SourceRuleId}\nSoft alignment preference; ${rule.CostPerMeter} per transverse ${world.Space.Unit}. Physical constraints remain mandatory.`,shape);
        }
        if(this.routeGuidanceRecords.length)key('Regional alignment preference (soft)','#805d9b');

        const fieldSelector=this.control('#terrain-field-view');

        if(fieldSelector){fieldSelector.disabled=!plan.heightField;fieldSelector.value='height';fieldSelector.onchange=null;}

        if(plan.boundary){

            const points=p=>p.map(v=>`${v.x},${v.y}`).join(' '),clip=this.element('clipPath',{id:'surface-boundary'},null,defs);

            this.element('polygon',{points:points(plan.boundary)},null,clip);

            this.groups.terrain.setAttribute('clip-path',`url(#${this.svgId('surface-boundary')})`);

            this.element('polygon',{points:points(plan.boundary),fill:'#f2ece1',stroke:'#526b64','stroke-width':.4},null,this.groups.terrain);

            if(plan.heightField){

                const field=plan.heightField,f=field.geometry,images={},resolution=1024;

                let maxSlope=this.terrainScale?.maximumSlope||0;for(const triangle of WorldSchematic.heightTriangles(field))maxSlope=Math.max(maxSlope,triangle.slope);
                const scaleMinimum=this.terrainScale?.minimum??field.minimum,scaleMaximum=this.terrainScale?.maximum??field.maximum;

                for(const mode of ['height','slope']){

                    const canvas=document.createElement('canvas');canvas.width=resolution;canvas.height=resolution;

                    const context=canvas.getContext('2d');

                    if(!context)throw new Error('Terrain canvas unavailable.');

                    for(const triangle of WorldSchematic.heightTriangles(field)){

                        const t=mode==='height'?(triangle.elevation-scaleMinimum)/(scaleMaximum-scaleMinimum||1):triangle.slope/(maxSlope||1);

                        context.fillStyle=mode==='height'?`hsl(${220-t*185} 48% ${35+t*35}%)`:`hsl(${280-t*250} 65% ${88-t*48}%)`;

                        context.beginPath();triangle.points.forEach((p,i)=>context[i?'lineTo':'moveTo']((p.x-field.origin.x)/field.width*resolution,(p.y-field.origin.y)/field.height*resolution));

                        context.closePath();context.fill();

                    }

                    images[mode]=canvas.toDataURL('image/png');

                }

                const image=this.element('image',{x:field.origin.x,y:field.origin.y,width:field.width,height:field.height,

                    href:images.height,preserveAspectRatio:'none','data-heightfield-id':f.Id},null,this.groups.terrain);

                this.element('title',{},`${f.Purpose}; ${field.columns} × ${field.rows} captured engine samples on ${f.Columns} × ${f.Rows} source lattice; elevation ${field.minimum.toFixed(2)}–${field.maximum.toFixed(2)} ${plan.unit}; slope 0–${maxSlope.toFixed(3)} rise/run. Display colors are triangle means.`,image);

                key(`Height ${field.minimum.toFixed(1)}–${field.maximum.toFixed(1)} ${plan.unit} · blue → gold`,'#668b9e');

                key(`Slope 0–${maxSlope.toFixed(2)} rise/run · pale → red`,'#c85f6e');

                if(fieldSelector)fieldSelector.onchange=()=>{image.setAttribute('href',images[fieldSelector.value]);this.notify('schematic.terrain.layer',`Showing ${fieldSelector.value==='height'?'elevation':'slope'} from engine terrain samples.`);};

            }

            const kinds=[...new Set(plan.terrain.map(p=>p.kind))].sort(),colors=new Map(kinds.map((k,i)=>[k,k===plan.water?.definition.Kind?'#4a9dce':`hsl(${(i*137+95)%360} 30% 75%)`]));

            for(const patch of plan.terrain){

                const shape=this.element('polygon',{points:points(patch.points),fill:colors.get(patch.kind),'data-terrain-id':patch.id,...(patch.water?{'data-water-id':patch.id}:{})},null,this.groups.terrain);

                const waterLabel=patch.water?.Definition?`\nRetained ${patch.water.Definition.Id}; level ${patch.water.Definition.Level} ${plan.unit}; capacity ${patch.water.Capacity} ${plan.unit}³`:
                    patch.water?`\nFlow ${patch.water.Flow} ${plan.water.rateUnit}; width ${patch.water.Width} ${plan.unit}`:'';
                this.element('title',{},`${patch.kind}: ${patch.id}\n${patch.purpose}${waterLabel}`,shape);

            }

            if(plan.groundCover){
                const cover=plan.groundCover,colors=new Map(cover.Definition.Materials.map((m,i)=>[m.Id,`hsl(${(i*137+35)%360} 35% 65%)`]));
                // One compound fill per material cancels shared internal edges.
                // Individual exact cells remain transparent inspection targets;
                // no geometry is unioned, snapped or recomputed in JavaScript.
                for(const cells of WorldSchematic.groundCoverBatches(cover)){
                    const id=cells[0].MaterialId??cells[0].Status;
                    const status=cells[0].Status,color=status==='Excluded'?'#434750':status==='Unresolved'?'#e29dc8':colors.get(id);
                    const path=cells.map(c=>c.Boundary.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');
                    this.element('path',{d:path,fill:color,'fill-rule':'nonzero',...(status==='Unresolved'?{stroke:'#9a245c','stroke-dasharray':'2 2','stroke-width':.2}:{}),
                        'data-ground-material-fill':id,'data-ground-fill-status':status,'data-ground-cell-count':cells.length},null,this.groups.ground);
                }
                for(const cell of cover.Cells){
                    const shape=this.element('polygon',{points:points(cell.Boundary.Vertices.map(v=>({x:v.X,y:v.Y}))),fill:'transparent',stroke:'none',
                        'data-ground-cell':cell.Id,'data-ground-material':cell.MaterialId??'','data-ground-status':cell.Status,'data-ground-area':cell.Area},null,this.groups.ground);
                    this.element('title',{},`${cell.MaterialId??cell.Status}: ${cell.Area} ${plan.unit}²; ${cell.SourceIds.join(', ')||cell.Diagnostic||cell.TerrainSourceId}`,shape);
                    shape.addEventListener('click',()=>{if(!this.dragged)this.showGroundCell(cell);});
                }
                for(const m of cover.Definition.Materials)key(`${m.Kind}: ${m.Purpose}`,colors.get(m.Id));
                if(cover.UnresolvedArea>0)key('Unresolved ground material','#e29dc8');
                const summary=document.createElement('p');summary.dataset.groundCoverAccounting='true';
                summary.textContent=`Ground cover: ${cover.CoveredArea.toFixed(2)} ${plan.unit}² assigned, ${cover.ExcludedArea.toFixed(2)} excluded, ${cover.UnresolvedArea.toFixed(2)} unresolved. ${cover.Sources.length} traced sources. Select a material cell for purpose and provenance; cover does not certify populated content.`;this.details.append(summary);
            }

            for(const exclusion of plan.exclusions){

                const shape=this.element('polygon',{points:points(exclusion.points),fill:'#434750',stroke:'#f8e8db','stroke-width':.3,'data-exclusion-id':exclusion.id},null,this.groups.terrain);

                this.element('title',{},`Excluded: ${exclusion.id}\n${exclusion.purpose}`,shape);

            }

            kinds.forEach(k=>key(k,colors.get(k)));key('Excluded','#434750');key('Passage envelope','#79634c');key('Destination marker','#e5b951');

        } else {key('Regions','#e6ede7');key('Sites','#b8ccd5');if(plan.corridors.length)key('Diagram channels','#76878b');if(world.Connections.length)key('Logical connections','#176891');}

        if(plan.generatedRegions){

            const regionPlan=plan.generatedRegions;

            regionPlan.Regions.forEach((region,index)=>{

                const color=`hsl(${(index*137+195)%360} 55% 48%)`,group=this.element('g',{'data-generated-region':region.Id},null,this.groups.regions);

                // One fill path without strokes on the internal terrain decomposition.

                const fill=region.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');

                const shape=this.element('path',{d:fill,fill:color,'fill-opacity':.3,'data-region-area':region.Area},null,group);

                this.element('title',{},`${region.Id}: ${region.Seed.Purpose}\nSuitability: ${(region.Seed.Profile||plan.generatedRegions.Request.Profile).Id}\n${region.Area.toFixed(2)} ${plan.unit}²; minimum ${region.Seed.MinimumArea}\n${region.Seed.SourceRuleId}`,shape);

                const outline=region.BoundaryEdges.map(e=>`M${e.From.X},${e.From.Y} L${e.To.X},${e.To.Y}`).join(' ');

                this.element('path',{d:outline,fill:'none',stroke:color,'stroke-width':2,'vector-effect':'non-scaling-stroke','data-region-outline':region.Id},null,group);

                key(`${region.Id} · ${region.Area.toFixed(1)} ${plan.unit}²`,color);

            });

            const summary=document.createElement('p');summary.dataset.regionAccounting='true';

            summary.textContent=`Generated regions: ${(regionPlan.SuitableArea-regionPlan.UnclaimedSuitableArea).toFixed(1)} ${plan.unit}² assigned; ${regionPlan.UnclaimedSuitableArea.toFixed(1)} suitable but unclaimed; ${regionPlan.IneligibleArea.toFixed(1)} outside the region rules. Region colors do not imply roads or completed content.`;

            this.details.append(summary);
            this.showRegionDemand();

        }

        if(plan.localLayouts){

            plan.localLayouts.Allocations.forEach((allocation,index)=>{

                const color=`hsl(${(index*137+25)%360} 58% 45%)`,shape=allocation.Shape;

                const group=this.element('g',{'data-local-allocation':allocation.Id},null,this.groups.regions);

                const fill=shape.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');

                const path=this.element('path',{d:fill,fill:color,'fill-opacity':.32,'data-allocation-area':shape.Area},null,group);

                this.element('title',{},`${allocation.Id}: ${allocation.Program.Purpose}\n${shape.Area.toFixed(2)} ${plan.unit}²; minimum ${allocation.Program.MinimumArea}\n${allocation.Program.SourceRuleId}`,path);

                this.element('path',{d:shape.BoundaryEdges.map(e=>`M${e.From.X},${e.From.Y} L${e.To.X},${e.To.Y}`).join(' '),fill:'none',stroke:color,'stroke-width':1.5,'vector-effect':'non-scaling-stroke','data-allocation-outline':allocation.Id},null,group);

                key(`${allocation.Id} · ${shape.Area.toFixed(1)} ${plan.unit}²`,color);

            });

            for(const item of plan.localLayouts.Interfaces){

                const edge=item.SelectedSpan;

                const line=this.element('line',{x1:edge.From.X,y1:edge.From.Y,x2:edge.To.X,y2:edge.To.Y,stroke:'#713b81','stroke-width':3,'stroke-dasharray':'5 3','vector-effect':'non-scaling-stroke','data-local-interface':item.Id},null,this.groups.regions);

                const physical=plan.interfaceRoutes.filter(r=>r.InterfaceId===item.Id);
                this.element('title',{},`${item.Requirement.Purpose}\nDeclared shared boundary. ${physical.length?'Realized crossings are shown in magenta.':'No physical passage is declared.'}`,line);

            }

            key('Required shared interface','#713b81');
            if(plan.interfaceRoutes.length)key('Physical interface crossing','#c23d84');

            const summary=document.createElement('p');summary.dataset.localLayoutAccounting='true';

            summary.textContent=`Local programs: ${plan.localLayouts.Allocations.length}. ${plan.localLayouts.Allocations.filter(a=>!plan.localLayouts.Definition.Layouts.some(l=>l.ParentAllocationId===a.Id)).reduce((n,a)=>n+a.Shape.Area,0).toFixed(1)} ${plan.unit}² in terminal allocations; ${plan.localLayouts.Areas.reduce((n,a)=>n+a.ResidualArea,0).toFixed(1)} residual. Colored allocations reserve space for programs; their boundaries are not roads.`;

            this.details.append(summary);

        }

        if(plan.suitability?.size){key('Declared checks passed','#3c9669');key('Declared check rejected','#ca5964');}

        if(world.AccessPlan){key('Solid footprint','#596b86');key('Declared entrance approach','#b48029');}

        if(world.ServicePlan?.SegmentCapacities?.length){key('Passage capacity available','#237e7b');key('Passage at capacity','#9d376d');}

        for(const source of world.ServicePlan?.Sources||[])for(const supply of source.Supplies)if(supply.Area){

            const area=this.element('polygon',{points:supply.Area.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),

                fill:'#399870','fill-opacity':.08,stroke:'#399870','stroke-width':1.5,'stroke-dasharray':'3 3',

                'vector-effect':'non-scaling-stroke','pointer-events':'none','data-resource-area':`${source.NodeId}/${supply.Id}`},null,this.groups.regions);

            this.element('title',{},`Resource area: ${supply.Kind} (${supply.SourceRuleId})`,area);

        }

        if(world.ServicePlan?.Sources.some(s=>s.Supplies.some(p=>p.Area)))key('Resource area','#399870');

        if(world.ContextPlan){

            key('Context region','#8064a2');
            for(const protection of world.ContextPlan.Protections||[]){
                const rule=protection.Definition;
                const area=this.element('polygon',{points:protection.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'#e68a24','fill-opacity':.22,stroke:'#b66510','stroke-width':1.5,'stroke-dasharray':'5 3',
                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-protection-id':rule.Id},null,this.groups.regions);
                this.element('title',{},`${rule.Kind}: ${rule.Purpose}; blockers: ${rule.Tags.join(', ')} (${rule.SourceRuleId})`,area);
                if(protection.Centerline)this.element('polyline',{points:protection.Centerline.Points.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'none',stroke:'#b66510','stroke-width':1,'vector-effect':'non-scaling-stroke','pointer-events':'none',
                    'data-protection-centerline':rule.Id},null,this.groups.regions);
            }
            if(world.ContextPlan.Protections?.length)key('Protected gameplay space','#e68a24');

            for(const zone of world.ContextPlan.Definition.Exclusions||[]){

                const area=this.element('polygon',{points:zone.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),

                    fill:'#c05b69','fill-opacity':.18,stroke:'#c05b69','stroke-width':1.5,'stroke-dasharray':'4 2',

                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-context-exclusion':zone.Id},null,this.groups.regions);

                this.element('title',{},`Excluded for ${zone.Tags.join(', ')}: ${zone.Id} (${zone.SourceRuleId})`,area);

            }

            if(world.ContextPlan.Definition.Exclusions?.length)key('Tagged exclusion','#c05b69');

            for(const region of world.ContextPlan.Definition.Regions){

                const polygon=this.element('polygon',{points:region.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),

                    fill:'#8064a2','fill-opacity':.06,stroke:'#8064a2','stroke-width':1.5,'stroke-dasharray':'7 4',

                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-context-region':region.Id},null,this.groups.regions);

                this.element('title',{},`${region.Id}: ${region.Use} (${region.SourceRuleId})`,polygon);

            }

        }

        if(plan.suitability&&[...plan.suitability.values()].some(entries=>entries.some(e=>e.Placement?.IsReservation)))key('Placement reservation','#a77b3b');

        const featureColor=kind=>['Garden','GreenBuffer','Lawn'].includes(kind)?'#6e9b69':'#b7a276';

        for(const kind of new Set((world.PlacementPlan?.SiteFeatures??[]).map(f=>f.Kind)))key(`Site: ${kind}`,featureColor(kind));

        const safeColor = (value, fallback) => typeof value === 'string' && /^#[a-f0-9]{6}$/i.test(value) ? value : fallback;

        // Physical fills must not intercept point markers inside their footprint.
        // Partition only the paint order; source geometry and plan order stay exact.
        const paintBoxes=[...plan.boxes.filter(box=>!box.marker||box.catalogFootprint),
            ...plan.boxes.filter(box=>box.marker&&!box.catalogFootprint)];
        for (const box of paintBoxes) {

            const pointChecks=plan.suitability?.get(box.id)||[];

            const h = box.node.Properties?.schematic ?? {}, label = h.label || (box.marker ? `${box.node.Kind} ${box.id.split('/').at(-1)}` : box.node.Kind);

            const group = this.element('g', { tabindex: 0, role: 'button', 'aria-label': `${label}: ${box.id}`, 'data-node-id': box.id, ...(box.node.VariationKey?{'data-variation-archetype':box.node.ArchetypeId}:{}) }, null,

                box.container ? this.groups.regions : this.groups.sites);

            for(const feature of pointChecks[0]?.Placement?.SiteFeatures??[]){

                const color=featureColor(feature.Kind);

                const polygon=this.element('polygon',{points:feature.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:color,'fill-opacity':.6,

                    stroke:color,'stroke-width':1,'vector-effect':'non-scaling-stroke','data-site-feature':feature.Id},null,group);

                this.element('title',{},`${feature.Kind}: ${feature.Purpose} (${feature.SourceRuleId})`,polygon);

            }

            for(const entry of pointChecks)if(entry.Footprint){

                const area=entry.Footprint,reserved=entry.Placement?.IsReservation===true,color=!area.Eligible?'#ca5964':reserved?'#a77b3b':box.physicalAccess?.Occupancy==='Solid'?'#596b86':'#3c9669';

                const polygon=this.element('polygon',{points:area.Footprint.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:reserved?'none':color,'fill-opacity':.16,

                    'stroke-dasharray':reserved?'5 3':'none',

                    stroke:color,'stroke-width':1.5,'vector-effect':'non-scaling-stroke','data-footprint-rule':entry.RuleId},null,group);

                this.element('title',{},`${reserved?'Placement reservation':'Whole-footprint'} ${entry.RuleId}: ${area.Eligible?'eligible':area.Rejections.join(', ')}`,polygon);

            }

            const shape = box.marker ? {cx:box.x+box.width/2,cy:box.y+box.height/2,r:box.catalogFootprint?Math.min(box.width/2,Math.min(box.focusBounds.width,box.focusBounds.height)*.15):box.width/2} : box.polygon ? { points:box.polygon.map(p=>`${p.x},${p.y}`).join(' ') } :

                {x:box.x,y:box.y,width:box.width,height:box.height,rx:plan.coordinateSpace?0:box.container?10:4};

            const rect = this.element(box.marker?'circle':box.polygon?'polygon':'rect', { ...shape,

                fill: box.marker&&pointChecks.length?(pointChecks.every(o=>o.Result.Eligible&&o.Footprint?.Eligible!==false)?'#3c9669':'#ca5964'):h.boundsOnly?'none':safeColor(h.color, box.marker?'#e5b951':box.container ? '#e6ede7' : '#b8ccd5'), stroke: h.boundsOnly?'none':'#637d82', 'stroke-width': box.depth === 0 ? 2 : 1, 'vector-effect':'non-scaling-stroke' }, null, group);

            if(!h.boundsOnly&&!(box.marker&&pointChecks.length)){rect.setAttribute('data-semantic-paint','fill');rect.setAttribute('data-source-paint',rect.getAttribute('fill'));rect.setAttribute('data-palette-node',box.id);}

            this.element('title', {}, `${label}\n${box.node.Purpose}`, group);

            if(box.marker){rect.setAttribute('data-source-radius',shape.r);this.markerNodes.push(rect);}

            if (!box.marker && !box.container && !box.polygon && h.footprint !== false) {

                this.element('rect', { x: box.x + box.width * 0.12, y: box.y + box.height * 0.12, width: box.width * 0.76,

                    height: box.height * 0.60, rx: 2, fill: '#ffffff', 'fill-opacity': 0.36, stroke: '#637d82' }, null, group);

            }

            const anchor=(box.polygon||h.boundsOnly)&&Array.isArray(box.node.Position)&&box.node.Position.length===2&&box.node.Position.every(Number.isFinite)?box.node.Position:null;

            const labelBounds=box.catalogFootprint?box.focusBounds:null;

            const labelSize=labelBounds?Math.min(plan.marker*1.8,labelBounds.height*.16,labelBounds.width/Math.max(2,String(label).length)*1.1):null;

            const labelText = this.element('text', { x: labelBounds?labelBounds.x+labelBounds.width/2:anchor?anchor[0]:box.container ? box.x + 14 : box.x + box.width / 2,

                y: labelBounds?labelBounds.y+labelSize:box.marker?box.y-box.height*.5:anchor?anchor[1]:box.container ? box.y + 23 : plan.coordinateSpace ? box.y + box.height * 0.65 : box.y + box.height - 9, 'text-anchor': labelBounds||anchor?'middle':box.container ? 'start' : 'middle',

                'font-size': labelSize??(box.marker?plan.marker*1.8:plan.coordinateSpace ? Math.min(box.container?14:6,box.width/Math.max(2,String(label).length)*1.2) : box.depth === 0 ? 22 : box.container ? 16 : 12), fill: '#18383e', 'font-weight': box.container ? 650 : 500,

                'pointer-events': 'none' }, String(label).slice(0, 44), this.groups.labels);

            const select = () => {

                if (this.dragged) return;

                if (this.selected) { this.selected.rect.setAttribute('stroke-width', this.selected.width); this.selected.rect.setAttribute('stroke', '#637d82'); }

                this.selected = { id:box.id, rect, width: box.depth === 0 ? '2' : '1' }; rect.setAttribute('stroke', '#135d90'); rect.setAttribute('stroke-width', '4');

                this.details.replaceChildren();

                const heading = document.createElement('h2'); heading.textContent = String(label); this.details.append(heading);

                const purpose = document.createElement('p'); purpose.textContent = box.node.Purpose; this.details.append(purpose);

                this.showPlacementFeedback();

                this.showCapacity(box.id);
                this.showRegionDemand(box.id);
                this.showProgression(box.id);
                this.showRouteGuidance();

                this.showServices(box.id);

                this.showContext(box.id);
                this.showVariation(box.id);
                this.showPopulationDomain(box.id);

                const allocationId=pointChecks.find(e=>e.Placement?.AllocationId)?.Placement.AllocationId;

                const allocation=plan.localLayouts?.Allocations.find(a=>a.Id===allocationId);

                if(allocation){const detail=document.createElement('p');detail.textContent=`Local program ${allocation.Id}: ${allocation.Program.Purpose}. Region ${allocation.RegionId}; ${allocation.Shape.Area.toFixed(1)} ${plan.unit}² allocated, minimum ${allocation.Program.MinimumArea}. Source footprint and reservation retained.`;this.details.append(detail);}

                if(box.physicalAccess){const access=document.createElement('p');access.textContent=`Occupancy: ${box.physicalAccess.Occupancy}. ${box.physicalAccess.Ports.length} declared boundary entrances. External routes avoid solid footprints; approach legs connect selected entrances.`;this.details.append(access);}

                if(box.groundUse){
                    const use=box.groundUse,title=document.createElement('h3'),intent=document.createElement('p'),scope=document.createElement('p');
                    title.textContent='Declared ground use';
                    intent.textContent=`${use.Kind}: ${use.Purpose}. Minimum open ground: ${use.MinimumOpenFraction*100}% of the source footprint after subtracting solid source footprints. Rule: ${use.SourceRuleId}.`;
                    scope.textContent='This is the declared requirement, not an area measurement. Native physical accounting evaluates it; walls, terrain and actor clearance are separate constraints.';
                    this.details.append(title,intent,scope);
                }else if(box.physicalAccess?.Occupancy==='Reservation'){
                    const scope=document.createElement('p');scope.textContent='Ground use is not declared for this reservation. Its extent does not certify completed or intentionally open ground.';this.details.append(scope);
                }

                for(const port of box.physicalAccess?.Ports||[]){const entry=document.createElement('p');entry.textContent=`${port.Id}: ${port.Purpose}. Width ${port.Width} ${world.Space.Unit}; boundary (${port.BoundaryPosition.X}, ${port.BoundaryPosition.Y}) → approach (${port.ApproachPosition.X}, ${port.ApproachPosition.Y}).`;this.details.append(entry);}

                const authoredPose=pointChecks.map(e=>e.Placement?.Pose).find(Boolean);
                if(authoredPose){const note=document.createElement('p');note.dataset.poseControl=authoredPose.Definition.Id;
                    note.textContent=`Authored pose: ${authoredPose.Definition.Purpose}. ${authoredPose.Definition.Frame} (${authoredPose.Definition.Position.X}, ${authoredPose.Definition.Position.Y}) → world (${authoredPose.WorldPosition.X}, ${authoredPose.WorldPosition.Y}) ${world.Space.Unit}; orientation ${authoredPose.RotationDegrees}°. Rule: ${authoredPose.Definition.SourceRuleId}.`;
                    this.details.append(note);}

                for(const entry of pointChecks){

                    const check=document.createElement('p'),result=entry.Result;

                    check.textContent=`Point check ${entry.RuleId}: ${result.Eligible?`eligible, preference score ${result.Score.toFixed(3)}`:`rejected (${result.Rejections.join(', ')})`}. ${entry.Footprint?`Whole footprint: ${entry.Footprint.Eligible?'eligible':`rejected (${entry.Footprint.Rejections.join(', ')})`}.`:'Whole footprint not evaluated.'}`;

                    this.details.append(check);

                    const values=document.createElement('pre');values.textContent=JSON.stringify({Fields:result.Observation.Values,Decisions:result.Decisions,

                        ...(entry.Footprint?{WholeFootprint:{CompleteCoverage:entry.Footprint.CompleteCoverage,Ranges:entry.Footprint.Ranges,Decisions:entry.Footprint.Decisions}}:{}),

                        ...(entry.Placement?{Placement:entry.Placement}:{})},null,2);this.details.append(values);

                }

                const button=(text,action)=>{const item=document.createElement('button');item.type='button';item.textContent=text;item.addEventListener('click',action);this.details.append(item);return item;};

                const navigate=target=>{this.dragged=false;this.selectors.get(target.id)?.();this.focusBox(target);};

                button('Focus selection',()=>this.focusBox(box));

                const parent=boxesById.get(box.node.ParentId);

                if(parent)button(`Parent: ${parent.node.Properties?.schematic?.label||parent.node.Kind}`,()=>navigate(parent));

                const children=childrenById.get(box.id)||[];

                if(children.length){

                    button('Focus contents',()=>this.focusContents(box));

                    const title=document.createElement('h3');title.textContent=`Contained items (${children.length})`;this.details.append(title);

                    const list=document.createElement('select');list.setAttribute('aria-label','Contained items');list.style.width='100%';

                    for(const child of children){const option=document.createElement('option');option.value=child.id;option.textContent=`${child.node.Properties?.schematic?.label||child.node.Kind} — ${child.id.split('/').pop()}`;list.append(option);}

                    this.details.append(list);button('Open and focus item',()=>navigate(boxesById.get(list.value)));

                }

                const metadata=document.createElement('details');const summary=document.createElement('summary');summary.textContent='Metadata';metadata.append(summary);this.details.append(metadata);

                const info = document.createElement('pre'); info.textContent = JSON.stringify({ Id: box.id, Kind: box.node.Kind, Parent: box.node.ParentId,

                    Diagram: { x: box.x, y: box.y, width: box.width, height: box.height }, WorldPosition: box.node.Position,

                    Polygon: box.polygon ?? null, Properties: box.node.Properties, StructureKey: box.node.StructureKey,

                    Object: this.plan.world.ObjectDefinitions?.[box.node.Properties?.object_key] ?? null,

                    Area: this.plan.world.AreaDefinitions?.[box.node.Properties?.area_key] ?? null }, null, 2); metadata.append(info);

                heading.tabIndex=-1;heading.focus();

                this.updateLabelVisibility();
                this.notify('schematic.node.selected', `Selected ${label}.`);

            };

            this.selectors.set(box.id,select);

            group.addEventListener('click', select);

            group.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); this.dragged = false; select(); } });

            labelText.setAttribute('data-source-font-size',labelText.getAttribute('font-size'));
            labelText.setAttribute('aria-hidden', 'true');

            labelText.setAttribute('data-source-label',h.hideLabel===true?'hidden':'visible');

            labelText.setAttribute('data-node-id',box.id);this.labelNodes.set(box.id,labelText);

            if (h.hideLabel === true) labelText.style.display = 'none';

        }

        for (const road of plan.corridors) {

            this.element('line', { ...road, stroke: '#76878b', 'stroke-width': road.width, 'stroke-linecap': plan.coordinateSpace === 'engine-world-meters' ? 'butt' : 'square' }, null, this.groups.roads);

            this.element('line', { x1: road.x1, y1: road.y1, x2: road.x2, y2: road.y2, stroke: '#f5f1d8',

                'stroke-width': 1, 'stroke-dasharray': '6 6' }, null, this.groups.roads);

        }

        for(const trace of plan.populationDomains||[]){
            const d=trace.Domain,color=d.Definition.Source==='SiteFeature'?'#237a58':'#84508c';
            const shape=this.element('path',{d:d.Shape.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' '),
                fill:color,'fill-opacity':.12,'pointer-events':'none','data-population-domain':`${trace.StageId}/${trace.DemandId}`},null,this.groups.population);
            this.element('title',{},`${trace.Purpose}\n${d.Definition.Source}: ${d.Definition.Selector}; ${trace.NodeIds.length} required members; ${d.Shape.Area.toFixed(3)} ${plan.unit}²\n${d.SourceIds.join(', ')}`,shape);
            for(const [i,p] of d.WorldPositions.entries()){
                const pin=this.element('circle',{cx:p.X,cy:p.Y,r:.09,fill:color,'pointer-events':'none','data-population-domain-member':trace.NodeIds[i]},null,this.groups.population);
                this.element('title',{},`Required source-domain anchor: ${trace.NodeIds[i]}`,pin);
            }
        }
        for(const trace of plan.populationAreas||[])for(const [index,stratum] of trace.Plan.Strata.entries()){
            const color=`hsl(${(index*137+45)%360} 45% 48%)`,group=this.element('g',{'data-population-area':`${trace.StageId}/${trace.DemandId}/${stratum.Id}`},null,this.groups.population);
            const fill=stratum.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');
            const shape=this.element('path',{d:fill,fill:color,'fill-opacity':.16},null,group);
            this.element('title',{},`${trace.Purpose}\nPopulation stratum: ${stratum.Area.toFixed(3)} ${plan.unit}²; mass ${stratum.Mass.toFixed(3)}; one representative member`,shape);
            for(const edge of stratum.BoundaryEdges)this.element('line',{x1:edge.From.X,y1:edge.From.Y,x2:edge.To.X,y2:edge.To.Y,stroke:color,'stroke-width':.035},null,group);
        }
        for(const trace of plan.populationContours||[])for(const contour of trace.Contours){
            const shape=this.element('polyline',{points:contour.Path.Points.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',stroke:'#278e62','stroke-width':.12,'stroke-dasharray':'.5 .3','data-population-contour':`${trace.StageId}/${trace.DemandId}/${contour.Id}`},null,this.groups.regions);
            this.element('title',{},`${trace.Purpose}\n${trace.FieldId}: connected contour ${contour.Id}; ${contour.Positions.length} required members`,shape);
        }
        for(const assembly of plan.interiorAssemblies||[]){
            for(const wall of assembly.Walls){
                const shape=this.element('polygon',{points:wall.Footprint.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:'#34404b','data-interior-wall':`${assembly.RuleId}/${wall.Id}`},null,this.groups.roads);
                this.element('title',{},`Generated boundary wall for ${wall.RoomNodeId}`,shape);
            }
            for(const opening of assembly.Openings){
                const shape=this.element('line',{x1:opening.From.X,y1:opening.From.Y,x2:opening.To.X,y2:opening.To.Y,stroke:'#167e84','stroke-width':.06,'data-interior-opening':opening.ConnectionId},null,this.groups.roads);
                this.element('title',{},`Derived shared-boundary opening: ${opening.Width} ${plan.unit}`,shape);
            }
        }
        const sharedJourneys=new Set((plan.physicalCorridors||[]).flatMap(g=>g.Journeys.map(j=>j.Id)));

        const segmentCapacity=new Map((world.ServicePlan?.SegmentCapacities||[]).map(c=>[c.SegmentId,c]));

        for(const group of plan.physicalCorridors||[]){

            for(const s of group.Segments){

                const capacity=segmentCapacity.get(s.Id),used=capacity?world.ServicePlan.Usage[`segment:${s.Id}`]:null;

                const shape=this.element('line',{x1:s.From.X,y1:s.From.Y,x2:s.To.X,y2:s.To.Y,stroke:capacity?(used>=capacity.Capacity?'#9d376d':'#237e7b'):'#79634c','stroke-width':s.Width,

                    'stroke-linecap':'round','data-corridor-segment':s.Id,...(capacity?{'data-segment-usage':used,'data-segment-capacity':capacity.Capacity,'data-capacity-unit':capacity.Unit}:{})},null,this.groups.roads);

                this.element('title',{},`Shared passage: ${s.ForwardJourneys.length} forward / ${s.ReverseJourneys.length} reverse journeys; ${s.Width} ${plan.unit}. ${capacity?`Allocated ${used} / ${capacity.Capacity} ${capacity.Unit}, combined directions. `:''}${s.SourceRuleIds.join(', ')}`,shape);
                const journeys=new Set([...s.ForwardJourneys,...s.ReverseJourneys]);
                this.bindRouteInspection(shape,(plan.routes||[]).filter(r=>journeys.has(r.id)),{Segment:s,
                    ...(capacity?{Capacity:capacity,Used:used}:{})});

            }

            for(const j of group.Junctions){

                const shape=this.element('polygon',{points:j.Envelope.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:'#d69843','fill-opacity':.7,

                    stroke:'#714819','stroke-width':.12,'data-corridor-junction':j.Id},null,this.groups.roads);

                this.element('title',{},`Surface junction: ${j.SegmentIds.length} incident segments, ${j.Transitions.length} permitted directed transitions. Circular-traveler clearance; no vehicle-turn qualification.`,shape);

            }

        }

        for(const route of plan.routes||[]){

            if(sharedJourneys.has(route.id))continue;

            const shape=this.element('polyline',{points:route.points.map(p=>`${p.x},${p.y}`).join(' '),fill:'none',stroke:'#79634c',

                'stroke-width':route.width,'stroke-linecap':'round','stroke-linejoin':'round','data-route-id':route.id},null,this.groups.roads);

            this.element('title',{},`${route.purpose}\n${route.sourceRuleId}\n${route.id}${route.guidanceCost?`\nRegional alignment cost: ${route.guidanceCost}`:''}`,shape);
            this.bindRouteInspection(shape,[route]);

        }

        for(const item of plan.interfaceRoutes||[]){
            const shape=this.element('polyline',{points:item.Forward.Crossing.Path.Points.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',
                stroke:'#c23d84','stroke-width':item.width,'stroke-linecap':'round','data-physical-interface':item.InterfaceId},null,this.groups.roads);
            this.element('title',{},`Physical interface ${item.InterfaceId}: ${item.FromDomainId} to ${item.ToDomainId}; width ${item.width} ${plan.unit}. ${item.Attempts.length} crossing attempts. ${item.Attempts.filter(a=>!a.Accepted).map(a=>a.Rejection).join('; ')}`,shape);
        }
        for(const box of plan.boxes)for(const port of box.physicalAccess?.Ports||[]){

            const a=port.BoundaryPosition,b=port.ApproachPosition;

            const shape=this.element('line',{x1:a.X,y1:a.Y,x2:b.X,y2:b.Y,stroke:'#b48029','stroke-width':port.Width,

                'stroke-dasharray':`${port.Width} ${port.Width*.5}`,'data-access-port':`${box.id}/${port.Id}`},null,this.groups.roads);

            this.element('title',{},`Declared entrance ${port.Id}: ${port.Purpose}; width ${port.Width} ${world.Space.Unit}. Terrain traversal is qualified for selected route profiles.`,shape);

        }

        const byId = new Map(plan.boxes.map(box => [box.id, box]));

        for (const link of world.Connections) {

            const a = byId.get(link.FromId), b = byId.get(link.ToId);

            if(!a||!b)continue;

            const ax = a.x + a.width / 2, ay = a.y + a.height / 2, bx = b.x + b.width / 2, by = b.y + b.height / 2;

            const path = this.element('path', { d: plan.boundary?`M${ax} ${ay} L${bx} ${by}`:`M${ax} ${ay} H${(ax + bx) / 2} V${by} H${bx}`, fill: 'none', stroke: '#176891',

                'stroke-width': 2, 'stroke-dasharray': '5 4', 'marker-end': link.Bidirectional ? '' : 'url(#arrow)',
                'data-connection-id':link.Id,'data-semantic-paint':'stroke','data-source-paint':'#176891' }, null, this.groups.links);

            this.element('title', {}, `${link.Kind}: ${link.FromId} → ${link.ToId}${link.Bidirectional ? ' (both ways)' : ''}`, path);

        }

        if(this.sceneOnly)return;
        this.syncLayerControls();
        this.syncSemanticControls();
        this.syncDomainControls();

        this.fit(); this.notify('schematic.loaded', `${world.Nodes.length} nodes · ${world.Connections.length} relationships · ${world.Coverage}.`);

        const title = this.control('#world-title'); if (title) title.textContent = world.Id.replaceAll('-', ' ');

        const scope = this.control('#scope'); if (scope) scope.textContent = world.Coverage === 'PartialScope'

            ? `Partial world scope · remaining: ${(world.RemainingScopes || []).join(', ')}` : 'Complete declared recipe scope';

        const note = this.control('#coordinate-note'); if (note) note.textContent = plan.boundary

            ? `Exact surface and route coordinates in ${plan.unit}. ${plan.heightField?'Height/slope colors display the sampled terrain triangles; polygon overlays remain above them. ':''}Circles mark destinations; outlined suitability polygons show declared footprints. ${plan.physicalCorridors?.length?'Shared passages are drawn once; amber polygons show qualified surface junctions. ':''}Check colors report saved engine decisions only. Dashed connections are logical intent. Artwork and gameplay remain separate.` : plan.coordinateSpace

            ? 'Exact engine-generated coordinates. JavaScript displays saved locations, connections and footprints without relocating them. Artwork and gameplay integration are separate.'

            : 'Schematic diagram units. Boxes and road channels express the declared design; they are not measured geometry or a playable map.';

    }



    bindRouteInspection(shape,routes,context=null) {
        if(!routes.length)return;
        const group=this.element('g',{role:'button',tabindex:0},null,shape.parentNode);shape.before(group);group.append(shape);
        const points=context?[context.Segment.From,context.Segment.To].map(p=>({x:p.X,y:p.Y})):routes[0].points;
        const xs=points.map(p=>p.x),ys=points.map(p=>p.y),padding=Math.max(.05,context?.Segment.Width??routes[0].width)/2;
        // Axis-aligned SVG paths have a zero-sized bounding-box dimension.
        // A non-intercepting rectangle gives their keyboard target real bounds;
        // pointer selection still follows the saved physical stroke.
        this.element('rect',{x:Math.min(...xs)-padding,y:Math.min(...ys)-padding,
            width:Math.max(...xs)-Math.min(...xs)+2*padding,height:Math.max(...ys)-Math.min(...ys)+2*padding,
            fill:'transparent','pointer-events':'none','data-route-focus-bounds':'true'},null,group);
        group.setAttribute('aria-label',context?`Shared passage: ${context.Segment.Id}`:`Route: ${routes[0].purpose} (${routes[0].reverse?'return':'forward'})`);
        group.setAttribute('data-route-inspection',routes.map(r=>r.id).join(' '));
        const select=()=>{if(!this.dragged)this.inspectRoutes(routes,routes[0].id,context);};
        group.addEventListener('click',event=>{event.stopPropagation();select();});
        group.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();this.dragged=false;select();}});
    }

    inspectRoutes(routes,id,context=null) {
        const route=routes.find(r=>r.id===id);if(!route?.inspection)throw new Error('Saved route inspection is unavailable.');
        const saved=route.inspection,world=this.plan.world,unit=world.Space.Unit;
        if(this.selected?.rect){this.selected.rect.setAttribute('stroke-width',this.selected.width);this.selected.rect.setAttribute('stroke','#637d82');}
        this.selected=null;this.details.replaceChildren();
        const heading=document.createElement('h2');heading.textContent=`Route: ${route.purpose}`;heading.tabIndex=-1;this.details.append(heading);
        const paragraph=text=>{const p=document.createElement('p');p.textContent=text;this.details.append(p);};
        const button=(label,action)=>{const b=document.createElement('button');b.type='button';b.textContent=label;b.addEventListener('click',action);this.details.append(b);};
        paragraph(`Source: ${route.sourceRuleId}. ${route.reverse?'Return':'Forward'} journey. Saved width ${route.width} ${unit}.`);
        paragraph(saved.Kind==='DestinationNetwork'?'The engine selected this connection for a declared destination network.':
            saved.Kind==='InteriorOpening'?'This connection uses a declared interior opening.':'This connection realizes declared world travel.');
        const from=route.reverse?saved.Connection.ToId:saved.Connection.FromId,to=route.reverse?saved.Connection.FromId:saved.Connection.ToId;
        paragraph(`From ${from} to ${to}. Connection kind: ${saved.Connection.Kind}.`);
        if(context)paragraph(`Shared passage ${context.Segment.Id}: ${routes.length} saved directed journeys use this segment.`+
            (context.Capacity?` Allocated ${context.Used} / ${context.Capacity.Capacity} ${context.Capacity.Unit}, combined directions.`:''));
        if(routes.length>1){
            const list=document.createElement('select');list.setAttribute('aria-label','Saved passage journey');list.style.width='100%';
            for(const r of routes){const option=document.createElement('option');option.value=r.id;option.textContent=`${r.reverse?'Return':'Forward'} — ${r.purpose} — ${r.id}`;list.append(option);}
            list.value=id;list.addEventListener('change',()=>this.inspectRoutes(routes,list.value,context));this.details.append(list);
        }
        const cost=saved.Result.Cost;
        if(cost)paragraph(`Cost from saved components ${cost.Terrain+cost.Ascent+cost.Descent+(cost.NewPassage||0)+(cost.Guidance||0)}; distance ${cost.Distance} ${unit}. Components and traversal limits are listed below.`);
        else paragraph('This packet does not supply a route cost breakdown.');
        for(const [label,nodeId] of [['From',from],['To',to]])if(this.selectors.has(nodeId))button(`Inspect ${label.toLowerCase()} endpoint`,()=>{
            this.dragged=false;this.selectors.get(nodeId)();const box=this.boxesById.get(nodeId);if(box)this.focusBox(box);
        });
        button('Focus route',()=>this.focusRoute(route));
        const evidence=document.createElement('details'),summary=document.createElement('summary');summary.textContent='Saved traversal rules, costs and diagnostics';evidence.append(summary);evidence.open=true;
        const pre=document.createElement('pre');pre.setAttribute('data-route-evidence',route.id);pre.textContent=JSON.stringify({
            ...saved,...(context?{SharedPassage:context}:{})},null,2);evidence.append(pre);this.details.append(evidence);
        paragraph('These are saved engine decisions. The inspector does not search routes or certify unrecorded alternatives.');
        heading.focus();this.updateLabelVisibility();this.notify('schematic.route.selected',`Inspecting saved route ${route.id}.`);
    }

    focusRoute(route) {
        const xs=route.points.map(p=>p.x),ys=route.points.map(p=>p.y),left=Math.min(...xs),top=Math.min(...ys);
        const bounds=this.svg.getBoundingClientRect(),ratio=bounds.width>0&&bounds.height>0?bounds.width/bounds.height:1;
        const margin=Math.max(1,route.width),w=Math.max(Math.max(...xs)-left+2*margin,(Math.max(...ys)-top+2*margin)*ratio);
        this.focusRootId=null;this.view=[left+(Math.max(...xs)-left-w)/2,top+(Math.max(...ys)-top-w/ratio)/2,w,w/ratio];this.applyView();
        this.notify('schematic.route.focused',`Focused saved route ${route.id}.`);
    }

    fullMapView() {
        let left=this.plan.minX||0,top=this.plan.minY||0,right=left+this.plan.width,bottom=top+this.plan.height;
        const margin=Math.max(this.plan.width,this.plan.height)*.04;
        // Geometry bounds already come from the engine plan. Measure labels only;
        // transparent interaction rectangles are deliberately larger than markers.
        for(const group of [this.groups.labels]){
            if(!group.childElementCount||getComputedStyle(group).display==='none')continue;
            const saved=[...this.labelNodes.values()].map(label=>({label,display:label.style.display,font:label.getAttribute('font-size')}));
            let b;
            try{
                for(const {label} of saved){label.style.display=label.getAttribute('data-source-label')==='hidden'?'none':'';label.setAttribute('font-size',label.getAttribute('data-source-font-size'));}
                b=group.getBBox();
            }finally{for(const {label,display,font} of saved){label.style.display=display;label.setAttribute('font-size',font);}}
            if(![b.x,b.y,b.width,b.height].every(Number.isFinite)||b.width===0&&b.height===0)continue;
            left=Math.min(left,b.x-margin);top=Math.min(top,b.y-margin);right=Math.max(right,b.x+b.width+margin);bottom=Math.max(bottom,b.y+b.height+margin);
        }
        return [left,top,right-left,bottom-top];
    }
    fit() { this.focusRootId=null;this.updateLabelVisibility();this.view=this.fullMapView();this.applyView();this.notify('schematic.view.fit','Showing the full map.'); }

    focusBox(box) {

        const frame=box.focusBounds||box;

        const bounds=this.svg.getBoundingClientRect(),ratio=bounds.width>0&&bounds.height>0?bounds.width/bounds.height:1;

        const engine=this.plan.coordinateSpace?.startsWith('engine-'),margin=engine?Math.max(1,Math.min(frame.width,frame.height)*.15):10;

        const width=Math.max(frame.width+2*margin,(frame.height+2*margin)*ratio,engine?1:this.plan.width/12),height=width/ratio;

        this.focusRootId=box.id;this.updateLabelVisibility();

        this.view=[frame.x+frame.width/2-width/2,frame.y+frame.height/2-height/2,width,height];this.applyView();

        this.notify('schematic.view.focused',`Focused ${box.node.Properties?.schematic?.label||box.node.Kind}.`);

    }

    focusContents(box) {
        const children=new Map();
        for(const item of this.plan.boxes){
            const parent=item.parentId??item.node.ParentId;
            if(!children.has(parent))children.set(parent,[]);children.get(parent).push(item);
        }
        const pending=[box.id],seen=new Set([box.id]),items=[];
        for(let i=0;i<pending.length;i++)for(const child of children.get(pending[i])||[]){
            if(seen.has(child.id))continue;seen.add(child.id);pending.push(child.id);items.push(child);
        }
        if(!items.length)return;
        const frames=items.map(item=>item.focusBounds||item),left=Math.min(...frames.map(f=>f.x)),top=Math.min(...frames.map(f=>f.y));
        const right=Math.max(...frames.map(f=>f.x+f.width)),bottom=Math.max(...frames.map(f=>f.y+f.height));
        this.focusBox({...box,focusBounds:{x:left,y:top,width:right-left,height:bottom-top}});
        this.notify('schematic.view.contents',`Focused ${items.length} contained source items.`);
    }

    showFieldStack(fields) {
        const panel=this.control('#field-tools');if(!panel)return;
        panel.hidden=fields.length===0;const list=this.control('#field-select'),index=this.control('#field-sample-index'),
            label=this.control('#field-sample-label'),details=this.control('#field-details');
        list.replaceChildren();details.textContent='';
        for(const field of fields){const option=document.createElement('option');option.value=field.Key;
            option.textContent=`${field.Kind}: ${field.Definition.Id} (${field.Definition.Unit})`;list.append(option);}
        const show=(report=true)=>{
            const field=fields.find(f=>f.Key===list.value);if(!field)return;
            const sampled=field.SourceValues!=null;label.hidden=!sampled;index.disabled=!sampled;
            let sample=null;
            if(sampled){index.max=String(field.SourceValues.Count-1);const value=Number(index.value);
                if(index.value===''||!Number.isInteger(value)||value<0||value>=field.SourceValues.Count){
                    index.setCustomValidity('Enter an available source sample index.');details.textContent='Enter an available source sample index.';
                    if(report)index.reportValidity();return;
                }
                index.setCustomValidity('');sample=WorldSchematic.fieldSample(this.plan.world,field.Key,value);
            }else index.setCustomValidity('');
            details.textContent=JSON.stringify({...field,...(sample?{SelectedSourceSample:sample}:{})},null,2);
        };
        list.onchange=()=>{index.value='0';show();this.notify('schematic.fields.selected','Inspecting saved environmental field '+list.value+'.');};
        index.oninput=()=>show(false);index.onchange=()=>show();index.value='0';show();
    }

    syncLayerControls() {
        this.layerSnapshot=null;this.soloLayer=null;
        const target=this.control('#layer-target');target.replaceChildren();
        for(const input of this.controlsAll('[data-toggle]')){
            const name=input.dataset.toggle,available=!!this.groups[name]?.childElementCount;
            input.disabled=!available;
            input.checked=available&&(input.defaultChecked||name==='links'&&!this.groups.roads.childElementCount);
            input.title=available?'': 'This world has no '+input.parentElement.textContent.trim().toLowerCase()+' layer.';
            this.layer(name,input.checked);
            this.groups[name].style.opacity='';
            if(available){const option=document.createElement('option');option.value=name;option.textContent=input.parentElement.textContent.trim();target.append(option);}
        }
        this.control('#map-coordinates').textContent='Move over the map for coordinates.';
        this.syncLayerTools();
    }

    layerView() {
        return Object.fromEntries(Object.entries(this.groups).map(([name,group])=>[name,{visible:group.style.display!=='none',opacity:Number(group.style.opacity||1)}]));
    }

    syncLayerTools() {
        const name=this.control('#layer-target').value,available=!!this.groups[name]?.childElementCount;
        this.control('#layer-target').disabled=!available;
        this.control('#layer-solo').disabled=!available;
        this.control('#layer-solo').setAttribute('aria-pressed',String(this.soloLayer===name));
        const opacity=this.control('#layer-opacity');opacity.disabled=!available;
        opacity.value=available?Math.round(Number(this.groups[name].style.opacity||1)*100):100;
        this.control('#layer-opacity-value').textContent=opacity.value+'%';
        this.control('#layer-restore').disabled=!this.layerSnapshot;
    }

    inspectLayer(solo) {
        const name=this.control('#layer-target').value;
        if(!this.groups[name]?.childElementCount)return;
        this.layerSnapshot??=this.layerView();
        if(solo){
            this.soloLayer=name;
            for(const input of this.controlsAll('[data-toggle]')){input.checked=!input.disabled&&input.dataset.toggle===name;this.layer(input.dataset.toggle,input.checked);}
        }else this.groups[name].style.opacity=String(Number(this.control('#layer-opacity').value)/100);
        this.syncLayerTools();
    }

    restoreLayers() {
        if(!this.layerSnapshot)return;
        for(const input of this.controlsAll('[data-toggle]')){
            const state=this.layerSnapshot[input.dataset.toggle];input.checked=state.visible;
            this.layer(input.dataset.toggle,state.visible);this.groups[input.dataset.toggle].style.opacity=String(state.opacity);
        }
        this.layerSnapshot=null;this.soloLayer=null;this.syncLayerTools();
    }

    syncSemanticControls() {
        const select=this.control('#semantic-layer');select.replaceChildren();
        const option=(value,label)=>{const item=document.createElement('option');item.value=value;item.textContent=label;select.append(item);};
        option('','All content');
        for(const [id,entry] of this.semantic.entries)option('layer:'+id,`${id} · ${entry.NodeIds.length} nodes / ${entry.ConnectionIds.length} links`);
        if([...this.semantic.nodes.values(),...this.semantic.connections.values()].some(v=>v===null))option('unassigned','Without a declared layer');
        select.value=this.semanticFilter;this.applySemanticView();
    }

    semanticMatch(layer) {
        return this.semanticFilter===''||(this.semanticFilter==='unassigned'?layer===null:layer===this.semanticFilter.slice(6));
    }

    applySemanticView() {
        const colorFor=id=>id!==null&&Object.hasOwn(this.paletteSettings.LayerColors,id)?this.paletteSettings.LayerColors[id]:null;
        for(const el of this.svg.querySelectorAll('g[data-node-id],path[data-connection-id]')){
            const link=el.hasAttribute('data-connection-id'),id=el.getAttribute(link?'data-connection-id':'data-node-id');
            const layer=(link?this.semantic.connections:this.semantic.nodes).get(id),visible=this.semanticMatch(layer);
            el.style.display=visible?'':'none';el.setAttribute('data-semantic-hidden',String(!visible));
        }
        for(const el of this.svg.querySelectorAll('[data-semantic-paint]')){
            const layer=el.hasAttribute('data-palette-node')?this.semantic.nodes.get(el.getAttribute('data-palette-node')):this.semantic.connections.get(el.getAttribute('data-connection-id'));
            el.setAttribute(el.getAttribute('data-semantic-paint'),colorFor(layer)||el.getAttribute('data-source-paint'));
        }
        for(const [id,label] of this.labelNodes)label.setAttribute('data-semantic-hidden',String(!this.semanticMatch(this.semantic.nodes.get(id))));
        const id=this.semanticFilter.startsWith('layer:')?this.semanticFilter.slice(6):null,entry=this.semantic.entries.get(id);
        const picker=this.control('#semantic-color');picker.disabled=!entry;picker.value=colorFor(id)||'#b8ccd5';
        const nodes=[...this.semantic.nodes.values()].filter(v=>this.semanticMatch(v)).length,links=[...this.semantic.connections.values()].filter(v=>this.semanticMatch(v)).length;
        this.control('#semantic-summary').textContent=`${nodes} nodes / ${links} logical links match. Terrain, physical paths and other context geometry remain unfiltered. `+
            (entry?`${entry.Definition.Kind}: ${entry.Definition.Purpose} Source: ${entry.Definition.SourceRuleId}. ${entry.Definition.Coverage}. Dependencies: ${entry.Definition.DependsOn.join(', ')||'none'}. Remaining: ${entry.Definition.RemainingScopes.join('; ')||'none'}.`:'Layer membership is explicit; it is not a physical floor or generation execution order.');
        const legend=this.control('#semantic-key');legend.replaceChildren();
        for(const [layer] of this.semantic.entries){
            const item=document.createElement('span');item.style.marginRight='12px';
            const color=colorFor(layer);
            if(color){const swatch=document.createElement('span');swatch.style.cssText=`display:inline-block;width:10px;height:10px;margin-right:4px;background:${color}`;item.append(swatch);}
            item.append(document.createTextNode(`${layer}: ${color||'source colors'}`));legend.append(item);
        }
        this.updateLabelVisibility();
    }

    resetSemanticView() {
        this.semanticFilter='';this.paletteSettings={Version:1,LayerColors:{}};this.control('#semantic-layer').value='';this.applySemanticView();
        this.notify('schematic.semantic.reset','Showing all content with source colors.');
    }

    syncDomainControls() {
        const section=this.control('#domain-navigation'),select=this.control('#domain-select'),list=this.control('#domain-transitions');
        select.replaceChildren();list.replaceChildren();section.hidden=!this.domainNavigation;
        if(!this.domainNavigation)return;
        const navigation=this.domainNavigation;
        for(const entry of navigation.Entries){const option=document.createElement('option');option.value=entry.Id;option.textContent=`${entry.Id} · ${entry.Purpose}`;select.append(option);}
        select.value=navigation.CurrentDomainId;
        this.control('#domain-scope').textContent=`${navigation.CollectionId} / ${navigation.CurrentDomainId} · Independent local coordinate domain. `+
            'Selecting another domain opens its own map. Transitions do not imply continuous 3D geometry or runtime traversal.';
        for(const transition of navigation.Transitions){
            if(transition.FromDomainId!==navigation.CurrentDomainId&&transition.ToDomainId!==navigation.CurrentDomainId)continue;
            const item=document.createElement('li');
            item.append(document.createTextNode(`${transition.Kind}: ${transition.FromDomainId} / ${transition.FromNodeId} ${transition.Bidirectional?'↔':'→'} ${transition.ToDomainId} / ${transition.ToNodeId}. ${transition.Purpose} Source: ${transition.SourceRuleId}. `));
            const other=transition.FromDomainId===navigation.CurrentDomainId?transition.ToDomainId:transition.FromDomainId;
            const entry=navigation.Entries.find(e=>e.Id===other),link=document.createElement('a');link.href=entry.Page;link.textContent=`Inspect ${entry.Id}`;item.append(link);list.append(item);
        }
        if(!list.childElementCount){const item=document.createElement('li');item.textContent='No declared transitions touch this domain.';list.append(item);}
    }

    showRegionDemand(nodeId=null) {
        for(const result of this.plan.world.RegionDemandPlan?.Results||[]){
            if(nodeId!=null&&!result.Sources.some(s=>s.Source.NodeId===nodeId))continue;
            const item=document.createElement('p');item.dataset.regionSourceDemand=result.Rule.Id;
            item.textContent=`${result.Rule.RegionId}: ${result.Sources.length} root program envelope(s), ${result.EnvelopeArea.toFixed(2)} ${this.plan.unit}²; maximum coverage ${(result.Rule.MaximumCoverage*100).toFixed(1)}%; required region area ${result.RequiredArea.toFixed(2)} ${this.plan.unit}² and anchor clearance ${result.RequiredClearance.toFixed(2)} ${this.plan.unit}. Source: ${result.Rule.SourceRuleId}. Complete placement and routes are checked separately.`;
            this.details.append(item);
        }
    }
    showRouteGuidance() {
        if(!this.routeGuidanceRecords.length)return;
        const details=document.createElement('details'),summary=document.createElement('summary');summary.textContent=`Regional route guidance: ${this.routeGuidanceRecords.length} scope(s)`;details.append(summary);
        for(const {profileId,rule} of this.routeGuidanceRecords){
            const p=document.createElement('p');p.textContent=`${profileId} / ${rule.Id}: ${rule.Purpose}. Source: ${rule.SourceRuleId}. `+
                `Undirected unit axes: ${rule.Axes.map(a=>`(${a.X}, ${a.Y})`).join(', ')}. Cost ${rule.CostPerMeter} per transverse ${this.plan.world.Space.Unit} inside this polygon. `+
                'Overlapping scopes add costs. This preference does not override terrain, full-width clearance or protected content.';details.append(p);
        }
        this.details.append(details);
    }

    downloadPalette() {
        const settings=WorldSchematic.palette(this.paletteSettings,this.semantic.entries);
        const url=URL.createObjectURL(new Blob([JSON.stringify(settings,null,2)],{type:'application/json'}));
        const a=document.createElement('a');a.href=url;a.download=`${this.worldId()}-palette.json`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
        this.notify('schematic.palette.exported','Presentation palette prepared. World data is unchanged.');
    }

    updateLabelVisibility() {
        if(!this.view||!this.labelNodes)return;
        const summary=this.control('#label-summary');
        if(this.groups.labels.style.display==='none'){if(summary)summary.textContent='Labels hidden';return;}
        const frame=this.svg.getBoundingClientRect(),scale=Math.min(frame.width/this.view[2],frame.height/this.view[3]);
        if(!(scale>0))return;
        const occupied=[],candidates=[],decisions=[];let eligible=0,visibleCount=0;
        const ranked=[...this.labelNodes].sort(([a],[b])=>
            Number(b===this.selected?.id)-Number(a===this.selected?.id)||this.boxesById.get(a).depth-this.boxesById.get(b).depth||a.localeCompare(b));
        for(const [id,label] of ranked){
            let visible=label.getAttribute('data-source-label')!=='hidden'&&label.getAttribute('data-semantic-hidden')!=='true';
            if(visible&&this.focusRootId){
                visible=false;let current=id;const visited=new Set();
                while(current&&!visited.has(current)){if(current===this.focusRootId){visible=true;break;}visited.add(current);const box=this.boxesById.get(current);current=box?.parentId??box?.node.ParentId;}
            }
            label.style.display=visible?'':'none';if(!visible)continue;
            eligible++;
            const original=Number(label.getAttribute('data-source-font-size'));
            label.setAttribute('font-size',Math.max(10,Math.min(16,original*scale))/scale);
            candidates.push([id,label]);
        }
        // Complete visibility/font writes before measuring any label. Alternating
        // a write and getBoundingClientRect forces a whole SVG layout per member.
        for(const [id,label] of candidates){
            const bounds=label.getBoundingClientRect();
            let visible=bounds.width>0&&bounds.height>0&&bounds.right>=frame.left&&bounds.left<=frame.right&&bounds.bottom>=frame.top&&bounds.top<=frame.bottom;
            if(visible&&id!==this.selected?.id)visible=!occupied.some(b=>bounds.left<b.right+4&&bounds.right>b.left-4&&bounds.top<b.bottom+3&&bounds.bottom>b.top-3);
            if(visible){occupied.push(bounds);visibleCount++;}
            decisions.push([label,visible]);
        }
        for(const [label,visible] of decisions)if(!visible)label.style.display='none';
        if(summary)summary.textContent=`${visibleCount} of ${eligible} labels · zoom or select for detail`;
    }

    showPopulationDomain(nodeId) {
        for(const trace of this.plan.populationDomains||[]){
            const index=trace.NodeIds.indexOf(nodeId);if(index<0)continue;
            const d=trace.Domain,p=document.createElement('p');
            p.textContent=`Content domain: ${d.Definition.Source} / ${d.Definition.Selector}. Parent source: ${d.SourceNodeId??'world'}. `+
                `${trace.Purpose} Rule: ${trace.SourceRuleId}. Exact source: ${d.SourceIds.join(', ')}. `+
                `Required member ${index+1} of ${trace.NodeIds.length}; source geometry ${d.SourceGeometrySha256}.`;
            p.setAttribute('data-population-domain-detail',nodeId);this.details.append(p);
        }
    }
    showVariation(nodeId) {
        const node=this.plan.world.Nodes.find(n=>n.Id===nodeId);if(!node?.VariationKey)return;
        const allocation=this.plan.world.VariationPlan.Allocations.find(a=>a.Key===node.VariationKey),rule=allocation.Request.Rule;
        const heading=document.createElement('h3');heading.textContent='Catalog variation';this.details.append(heading);
        for(const text of [`Selected form: ${node.ArchetypeId}.`,rule.Purpose,`Permitted forms: ${rule.ArchetypeIds.join(', ')}.`,
            `Rule: ${rule.Id}; seed: ${rule.Seed}; source: ${rule.SourceRuleId}.`,
            'The selected catalog program retains its source dimensions and required constituents.']){
            const p=document.createElement('p');p.textContent=text;this.details.append(p);
        }
    }
    showContext(nodeId) {

        const plan=this.plan.world.ContextPlan,source=plan?.Sources.find(s=>s.NodeId===nodeId);if(!source)return;

        const heading=document.createElement('h3');heading.textContent='Region and neighbors';this.details.append(heading);

        const paragraph=text=>{const p=document.createElement('p');p.textContent=text;this.details.append(p);};

        const region=plan.Definition.Regions.find(r=>r.Id===source.RegionId);

        paragraph(`Region: ${region.Id} - ${region.Use}. Tags: ${source.Tags.join(', ')||'none'}. Source: ${source.PolicyId||'game-authored context'}.`);

        for(const e of plan.Evaluations.filter(e=>e.SiteId===nodeId||e.RuleId.startsWith('separation:')&&e.RelatedId===nodeId))

            paragraph(`${e.RuleId}: passed - ${e.RelatedId}${e.Distance==null?'':` - edge clearance ${e.Distance.toFixed(2)} / minimum ${e.Minimum} meters`}.`);

        paragraph('Engine checks cover declared region use, complete reserved geometry and authored neighbor clearance. Other source requirements remain separately qualified.');

    }



    showServices(nodeId) {

        const plan=this.plan.world.ServicePlan;if(!plan)return;

        const source=plan.Sources.find(s=>s.NodeId===nodeId);if(!source)return;

        const title=document.createElement('h3');title.textContent='Service delivery';this.details.append(title);

        const paragraph=text=>{const p=document.createElement('p');p.textContent=text;p.style.overflowWrap='anywhere';this.details.append(p);};

        for(const supply of source.Supplies)paragraph(`Supply ${supply.Kind}: ${plan.Usage[`source:${nodeId}/${supply.Id}`]} / ${supply.Quantity} ${supply.Unit} allocated.`);

        for(const allocation of plan.Allocations.filter(a=>a.ConsumerNodeId===nodeId)){

            paragraph(`${allocation.Kind}: ${allocation.Quantity} ${allocation.Unit} from ${allocation.ProviderNodeId}. ${allocation.AreaContained?'Contained resource: full allocated site lies within the source area.':allocation.Physical?`Physical delivery: ${allocation.Distance.toFixed(2)} meters, including entrance approaches.`:'Logical delivery only; physical access is unqualified.'}`);

            paragraph(allocation.AreaContained?'No transport links consumed.':`Connection chain: ${allocation.ConnectionIds.join(' → ')}.`);

            for(const use of allocation.SegmentUses||[])paragraph(`Physical passage ${use.SegmentId}: ${use.Traversals} traversal(s), ${use.Quantity} ${allocation.Unit} reserved in the shared pool.`);

        }

        paragraph(`Source: ${source.SourceId}.`);

        for(const check of plan.PolicyEvaluations?.filter(p=>p.ConsumerNodeId===nodeId)||[])

            paragraph(`Catalog supply check: ${check.RequirementId} — ${check.PolicyId}. Engine-evaluated source: ${check.SourceId}.`);

        if(source.RemainingRequirements.length)paragraph(`Unqualified requirements: ${source.RemainingRequirements.join(', ')}.`);

    }

    showProgression(nodeId=null) {
        const plan=this.plan.world.ProgressionPlan;
        if(!plan)return;
        const results=plan.Results.filter(r=>!nodeId||r.Paths.some(p=>p.NodeIds.includes(nodeId))||plan.Rules.find(rule=>rule.Id===r.RuleId)?.ScopeNodeId===nodeId);
        if(!results.length)return;
        const panel=document.createElement('details'),summary=document.createElement('summary');
        summary.textContent=`Gameplay progression: ${results.length} rule${results.length===1?'':'s'}`;panel.append(summary);
        const paragraph=text=>{const p=document.createElement('p');p.style.overflowWrap='anywhere';p.textContent=text;panel.append(p);};
        for(const result of results){
            const rule=plan.Rules.find(r=>r.Id===result.RuleId);
            paragraph(`${rule.Purpose}. Required order: ${rule.RequiredMilestones.join(' → ')||'no mandatory milestones'}.`);
            for(const path of result.Paths.slice(0,8))paragraph(`Saved journey to ${path.GoalNodeId}: ${path.NodeIds.join(' → ')}`);
            if(result.Paths.length>8)paragraph('First 8 goals shown; all paths are in the saved metadata.');
        }
        paragraph('Engine-validated logical reachability and mandatory milestones. Physical movement, inventory and encounter behavior are separate checks.');
        this.details.append(panel);
    }
    showCapacity(nodeId=null) {

        const all=this.plan.world.CapacityPlan?.Allocations||[];

        const allocations=nodeId?all.filter(a=>a.Request.ParentNodeId===nodeId||a.NodeIds.includes(nodeId)):all;

        if(!allocations.length)return;

        const panel=document.createElement('details'),summary=document.createElement('summary');

        summary.textContent=`Content capacity: ${allocations.length} demand group${allocations.length===1?'':'s'}`;panel.append(summary);

        for(const allocation of allocations.slice(0,16)){

            const item=document.createElement('p');item.style.overflowWrap='anywhere';

            item.textContent=`${allocation.Request.Rule.Purpose}: ${allocation.Request.RequiredQuantity} ${allocation.Request.Rule.Unit} required; ${allocation.Count} instances provide ${allocation.ProvidedQuantity}. Sources: ${allocation.Contributions.map(c=>`${c.Source.SourceId} (${c.NodeIds.length} × ${c.Source.Quantity})`).join(', ')}.`;

            panel.append(item);

        }

        const scope=document.createElement('p');scope.textContent='Declared content quantity only. Service delivery and complete siting remain separate checks.';

        if(allocations.length>16)scope.textContent+=' First 16 groups shown; all groups are in the metadata.';

        panel.append(scope);this.details.append(panel);

    }

    showPlacementFeedback() {
        const adaptation=this.plan.layoutAdaptation;
        if(adaptation){
            const panel=document.createElement('details'),summary=document.createElement('summary');
            summary.textContent=`Local repartition: ${adaptation.Attempts.length-1} rejected layouts, 1 accepted`;
            panel.append(summary);const description=document.createElement('p');
            description.textContent=`${adaptation.Policy.Purpose}. Only ${adaptation.Policy.LayoutId} may change. Source sizes and protected anchors are retained. Work: ${adaptation.Work} / ${adaptation.Policy.MaximumWork}.`;
            panel.append(description);const list=document.createElement('ol');
            for(const attempt of adaptation.Attempts){
                const item=document.createElement('li');item.style.overflowWrap='anywhere';
                item.textContent=`${Object.entries(attempt.Steps).map(([key,value])=>`${key}: ${value}`).join(', ')}: ${attempt.Accepted?'Accepted':attempt.Rejection}`;
                list.append(item);
            }
            panel.append(list);this.details.append(panel);
        }

        const feasibility=this.plan.feasibility;if(!feasibility)return;

        const panel=document.createElement('details'),summary=document.createElement('summary');

        summary.textContent=`Placement feasibility: ${feasibility.attempts.length-1} rejected candidates, 1 accepted`;

        panel.append(summary);

        const explanation=document.createElement('p');

        explanation.textContent=`The engine checked declared entrances and required connections before accepting placement. Feasibility work: ${feasibility.work}; total placement work: ${feasibility.totalWork}.`;

        panel.append(explanation);

        const list=document.createElement('ol');

        for(const attempt of feasibility.attempts){

            const item=document.createElement('li');item.textContent=attempt.Accepted?'Accepted':attempt.Rejection;

            item.style.overflowWrap='anywhere';list.append(item);

        }

        panel.append(list);this.details.append(panel);

    }

    applyView() {
        this.svg.setAttribute('viewBox',this.view.join(' '));
        const frame=this.svg.getBoundingClientRect(),scale=Math.min(frame.width/this.view[2],frame.height/this.view[3]);
        if(scale>0)for(const marker of this.markerNodes)marker.setAttribute('r',Math.min(Number(marker.getAttribute('data-source-radius')),5/scale));
        this.updateLabelVisibility();
    }

    zoom(factor) {

        const width = this.view[2] / factor, height = this.view[3] / factor;

        if (width < (this.plan.coordinateSpace?.startsWith('engine-') ? 1 : this.plan.width / 12) || width > this.plan.width * 3) return;

        this.view = [this.view[0] + (this.view[2] - width) / 2, this.view[1] + (this.view[3] - height) / 2, width, height]; this.applyView();

    }

    layer(name, visible) { if (this.groups[name]) this.groups[name].style.display = visible ? '' : 'none'; if(name==='labels')this.updateLabelVisibility(); }

    notify(event, message, outcome = 'succeeded') {

        this.status.textContent = message;

        const entry = { event, occurredAtUtc: new Date().toISOString(), target: this.plan?this.worldId():null, outcome };

        this.events.push(entry); if (this.events.length > 200) this.events.shift();

        this.svg.dispatchEvent(new CustomEvent('worldcreator.operation', { bubbles: true, detail: entry }));

    }

    downloadSvg() {

        try {

        this.notify('schematic.svg.prepared', 'SVG export prepared.');

        const copy = this.svg.cloneNode(true); copy.setAttribute('viewBox', this.fullMapView().join(' '));

        for(const label of copy.querySelectorAll('text[data-source-label]')){label.style.display=label.getAttribute('data-source-label')==='hidden'||label.getAttribute('data-semantic-hidden')==='true'?'none':'';label.setAttribute('font-size',label.getAttribute('data-source-font-size'));}
        for(const marker of copy.querySelectorAll('circle[data-source-radius]'))marker.setAttribute('r',marker.getAttribute('data-source-radius'));

        copy.setAttribute('width', this.plan.width); copy.setAttribute('height', this.plan.height); copy.setAttribute('font-family', 'system-ui, sans-serif');

        this.element('metadata', {}, JSON.stringify({ ...this.exportProvenance(),

            CoordinateSpace: this.plan.coordinateSpace || 'schematic-diagram-units', LayerView: this.layerView(), Palette:this.paletteSettings,
            Domain:this.domainNavigation?{CollectionId:this.domainNavigation.CollectionId,DefinitionSha256:this.domainNavigation.DefinitionSha256,CurrentDomainId:this.domainNavigation.CurrentDomainId}:null,
            SemanticFilter:{Selection:this.semanticFilter,Scope:'nodes, labels and logical connections; physical context unfiltered'}, LocalSessionEvents: this.events }), copy);

        const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(copy)], { type: 'image/svg+xml' }));

        const a = document.createElement('a'); a.href = url; a.download = `${this.worldId()}-schematic.svg`; a.click();

        setTimeout(() => URL.revokeObjectURL(url), 1000);

        } catch (error) { this.notify('schematic.svg.failed', 'Could not prepare the SVG export.', 'failed'); console.error('schematic.svg.failed', error); }

    }



    static async start() {

        const status = document.querySelector('#status');

        try {

            const source=JSON.parse(document.querySelector('#world-data').textContent);
            if(source?.StorageFormat)status.textContent='Loading verified world data…';
            const navigation=JSON.parse(document.querySelector('#domain-data').textContent);
            const world=await WorldSchematic.readStored(source);
            const Renderer=world?.Definition?.Regions&&world?.Regions?WorldRegionalSchematic:WorldSchematic;
            const viewer = new Renderer(document.querySelector('#map'), world, document.querySelector('#details'), status,navigation);
            document.querySelector('#domain-select').addEventListener('change',event=>{
                const entry=viewer.domainNavigation?.Entries.find(e=>e.Id===event.target.value);
                if(entry){viewer.notify('schematic.domain.opened',`Opening coordinate domain ${entry.Id}.`);location.assign(entry.Page);}
            });

            document.querySelector('#fit').addEventListener('click', () => viewer.fit());

            document.querySelector('#zoom-in').addEventListener('click', () => viewer.zoom(1.3));

            document.querySelector('#zoom-out').addEventListener('click', () => viewer.zoom(1 / 1.3));

            document.querySelector('#export').addEventListener('click', () => viewer.downloadSvg());

            for (const input of document.querySelectorAll('[data-toggle]')) input.addEventListener('change', () => {viewer.layer(input.dataset.toggle,input.checked);viewer.soloLayer=null;viewer.syncLayerTools();});
            document.querySelector('#layer-target').addEventListener('change',()=>viewer.syncLayerTools());
            document.querySelector('#layer-solo').addEventListener('click',()=>viewer.inspectLayer(true));
            document.querySelector('#layer-opacity').addEventListener('input',()=>viewer.inspectLayer(false));
            document.querySelector('#layer-restore').addEventListener('click',()=>viewer.restoreLayers());
            document.querySelector('#semantic-layer').addEventListener('change',event=>{viewer.semanticFilter=event.target.value;viewer.applySemanticView();});
            document.querySelector('#semantic-color').addEventListener('input',event=>{
                if(!viewer.semanticFilter.startsWith('layer:'))return;
                viewer.paletteSettings=WorldSchematic.palette({Version:1,LayerColors:{...viewer.paletteSettings.LayerColors,[viewer.semanticFilter.slice(6)]:event.target.value}},viewer.semantic.entries);
                viewer.applySemanticView();
            });
            document.querySelector('#semantic-reset').addEventListener('click',()=>viewer.resetSemanticView());
            document.querySelector('#palette-export').addEventListener('click',()=>viewer.downloadPalette());
            document.querySelector('#palette-load').addEventListener('change',async event=>{
                try{
                    const file=event.target.files[0];if(!file)return;
                    if(file.size>65536)throw new Error('Palette file exceeds 64 KiB.');
                    const settings=WorldSchematic.palette(JSON.parse(await file.text()),viewer.semantic.entries);
                    viewer.paletteSettings=settings;viewer.applySemanticView();viewer.notify('schematic.palette.loaded','Presentation palette loaded. World data is unchanged.');
                }catch(error){viewer.notify('schematic.palette.failed',`Cannot load palette: ${error.message}`,'failed');}
                finally{event.target.value='';}
            });

            document.querySelector('#load').addEventListener('change', async event => {

                try {

                    const file = event.target.files[0]; if (!file) return;

                    if (file.size > 16777216) throw new Error('Metadata file is too large (maximum 16 MiB).');

                    const source=JSON.parse(await file.text());
                    if(source?.StorageFormat)throw new Error('Open schematic.html from the served export folder to load partitioned world data.');
                    viewer.load(source);

                    for (const input of document.querySelectorAll('[data-toggle]')) viewer.layer(input.dataset.toggle, input.checked);

                } catch (error) { viewer.notify('schematic.load.failed', `Cannot load schematic: ${error.message}`, 'failed'); console.error('schematic.load.failed', error); }

                finally { event.target.value = ''; }

            });

            globalThis.worldSchematic = viewer;

        } catch (error) { status.textContent = `Cannot display schematic: ${error.message}`; console.error('schematic.start.failed', error); }

    }

}

if (typeof module !== 'undefined') module.exports = WorldSchematic;

if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', () => WorldSchematic.start());

