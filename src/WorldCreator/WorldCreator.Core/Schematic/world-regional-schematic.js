/* Shared-coordinate inspection variant. The engine owns every saved world and path. */
const RegionalSchematicBase=typeof module!=='undefined'?require('./world-schematic.js'):WorldSchematic;
class WorldRegionalSchematic extends RegionalSchematicBase {
    static address(owner,id) { return JSON.stringify([owner,id]); }
    static regionalSurfaceSources(surface,boundary) {
        const points=boundary.Vertices,minX=Math.min(...points.map(p=>p.X)),maxX=Math.max(...points.map(p=>p.X)),
            minY=Math.min(...points.map(p=>p.Y)),maxY=Math.max(...points.map(p=>p.Y));
        // Match the common native surface owner's closed conservative AABB filter.
        // Relevant sources keep their whole geometry, including seam-spanning voids.
        const relevant=shape=>!shape.Vertices.every(p=>p.X<minX-1e-8)&&!shape.Vertices.every(p=>p.X>maxX+1e-8)&&
            !shape.Vertices.every(p=>p.Y<minY-1e-8)&&!shape.Vertices.every(p=>p.Y>maxY+1e-8);
        const ordinal=(a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0;
        return {Patches:surface.Patches.filter(p=>relevant(p.Boundary)).sort((a,b)=>b.Priority-a.Priority||ordinal(a,b)),
            Exclusions:surface.Exclusions.filter(e=>relevant(e.Boundary)).sort(ordinal)};
    }
    static terrainAnalysis(analysis,world) {
        const t=analysis?.TerrainAnalysis,fail=()=>{throw new Error('Invalid terrain analysis binding. Regenerate with the engine.');};
        if(t==null){if(world?.TerrainDemandPlan!=null)fail();return;}
        const original=world?.PlacementPlan?.Definition?.SourceWorld,equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        if(!original||!t.Source||!t.Plan||!/^[0-9a-f]{64}$/.test(t.HabitatSha256)||!Number.isSafeInteger(t.PreparationWork)||t.PreparationWork<1||t.PreparationWork>analysis.Work||
            t.Source.RecipeSha256!==analysis.SourceProgram?.RecipeSha256||t.Source.CatalogSha256!==analysis.SourceProgram?.CatalogSha256||
            t.Source.RecipeSha256!==t.Plan.SourceRecipeSha256||t.Source.CatalogSha256!==t.Plan.SourceCatalogSha256)fail();
        const actual={RecipeSha256:original.RecipeSha256,CatalogSha256:original.CatalogSha256,Surface:original.Surface,
            Environment:original.EnvironmentPlan?.Definition,Regions:original.RegionPlan,LocalLayouts:original.LocalLayoutPlan};
        if(!equal({...t.Source,RecipeSha256:original.RecipeSha256},actual)||
            !equal({...t.Plan,SourceRecipeSha256:original.RecipeSha256},world.TerrainDemandPlan))fail();
        if(t.SourceAdaptation!=null){
            const a=t.SourceAdaptation,live=original.LayoutAdaptationPlan;
            if(!live||!Number.isSafeInteger(t.SourceAllocationWork)||t.SourceAllocationWork<1||t.SourceAllocationWork!==a.Work||t.SourceAllocationWork>t.PreparationWork||
                !Array.isArray(a.Attempts)||a.Attempts.length<1||a.Attempts.length>a.Policy?.MaximumAttempts||
                a.Attempts.some((v,i)=>v.Ordinal!==i+1||v.Accepted!==(i===a.Attempts.length-1))||
                !equal(a.OriginalLayouts,live.OriginalLayouts)||!equal(a.Policy,live.Policy)||a.Attempts.at(-1).LayoutSha256!==live.Attempts?.at(-1)?.LayoutSha256)fail();
        }else if((t.SourceAllocationWork??0)!==0||original.LayoutAdaptationPlan!=null)fail();
    }
    static sourceProgramComposition(analysis,source) {
        const q=analysis?.QualifiedComposition,actual=source?.ProgramCompositionPlan,equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        if(actual==null){if(q!=null)throw new Error('Unexpected qualified source composition.');return;}
        const selected=q?.Attempts?.at(-1),mixes=source.ProgramMixPlan?.Allocations??[];
        if(!q||q.Version!==1||!selected?.Accepted||!equal(q.SourceCatalog,actual.SourceCatalog)||!Array.isArray(selected.Selections)||
            selected.Selections.length!==mixes.length||selected.Selections.some(a=>!equal(a,mixes.find(v=>v.Key===a.Key)))||
            selected.RegionSelection!=null&&!equal(selected.RegionSelection,source.RegionPlan))throw new Error('Invalid qualified source composition. Regenerate with the engine.');
        this.programComposition({...source,ProgramCompositionPlan:q});
    }
    static regionAnalysis(analysis,world) {
        const r=analysis?.RegionAnalysis;if(r==null)return;
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        if(!r.Plan||!Number.isSafeInteger(r.PreparationWork)||r.PreparationWork<1||r.PreparationWork>analysis.Work||
            !equal(r.Plan,world.RegionPlan)||!equal(r.DemandPlan??null,world.RegionDemandPlan??null))throw new Error('Invalid program region analysis binding. Regenerate with the engine.');
    }
    static analysisFailures(choice,rule) {
        const failures=choice?.RejectedAnalyses;if(failures==null)return;
        const rank=n=>Number.isSafeInteger(n)&&n>=0&&n<Math.max(rule.MaximumAlternativesPerMix,rule.MaximumRegionAlternatives);
        if(!Array.isArray(failures)||failures.length<1||failures.length>rule.MaximumAttempts||failures.some((f,i)=>
            !Number.isSafeInteger(f.Ordinal)||f.Ordinal<1||f.Ordinal>rule.MaximumAttempts||i>0&&f.Ordinal<=failures[i-1].Ordinal||
            !Array.isArray(f.Prefix)||!f.Prefix.every(rank)||!Array.isArray(f.ActualRanks)||!f.ActualRanks.every(rank)||
            !Array.isArray(f.Selections)||f.RegionRequested!==true||f.Rejection!=='WORLD_REGION_INTENT_UNSATISFIED'||
            !Number.isSafeInteger(f.Work)||f.Work<1))throw new Error('Invalid rejected program analysis. Regenerate with the engine.');
    }
    static spatialContext(source,ownership,recipe,fail) {
        const c=source.SpatialContext,consumer=source.Recipe,population=consumer.PopulationSource,r=population?.Recipe??consumer;
        if(c==null){if(consumer.Regions!=null||consumer.LocalLayouts!=null||consumer.RegionDemands!=null||population?.Recipe.ProgramComposition!=null)fail();return;}
        if(!c.Purpose||!c.SourceRuleId||!source.Capacity||!consumer.ProgramComposition||!r.ProgramComposition||
            !r.Regions||r.Regions.Boundary!=null||!Array.isArray(r.Regions.Seeds)||r.Regions.Seeds.length||!Array.isArray(r.Regions.Intents)||!r.Regions.Intents.length||
            (r.Regions.ExcludedAssignments?.length??0)>0||r.Regions.RequiredAssignment!=null||r.LocalLayouts?.Layouts?.some(l=>l.Strategy!=='AreaBalanced'&&l.RegionFrame==null))fail();
        if(population!=null){
            if(population.Context?.UsePlacementBoundary!==true||consumer.Regions!=null||consumer.LocalLayouts!=null||consumer.RegionDemands!=null||consumer.LayoutAdaptation!=null)fail();
            return;
        }
        if(r.TerrainDemands!=null)fail();
        recipe.Regions.Boundary=ownership;
    }
    static authoredAnalysisPosition(node,analysis,catalog,origin) {
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),position=node.Position;
        if(position==null)return true;
        if(!Array.isArray(position)||position.length!==2||!position.every(Number.isFinite))return false;
        if(node.ParentId==null)return equal(position,origin);
        // Metadata coordinates may come from a declared relative grammar.
        // Physical sources still remain unplaced until normal allocation.
        if(analysis.FootprintDefinitions?.[node.ArchetypeId]!=null||node.StructureKey!=null)return false;
        const parent=analysis.Nodes.find(n=>n.Id===node.ParentId),type=catalog.Archetypes.find(t=>t.Id===parent?.ArchetypeId);
        return parent?.Position!=null&&type?.Parts.some(p=>p.Layout!=null&&(p.ArchetypeId===node.ArchetypeId||p.ProgramMix?.Offers?.some(o=>o.ArchetypeId===node.ArchetypeId))&&
            node.Id.startsWith(parent.Id+'/'+p.Id+'-')&&/^\d+$/.test(node.Id.slice(parent.Id.length+p.Id.length+2)))===true;
    }
    static regionalTopology(definition) {
        const s=definition?.OwnershipPlan?.Source,t=s?.Topology,p=definition?.TopologyPlan;
        const fail=()=>{throw new Error('Invalid regional topology binding. Regenerate with the engine.');};
        if(t==null){if(p!=null)fail();return 0;}
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),integer=(v,min,max)=>Number.isSafeInteger(v)&&v>=min&&v<=max,
            text=v=>typeof v==='string'&&v.trim().length>0,sort=values=>values.slice().sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);
        if(!p||p.Version!==1||t.Version!==1||!text(t.Id)||!text(t.Purpose)||!text(t.SourceRuleId)||
            !Array.isArray(t.Destinations)||!integer(t.Destinations.length,2,16)||!Array.isArray(t.Rules)||!integer(t.Rules.length,1,16)||
            !equal(t,p.Definition)||!equal(t.Destinations,sort(t.Destinations))||!equal(t.Rules.map(r=>r.Topology),sort(t.Rules.map(r=>r.Topology)))||
            !equal(s.Connections,p.SourceConnections)||!equal(definition.Connections,p.Connections)||!Array.isArray(p.Nodes)||p.Nodes.length!==t.Destinations.length+1||
            !integer(p.Work,1,definition.MaximumWork)||p.Work>definition.OwnershipPlan.Work)fail();
        const nodes=new Map(p.Nodes.map(n=>[n?.Id,n])),destinations=new Map(),owners=new Set(),root=nodes.get(s.Id);
        if(nodes.size!==p.Nodes.length||root?.ParentId!=null||root?.Kind!=='world-topology-scope'||root.ArchetypeId!=='world-topology-scope'||
            root.Purpose!==t.Purpose||!equal(root.Properties,{})||root.Position!=null||root.StructureKey!=null||root.LayerId!=null)fail();
        for(const d of t.Destinations){
            const id=s.Id+'/destinations/'+d.Id,n=nodes.get(id),e=d.Endpoint;
            if(!text(d.Id)||!text(d.Kind)||!text(d.Purpose)||!text(d.SourceRuleId)||destinations.has(id)||!e||owners.has(e.RegionId)||
                !s.Regions.some(r=>r.Id===e.RegionId)||!text(e.NodeId)||e.PortId!=null&&!text(e.PortId)||n?.ParentId!==s.Id||
                n.ArchetypeId!=='regional-destination'||n.Kind!==d.Kind||n.Purpose!==d.Purpose||!equal(n.Properties,{source_rule_id:d.SourceRuleId})||
                n.Position!=null||n.StructureKey!=null||n.LayerId!=null)fail();
            destinations.set(id,d);owners.add(e.RegionId);
        }
        const graph=p.Topology,rules=t.Rules.map(r=>r.Topology),edges=[],used=new Set(),kinds=new Set(s.Connections.map(c=>c.Kind));
        if(graph?.Version!==1||!equal(graph.Rules,rules)||!Array.isArray(graph.Results)||graph.Results.length!==rules.length||!integer(graph.Work,1,p.Work))fail();
        let work=0;
        for(let i=0;i<rules.length;i++){
            const r=rules[i],result=graph.Results[i],degrees=new Map(),pairs=new Set(),stageIds=new Set();
            if(!text(r.Id)||!text(r.Purpose)||!text(r.SourceRuleId)||!text(r.Seed)||r.ScopeNodeId!==s.Id||r.LayerId!=null||
                !destinations.has(r.StartNodeId)||!text(r.ConnectionKind)||kinds.has(r.ConnectionKind)||!integer(r.MaximumDegree,2,32)||
                !integer(r.ExtraConnections,0,32768)||!integer(r.MaximumWork,1,10000000)||(r.Directed!==undefined&&typeof r.Directed!=='boolean')||(r.ReturnConnections!==undefined&&!integer(r.ReturnConnections,0,r.ExtraConnections))||((r.ReturnConnections??0)>0&&r.Directed!==true)||!Array.isArray(r.Stages)||!integer(r.Stages.length,1,16)||
                result?.RuleId!==r.Id||!integer(result.Work,1,r.MaximumWork)||!Array.isArray(result.Stages)||result.Stages.length!==r.Stages.length||
                !Array.isArray(result.Connections))fail();
            work+=result.Work;kinds.add(r.ConnectionKind);
            const landmarks=[r.StartNodeId,...r.Stages.map(v=>v.ExitNodeId)],memberKinds=new Set();let entry=r.StartNodeId,members=0,loops=0;
            if(new Set(landmarks).size!==landmarks.length||landmarks.some(id=>!destinations.has(id)))fail();
            for(let j=0;j<r.Stages.length;j++){
                const stage=r.Stages[j],m=result.Stages[j];
                if(!text(stage.Id)||stageIds.has(stage.Id)||!text(stage.Purpose)||!Array.isArray(stage.MemberKinds)||stage.MemberKinds.some(k=>!text(k)||memberKinds.has(k))||
                    new Set(stage.MemberKinds).size!==stage.MemberKinds.length||!integer(stage.MinimumMembers,0,254)||!integer(stage.MaximumMembers,stage.MinimumMembers,254)||
                    !integer(stage.MinimumExtraConnections??0,0,r.ExtraConnections)||
                    (stage.MaximumExtraConnections!=null&&!integer(stage.MaximumExtraConnections,stage.MinimumExtraConnections??0,32768)))fail();
                stageIds.add(stage.Id);stage.MemberKinds.forEach(k=>memberKinds.add(k));
                const selected=[...destinations].filter(([id,d])=>!landmarks.includes(id)&&stage.MemberKinds.includes(d.Kind)).map(([id])=>id).sort();
                if(selected.length<stage.MinimumMembers||selected.length>stage.MaximumMembers||m?.StageId!==stage.Id||m.EntryNodeId!==entry||
                    m.ExitNodeId!==stage.ExitNodeId||!equal(m.MemberNodeIds,selected))fail();
                members+=selected.length;entry=stage.ExitNodeId;
            }
            if(result.Connections.length!==members+landmarks.length-1+r.ExtraConnections)fail();
            result.Connections.forEach((edge,index)=>{
                const stage=result.Stages.find(v=>v.StageId===edge.Properties?.stage),local=stage?[stage.EntryNodeId,stage.ExitNodeId,...stage.MemberNodeIds]:[];
                const pair=[edge.FromId,edge.ToId].sort().join('\n');
                if(edge.Id!==s.Id+'/topology/'+r.Id+'/edge-'+String(index).padStart(5,'0')||edge.Kind!==r.ConnectionKind||edge.SourceRuleId!==r.SourceRuleId||
                    edge.Bidirectional!==(r.Directed!==true)||edge.LayerId!=null||edge.FromId===edge.ToId||!local.includes(edge.FromId)||!local.includes(edge.ToId)||pairs.has(pair)||
                    edge.Properties.topology_rule!==r.Id||edge.Properties.purpose!==r.Purpose||!['stage-tree','stage-loop'].includes(edge.Properties.reason))fail();
                if(edge.Properties.reason==='stage-loop')loops++;
                if(edge.Properties.return!==undefined&&(edge.Properties.return!==true||edge.Properties.reason!=='stage-loop'||r.Directed!==true))fail();
                pairs.add(pair);for(const id of [edge.FromId,edge.ToId]){used.add(id);degrees.set(id,(degrees.get(id)??0)+1);if(degrees.get(id)>r.MaximumDegree)fail();}
                edges.push({edge,rule:t.Rules[i]});
            });
            if(loops!==r.ExtraConnections||result.Connections.filter(e=>e.Properties.return===true).length!==(r.ReturnConnections??0)||r.Stages.some(stage=>{const count=result.Connections.filter(e=>e.Properties.reason==='stage-loop'&&e.Properties.stage===stage.Id).length;
                return count<(stage.MinimumExtraConnections??0)||(stage.MaximumExtraConnections!=null&&count>stage.MaximumExtraConnections);}))fail();
            for(const stage of result.Stages){
                const required=[stage.EntryNodeId,stage.ExitNodeId,...stage.MemberNodeIds],reached=new Set([stage.EntryNodeId]),queue=[stage.EntryNodeId];
                for(let index=0;index<queue.length;index++)for(const edge of result.Connections.filter(c=>c.Properties.stage===stage.StageId)){
                    const next=edge.FromId===queue[index]?edge.ToId:edge.Bidirectional&&edge.ToId===queue[index]?edge.FromId:null;
                    if(next!=null&&!reached.has(next)){reached.add(next);queue.push(next);}
                }
                if(required.some(id=>!reached.has(id)))fail();
                if(r.Directed===true){
                    const local=result.Connections.filter(c=>c.Properties.stage===stage.StageId),localEdges=local.filter(c=>c.Properties.return!==true),indegree=new Map(required.map(id=>[id,0]));
                    const tree=local.filter(c=>c.Properties.reason==='stage-tree');
                    for(const back of local.filter(c=>c.Properties.return===true)){
                        const visited=new Set([back.ToId]),pending=[back.ToId];
                        for(let k=0;k<pending.length;k++)for(const edge of tree.filter(c=>c.FromId===pending[k]))if(!visited.has(edge.ToId)){visited.add(edge.ToId);pending.push(edge.ToId);}
                        if(!visited.has(back.FromId))fail();
                    }
                    for(const edge of localEdges)indegree.set(edge.ToId,indegree.get(edge.ToId)+1);
                    const ready=required.filter(id=>indegree.get(id)===0);let count=0;
                    for(let index=0;index<ready.length;index++){
                        const id=ready[index];count++;
                        for(const edge of localEdges.filter(c=>c.FromId===id)){indegree.set(edge.ToId,indegree.get(edge.ToId)-1);if(indegree.get(edge.ToId)===0)ready.push(edge.ToId);}
                    }
                    if(count!==required.length)fail();
                }
            }
        }
        if(work!==graph.Work||used.size!==destinations.size||[...destinations.keys()].some(id=>!used.has(id))||
            !Array.isArray(p.Links)||p.Links.length!==edges.length||new Set(p.Links.map(l=>l.RegionalConnectionId)).size!==edges.length||
            !Array.isArray(p.Connections)||p.Connections.length!==p.SourceConnections.length+edges.length||
            !integer(p.Progression?.Work,1,p.Work)||
            p.Work!==1+s.Regions.length+s.Connections.length+2*t.Destinations.length+2*t.Rules.length+edges.length+graph.Work+p.Progression.Work)fail();
        const generated=new Set();
        edges.forEach(({edge,rule},i)=>{
            const link=p.Links[i],actual=p.Connections.find(c=>c.Id===link?.RegionalConnectionId),r=rule.Topology;
            if(link?.LogicalConnectionId!==edge.Id||!/^topology-[0-9a-f]{32}$/.test(link.RegionalConnectionId)||!actual||
                actual.Kind!==edge.Kind||actual.Purpose!==r.Purpose||actual.SourceRuleId!==r.SourceRuleId||actual.Bidirectional!==edge.Bidirectional||
                !equal(actual.From,destinations.get(edge.FromId).Endpoint)||!equal(actual.To,destinations.get(edge.ToId).Endpoint)||
                actual.OwnerRegionId!==actual.From.RegionId||!equal(actual.Profile,rule.Profile)||!equal(actual.Search,rule.Search)||
                !equal(actual.RoutingDomain,rule.RoutingDomain??s.Surface.Boundary))fail();
            generated.add(actual.Id);
        });
        if(!equal(p.Connections.filter(c=>!generated.has(c.Id)),p.SourceConnections))fail();
        const progression=rules.map(r=>({Id:'topology-'+r.Id,Purpose:r.Purpose,SourceRuleId:r.SourceRuleId,ScopeNodeId:r.ScopeNodeId,StartNodeId:r.StartNodeId,
            GoalNodeIds:[r.Stages.at(-1).ExitNodeId],ConnectionKinds:[r.ConnectionKind],RequiredMilestones:r.Stages.slice(0,-1).map(v=>v.ExitNodeId),MaximumWork:r.MaximumWork,Capabilities:null}));
        if(!equal(p.Progression.Rules,progression))fail();
        this.progression({ProgressionPlan:p.Progression,Connections:edges.map(e=>e.edge)},nodes);
        return p.Work;
    }
    static componentDomains(search,regions,maximumWork,fail) {
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),map=new Map();
        if(!search||!Array.isArray(search.Domains)||search.Domains.length!==regions.length||!Number.isSafeInteger(search.Work)||search.Work<1||search.Work>maximumWork||typeof search.Truncated!=='boolean')fail();
        search.Domains.forEach((d,i)=>{
            if(d?.RegionId!==regions[i].Id||!/^[0-9a-f]{64}$/.test(d.ContextSha256)||!Array.isArray(d.Options)||d.Options.length<1||d.Options.length>4096||
                new Set(d.Options.map(o=>o.Component)).size!==d.Options.length||d.Options.some(o=>o.IntentId!==d.RegionId||o.Eligible!==true||o.Rejection!=null||
                    !Number.isSafeInteger(o.Component)||o.Component<0||o.Component>4095||!Number.isFinite(o.Area)||o.Area<=0||!o.Anchor||
                    ![o.Anchor.Position?.X,o.Anchor.Position?.Y,o.Anchor.Clearance,o.Anchor.UpperBound].every(Number.isFinite)||o.Anchor.Clearance<=0||o.Anchor.UpperBound<o.Anchor.Clearance))fail();
            map.set(d.RegionId,d.Options);
        });
        return choices=>{
            if(!Array.isArray(choices)||choices.length!==regions.length||choices.some((c,i)=>c?.RegionId!==regions[i].Id||
                !map.get(c.RegionId).some(o=>o.Component===c.Component&&equal(o.Anchor.Position,c.Position))))fail();
        };
    }
    static networkFailure(failure,definition,fail) {
        const rule=definition.NetworkRules?.find(r=>r.Id===failure?.RuleId),p=failure?.Plan,equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        if(!rule||!p||p.Id!==definition.Id+'/regional-network/'+rule.Id||!equal(p.Options,rule.Options)||
            !['RequiredConnectionUnresolved','CandidateGraphDisconnected','RedundancyUnresolved'].includes(p.Status)||
            !Array.isArray(p.Destinations)||p.Destinations.length!==rule.Destinations.length||
            p.Destinations.some((d,i)=>d.Id!==rule.Destinations[i].Id||d.Purpose!==rule.Destinations[i].Purpose||![d.Position?.X,d.Position?.Y].every(Number.isFinite))||
            !Array.isArray(p.Attempts)||(p.Attempts.length===0&&p.Status!=='CandidateGraphDisconnected')||p.Attempts.length>rule.Options.MaximumPairChecks||!Array.isArray(p.Edges)||
            !Number.isSafeInteger(p.Work)||p.Work<0||(p.Attempts.length===0?p.Work!==0:p.Work<1)||p.Work>rule.Options.MaximumWork)fail();
        if(p.Attempts.length===0){
            const pair=(a,b)=>JSON.stringify([a,b].sort()),blocked=new Set((rule.Options.ForbiddenConnections||[]).map(f=>pair(f.FromId,f.ToId)));
            if(p.Edges.length!==0||p.Destinations.some((a,i)=>p.Destinations.slice(i+1).some(b=>!blocked.has(pair(a.Id,b.Id)))))fail();
        }
    }
    static partitionChoices(choices,source,fail) {
        const keys=[];const visit=(first,members)=>{if(members<2)return;keys.push([first,members]);const n=Math.floor(members/2);visit(first,n);visit(first+n,members-n);};visit(0,source.Regions.length);
        keys.sort((a,b)=>a[0]-b[0]||b[1]-a[1]);const policy=source.Pattern.PartitionSearch,offsets=policy?.AxisOffsetsDegrees,alternates=policy?.AlternateBends,positions=policy?.AlternateBendOffsets;
        if(!Array.isArray(choices)||choices.length!==(offsets?keys.length:0)||choices.some((c,i)=>c.FirstMember!==keys[i][0]||c.Members!==keys[i][1]||!offsets.includes(c.AxisOffsetDegrees)||
            (alternates==null?c.Bend!=null:!Number.isFinite(c.Bend)||![policy.Bend??0,...alternates].includes(c.Bend))||
            (positions==null?c.BendOffset!=null:!Number.isFinite(c.BendOffset)||![policy.BendOffset??0,...positions].includes(c.BendOffset)||
                (c.Bend??policy.Bend??0)===0&&c.BendOffset!==(policy.BendOffset??0))))fail();
    }
    static partitionFailure(q,source,maximumWork,fail) {
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),digest=h=>typeof h==='string'&&/^[0-9a-f]{64}$/.test(h);
        if(!q||!Array.isArray(q.Attempts)||q.Attempts.length>65536||q.SelectedAttempts?.length!==0||!Number.isSafeInteger(q.Work)||q.Work<0||q.Work>maximumWork||q.Work!==q.Attempts.reduce((n,a)=>n+a.Work,0))fail();
        let refs=0;
        q.Attempts.forEach((a,i)=>{
            const r=source.Regions.find(r=>r.Id===a.RegionId),request=a.Request,intent=request?.Intents?.[0];
            if(!r||a.Ordinal!==i+1||!digest(a.StratumSha256)||!Number.isSafeInteger(a.Work)||a.Work<1||typeof a.Qualified!=='boolean'||
                !Array.isArray(a.Ownership?.Vertices)||a.Ownership.Vertices.length<3||a.Ownership.Vertices.length>64||a.Ownership.Vertices.some(p=>![p.X,p.Y].every(Number.isFinite))||
                request.Id!==r.Id||!equal(request.Boundary,a.Ownership)||!equal(intent?.RequiredPosition??null,r.PinnedAnchor?.Position??null)||
                request.Intents.length!==1||intent.MinimumArea<r.MinimumArea||intent.MinimumClearance<r.MinimumClearance||request.Profile?.Id!==r.Profile.Id||
                request.RequiredAssignment!=null||request.ExcludedAssignments!=null||request.IntentPartition!=null)fail();
            if(a.Qualified){if(!digest(a.AnchorPlanSha256)||a.Failure!=null||a.Rejection!=null)fail();RegionalSchematicBase.regionIntentSeeds({Request:request,IntentPlan:a.IntentPlan});}
            else {if(a.AnchorPlanSha256!=null||a.IntentPlan!=null||a.Rejection!=='WORLD_REGION_INTENT_UNSATISFIED'||!a.Failure)fail();RegionalSchematicBase.regionIntentSeeds({Request:request,IntentPlan:{...a.Failure,Selections:[]}},true);}
            refs+=a.Ownership.Vertices.length+(a.IntentPlan?.Candidates.length??a.Failure.Candidates.length);if(refs>65536)fail();
        });
    }
    static physicalDerivation(definition,fail,topologyWork) {
        const area=p=>Math.abs(p.Vertices.reduce((sum,a,i)=>{const b=p.Vertices[(i+1)%p.Vertices.length];return sum+a.X*b.Y-b.X*a.Y;},0))/2;
        const p=definition.OwnershipPlan,s=p.Source,physical=s.PhysicalFeedback,feedback=p.PhysicalFeedbackPlan;
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),finite=n=>Number.isFinite(n)&&n>0,
            digest=h=>typeof h==='string'&&/^[0-9a-f]{64}$/.test(h),near=(a,b)=>Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        if(physical==null){if(feedback!=null||p.Anchors.some(a=>a.Plan.Request.ExcludedAssignments?.length))fail();}else{
            if(!physical.Purpose||!physical.SourceRuleId||!Number.isSafeInteger(physical.MaximumAttempts)||physical.MaximumAttempts<1||physical.MaximumAttempts>32||
                !feedback||!Array.isArray(feedback.Attempts)||feedback.Attempts.length<1||feedback.Attempts.length>physical.MaximumAttempts)fail();
            if(!Array.isArray(feedback.Contexts)||feedback.Contexts.length<1||feedback.Contexts.length>feedback.Attempts.length||
                !Number.isSafeInteger(feedback.PartitionSearchWork)||feedback.PartitionSearchWork<0)fail();
            let work=feedback.PartitionSearchWork;
            feedback.Attempts.forEach((a,i)=>{if(a.Ordinal!==i+1||!Number.isSafeInteger(a.ContextOrdinal)||a.ContextOrdinal<1||a.ContextOrdinal>feedback.Contexts.length||
                typeof a.PlanningRejected!=='boolean'||i>0&&(a.ContextOrdinal<feedback.Attempts[i-1].ContextOrdinal||a.ContextOrdinal>feedback.Attempts[i-1].ContextOrdinal+1))fail();});
            const partitionQueue=[],partitionSeen=new Set(),enabled=s.Pattern.PartitionSearch?.AxisOffsetsDegrees.length>1||s.Pattern.PartitionSearch?.AlternateBends?.length>0||s.Pattern.PartitionSearch?.AlternateBendOffsets?.length>0;let partitionWork=0;
            for(const [ci,context] of feedback.Contexts.entries()){
            const group=feedback.Attempts.filter(a=>a.ContextOrdinal===ci+1),last=ci===feedback.Contexts.length-1;
            if(context.Ordinal!==ci+1||!group.length||ci===0&&context.RequestedPartitions!=null||ci>0&&!equal(context.RequestedPartitions,partitionQueue.shift()))fail();
            if(context.SelectedPartitions!=null){
                this.partitionChoices(context.SelectedPartitions,s,fail);
                if(context.RequestedPartitions!=null&&!equal(context.SelectedPartitions,context.RequestedPartitions)||!digest(context.OwnershipSha256)||
                    !Array.isArray(context.Masks)||context.Masks.length!==s.Regions.length||context.Masks.some((m,i)=>m.Id!==s.Regions[i].Id||!equal(m.Ownership,m.GenerationDomain)||
                    !Array.isArray(m.Ownership?.Vertices)||m.Ownership.Vertices.length<3||m.Ownership.Vertices.length>64||
                    m.Ownership.Vertices.some(p=>![p.X,p.Y].every(Number.isFinite))))fail();
                if(enabled){partitionWork+=context.SelectedPartitions.length+1;partitionSeen.add(JSON.stringify(context.SelectedPartitions));}
                if(last){
                    const actual=(p.Partition.PartitionAttempts??[]).filter(a=>a.Status==='Selected').map(a=>({FirstMember:a.FirstMember,Members:a.Members,AxisOffsetDegrees:a.AxisOffsetDegrees,...(a.Bend==null?{}:{Bend:a.Bend}),...(a.BendOffset==null?{}:{BendOffset:a.BendOffset})})).sort((a,b)=>a.FirstMember-b.FirstMember||b.Members-a.Members);
                    if(!equal(actual,context.SelectedPartitions)||!equal(context.Masks,definition.Regions.map(r=>({Id:r.Id,Ownership:r.Ownership,GenerationDomain:r.GenerationDomain}))))fail();
                }
            }else{
                const a=group[0];this.partitionChoices(context.RequestedPartitions,s,fail);
                if(ci===0||last||group.length!==1||!a.PlanningRejected||a.CandidateSha256!=null||a.Accepted||a.GeometrySha256!=null||a.Selections?.length!==0||
                    a.RequestedComponents!=null||!Number.isSafeInteger(a.PlanningWork)||a.PlanningWork<1||a.QualificationWork!==0||!a.Rejection||
                    a.FailureRegionId!=null||a.ConnectionId!=null||a.RouteStatus!=null||a.DomainDisconnection!=null||a.NetworkFailure!=null||a.PlacementFailure!=null||
                    context.OwnershipSha256!=null||context.Masks!=null||context.ComponentSearch!=null)fail();
                this.partitionFailure(a.PartitionFailure,s,a.PlanningWork,fail);work+=a.PlanningWork;
            }
            if(!last){
                const choices=context.SelectedPartitions??context.RequestedPartitions;
                if(!enabled)fail();partitionWork+=choices.length+1;partitionSeen.add(JSON.stringify(choices));
                for(let j=0;j<choices.length;j++){
                    for(const offset of s.Pattern.PartitionSearch.AxisOffsetsDegrees){
                        partitionWork+=choices.length+1;if(offset===choices[j].AxisOffsetDegrees)continue;
                        const next=choices.map((c,k)=>j===k?{...c,AxisOffsetDegrees:offset}:c),key=JSON.stringify(next);
                        if(partitionSeen.has(key)||partitionSeen.size>=physical.MaximumAttempts)continue;partitionSeen.add(key);partitionQueue.push(next);
                    }
                    const alternates=s.Pattern.PartitionSearch.AlternateBends;
                    if(alternates!=null)for(const bend of [s.Pattern.PartitionSearch.Bend??0,...alternates]){
                        partitionWork+=choices.length+1;if(bend===choices[j].Bend)continue;
                        const next=choices.map((c,k)=>j===k?{...c,Bend:bend,...(bend===0&&s.Pattern.PartitionSearch.AlternateBendOffsets!=null?{BendOffset:s.Pattern.PartitionSearch.BendOffset??0}:{})}:c),key=JSON.stringify(next);
                        if(partitionSeen.has(key)||partitionSeen.size>=physical.MaximumAttempts)continue;partitionSeen.add(key);partitionQueue.push(next);
                    }
                    const positions=s.Pattern.PartitionSearch.AlternateBendOffsets;
                    if(positions!=null&&(choices[j].Bend??s.Pattern.PartitionSearch.Bend??0)!==0)for(const position of [s.Pattern.PartitionSearch.BendOffset??0,...positions]){
                        partitionWork+=choices.length+1;if(position===choices[j].BendOffset)continue;
                        const next=choices.map((c,k)=>j===k?{...c,BendOffset:position}:c),key=JSON.stringify(next);
                        if(partitionSeen.has(key)||partitionSeen.size>=physical.MaximumAttempts)continue;partitionSeen.add(key);partitionQueue.push(next);
                    }
                }
            }
            if(context.SelectedPartitions==null)continue;
            const search=context.ComponentSearch,validateChoices=this.componentDomains(search,s.Regions,s.MaximumWork,fail),seen=new Set(),queue=[];
            const key=choices=>JSON.stringify(choices.map(c=>[c.RegionId,c.Component]));work+=search.Work;
            group.forEach((attempt,i)=>{
                if(attempt.PlanningRejected||attempt.PartitionFailure!=null||!digest(attempt.CandidateSha256)||!Number.isSafeInteger(attempt.PlanningWork)||attempt.PlanningWork<1||
                    !Number.isSafeInteger(attempt.QualificationWork)||attempt.QualificationWork<1||attempt.Accepted!==(attempt.Ordinal===feedback.Attempts.length)||
                    !Array.isArray(attempt.Selections)||attempt.Selections.length!==s.Regions.length)fail();
                work+=attempt.PlanningWork+attempt.QualificationWork;
                validateChoices(attempt.Selections);
                if(i===0){if(attempt.RequestedComponents!=null)fail();seen.add(key(attempt.Selections));}
                else{const expected=queue.shift();if(!expected||!equal(expected,attempt.RequestedComponents)||!equal(expected,attempt.Selections))fail();}
                attempt.Selections.forEach((choice,j)=>{
                    if(choice.RegionId!==s.Regions[j].Id||!Number.isSafeInteger(choice.Component)||choice.Component<0||choice.Component>4095||
                        !choice.Position||![choice.Position.X,choice.Position.Y].every(Number.isFinite))fail();
                });
                if(attempt.Accepted){
                    if(!digest(attempt.GeometrySha256)||attempt.FailureRegionId!=null||attempt.ConnectionId!=null||attempt.RouteStatus!=null||attempt.Rejection!=null||attempt.DomainDisconnection!=null||attempt.NetworkFailure!=null||attempt.PlacementFailure!=null)fail();
                    attempt.Selections.forEach((choice,j)=>{
                        const actual=p.Anchors[j].Plan.IntentPlan?.Selections?.[0];
                        if(!actual||choice.Component!==actual.Component||!equal(choice.Position,actual.Seed.Position))fail();
                    });
                }else{
                    let priority;
                    if(attempt.PlacementFailure!=null){
                        const f=attempt.PlacementFailure;
                        if(!s.Regions.some(r=>r.Id===f.RegionId)||f.Diagnostic!==attempt.Rejection||typeof f.Diagnostic!=='string'||!f.Diagnostic||
                            attempt.FailureRegionId!=null||attempt.ConnectionId!=null||attempt.RouteStatus!=null||attempt.DomainDisconnection!=null||
                            attempt.NetworkFailure!=null||attempt.GeometrySha256!=null)fail();
                        priority=[f.RegionId];
                    }else if(attempt.NetworkFailure!=null){
                        this.networkFailure(attempt.NetworkFailure,definition,fail);
                        if(attempt.FailureRegionId!=null||attempt.ConnectionId!=null||attempt.RouteStatus!=null||attempt.DomainDisconnection!=null||attempt.GeometrySha256!=null||
                            attempt.Rejection!==`METADATA_REGIONAL_NETWORK_UNRESOLVED:${attempt.NetworkFailure.RuleId}:${attempt.NetworkFailure.Plan.Status}`)fail();
                        priority=[...new Set(definition.NetworkRules.find(r=>r.Id===attempt.NetworkFailure.RuleId).Destinations.map(d=>d.Endpoint.RegionId))].sort();
                    }else{
                    const connection=definition.Connections.find(c=>c.Id===attempt.ConnectionId),choice=attempt.Selections.find(c=>c.RegionId===attempt.FailureRegionId);
                    if(!connection||connection.OwnerRegionId!==attempt.FailureRegionId||!choice||attempt.GeometrySha256!=null||
                        !['CandidateGraphDisconnected','TraceRejected'].includes(attempt.RouteStatus)||
                        attempt.Rejection!==`METADATA_REGIONAL_CONNECTION_UNRESOLVED:${attempt.ConnectionId}:${attempt.RouteStatus}`)fail();
                    const witness=attempt.DomainDisconnection;
                    if(witness!=null){
                        const validPoint=p=>p&&[p.X,p.Y].every(Number.isFinite),validIndexes=values=>Array.isArray(values)&&values.length>0&&
                            new Set(values).size===values.length&&values.every(n=>Number.isSafeInteger(n)&&n>=0&&n<witness.Components.length);
                        if(attempt.RouteStatus!=='CandidateGraphDisconnected'||!equal(witness.Boundary,connection.RoutingDomain)||
                            witness.ActorRadius!==connection.Profile.Radius||!finite(witness.ActorRadius)||!validPoint(witness.From)||!validPoint(witness.To)||
                            !Number.isSafeInteger(witness.Work)||witness.Work<1||witness.Work>attempt.QualificationWork||
                            !Array.isArray(witness.Components)||witness.Components.length<2||witness.Components.length>4096||
                            !validIndexes(witness.FromComponents)||!validIndexes(witness.ToComponents)||witness.FromComponents.some(n=>witness.ToComponents.includes(n)))fail();
                        let cells=0;
                        for(const component of witness.Components){
                            if(!Array.isArray(component.Cells)||component.Cells.length<1||!finite(component.Area))fail();
                            cells+=component.Cells.length;if(cells>4096)fail();
                            if(component.Cells.some(c=>!Array.isArray(c.Vertices)||c.Vertices.length<3||c.Vertices.length>64||!c.Vertices.every(validPoint))||
                                !near(component.Area,component.Cells.reduce((sum,c)=>sum+area(c),0)))fail();
                        }
                    }
                    priority=[...new Set([attempt.FailureRegionId,connection.From.RegionId,connection.To.RegionId])];
                    }
                    const domains=search.Domains.slice().sort((a,b)=>{
                        const ai=priority.indexOf(a.RegionId),bi=priority.indexOf(b.RegionId);return (ai<0?Infinity:ai)-(bi<0?Infinity:bi)||(a.RegionId<b.RegionId?-1:a.RegionId>b.RegionId?1:0);
                    });
                    for(const domain of domains)for(const option of domain.Options){
                        if(option.Component===attempt.Selections.find(c=>c.RegionId===domain.RegionId).Component)continue;
                        const next=attempt.Selections.map(c=>c.RegionId===domain.RegionId?{RegionId:c.RegionId,Component:option.Component,Position:option.Anchor.Position}:c),k=key(next);
                        if(seen.has(k)||seen.size>=physical.MaximumAttempts)continue;seen.add(k);queue.push(next);
                    }
                }
            });
            if(!last&&queue.length)fail();
            }
            if(partitionWork!==feedback.PartitionSearchWork||work!==(p.CompositionPlan?.Attempts?.at(-1)?.Work??p.Work-topologyWork))fail();
            p.Anchors.forEach(a=>{const requested=feedback.Attempts.at(-1).RequestedComponents?.find(c=>c.RegionId===a.RegionId);
                if((a.Plan.Request.ExcludedAssignments?.length??0)>0||!equal(a.Plan.Request.RequiredAssignment??null,requested?[requested.Component]:null))fail();});
        }
    }
    static partitionQualification(owner,effective,definition,fail) {
        const q=owner.PartitionQualification,s=owner.Source,equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),digest=v=>typeof v==='string'&&/^[0-9a-f]{64}$/.test(v);
        const work=v=>Number.isSafeInteger(v)&&v>0&&v<=s.MaximumWork,point=p=>p&&[p.X,p.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=1e7);
        if(!q||!Array.isArray(q.Attempts)||q.Attempts.length<effective.length||q.Attempts.length>65536||!Array.isArray(q.SelectedAttempts)||
            q.SelectedAttempts.length!==effective.length||new Set(q.SelectedAttempts).size!==effective.length||!work(q.Work)||
            q.Work!==q.Attempts.reduce((n,a)=>n+a.Work,0)||owner.Work<owner.Partition.Work+q.Work)fail();
        let references=0;
        for(let i=0;i<q.Attempts.length;i++){
            const a=q.Attempts[i],r=effective.find(r=>r.Id===a?.RegionId),request=a?.Request;
            if(!r||a.Ordinal!==i+1||!digest(a.StratumSha256)||!work(a.Work)||typeof a.Qualified!=='boolean'||
                !Array.isArray(a.Ownership?.Vertices)||a.Ownership.Vertices.length<3||a.Ownership.Vertices.length>64||!a.Ownership.Vertices.every(point)||
                request?.Id!==r.Id||request.Purpose!==r.Purpose||request.SourceRuleId!==r.SourceRuleId||!equal(request.Boundary,a.Ownership)||
                !Array.isArray(request.Seeds)||request.Seeds.length||request.Intents?.length!==1||request.Intents[0].Id!==r.Id||
                request.Intents[0].MinimumArea!==r.MinimumArea||request.Intents[0].MinimumClearance!==r.MinimumClearance||
                !equal(request.Intents[0].RequiredPosition??null,r.PinnedAnchor?.Position??null)||request.Profile?.Id!==r.Profile?.Id||
                request.RequiredAssignment!=null||request.ExcludedAssignments!=null||request.IntentPartition!=null)fail();
            if(a.Qualified){
                if(!digest(a.AnchorPlanSha256)||a.Failure!=null||a.Rejection!=null||!a.IntentPlan)fail();
                RegionalSchematicBase.regionIntentSeeds({Request:request,IntentPlan:a.IntentPlan});
            }else{
                if(a.AnchorPlanSha256!=null||a.IntentPlan!=null||a.Rejection!=='WORLD_REGION_INTENT_UNSATISFIED'||!a.Failure)fail();
                RegionalSchematicBase.regionIntentSeeds({Request:request,IntentPlan:{...a.Failure,Selections:[]}},true);
            }
            references+=a.Ownership.Vertices.length+(a.IntentPlan?.Candidates.length??a.Failure.Candidates.length);if(references>65536)fail();
        }
        for(let i=0;i<effective.length;i++){
            const index=q.SelectedAttempts[i],a=q.Attempts[index-1],main=owner.Anchors[i]?.Plan;
            if(!Number.isSafeInteger(index)||index<1||index>q.Attempts.length||!a?.Qualified||a.RegionId!==effective[i].Id||!equal(a.Ownership,definition.Regions[i].Ownership)||!main)fail();
            const request={...main.Request},base={...a.Request};
            for(const key of ['ExcludedAssignments','RequiredAssignment']){delete request[key];delete base[key];}
            if(!equal(request,base)||!equal(main.IntentPlan?.Candidates,a.IntentPlan.Candidates))fail();
            if(main.Request.RequiredAssignment==null&&main.Request.ExcludedAssignments==null&&!equal(main.IntentPlan,a.IntentPlan))fail();
        }
        for(const a of q.Attempts.filter(a=>!a.Qualified)){
            if(!owner.Partition.PartitionAttempts?.some(p=>p.Status==='Rejected'&&p.Rejection?.endsWith('WORLD_AREA_QUALIFICATION_REJECTED:'+a.RegionId)))fail();
        }
    }
    static ownershipDerivation(definition) {
        const p=definition?.OwnershipPlan,fail=()=>{throw new Error('Invalid regional ownership derivation. Regenerate with the engine.');};
        const topologyWork=this.regionalTopology(definition);
        if(p==null){if(definition?.OwnershipDerivationId!=null)fail();return null;}
        const s=p.Source,a=p.Partition,equal=(x,y)=>JSON.stringify(x)===JSON.stringify(y),finite=n=>Number.isFinite(n)&&n>0;
        if(!equal(s?.NetworkRules??null,definition.NetworkRules??null))fail();
        const near=(x,y)=>Number.isFinite(x)&&Number.isFinite(y)&&Math.abs(x-y)<=1e-8*Math.max(1,Math.abs(x),Math.abs(y));
        const density=s?.Pattern?.DensityFieldId,fields=s?.OwnershipEnvironment;
        if(density!=null?(!fields||fields.SchemaVersion!==1||!Array.isArray(fields.Fields)||!fields.Fields.some(f=>f.Id===density)):fields!=null)fail();
        const demands=p.ProgramDemands??[],policies=s?.Regions?.filter(r=>r.Capacity!=null)??[];
        if(!Array.isArray(demands)||demands.length!==policies.length||new Set(demands.map(d=>d?.RegionId)).size!==demands.length)fail();
        const effective=s?.Regions?.map(r=>{
            const d=demands.find(d=>d?.RegionId===r.Id);if(r.Capacity==null){if(d)fail();return r;}
            if(!d||!finite(r.Capacity.MaximumCoverage)||r.Capacity.MaximumCoverage>1||!r.Capacity.Purpose||!r.Capacity.SourceRuleId||
                d.Analysis?.Version!==1||d.Analysis.RegionSelectionDeferred===true||!Array.isArray(d.Analysis.Nodes)||!d.Analysis.Nodes.length||d.Analysis.Nodes.length>100000||
                d.Analysis.Nodes.some(n=>n.Position!=null&&(s.Coupling==null||!this.authoredAnalysisPosition(n,d.Analysis,r.Catalog,definition.Regions.find(x=>x.Id===r.Id)?.Recipe.Origin)))||
                !Array.isArray(d.Analysis.Connections)||d.Analysis.Connections.length>500000||!Array.isArray(d.Analysis.RelativePositions)||!d.Analysis.FootprintDefinitions||!d.Analysis.StructureDefinitions||
                !Array.isArray(d.Analysis.Dependencies)||d.Analysis.Dependencies.length>256||!Array.isArray(d.Sources)||!d.Sources.length||d.Sources.length>100000||
                new Set(d.Sources.map(s=>s.Source?.NodeId)).size!==d.Sources.length||d.Sources.some(s=>!finite(s.EnvelopeArea)||!finite(s.AnchorRadius)||!d.Analysis.Nodes.some(n=>n.Id===s.Source?.NodeId))||
                !near(d.EnvelopeArea,d.Sources.reduce((n,s)=>n+s.EnvelopeArea,0))||!near(d.RequiredArea,Math.max(r.MinimumArea,d.EnvelopeArea/r.Capacity.MaximumCoverage))||
                !near(d.RequiredClearance,Math.max(r.MinimumClearance,...d.Sources.map(s=>s.AnchorRadius)))||!near(d.Weight,r.Weight*d.RequiredArea))fail();
            return {...r,Weight:d.Weight,MinimumArea:d.RequiredArea,MinimumClearance:d.RequiredClearance};
        });
        const area=shape=>{const v=shape.Vertices,o=v[0];let sum=0;for(let i=1;i+1<v.length;i++)sum+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(sum)/2;};
        if(p.Version!==1||s?.Version!==1||definition.OwnershipDerivationId!==s.Id||s.Id!==definition.Id||s.Purpose!==definition.Purpose||s.SourceRuleId!==definition.SourceRuleId||
            !equal(s.Surface,definition.Surface)||!equal(definition.TopologyPlan?.Connections??s.Connections,definition.Connections)||!Array.isArray(s.Regions)||s.Regions.length!==definition.Regions.length||
            !Array.isArray(p.Anchors)||p.Anchors.length!==s.Regions.length||!Number.isSafeInteger(p.Work)||p.Work<1||p.Work>definition.MaximumWork||
            a?.SchemaVersion!==1||a.RegionId!=='ownership-domain'||!equal(a.Pattern,s.Pattern)||!Array.isArray(a.Strata)||a.Strata.length!==s.Regions.length||
            !equal(a.RelativeWeights,effective.map(r=>r.Weight))||!near(a.Area,area(s.Surface.Boundary))||!finite(a.Mass)||density==null&&!near(a.Mass,a.Area))fail();
        RegionalSchematicBase.areaPartitionWitness(a,fail);
        for(const key of ['MaximumTotalNodes','MaximumTotalConnections','MaximumReferences','MaximumWork'])if(s[key]!==definition[key])fail();
        const seen=new Set();let total=0;
        for(let i=0;i<s.Regions.length;i++){
            const source=s.Regions[i],bounds=effective[i],region=definition.Regions[i],stratum=a.Strata[i],anchor=p.Anchors[i],proof=anchor?.Plan;
            if(!source||seen.has(source.Id)||region?.Id!==source.Id||anchor?.RegionId!==source.Id||!finite(source.Weight)||!finite(source.MinimumArea)||
                !Number.isFinite(source.MinimumClearance)||source.MinimumClearance<0||source.Recipe?.Origin!=null||source.Recipe?.Surface!=null||
                !equal(source.Catalog,region.Catalog)||!equal(region.Ownership,region.GenerationDomain)||stratum?.Area<bounds.MinimumArea||
                !near(stratum?.Area,area(region.Ownership))||!near(stratum.Mass,a.Mass*bounds.Weight/effective.reduce((sum,r)=>sum+r.Weight,0))||
                !equal(proof?.Request?.Boundary,region.Ownership)||proof?.Request?.Id!==region.Id||
                !Array.isArray(proof.Request.Seeds)||proof.Request.Seeds.length||proof.Request.Intents?.length!==1||
                proof.Request.Intents[0].MinimumArea!==bounds.MinimumArea||proof.Request.Intents[0].MinimumClearance!==bounds.MinimumClearance||
                proof.Request.Profile.Id!==source.Profile?.Id||proof.Regions?.length!==1)fail();
            seen.add(source.Id);total+=stratum.Area;
            const pin=source.PinnedAnchor;
            if(pin!=null&&(!pin.Purpose||!pin.SourceRuleId||!pin.Position||![pin.Position.X,pin.Position.Y].every(Number.isFinite)))fail();
            if(!equal(proof.Request.Intents[0].RequiredPosition??null,pin?.Position??null))fail();
            const seeds=RegionalSchematicBase.regionIntentSeeds(proof),point=seeds[0]?.Position;
            if(seeds.length!==1||!equal(point,proof.Regions[0].Seed.Position))fail();
            if(pin!=null&&!equal(point,pin.Position))fail();
            const onBoundary=point=>region.Ownership.Vertices.some((a,n)=>{
                const b=region.Ownership.Vertices[(n+1)%region.Ownership.Vertices.length],dx=b.X-a.X,dy=b.Y-a.Y;
                const t=Math.max(0,Math.min(1,((point.X-a.X)*dx+(point.Y-a.Y)*dy)/(dx*dx+dy*dy)));
                return Math.hypot(point.X-a.X-t*dx,point.Y-a.Y-t*dy)<=1e-7;
            });
            if(!Array.isArray(stratum.BoundaryEdges)||stratum.BoundaryEdges.length<3||stratum.BoundaryEdges.length>32768||
                stratum.BoundaryEdges.some(e=>!onBoundary(e.From)||!onBoundary(e.To)))fail();
            const recipe=structuredClone(source.Recipe);recipe.Origin=[point.X,point.Y];
            if(source.SpatialContext!=null&&(s.Coupling==null||s.ProgramComposition==null))fail();
            this.spatialContext(source,region.Ownership,recipe,fail);
            if(s.ProgramComposition!=null){
                const selected=(p.JointPlan?.Attempts?.at(-1)?.OutputChoices??p.CompositionPlan?.Attempts?.at(-1)?.Choices)?.find(c=>c.RegionId===source.Id);
                if(recipe.ProgramComposition!=null){if(!selected||recipe.ProgramComposition.RequiredRanks!=null)fail();recipe.ProgramComposition.RequiredRanks=selected.RequiredRanks;}
                else if(selected)fail();
            }
            if(recipe.Placement){
                if(recipe.Placement.Request.Boundary!=null||recipe.Placement.Request.Anchors.some(a=>a.Id===source.PlacementAnchorId))fail();
                recipe.Placement.Request.Boundary=region.Ownership;recipe.Placement.Request.Anchors.push({Id:source.PlacementAnchorId,Position:point});
            }
            if(!equal(recipe,region.Recipe))fail();
        }
        if(!near(total,a.Area))fail();
        this.partitionQualification(p,effective,definition,fail);
        if(s.Coupling!=null&&s.ProgramComposition!=null){this.jointDerivation(definition,fail);return p;}
        if(p.JointPlan!=null)fail();
        const coupling=s.Coupling,trace=p.CouplingPlan,digest=h=>typeof h==='string'&&/^[0-9a-f]{64}$/.test(h);
        if(coupling==null){if(trace!=null)fail();}else{
            if(!coupling.Purpose||!coupling.SourceRuleId||!Number.isSafeInteger(coupling.MaximumIterations)||coupling.MaximumIterations<1||coupling.MaximumIterations>32||
                !s.Regions.some(r=>r.Recipe.TerrainDemands!=null)||!trace||!Array.isArray(trace.Attempts)||trace.Attempts.length<1||trace.Attempts.length>coupling.MaximumIterations||
                !Number.isSafeInteger(trace.InitializationWork)||trace.InitializationWork<1)fail();
            let work=trace.InitializationWork;
            trace.Attempts.forEach((attempt,i)=>{
                if(attempt.Iteration!==i+1||!digest(attempt.InputSha256)||!digest(attempt.OutputSha256)||!digest(attempt.OwnershipSha256)||
                    !Number.isSafeInteger(attempt.Work)||attempt.Work<1||attempt.Stable!==(i===trace.Attempts.length-1)||
                    attempt.Stable!==(attempt.InputSha256===attempt.OutputSha256)||i>0&&attempt.InputSha256!==trace.Attempts[i-1].OutputSha256||
                    !Array.isArray(attempt.Demands)||attempt.Demands.length!==demands.length||new Set(attempt.Demands.map(d=>d.RegionId)).size!==demands.length)fail();
                work+=attempt.Work;
                for(const measured of attempt.Demands){
                    const final=demands.find(d=>d.RegionId===measured.RegionId);
                    if(!final||!finite(measured.RequiredArea)||!finite(measured.RequiredClearance)||!finite(measured.Weight)||!digest(measured.AnalysisSha256)||
                        !Number.isSafeInteger(measured.NodeCount)||measured.NodeCount<1||measured.HabitatSha256!=null&&!digest(measured.HabitatSha256))fail();
                    if(attempt.Stable&&(!near(measured.RequiredArea,final.RequiredArea)||!near(measured.RequiredClearance,final.RequiredClearance)||
                        !near(measured.Weight,final.Weight)||measured.NodeCount!==final.Analysis.Nodes.length||measured.HabitatSha256!==(final.Analysis.TerrainAnalysis?.HabitatSha256??null)))fail();
                }
            });
            if(work!==(p.PhysicalFeedbackPlan?.Attempts?.at(-1)?.PlanningWork??p.CompositionPlan?.Attempts?.at(-1)?.Work??p.Work-topologyWork))fail();
        }
        this.physicalDerivation(definition,fail,topologyWork);
        const compositionRule=s.ProgramComposition,composition=p.CompositionPlan;
        if(compositionRule==null){if(composition!=null)fail();}else{
            const programs=s.Regions.filter(r=>r.Recipe.ProgramComposition!=null);
            if(!compositionRule.Purpose||!compositionRule.SourceRuleId||!Number.isSafeInteger(compositionRule.MaximumAttempts)||compositionRule.MaximumAttempts<1||compositionRule.MaximumAttempts>64||
                !programs.length||s.Coupling!=null||s.Regions.some(r=>r.Recipe.TerrainDemands!=null)||!composition||!Array.isArray(composition.Attempts)||
                composition.Attempts.length<1||composition.Attempts.length>compositionRule.MaximumAttempts||!Number.isSafeInteger(composition.EnumerationWork)||composition.EnumerationWork<1)fail();
            let work=composition.EnumerationWork;const blocked=new Set();
            const key=c=>JSON.stringify([c.RegionId,c.RequiredRanks]);
            composition.Attempts.forEach((attempt,i)=>{
                if(attempt.Ordinal!==i+1||attempt.Accepted!==(i===composition.Attempts.length-1)||!Number.isSafeInteger(attempt.Work)||attempt.Work<0||
                    !Array.isArray(attempt.Choices)||attempt.Choices.length!==programs.length||attempt.CandidateSha256!=null&&!digest(attempt.CandidateSha256))fail();
                work+=attempt.Work;
                attempt.Choices.forEach((choice,j)=>{
                    if(choice.RegionId!==programs[j].Id||!digest(choice.AnalysisSha256)||!Array.isArray(choice.RequiredRanks)||choice.RequiredRanks.length<1||choice.RequiredRanks.length>4096||
                        choice.RequiredRanks.some(r=>!Number.isSafeInteger(r)||r<0||r>=Math.max(programs[j].Recipe.ProgramComposition.MaximumAlternativesPerMix,programs[j].Recipe.ProgramComposition.MaximumRegionAlternatives))||blocked.has(key(choice)))fail();
                    this.analysisFailures(choice,programs[j].Recipe.ProgramComposition);
                });
                if(attempt.Accepted){
                    if(!digest(attempt.CandidateSha256)||!digest(attempt.GeometrySha256)||attempt.Rejection!=null||attempt.FailureRegionId!=null||attempt.InvariantChoice!=null)fail();
                }else{
                    if(typeof attempt.Rejection!=='string'||!attempt.Rejection||attempt.GeometrySha256!=null||attempt.FailureRegionId!=null&&!s.Regions.some(r=>r.Id===attempt.FailureRegionId))fail();
                    if(attempt.InvariantChoice!=null){
                        const proof=attempt.InvariantChoice,choice=attempt.Choices.find(c=>c.RegionId===attempt.FailureRegionId),witness=proof.NestedSourceEnvelope;
                        if(!choice||!equal(choice,proof.Choice)||!witness||![witness.Required,witness.Available,witness.Uncertainty].every(Number.isFinite)||
                            witness.Uncertainty<0||witness.Required<=witness.Available+witness.Uncertainty||typeof witness.NodeId!=='string'||typeof witness.ParentNodeId!=='string')fail();
                        blocked.add(key(choice));
                    }
                }
            });
            if(work+topologyWork!==p.Work)fail();
        }
        return p;
    }
    static jointDerivation(definition,fail) {
        const p=definition.OwnershipPlan,s=p.Source,plan=p.JointPlan,rule=s.ProgramComposition,coupling=s.Coupling,physical=s.PhysicalFeedback;
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),digest=h=>typeof h==='string'&&/^[0-9a-f]{64}$/.test(h),
            integer=(n,min,max)=>Number.isSafeInteger(n)&&n>=min&&n<=max,
            near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        const programs=s.Regions.filter(r=>r.Recipe.ProgramComposition!=null),policies=s.Regions.filter(r=>r.Capacity!=null);
        if(!rule?.Purpose||!rule.SourceRuleId||!integer(rule.MaximumAttempts,1,64)||!coupling?.Purpose||!coupling.SourceRuleId||!integer(coupling.MaximumIterations,1,32)||
            !programs.length||!s.Regions.some(r=>r.Recipe.TerrainDemands!=null||r.SpatialContext!=null)||programs.some(r=>!r.Capacity||r.Recipe.ProgramComposition.RequiredRanks!=null)||
            p.CompositionPlan!=null||p.CouplingPlan!=null||p.PhysicalFeedbackPlan!=null||!plan||!Array.isArray(plan.Attempts)||!integer(plan.Attempts.length,1,rule.MaximumAttempts)||
            !integer(plan.EnumerationWork,0,s.MaximumWork))fail();
        if(physical!=null&&(!physical.Purpose||!physical.SourceRuleId||!integer(physical.MaximumAttempts,1,32)||!definition.Connections.length&&!definition.NetworkRules?.length))fail();
        const choices=(values,initial=false)=>{
            if(!Array.isArray(values)||values.length!==programs.length)fail();
            values.forEach((c,j)=>{const deferred=c?.RegionSelectionDeferred===true;
                if(deferred&&(!initial||programs[j].SpatialContext==null))fail();
                const inherited=programs[j].Recipe.PopulationSource?.Context?.UsePlacementBoundary===true&&programs[j].Recipe.PopulationSource?.Recipe.ProgramComposition!=null;
                if(c?.RegionId!==programs[j].Id||!digest(c.AnalysisSha256)||!Array.isArray(c.RequiredRanks)||!integer(c.RequiredRanks.length,deferred||inherited?0:1,4096)||
                c.RequiredRanks.some(n=>!integer(n,0,Math.max(programs[j].Recipe.ProgramComposition.MaximumAlternativesPerMix,programs[j].Recipe.ProgramComposition.MaximumRegionAlternatives)-1)))fail();
                this.analysisFailures(c,programs[j].Recipe.ProgramComposition);});
        };
        const exclusions=values=>{
            if(!values||typeof values!=='object'||Array.isArray(values))fail();
            for(const [id,items] of Object.entries(values)){
                if(!physical||!s.Regions.some(r=>r.Id===id)||!Array.isArray(items)||!integer(items.length,1,physical.MaximumAttempts)||
                    items.some(v=>!Array.isArray(v)||v.length!==1||!integer(v[0],0,4095))||new Set(items.map(v=>v[0])).size!==items.length)fail();
            }
        };
        const canonical=v=>Object.fromEntries(Object.entries(v).sort(([a],[b])=>a.localeCompare(b)));
        let work=plan.EnumerationWork,qualifications=0;
        plan.Attempts.forEach((a,i)=>{
            if(a.Ordinal!==i+1||!integer(a.Iteration,1,coupling.MaximumIterations)||!digest(a.InputSha256)||a.OutputSha256!=null&&!digest(a.OutputSha256)||
                !integer(a.Work,0,s.MaximumWork)||a.Accepted!==(i===plan.Attempts.length-1)||a.Stable!==(a.InputSha256===a.OutputSha256)||
                a.OwnershipSha256!=null&&!digest(a.OwnershipSha256)||a.CandidateSha256!=null&&!digest(a.CandidateSha256))fail();
            work+=a.Work;choices(a.InputChoices,a.ParentOrdinal==null);if(a.OutputChoices!=null)choices(a.OutputChoices);exclusions(a.ComponentExclusions);
            if(a.RequestedPartitions!=null){if(!physical)fail();this.partitionChoices(a.RequestedPartitions,s,fail);}
            if(a.SelectedPartitions!=null){
                this.partitionChoices(a.SelectedPartitions,s,fail);
                if(a.RequestedPartitions!=null&&!equal(a.RequestedPartitions,a.SelectedPartitions))fail();
                if(a.Accepted){const selected=(p.Partition.PartitionAttempts??[]).filter(c=>c.Status==='Selected').map(c=>({FirstMember:c.FirstMember,Members:c.Members,AxisOffsetDegrees:c.AxisOffsetDegrees,...(c.Bend==null?{}:{Bend:c.Bend}),...(c.BendOffset==null?{}:{BendOffset:c.BendOffset})})).sort((x,y)=>x.FirstMember-y.FirstMember||y.Members-x.Members);if(!equal(selected,a.SelectedPartitions))fail();}
            }
            if(a.PartitionFailure!=null){if(a.RequestedPartitions==null||a.SelectedPartitions!=null||a.OutputSha256!=null||a.CandidateSha256!=null)fail();this.partitionFailure(a.PartitionFailure,s,a.Work,fail);}
            if(a.ParentOrdinal==null){if(a.Iteration!==1||Object.keys(a.ComponentExclusions).length)fail();}
            else{
                if(!integer(a.ParentOrdinal,1,i))fail();const parent=plan.Attempts[a.ParentOrdinal-1];
                if(parent.OutputSha256!==a.InputSha256||a.Iteration!==parent.Iteration+1)fail();
                if(!equal(a.RequestedPartitions??null,parent.RequestedPartitions??null)&&
                    (!parent.Stable||!parent.Rejection||parent.InvariantChoice!=null||a.RequestedComponents!=null))fail();
                const inherited=structuredClone(parent.ComponentExclusions);
                if(!equal(canonical(inherited),canonical(a.ComponentExclusions)))fail();
                if(!equal(parent.OutputChoices?.map(c=>[c.RegionId,c.RequiredRanks]),a.InputChoices.map(c=>[c.RegionId,c.RequiredRanks])))fail();
            }
            if(!Array.isArray(a.Demands)||a.Demands.length!==policies.length)fail();
            a.Demands.forEach((d,j)=>{
                if(d.RegionId!==policies[j].Id||![d.RequiredArea,d.RequiredClearance,d.Weight].every(n=>Number.isFinite(n)&&n>0)||
                    !integer(d.NodeCount,1,100000)||!digest(d.AnalysisSha256)||d.HabitatSha256!=null&&!digest(d.HabitatSha256))fail();
                if(a.Accepted){const final=p.ProgramDemands.find(f=>f.RegionId===d.RegionId);
                    if(!near(d.RequiredArea,final.RequiredArea)||!near(d.RequiredClearance,final.RequiredClearance)||!near(d.Weight,final.Weight)||
                        d.NodeCount!==final.Analysis.Nodes.length||d.HabitatSha256!==(final.Analysis.TerrainAnalysis?.HabitatSha256??null))fail();}
            });
            if(a.Stable){
                if(!digest(a.CandidateSha256)||!Array.isArray(a.Components)||a.Components.length!==s.Regions.length)fail();qualifications++;
                a.Components.forEach((c,j)=>{if(c.RegionId!==s.Regions[j].Id||!integer(c.Component,0,4095)||!c.Position||![c.Position.X,c.Position.Y].every(Number.isFinite)||
                    (a.ComponentExclusions[c.RegionId]??[]).some(v=>v[0]===c.Component))fail();});
            }else if(a.Components!=null||a.CandidateSha256!=null||a.GeometrySha256!=null)fail();
            if(a.ComponentSearch!=null){
                if(!physical)fail();const validate=this.componentDomains(a.ComponentSearch,s.Regions,s.MaximumWork,fail);
                if(a.Components!=null)validate(a.Components);
                if(a.RequestedComponents!=null){validate(a.RequestedComponents);if(a.Stable&&!equal(a.RequestedComponents,a.Components))fail();}
            }else if(a.RequestedComponents!=null||a.NetworkFailure!=null)fail();
            if(a.Accepted){
                if(!a.Stable||!digest(a.GeometrySha256)||a.Rejection!=null||a.FailureRegionId!=null||a.ConnectionId!=null||a.RouteStatus!=null||a.InvariantChoice!=null||a.DomainDisconnection!=null||a.NetworkFailure!=null)fail();
                a.OutputChoices.forEach(c=>{if(!equal(c.RequiredRanks,definition.Regions.find(r=>r.Id===c.RegionId)?.Recipe.ProgramComposition.RequiredRanks))fail();});
                a.Components.forEach((c,j)=>{const actual=p.Anchors[j].Plan.IntentPlan?.Selections?.[0];if(c.Component!==actual?.Component||!equal(c.Position,actual?.Seed.Position))fail();});
                p.Anchors.forEach(anchor=>{const requested=a.RequestedComponents?.find(c=>c.RegionId===anchor.RegionId);
                    if(!equal(anchor.Plan.Request.ExcludedAssignments??[],a.ComponentExclusions[anchor.RegionId]??[])||
                        !equal(anchor.Plan.Request.RequiredAssignment??null,requested?[requested.Component]:null))fail();});
            }else if(a.Rejection!=null&&typeof a.Rejection!=='string'||a.GeometrySha256!=null||a.Stable&&!a.Rejection)fail();
            if(a.FailureRegionId!=null&&!s.Regions.some(r=>r.Id===a.FailureRegionId))fail();
            if(a.ConnectionId!=null){
                const c=definition.Connections.find(c=>c.Id===a.ConnectionId);
                if(!physical||!a.Stable||c?.OwnerRegionId!==a.FailureRegionId||!['CandidateGraphDisconnected','TraceRejected'].includes(a.RouteStatus)||
                    a.Rejection!==`METADATA_REGIONAL_CONNECTION_UNRESOLVED:${a.ConnectionId}:${a.RouteStatus}`)fail();
                if(a.DomainDisconnection!=null){const d=a.DomainDisconnection;
                    if(a.RouteStatus!=='CandidateGraphDisconnected'||!equal(d.Boundary,c.RoutingDomain)||d.ActorRadius!==c.Profile.Radius||!Array.isArray(d.Components)||
                        !integer(d.Components.length,2,4096)||!integer(d.Work,1,a.Work)||!Array.isArray(d.FromComponents)||!Array.isArray(d.ToComponents)||
                        !d.FromComponents.length||!d.ToComponents.length||[...d.FromComponents,...d.ToComponents].some(n=>!integer(n,0,d.Components.length-1))||d.FromComponents.some(n=>d.ToComponents.includes(n)))fail();}
            }else if(a.RouteStatus!=null||a.DomainDisconnection!=null)fail();
            if(a.NetworkFailure!=null){
                if(!physical||!a.Stable||a.ConnectionId!=null||a.FailureRegionId!=null||a.RouteStatus!=null||a.DomainDisconnection!=null)fail();
                this.networkFailure(a.NetworkFailure,definition,fail);
                if(a.Rejection!==`METADATA_REGIONAL_NETWORK_UNRESOLVED:${a.NetworkFailure.RuleId}:${a.NetworkFailure.Plan.Status}`)fail();
            }
            if(a.InvariantChoice!=null){
                const proof=a.InvariantChoice,w=proof.NestedSourceEnvelope,c=(a.OutputChoices??a.InputChoices)?.find(c=>c.RegionId===a.FailureRegionId);
                if(!digest(a.InvariantProgramSha256)||!c||!equal(c,proof.Choice)||!w||![w.Required,w.Available,w.Uncertainty].every(Number.isFinite)||
                    w.Uncertainty<0||w.Required<=w.Available+w.Uncertainty||typeof w.NodeId!=='string'||typeof w.ParentNodeId!=='string')fail();
            }else if(a.InvariantProgramSha256!=null)fail();
        });
        if(work+(definition.TopologyPlan?.Work??0)!==p.Work||physical!=null&&qualifications>physical.MaximumAttempts)fail();
    }
    static regionalNetworks(world,endpoint) {
        const d=world.Definition,rules=d.NetworkRules,p=world.NetworkPlan,fail=()=>{throw new Error('Invalid regional network witness. Regenerate with the engine.');};
        if(rules==null){if(p!=null)fail();return d.Connections;}
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b),integer=(n,min,max)=>Number.isSafeInteger(n)&&n>=min&&n<=max,
            text=s=>typeof s==='string'&&s.trim().length>0,pair=(a,b)=>JSON.stringify([a,b].sort());
        if(!Array.isArray(rules)||!integer(rules.length,1,4)||p?.Version!==1||!Array.isArray(p.Networks)||p.Networks.length!==rules.length||
            !integer(p.Work,1,world.Work))fail();
        const kinds=new Set(d.Connections.map(c=>c.Kind)),ids=new Set(d.Connections.map(c=>c.Id)),ruleIds=new Set(),connections=[...d.Connections];let work=0;
        for(let index=0;index<rules.length;index++){
            const r=rules[index],n=p.Networks[index],plan=n?.Plan,o=r?.Options;
            if(!text(r?.Id)||ruleIds.has(r.Id)||!text(r.ConnectionKind)||kinds.has(r.ConnectionKind)||!Array.isArray(r.Destinations)||!integer(r.Destinations.length,2,32)||
                !o||!text(o.Purpose)||!text(o.SourceRuleId)||!text(o.Scale)||!['Connected','SingleConnectionFailure'].includes(o.Connectivity)||
                !integer(o.NearestNeighbors,1,31)||!integer(o.MaximumPairChecks,1,496)||!integer(o.MaximumWork,1,200000000)||
                n?.RuleId!==r.Id||!integer(n.Work,1,p.Work)||plan?.Id!==d.Id+'/regional-network/'+r.Id||plan.Status!=='Found'||!equal(plan.Options,o)||
                !integer(plan.Work,1,o.MaximumWork)||plan.Work>=n.Work||!Array.isArray(plan.Edges)||!integer(plan.Edges.length,r.Destinations.length-1,128)||
                !Array.isArray(plan.Attempts)||!integer(plan.Attempts.length,plan.Edges.length,o.MaximumPairChecks)||!Array.isArray(n.Links)||n.Links.length!==plan.Edges.length||
                !integer(plan.NearestNeighborsUsed,Math.min(o.NearestNeighbors,r.Destinations.length-1),r.Destinations.length-1))fail();
            ruleIds.add(r.Id);kinds.add(r.ConnectionKind);work+=n.Work;
            const destinations=new Map(),bindings=new Set(),positions=[],resolved=[],hasSelector=r.Destinations.some(b=>b?.Endpoint?.Selector!=null);
            for(const binding of r.Destinations){
                const e=binding?.Endpoint,key=JSON.stringify(e);
                if(!text(binding?.Id)||destinations.has(binding.Id)||!text(binding.Purpose)||!text(binding.SourceRuleId)||!e||bindings.has(key))fail();
                bindings.add(key);
                let actual=e;
                if(e.Selector!=null){
                    const s=e.Selector,region=world.Regions.find(v=>v.RegionId===e.RegionId);
                    if(e.NodeId!==''||!text(s.NodeKind)||(s.ArchetypeId!=null&&!text(s.ArchetypeId))||!Array.isArray(region?.World?.Nodes))fail();
                    const matches=region.World.Nodes.filter(v=>v.Kind===s.NodeKind&&(s.ArchetypeId==null||v.ArchetypeId===s.ArchetypeId));
                    if(matches.length!==1)fail();
                    actual={RegionId:e.RegionId,NodeId:matches[0].Id,PortId:e.PortId??null};
                }
                const position=endpoint(actual);if(positions.some(v=>equal(v,position)))fail();
                positions.push(position);destinations.set(binding.Id,{...binding,Endpoint:actual});
                resolved.push({DestinationId:binding.Id,Endpoint:actual});
            }
            if(hasSelector?!equal(n.ResolvedDestinations,resolved):n.ResolvedDestinations!=null)fail();
            const projected=[...destinations].sort(([a],[b])=>a<b?-1:a>b?1:0).map(([Id,b])=>({Id,Purpose:b.Purpose,Position:endpoint(b.Endpoint)}));
            if(!equal(plan.Destinations,projected))fail();
            const attempts=new Map(),pairs=new Set(),edges=new Map();
            for(const a of plan.Attempts){
                const link=a?.Link,key=pair(link?.FromId,link?.ToId),found=a?.ForwardStatus==='Found'&&a.ReverseStatus==='Found';
                if(!link||!text(link.Id)||!link.Id.startsWith(plan.Id+'/edge-')||attempts.has(link.Id)||pairs.has(key)||link.FromId===link.ToId||
                    !destinations.has(link.FromId)||!destinations.has(link.ToId)||!text(a.ForwardStatus)||!text(a.ReverseStatus)||
                    (found?(!Number.isFinite(a.Cost)||a.Cost<0):a.Cost!=null))fail();
                pairs.add(key);attempts.set(link.Id,a);
            }
            const required=new Set();
            if(!Array.isArray(o.RequiredConnections))fail();
            for(const requiredPair of o.RequiredConnections){
                const key=pair(requiredPair?.FromId,requiredPair?.ToId);
                if(!destinations.has(requiredPair?.FromId)||!destinations.has(requiredPair?.ToId)||requiredPair.FromId===requiredPair.ToId||required.has(key))fail();required.add(key);
            }
            const forbidden=new Set();
            if(o.ForbiddenConnections!=null){
                if(!Array.isArray(o.ForbiddenConnections)||o.ForbiddenConnections.length>496)fail();
                for(const forbiddenPair of o.ForbiddenConnections){
                    const key=pair(forbiddenPair?.FromId,forbiddenPair?.ToId);
                    if(!destinations.has(forbiddenPair?.FromId)||!destinations.has(forbiddenPair?.ToId)||forbiddenPair.FromId===forbiddenPair.ToId||forbidden.has(key)||required.has(key)||pairs.has(key))fail();
                    forbidden.add(key);
                }
            }
            for(let edgeIndex=0;edgeIndex<plan.Edges.length;edgeIndex++){
                const edge=plan.Edges[edgeIndex],link=edge?.Link,a=attempts.get(link?.Id),mapping=n.Links[edgeIndex];
                if(!link||edges.has(link.Id)||!a||!equal(a.Link,link)||a.ForwardStatus!=='Found'||a.ReverseStatus!=='Found'||
                    !['required','backbone','single-connection-failure'].includes(edge.Reason)||required.has(pair(link.FromId,link.ToId))!==(edge.Reason==='required')||
                    mapping?.NetworkConnectionId!==link.Id||!/^network-[0-9a-f]{32}$/.test(mapping.RegionalConnectionId)||ids.has(mapping.RegionalConnectionId))fail();
                edges.set(link.Id,link);ids.add(mapping.RegionalConnectionId);
                const from=destinations.get(link.FromId).Endpoint,to=destinations.get(link.ToId).Endpoint,id=mapping.RegionalConnectionId;
                const actual=world.Connections.find(c=>c.Definition?.Id===id),definition={Id:id,Kind:r.ConnectionKind,Purpose:o.Purpose,SourceRuleId:o.SourceRuleId,
                    From:from,To:to,OwnerRegionId:from.RegionId,RoutingDomain:r.RoutingDomain??d.Surface.Boundary,Profile:o.Profile,Search:o.Search,Bidirectional:true};
                if(!actual||!equal(actual.Definition,definition)||!equal(actual.Forward,{...edge.Forward,Id:id+'/forward'})||!equal(actual.Reverse,{...edge.Reverse,Id:id+'/reverse'}))fail();
                connections.push(definition);
            }
            if([...required].some(key=>![...edges.values()].some(e=>pair(e.FromId,e.ToId)===key)))fail();
            const reachable=removed=>{
                const first=projected[0].Id,seen=new Set([first]),queue=[first];
                for(let i=0;i<queue.length;i++)for(const [id,e] of edges){
                    if(id===removed)continue;const next=e.FromId===queue[i]?e.ToId:e.ToId===queue[i]?e.FromId:null;
                    if(next!=null&&!seen.has(next)){seen.add(next);queue.push(next);}
                }
                return seen.size;
            };
            const bridges=[...edges.keys()].filter(id=>reachable(id)!==destinations.size).sort();
            if(reachable(null)!==destinations.size||o.Connectivity==='SingleConnectionFailure'&&bridges.length||
                !equal(plan.Analysis?.Components,[projected.map(p=>p.Id)])||!equal(plan.Analysis?.Bridges,bridges))fail();
        }
        if(work!==p.Work||connections.length!==world.Connections.length||connections.length>128)fail();
        return connections;
    }
    static layout(source) {
        const fail=message=>{throw new Error(`Invalid regional schematic: ${message}.`);};
        const text=s=>typeof s==='string'&&s.length>0&&s.length<=4096;
        const digest=s=>typeof s==='string'&&/^[0-9a-f]{64}$/.test(s);
        const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        const point=p=>p&&[p.X,p.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=10000000);
        const polygon=p=>p&&Array.isArray(p.Vertices)&&p.Vertices.length>=3&&p.Vertices.length<=64&&p.Vertices.every(point);
        const d=source?.Definition;
        this.ownershipDerivation(d);
        if(source?.Version!==1||d?.Version!==1||!text(d.Id)||!text(d.Purpose)||!digest(source.DefinitionSha256)||
            !polygon(d.Surface?.Boundary)||!Array.isArray(d.Regions)||d.Regions.length<2||d.Regions.length>16||
            !Array.isArray(source.Regions)||source.Regions.length!==d.Regions.length||
            !Array.isArray(d.Connections)||d.Connections.length>128||!Array.isArray(source.Connections)||
            source.Connections.length<d.Connections.length||source.Connections.length>128||!Array.isArray(source.Members)||source.Members.length>200000||
            !Number.isSafeInteger(source.Work)||source.Work<0||source.Work>d.MaximumWork)fail('document or budget');
        const definitions=new Map(),regions=new Map(),members=new Map(),nodeKeys=new Set();
        for(const r of d.Regions){
            if(!text(r?.Id)||definitions.has(r.Id)||!polygon(r.Ownership)||!polygon(r.GenerationDomain))fail('region definition');
            definitions.set(r.Id,r);
        }
        let nodes=0,minimum=Infinity,maximum=-Infinity,maximumSlope=0;
        const samples=new Map();let fieldSource=null;
        const views=source.Regions.map((r,index)=>{
            const definition=definitions.get(r?.RegionId),world=r?.World;
            const expectedSources=definition?this.regionalSurfaceSources(d.Surface,definition.GenerationDomain):null;
            if(!definition||regions.has(r.RegionId)||r.Version!==1||!digest(r.ContextSha256)||!text(r.ImplementationId)||
                !Array.isArray(r.Dependencies)||world?.Space?.Unit!==d.Surface.Unit||!equal(world.Surface?.Boundary,definition.GenerationDomain)||
                !equal(world.Surface?.Terrain,d.Surface.Terrain)||!equal(world.Surface?.Patches,expectedSources.Patches)||
                !equal(world.Surface?.Exclusions,expectedSources.Exclusions))fail('regional source binding');
            const plan=RegionalSchematicBase.layout(world);nodes+=world.Nodes.length;
            const demand=d.OwnershipPlan?.ProgramDemands?.find(d=>d.RegionId===r.RegionId);
            if(demand){
                const a=demand.Analysis,unplaced=nodes=>nodes.map(n=>({...n,Position:null})),ids=new Set(a.Connections.map(c=>c.Id));
                this.terrainAnalysis(a,world);
                this.regionAnalysis(a,world);
                if(a.Nodes.some(n=>n.Position!=null&&!equal(n.Position,world.Nodes.find(actual=>actual.Id===n.Id)?.Position)))fail('authored analysis coordinates');
                if(!equal(unplaced(a.Nodes),unplaced(world.Nodes))||!equal(a.Connections,world.Connections.filter(c=>ids.has(c.Id)))||
                    !equal(a.FootprintDefinitions,world.FootprintDefinitions??{})||!equal(a.StructureDefinitions,world.StructureDefinitions)||
                    !equal(a.CapacityPlan,world.CapacityPlan)||!equal(a.VariationPlan,world.VariationPlan)||!equal(a.ProgramMixPlan,world.ProgramMixPlan)||!equal(a.Dependencies,r.Dependencies))fail('program analysis binding');
                const declared=definition.Recipe.PopulationSource,sourceProgram=a.SourceProgram;
                if(declared){
                    const original=world.PlacementPlan?.Definition?.SourceWorld;
                    this.sourceProgramComposition(sourceProgram,original);
                    if(!original||sourceProgram?.Version!==1||sourceProgram.SourceProgram!=null||!Array.isArray(sourceProgram.Nodes)||
                        sourceProgram.Nodes.some(n=>n.Position!=null&&(d.OwnershipPlan?.Source.Coupling==null||!this.authoredAnalysisPosition(n,sourceProgram,declared.Catalog,definition.Recipe.Origin)))||
                        !Array.isArray(sourceProgram.Connections)||!Array.isArray(sourceProgram.RelativePositions)||!Array.isArray(sourceProgram.Dependencies)||
                        !digest(sourceProgram.RecipeSha256)||sourceProgram.CatalogSha256!==original.CatalogSha256||
                        !Number.isSafeInteger(sourceProgram.Work)||sourceProgram.Work<1||sourceProgram.Work>a.Work||
                        !equal(unplaced(sourceProgram.Nodes),unplaced(original.Nodes))||!equal(sourceProgram.FootprintDefinitions,original.FootprintDefinitions??{})||
                        !equal(sourceProgram.StructureDefinitions,original.StructureDefinitions)||!equal(sourceProgram.CapacityPlan,original.CapacityPlan)||
                        !equal(sourceProgram.VariationPlan,original.VariationPlan)||!equal(sourceProgram.ProgramMixPlan,original.ProgramMixPlan))fail('population source analysis binding');
                    const sourceIds=new Set(sourceProgram.Connections.map(c=>c.Id));
                    if(!equal(sourceProgram.Connections,original.Connections.filter(c=>sourceIds.has(c.Id))))fail('population source connections');
                    if(declared.Context!=null){
                        const context=declared.Context,request=declared.Recipe?.Placement?.Request;
                        const actual=original.PlacementPlan?.TemplateDefinition?.Request??original.PlacementPlan?.Definition?.Request;
                        const consumer=world.PlacementPlan?.TemplateDefinition?.Request??world.PlacementPlan?.Definition?.Request;
                        const terrainOnly=declared.Recipe.Placement==null;
                        if(!Array.isArray(context.PlacementAnchorIds)||context.PlacementAnchorIds.length>32||new Set(context.PlacementAnchorIds).size!==context.PlacementAnchorIds.length||
                            declared.Recipe.Surface!=null||declared.Recipe.Origin!=null||declared.Recipe.PopulationSource!=null||!equal(declared.Recipe.Space,world.Space)||
                            !equal(original.Surface,world.Surface)||!equal(sourceProgram.RelativePositions,original.PlacementPlan?.RelativePositions??[])||
                            !consumer||(terrainOnly?(context.PlacementAnchorIds.length!==0||original.PlacementPlan!=null):
                                (!Array.isArray(request?.Anchors)||!Array.isArray(request?.Relations)||!actual)))fail('population spatial context');
                        for(const id of context.PlacementAnchorIds){
                            const from=consumer.Anchors.filter(a=>a.Id===id),to=actual.Anchors.filter(a=>a.Id===id);
                            if(typeof id!=='string'||!/^[A-Za-z0-9_.:-]{1,160}$/.test(id)||request.Anchors.some(a=>a.Id===id)||!request.Relations.some(r=>r.ToId===id)||
                                from.length!==1||to.length!==1||!equal(from[0],to[0]))fail('population source anchor binding');
                        }
                        if(context.UsePlacementBoundary!=null&&typeof context.UsePlacementBoundary!=='boolean')fail('population boundary declaration');
                        if(context.UsePlacementBoundary&&(!declared.Recipe.Regions||declared.Recipe.Regions.Boundary!=null||request?.Boundary!=null||!consumer.Boundary||
                            !terrainOnly&&!equal(actual.Boundary,consumer.Boundary)||!equal(original.RegionPlan?.Request.Boundary,consumer.Boundary)))fail('population boundary binding');
                    }
                }else if(sourceProgram!=null)fail('unexpected population source analysis');
            }
            if(nodes>50000||nodes>d.MaximumTotalNodes)fail('node budget');
            const view={packet:r,definition,plan,index};regions.set(r.RegionId,view);
            for(const n of world.Nodes)nodeKeys.add(this.address(r.RegionId,n.Id));
            const field=plan.heightField;
            if(field){
                const identity=JSON.stringify(field.geometry);
                if(fieldSource!==null&&fieldSource!==identity)fail('shared terrain lattice');fieldSource=identity;
                minimum=Math.min(minimum,field.minimum);maximum=Math.max(maximum,field.maximum);
                for(const t of RegionalSchematicBase.heightTriangles(field))maximumSlope=Math.max(maximumSlope,t.slope);
                for(let row=0;row<field.rows;row++)for(let col=0;col<field.columns;col++){
                    const key=`${field.firstColumn+col}/${field.firstRow+row}`,height=field.values[row*field.columns+col];
                    if(samples.has(key)&&!Object.is(samples.get(key),height))fail('changed shared terrain sample');samples.set(key,height);
                }
            }
            return view;
        });
        for(const m of source.Members){
            const key=JSON.stringify([m?.OwnerRegionId,m?.Kind,m?.Id]);
            if(!regions.has(m?.OwnerRegionId)||!text(m.Id)||!['Node','Path'].includes(m.Kind)||members.has(key)||
                !Array.isArray(m.RegionIds)||!m.RegionIds.length||m.RegionIds.length>16||
                new Set(m.RegionIds).size!==m.RegionIds.length||!m.RegionIds.includes(m.OwnerRegionId)||m.RegionIds.some(id=>!regions.has(id)))fail('member identity');
            members.set(key,m);
            if(m.Kind==='Node'&&!nodeKeys.delete(this.address(m.OwnerRegionId,m.Id)))fail('missing or repeated source node');
        }
        if(nodeKeys.size)fail('incomplete source nodes');
        const endpoint=e=>{
            const view=regions.get(e?.RegionId),box=view?.plan.boxes.find(b=>b.id===e.NodeId);
            if(!box)fail('connection endpoint');
            if(e.PortId!=null){const port=box.physicalAccess?.Ports.find(p=>p.Id===e.PortId);if(!port)fail('connection port');return port.ApproachPosition;}
            if(box.physicalAccess?.Occupancy==='Solid'||!box.node.Position)fail('solid or unpositioned endpoint');
            return {X:box.node.Position[0],Y:box.node.Position[1]};
        };
        const connectionDefinitions=this.regionalNetworks(source,endpoint),connectionIds=new Set();
        for(const route of source.Connections){
            const rule=route?.Definition;
            if(!rule||!text(rule.Id)||connectionIds.has(rule.Id)||!equal(rule,connectionDefinitions.find(c=>c.Id===rule.Id))||
                !regions.has(rule.OwnerRegionId)||!Number.isFinite(rule.Profile?.Radius)||rule.Profile.Radius<=0||
                !equal(route.From,endpoint(rule.From))||!equal(route.To,endpoint(rule.To)))fail('connection source');
            connectionIds.add(rule.Id);
            for(const [direction,from,to] of [['Forward',route.From,route.To],['Reverse',route.To,route.From]]){
                const p=route[direction];
                if(direction==='Reverse'&&!rule.Bidirectional){if(p!=null)fail('unexpected reverse path');continue;}
                RegionalSchematicBase.routeRoundingWitness(p,()=>fail('route rounding witness'));
                const points=p?.Path?.Points;
                if(p?.Id!==`${rule.Id}/${direction.toLowerCase()}`||p.Status!=='Found'||p.ProfileId!==rule.Profile.Id||
                    !Array.isArray(points)||points.length<2||points.length>100000||!points.every(point)||
                    !equal(points[0],from)||!equal(points.at(-1),to)||!members.has(JSON.stringify([rule.OwnerRegionId,'Path',p.Id])))fail('directed path');
                let distance=0;for(let i=1;i<points.length;i++)distance+=Math.hypot(points[i].X-points[i-1].X,points[i].Y-points[i-1].Y);
                if(!p.Cost||Object.values(p.Cost).some(n=>!Number.isFinite(n)||n<0)||Math.abs(distance-p.Cost.Distance)>1e-8*Math.max(1,distance))fail('path cost');
            }
        }
        const bounds=views.map(v=>v.plan),minX=Math.min(...bounds.map(p=>p.minX)),minY=Math.min(...bounds.map(p=>p.minY));
        const boxes=views.flatMap(v=>v.plan.boxes.map(b=>({...b,id:this.address(v.packet.RegionId,b.id),
            parentId:b.node.ParentId==null?null:this.address(v.packet.RegionId,b.node.ParentId),ownerRegionId:v.packet.RegionId,sourceBox:b})));
        return {world:source,views,regions,members,boxes,minX,minY,width:Math.max(...bounds.map(p=>p.minX+p.width))-minX,
            height:Math.max(...bounds.map(p=>p.minY+p.height))-minY,unit:d.Surface.Unit,coordinateSpace:'engine-regional-surface',
            terrainScale:Number.isFinite(minimum)?{minimum,maximum,maximumSlope}:null};
    }
    worldId() { return this.plan.world.Definition.Id; }
    exportProvenance() {
        const w=this.plan.world;
        return {RegionalWorld:{Version:w.Version,DefinitionSha256:w.DefinitionSha256,
            Regions:w.Regions.map(r=>({RegionId:r.RegionId,ContextSha256:r.ContextSha256,ImplementationId:r.ImplementationId,Dependencies:r.Dependencies}))}};
    }
    load(source,navigation=null) {
        if(navigation)throw new Error('Independent coordinate domains cannot be overlaid as shared-coordinate regions.');
        const packetNavigation=this.plan===undefined;
        const plan=WorldRegionalSchematic.layout(source),scenes=[];
        // Build all scenes before changing the working view, including canvas failures.
        for(const v of plan.views){
            const controls=document.createElement('div');
            controls.innerHTML='<div class="key"></div><select id="terrain-field-view"><option value="height">Elevation</option><option value="slope">Slope</option></select>';
            const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');
            const scene=RegionalSchematicBase.renderScene(v.packet.World,svg,controls,`region-${v.index}`,plan.terrainScale);
            scenes.push({view:v,scene,controls,svg});
        }
        this.plan=plan;this.packetNavigation=packetNavigation;this.scenes=scenes;this.selected=null;this.focusRootId=null;this.selectors=new Map();this.boxesById=new Map(plan.boxes.map(b=>[b.id,b]));
        this.labelNodes=new Map();this.markerNodes=[];this.domainNavigation=null;this.paletteSettings={Version:1,LayerColors:{}};this.semanticFilter='';
        this.semantic={entries:new Map(),nodes:new Map(),connections:new Map()};this.svg.replaceChildren();
        this.element('title',{},`${this.worldId()}: shared-coordinate world`);
        this.element('desc',{},'Complete regional source geometry and directed connecting paths in original engine coordinates. Tab and Enter inspect sources.');
        this.groups={};for(const name of ['terrain','ground','regions','population','roads','sites','links','labels'])this.groups[name]=this.element('g',{'data-layer':name});
        const defs=this.element('defs');
        for(const {view:v,scene,controls,svg} of scenes){
            const owner=v.packet.RegionId,key=id=>WorldRegionalSchematic.address(owner,id);
            for(const definition of [...svg.querySelectorAll('defs')])defs.append(...definition.childNodes);
            const clip=this.element('clipPath',{id:`ownership-${v.index}`},null,defs);
            this.element('polygon',{points:v.definition.Ownership.Vertices.map(p=>`${p.X},${p.Y}`).join(' ')},null,clip);
            for(const [name,group] of Object.entries(scene.groups)){
                if(!group.childElementCount)continue;
                const wrapper=this.element('g',{'data-owner-region':owner,...((name==='terrain'||name==='ground')?{'clip-path':`url(#ownership-${v.index})`}:{})},null,this.groups[name]);
                wrapper.append(group);
            }
            this.element('polygon',{points:v.definition.Ownership.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',stroke:'#7750a1',
                'stroke-width':1,'stroke-dasharray':'5 4','vector-effect':'non-scaling-stroke','data-generation-ownership':owner},null,this.groups.regions);
            for(const group of Object.values(scene.groups))for(const el of group.querySelectorAll('[data-node-id],[data-palette-node],[data-connection-id]')){
                for(const attr of ['data-node-id','data-palette-node','data-connection-id'])if(el.hasAttribute(attr)){
                    el.setAttribute(attr.replace('data-','data-source-'),el.getAttribute(attr));el.setAttribute(attr,key(el.getAttribute(attr)));
                }
            }
            for(const [id,label] of scene.labelNodes)this.labelNodes.set(key(id),label);
            this.markerNodes.push(...scene.markerNodes);
            for(const [id,entry] of scene.semantic.entries)this.semantic.entries.set(key(id),{...entry,
                NodeIds:entry.NodeIds.map(key),ConnectionIds:entry.ConnectionIds.map(key)});
            for(const [id,layer] of scene.semantic.nodes)this.semantic.nodes.set(key(id),layer==null?null:key(layer));
            for(const [id,layer] of scene.semantic.connections)this.semantic.connections.set(key(id),layer==null?null:key(layer));
            scene.details=this.details;
            Object.defineProperty(scene,'dragged',{get:()=>this.dragged,set:value=>{this.dragged=value;}});
            scene.updateLabelVisibility=()=>this.updateLabelVisibility();
            scene.focusBox=box=>this.focusBox(this.boxesById.get(key(box.id)));
            scene.focusRoute=route=>this.focusRoute({...route,id:key(route.id)});
            scene.notify=(event,message,outcome='succeeded')=>{
                if(event==='schematic.route.selected'){
                    if(this.selected?.rect){this.selected.rect.setAttribute('stroke-width',this.selected.width);this.selected.rect.setAttribute('stroke','#637d82');}
                    this.selected=null;
                    const p=document.createElement('p');p.textContent=`Semantic owner: ${owner}. Saved route geometry and source rules belong to this regional program.`;
                    this.details.querySelector('h2')?.after(p);this.appendRegionLink(v);
                }
                if(event==='schematic.node.selected'){
                    if(this.selected&&this.selected.rect!==scene.selected.rect){this.selected.rect.setAttribute('stroke-width',this.selected.width);this.selected.rect.setAttribute('stroke','#637d82');}
                    this.selected={...scene.selected,id:key(scene.selected.id)};
                    const m=plan.members.get(JSON.stringify([owner,'Node',scene.selected.id]));
                    const pin=source.Definition.OwnershipPlan?.Source.Regions.find(r=>r.Id===owner)?.PinnedAnchor;
                    const p=document.createElement('p');p.textContent=`Semantic owner: ${owner}. Source node: ${scene.selected.id}. Referenced regions: ${m.RegionIds.join(', ')}. Whole source geometry is retained.`+
                        (pin?` Regional origin pinned at (${pin.Position.X}, ${pin.Position.Y}): ${pin.Purpose}. Source: ${pin.SourceRuleId}.`:'');
                    if(pin)p.dataset.pinnedOrigin=owner;
                    const heading=this.details.querySelector('h2');if(heading)heading.after(p);else this.details.prepend(p);
                    const queries=source.Definition.OwnershipPlan?.PartitionQualification?.Attempts.filter(a=>a.RegionId===owner);
                    if(queries){const item=document.createElement('p');item.dataset.partitionQualification=owner;
                        item.textContent=`Ownership terrain qualification: ${queries.length} queries; ${queries.filter(a=>!a.Qualified).length} rejected. Program area, source-derived clearance and declared pins remained required.`;p.after(item);}
                    this.appendOwnershipSearch(source);
                    this.appendRegionLink(v);this.updateLabelVisibility();
                }
                this.notify(event,`${owner}: ${message}`,outcome);
            };
            for(const [id,select] of scene.selectors)this.selectors.set(key(id),select);
        }
        for(const route of source.Connections)for(const direction of ['Forward','Reverse']){
            const path=route[direction];if(!path)continue;
            const group=this.element('g',{role:'button',tabindex:0,'aria-label':`${direction}: ${route.Definition.Purpose}`,
                'data-regional-route':path.Id,'data-owner-region':route.Definition.OwnerRegionId},null,this.groups.roads);
            const shape=this.element('polyline',{points:path.Path.Points.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',stroke:direction==='Forward'?'#246fa6':'#27a4b3',
                'stroke-width':route.Definition.Profile.Radius*2,'stroke-linecap':'round','stroke-linejoin':'round',
                ...(direction==='Reverse'?{'stroke-dasharray':`${route.Definition.Profile.Radius*2} ${route.Definition.Profile.Radius}`}:{})},null,group);
            this.element('title',{},`${direction}: ${route.Definition.From.RegionId} / ${route.Definition.From.NodeId} to ${route.Definition.To.RegionId} / ${route.Definition.To.NodeId}; ${path.Cost.Distance} ${plan.unit}`,shape);
            // SVG line geometry has a zero-height/width focus box on axis-aligned
            // paths. A non-painted envelope makes keyboard focus well-defined;
            // pointer hits and the saved path still use the actual stroke.
            const radius=route.Definition.Profile.Radius,xs=path.Path.Points.map(p=>p.X),ys=path.Path.Points.map(p=>p.Y);
            this.element('rect',{x:Math.min(...xs)-radius,y:Math.min(...ys)-radius,width:Math.max(...xs)-Math.min(...xs)+radius*2,
                height:Math.max(...ys)-Math.min(...ys)+radius*2,fill:'none','pointer-events':'none','data-interaction-bounds':'true'},null,group);
            const select=()=>{if(!this.dragged)this.showRegionalPath(route,direction);};group.addEventListener('click',select);
            group.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();this.dragged=false;select();}});
        }
        const field=this.control('#terrain-field-view');field.disabled=!plan.terrainScale;field.value='height';
        field.onchange=()=>{for(const {controls} of scenes){const child=controls.querySelector('#terrain-field-view');child.value=field.value;child.onchange?.();}
            this.notify('schematic.terrain.layer',`Showing shared-scale ${field.value} from saved regional samples.`);};
        this.control('.key').replaceChildren();
        for(const [label,color] of [['Ownership (not a physical barrier)','#7750a1'],['Whole source geometry','#b8ccd5'],['Regional forward / return paths','#246fa6']]){
            const item=document.createElement('span');item.textContent=label;item.style.setProperty('--color',color);this.control('.key').append(item);
        }
        if(plan.terrainScale){const item=document.createElement('span');item.textContent=`Shared field scale: ${plan.terrainScale.minimum.toFixed(2)}–${plan.terrainScale.maximum.toFixed(2)} ${plan.unit}; slope 0–${plan.terrainScale.maximumSlope.toFixed(3)}`;this.control('.key').append(item);}
        this.control('#world-title').textContent=this.worldId().replaceAll('-',' ');
        const remaining=source.Regions.flatMap(r=>(r.World.RemainingScopes||[]).map(s=>`${r.RegionId}: ${s}`));
        this.control('#scope').textContent=`${scenes.length} shared-coordinate regions · ${plan.boxes.length} nodes · ${source.Connections.length} regional connections`;
        this.control('#coordinate-note').textContent=`Exact engine coordinates in ${plan.unit}. Ownership clips terrain presentation only; structures and paths remain whole. Shared terrain colors use saved triangle values. Generation, collision proof and scope acceptance belong to the C# engine; this viewer performs bounded structural checks. Artwork and gameplay integration remain separate.`;
        if(packetNavigation)this.control('#packet-links').innerHTML='<a href="world-regions.json">Complete world metadata</a> · <a href="definition.json">World definition</a>';
        else this.control('#packet-links').textContent='Loaded metadata file: links to the original export folder are unavailable. Native read/replay is required to authenticate this input.';
        this.syncDomainControls();this.syncRegionControls();this.syncLayerControls();this.syncSemanticControls();
        this.details.replaceChildren();const p=document.createElement('p');p.textContent=`${source.Definition.Purpose}. Select any complete source or regional path. ${remaining.length?'Remaining scope: '+remaining.join('; '):'All regions report complete declared recipe scope; whole-game acceptance is separate.'}`;this.details.append(p);
        if(source.Definition.OwnershipPlan){
            const pinned=source.Definition.OwnershipPlan.Source.Regions.filter(r=>r.PinnedAnchor!=null).length;
            const item=document.createElement('p');item.dataset.ownershipDerivation='true';
            item.textContent=`Generation ownership and ${source.Regions.length-pinned} automatic program anchors were derived by the engine; ${pinned} program origins are pinned by explicit source constraints. Ownership includes excluded terrain; every anchor uses its program’s terrain rules. Ownership is separate from streets, populated ground and storage cells. Source: ${source.Definition.SourceRuleId}.`;this.details.append(item);
            const q=source.Definition.OwnershipPlan.PartitionQualification;
            if(q){const note=document.createElement('p');note.dataset.partitionQualification='world';note.textContent=`Ownership terrain qualification: ${q.Attempts.length} leaf queries; ${q.Attempts.filter(a=>!a.Qualified).length} rejected; ${q.SelectedAttempts.length} final leaves selected. Rejected queries are diagnostic history, not placed content.`;this.details.append(note);}
        }
        const covers=source.Regions.filter(r=>r.World.GroundCoverPlan).map(r=>`${r.RegionId}: ${r.World.GroundCoverPlan.CoveredArea.toFixed(2)} ${plan.unit}² assigned, ${r.World.GroundCoverPlan.UnresolvedArea.toFixed(2)} unresolved`);if(covers.length){const p=document.createElement('p');p.dataset.groundCoverAccounting='true';p.textContent='Ground cover: '+covers.join('; ')+'. Coverage certifies material intent only.';this.details.append(p);}
        if(source.Definition.OwnershipPlan?.ProgramDemands){const item=document.createElement('p');item.dataset.programDemand='true';item.textContent='Program capacity: '+source.Definition.OwnershipPlan.ProgramDemands.map(d=>`${d.RegionId}: ${d.Analysis.Nodes.length} declared members, ${d.Sources.length} root envelopes, ${d.RequiredArea} ${plan.unit}² required area`).join('; ')+'. Area demand does not prove packing or complete ground content.';this.details.append(item);}
        this.appendOwnershipSearch(source);
        this.fit();this.notify('schematic.regional.loaded',`${plan.boxes.length} nodes in ${scenes.length} regions; ${source.Connections.reduce((n,r)=>n+1+Number(!!r.Reverse),0)} directed regional paths.`);
    }
    appendRegionLink(v) {
        if(!this.packetNavigation)return;
        const a=document.createElement('a');a.href=`region-${String(v.index).padStart(4,'0')}/schematic.html`;a.textContent=`Inspect complete ${v.packet.RegionId} packet`;this.details.append(a);
    }
    appendOwnershipSearch(source) {
        const p=source.Definition.OwnershipPlan?.PhysicalFeedbackPlan;if(!p)return;
        const item=document.createElement('p');item.dataset.ownershipSearch='true';
        item.textContent=`Physical ownership search: ${p.Contexts.length} contexts; ${p.Attempts.filter(a=>a.PlanningRejected).length} planning rejections; ${p.Attempts.filter(a=>!a.Accepted&&!a.PlanningRejected).length} rejected physical candidates. Accepted context ${p.Attempts.at(-1).ContextOrdinal}. Component choices belong to their exact ownership context; required content and journeys remain constraints.`;
        this.details.append(item);
    }
    syncRegionControls() {
        this.control('#generation-navigation').hidden=false;
        const select=this.control('#generation-region-select');select.replaceChildren();
        const all=document.createElement('option');all.value='';all.textContent='Whole world';select.append(all);
        for(const v of this.plan.views){const option=document.createElement('option');option.value=v.packet.RegionId;option.textContent=v.packet.RegionId;select.append(option);}
        select.onchange=()=>{
            const v=this.plan.regions.get(select.value);if(!v){this.fit();return;}
            const p=v.plan;this.view=[p.minX,p.minY,p.width,p.height];this.focusRootId=null;this.applyView();
            this.notify('schematic.region.focused',`Showing ${v.packet.RegionId} generation domain; neighboring geometry remains visible.`);
        };
    }
    showRegionalPath(route,direction) {
        const path=route[direction];this.details.replaceChildren();
        const h=document.createElement('h2');h.textContent=`${direction}: ${route.Definition.Id}`;
        const p=document.createElement('p');p.textContent=`${route.Definition.Purpose}. Owner: ${route.Definition.OwnerRegionId}. Full width ${route.Definition.Profile.Radius*2} ${this.plan.unit}. ${direction==='Forward'?'From':'To'} ${route.Definition.From.RegionId} / ${route.Definition.From.NodeId} ${direction==='Forward'?'to':'from'} ${route.Definition.To.RegionId} / ${route.Definition.To.NodeId}. Source: ${route.Definition.SourceRuleId}.`;
        const pre=document.createElement('pre');pre.textContent=JSON.stringify({Direction:direction,Path:path.Path,Cost:path.Cost,Profile:route.Definition.Profile},null,2);
        this.details.append(h,p,pre);h.tabIndex=-1;h.focus();this.notify('schematic.regional.path.selected',`Selected ${path.Id}.`);
    }
}
if(typeof module!=='undefined')module.exports=WorldRegionalSchematic;
