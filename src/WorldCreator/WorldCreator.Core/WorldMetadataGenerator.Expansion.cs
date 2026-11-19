using System.Collections.ObjectModel;
using System.Text.Json;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    private MetadataExpandedContent ExpandProgramContent(WorldMetadataRecipe recipe,IReadOnlyDictionary<string,WorldMetadataArchetype> types,
        MetadataTerrainDemandPlan? terrainDemandPlan,MetadataProgramMixPlanner.ChoiceCursor? programChoices,CancellationToken token,
        bool deferOrigin=false,Action<long>? charge=null,MetadataProgramMixPlan? sourcePrograms=null)
    {
        var nodes = new List<WorldMetadataNode>(); var connections = new List<WorldMetadataConnection>();
        var deferredCoordinates=new List<MetadataRelativePosition>();
        var footprintDefinitions=new SortedDictionary<string,MetadataCatalogFootprint>(StringComparer.Ordinal);
        var capacityDefinitions=new SortedDictionary<string,IReadOnlyList<MetadataCatalogCapacity>>(StringComparer.Ordinal);
        var terrainDemandUsed=new HashSet<string>(StringComparer.Ordinal);
        var capacityRequests=new List<MetadataCapacityRequest>();
        var variationRequests=new List<MetadataVariationRequest>();long variationWork=0;
        var programMixAllocations=new List<MetadataProgramMixAllocation>();
        var inherited=sourcePrograms?.Allocations.ToDictionary(a=>a.Key,StringComparer.Ordinal);
        var inheritedUsed=new HashSet<string>(StringComparer.Ordinal);
        var structures = new SortedDictionary<string, MetadataStructureDefinition>(StringComparer.Ordinal); long propertyBytes=0;
        Visit(recipe.RootArchetypeId, recipe.Id, null, types[recipe.RootArchetypeId].Purpose, recipe.Origin?.ToArray(), 0);
        if(inherited is not null&&!inheritedUsed.SetEquals(inherited.Keys))throw new ArgumentException("METADATA_POPULATION_SOURCE_PROGRAM_UNUSED");
        if(terrainDemandPlan is not null)
        {
            if(!terrainDemandUsed.SetEquals(terrainDemandPlan.Definition.Rules.Select(r=>r.Id)))throw new ArgumentException("METADATA_TERRAIN_DEMAND_BINDING_UNUSED");
            MetadataTerrainDemandPlanner.MarkBindings(terrainDemandPlan,nodes);
            MetadataTerrainDemandPlanner.ValidateBindings(terrainDemandPlan,nodes,token);
        }
        var expandedCapacity=capacityRequests.Count==0?null:new MetadataCapacityPlanner().Plan(capacityRequests,capacityDefinitions,structures,nodes,token);
        var nodeIds=nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);var explicitIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var link in recipe.AdditionalConnections ?? [])
        {
            token.ThrowIfCancellationRequested();charge?.Invoke(1);
            if(link is null || !SettlementCatalogRegistry.ValidId(link.Id) || !explicitIds.Add(link.Id) ||
                !SettlementCatalogRegistry.ValidId(link.Kind) || !nodeIds.Contains(link.FromId) || !nodeIds.Contains(link.ToId) || link.FromId==link.ToId)
                throw new ArgumentException("METADATA_EXPLICIT_CONNECTION_INVALID");
            if(connections.Count>=recipe.MaximumConnections)throw new ArgumentException("METADATA_CONNECTION_BUDGET");
            connections.Add(new(recipe.Id+"/explicit/"+link.Id,link.Kind,link.FromId,link.ToId,link.Bidirectional,"recipe:"+link.Id,Properties(link.Properties),link.LayerId));
        }
        var variation=variationRequests.Count==0?null:new MetadataVariationPlanner().Plan(variationRequests,nodes,token);
        var programMix=programMixAllocations.Count==0?null:MetadataProgramMixPlanner.Assemble(programMixAllocations,nodes,token);
        return new(nodes.ToArray(),connections.ToArray(),deferredCoordinates.ToArray(),footprintDefinitions,structures,expandedCapacity,variation,programMix);

        string? ResolveStructure(WorldMetadataArchetype type)
        {
            charge?.Invoke(1);
            if(type.Structure is not {} reference)return null;
            if(structureCatalogs is null)throw new ArgumentException("METADATA_STRUCTURE_CATALOG_REQUIRED");
            var pack=structureCatalogs.Resolve(reference.Catalog);
            var definition=pack.Structures.SingleOrDefault(d=>d.Id==reference.DefinitionId)??throw new ArgumentException("METADATA_STRUCTURE_UNKNOWN");
            if(reference.Technology is {} technology)
            {
                if(!SettlementCatalogRegistry.ValidId(technology)||definition.SitingProfile?.AllowedTechnologies.Contains(technology)!=true)
                    throw new ArgumentException("METADATA_STRUCTURE_TECHNOLOGY_INVALID");
                definition=pack.Policy.Resolve(definition,technology);definition.Validate();
                if(pack.Policy.RequiredCapabilities(definition,technology).Any(c=>!pack.Provenance.EnabledCapabilities.Contains(c)))
                    throw new ArgumentException("METADATA_STRUCTURE_TECHNOLOGY_CAPABILITY_REQUIRED");
            }
            var variant=definition.SizeVariants?.SingleOrDefault(v=>v.Id==reference.VariantId)??throw new ArgumentException("METADATA_STRUCTURE_VARIANT_UNKNOWN");
            string key=pack.Descriptor.Id+"/"+definition.Id+"/"+variant.Id;
            if(reference.Technology is not null)key+="/"+reference.Technology;
            structures.TryAdd(key,new(pack.Provenance,definition,variant,reference.Technology));return key;
        }

        void Visit(string typeId, string id, string? parentId, string purpose, double[]? position, int depth,string? variationKey=null,string? programMixKey=null,string? capacityKey=null)
        {
            token.ThrowIfCancellationRequested();charge?.Invoke(1);
            if (depth > 32 || id.Length>2048 || nodes.Count >= recipe.MaximumNodes) throw new ArgumentException("METADATA_NODE_OR_DEPTH_BUDGET");
            if (!deferOrigin && recipe.RequireAllPositions && position is null && recipe.Placement is null) throw new ArgumentException("METADATA_POSITION_REQUIRED:" + id);
            var type = types[typeId]; string? structureKey = ResolveStructure(type);
            if(type.Component?.Role==MetadataComponentRole.MovingPart&&parentId is null)
                throw new ArgumentException("METADATA_COMPONENT_PARENT_REQUIRED:"+id);
            if(type.PhysicalFootprint is {} physical)footprintDefinitions.TryAdd(type.Id,MetadataFootprintResolver.Snapshot(physical,recipe.Space.Unit));
            nodes.Add(new(id,type.Id,type.Kind,parentId,purpose,Properties(type.Properties),position,structureKey,type.LayerId,variationKey,ProgramMixKey:programMixKey,CapacityKey:capacityKey,Component:type.Component));
            var slots = new Dictionary<string,List<string>>(StringComparer.Ordinal) { ["self"] = [id] };
            var childKinds = new List<string>();
            foreach (var part in type.Parts.OrderBy(p=>p.Id,StringComparer.Ordinal))
            {
                charge?.Invoke(1);
                token.ThrowIfCancellationRequested();
                int? terrainCount=MetadataTerrainDemandPlanner.Count(terrainDemandPlan,id,part,terrainDemandUsed);
                int count=terrainCount??(part.CountParameter is { } parameter ? recipe.Parameters.TryGetValue(parameter,out int n) ? n :
                    throw new ArgumentException("METADATA_PARAMETER_MISSING:"+parameter) : part.Count);
                if(part.Capacity is {} capacity)
                {
                    if(capacityRequests.Count>=4096)throw new ArgumentException("METADATA_CAPACITY_REQUEST_BUDGET");
                    var target=types[part.ArchetypeId];string? key=ResolveStructure(target);
                    if(target.Capacities is {} quantities)capacityDefinitions.TryAdd(target.Id,quantities);
                    IReadOnlyDictionary<string,string?>? candidateKeys=null;
                    if(part.Variation is {} choice)
                    {
                        var keys=new SortedDictionary<string,string?>(StringComparer.Ordinal);
                        foreach(string candidate in choice.ArchetypeIds.Order(StringComparer.Ordinal))
                        {
                            var variant=types[candidate];keys.Add(candidate,ResolveStructure(variant));
                            if(variant.Capacities is {} variantCapacities)capacityDefinitions.TryAdd(variant.Id,variantCapacities);
                        }
                        candidateKeys=keys;
                    }
                    double required=capacity.QuantityParameter is {} demandParameter?recipe.Parameters.TryGetValue(demandParameter,out int quantity)?quantity:
                        throw new ArgumentException("METADATA_PARAMETER_MISSING:"+demandParameter):capacity.RequiredQuantity;
                    var request=new MetadataCapacityRequest(id,part.Id,target.Id,key,terrainCount is null?count:part.Count,required,capacity,part.Variation,candidateKeys,terrainCount);
                    charge?.Invoke(1);
                    count=MetadataCapacityPlanner.Expand(request,capacityDefinitions,structures,amount=>{
                        if(part.Variation is not null){variationWork+=amount;if(variationWork>4000000)throw new ArgumentException("METADATA_VARIATION_WORK_BUDGET");}
                        charge?.Invoke(amount);
                    },token).Count;capacityRequests.Add(request);
                }
                MetadataProgramMixAllocation? mix=null;
                if(part.ProgramMix is {} mixRule)
                {
                    if(programMixAllocations.Count>=4096)throw new ArgumentException("METADATA_PROGRAM_MIX_PLAN_INVALID");
                    var supplies=mixRule.Offers.Select(o=>MetadataProgramMixPlanner.Supply(types[o.ArchetypeId],o,ResolveStructure(types[o.ArchetypeId]),structures)).ToArray();
                    var parameters=new SortedDictionary<string,int>(StringComparer.Ordinal);
                    foreach(string parameterId in mixRule.Demands.Where(d=>d.QuantityParameter is not null).Select(d=>d.QuantityParameter!).Distinct(StringComparer.Ordinal))
                        parameters.Add(parameterId,recipe.Parameters.TryGetValue(parameterId,out int value)?value:throw new ArgumentException("METADATA_PARAMETER_MISSING:"+parameterId));
                    var mixRequest=new MetadataProgramMixRequest(id,type.Id,part.Id,part.ArchetypeId,count,mixRule,supplies,parameters,ExactCount:terrainCount);
                    if(inherited is not null&&inherited.TryGetValue(MetadataProgramMixPlanner.Key(mixRequest),out var sourceMix))
                    {mix=new MetadataProgramMixPlanner().BindSource(mixRequest,sourceMix,structures,token);inheritedUsed.Add(sourceMix.Key);}
                    else mix=programChoices is null?new MetadataProgramMixPlanner().Solve(mixRequest,structures,token):programChoices.Select(mixRequest,structures,token);
                    charge?.Invoke(mix.Work);programMixAllocations.Add(mix);if(programMixAllocations.Sum(a=>a.Work)>20000000)throw new ArgumentException("METADATA_PROGRAM_MIX_TOTAL_WORK_BUDGET");
                    count=mix.Choices.Count;
                }
                if(count<0 || count>recipe.MaximumNodes || (long)nodes.Count+count>recipe.MaximumNodes)
                    throw new ArgumentException("METADATA_PART_COUNT_OR_NODE_BUDGET");
                var relative=part.Layout is {} layout?Plan(layout,recipe.Space.Axes.Count,count,token):null;
                MetadataVariationRequest? variationRequest=null;
                if(part.Variation is {} variationRule)
                {
                    if(variationRequests.Count>=4096)throw new ArgumentException("METADATA_VARIATION_LIMIT");
                    MetadataVariationPlanner.ValidateRule(variationRule);variationWork+=(long)count*MetadataVariationPlanner.Tickets(variationRule);
                    if(variationWork>4000000)throw new ArgumentException("METADATA_VARIATION_WORK_BUDGET");
                    charge?.Invoke((long)count*MetadataVariationPlanner.Tickets(variationRule));
                    variationRequest=new(id,type.Id,part.Id,part.ArchetypeId,types[part.ArchetypeId].Kind,count,variationRule);
                    variationRequests.Add(variationRequest);
                }
                var members = new List<string>();slots.Add(part.Id,members);
                for(int i=0;i<count;i++)
                {
                    string child=id+"/"+part.Id+"-"+i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    double[]? childPosition=null;
                    if(relative is not null)
                    {
                        if(position is null)
                        {
                            if(!deferOrigin&&recipe.Placement is null)throw new ArgumentException("METADATA_PARENT_POSITION_REQUIRED:"+id);
                            deferredCoordinates.Add(new(child,id,relative[i]));
                        }
                        else
                        {
                            childPosition=position.Select((p,axis)=>p+relative[i][axis]).ToArray();
                            if(childPosition.Any(p=>!double.IsFinite(p)))throw new ArgumentException("METADATA_POSITION_NONFINITE");
                        }
                    }
                    string selected=mix is not null?mix.Choices[i].ArchetypeId:variationRequest is null?part.ArchetypeId:MetadataVariationPlanner.Select(variationRequest,i,token).ArchetypeId;
                    members.Add(child);childKinds.Add(types[selected].Kind);
                    Visit(selected,child,id,part.Purpose,childPosition,depth+1,variationRequest is null?null:MetadataVariationPlanner.Key(variationRequest),mix?.Key,
                        part.Capacity is null?null:MetadataCapacityPlanner.Key(id,part.Id));
                }
            }
            foreach(var requirement in type.ChildRequirements)
            {
                int count=childKinds.Count(k=>k==requirement.Kind);
                if(count<requirement.Minimum || count>requirement.Maximum)
                    throw new ArgumentException("METADATA_CHILD_REQUIREMENT:"+id+":"+requirement.Kind);
            }
            var localEdges=new List<(string From,string To)>();
            foreach(var rule in type.Connections.OrderBy(r=>r.Id,StringComparer.Ordinal))
            {
                var from=slots[rule.FromPart]; var to=rule.ToPart is null ? [] : slots[rule.ToPart]; int index=0;
                switch(rule.Pattern)
                {
                    case MetadataConnectionPattern.Chain:
                        for(int i=1;i<from.Count;i++)Add(from[i-1],from[i]);break;
                    case MetadataConnectionPattern.Hub:
                        if(to.Count==0)break;
                        if(from.Count!=1)throw new ArgumentException("METADATA_HUB_REQUIRES_ONE_SOURCE:"+id);
                        foreach(var target in to)Add(from[0],target);break;
                    case MetadataConnectionPattern.Pairs:
                        if(from.Count!=to.Count)throw new ArgumentException("METADATA_PAIR_COUNT_MISMATCH:"+id);
                        for(int i=0;i<from.Count;i++)Add(from[i],to[i]);break;
                    case MetadataConnectionPattern.AllPairs:
                        if((long)from.Count*to.Count>recipe.MaximumConnections-connections.Count)throw new ArgumentException("METADATA_CONNECTION_BUDGET");
                        foreach(var a in from)foreach(var b in to)Add(a,b);break;
                    default:throw new ArgumentException("METADATA_CONNECTION_PATTERN_INVALID");
                }
                void Add(string a,string b)
                {
                    token.ThrowIfCancellationRequested();charge?.Invoke(1);
                    if(a==b)throw new ArgumentException("METADATA_CONNECTION_SELF_LINK");
                    if(connections.Count>=recipe.MaximumConnections)throw new ArgumentException("METADATA_CONNECTION_BUDGET");
                    connections.Add(new(id+"/link-"+rule.Id+"-"+index++,rule.Kind,a,b,rule.Bidirectional,type.Id+":"+rule.Id,Properties(rule.Properties),rule.LayerId));
                    localEdges.Add((a,b));
                }
            }
            if(type.RequireWeaklyConnectedChildren && childKinds.Count>1)
            {
                var children=slots.Where(s=>s.Key!="self").SelectMany(s=>s.Value).ToArray();
                var seen=new HashSet<string>(StringComparer.Ordinal);var queue=new Queue<string>();queue.Enqueue(children[0]);
                var graph=new Dictionary<string,List<string>>(StringComparer.Ordinal);
                foreach(var edge in localEdges)
                {
                    if(!graph.TryGetValue(edge.From,out var a))graph[edge.From]=a=[];a.Add(edge.To);
                    if(!graph.TryGetValue(edge.To,out var b))graph[edge.To]=b=[];b.Add(edge.From);
                }
                while(queue.TryDequeue(out string? next))
                {token.ThrowIfCancellationRequested();charge?.Invoke(1);if(!seen.Add(next))continue;if(graph.TryGetValue(next,out var linked))foreach(var neighbor in linked)queue.Enqueue(neighbor);}
                if(children.Any(c=>!seen.Contains(c)))throw new ArgumentException("METADATA_CHILDREN_DISCONNECTED:"+id);
            }
        }
        IReadOnlyDictionary<string,JsonElement> Properties(IReadOnlyDictionary<string,JsonElement>? values)
        {
            charge?.Invoke(1);
            if(values is null)return new ReadOnlyDictionary<string,JsonElement>(new Dictionary<string,JsonElement>());
            ValidateProperties(values,token);
            propertyBytes+=JsonSerializer.SerializeToUtf8Bytes(values).Length;charge?.Invoke(1+values.Count);
            if(propertyBytes>67108864)throw new ArgumentException("METADATA_EXPANDED_PROPERTIES_BUDGET");
            var snapshot=new SortedDictionary<string,JsonElement>(StringComparer.Ordinal);
            foreach(var value in values)snapshot.Add(value.Key,value.Value.Clone());
            return new ReadOnlyDictionary<string,JsonElement>(snapshot);
        }
    }
}
