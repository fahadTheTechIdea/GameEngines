using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>A regional world and independent game-authored coordinate domains.</summary>
public sealed record MetadataRegionalDomainWorldDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    MetadataRegionalWorldDefinition? RegionalWorld,IReadOnlyList<MetadataWorldDomain> AdditionalDomains,
    IReadOnlyList<MetadataDomainTransition> Transitions,IReadOnlyList<MetadataDomainReachability>? Reachability=null,
    IReadOnlyList<MetadataDomainJourney>? Journeys=null,int MaximumTotalNodes=50000,int MaximumTotalConnections=100000,
    long MaximumTransitionWork=20000000,long MaximumGraphWork=1000000,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignRequest? RegionalDesign=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataDomainProgressionRule>? Progression=null);

/// <summary>Associates a domain graph edge with its actual qualified regional route.</summary>
public sealed record MetadataRegionalDomainRoute(string TransitionId,string RegionalConnectionId,
    WorldRouteResult Forward,WorldRouteResult? Reverse);

public sealed record MetadataRegionalDomainSelection(IReadOnlyList<MetadataRegionalDesignAttempt> Attempts,
    IReadOnlyList<MetadataRegionalDesignSeedDerivation>? SeedDerivations);

public sealed record MetadataRegionalDomainWorldDocument(int Version,MetadataRegionalDomainWorldDefinition Definition,
    string DefinitionSha256,MetadataRegionalWorldDocument RegionalWorld,MetadataWorldDomainDocument Domains,
    IReadOnlyList<MetadataRegionalDomainRoute> RegionalRoutes,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDomainSelection? Selection=null);

/// <summary>Composes qualified regional routes with independently generated domains.
/// Region IDs are domain IDs. Explicit domain changes remain game-defined and have no inferred geometry.</summary>
public sealed class MetadataRegionalDomainWorldGenerator
{
    private readonly WorldMetadataGenerator metadata;
    private readonly MetadataRegionalWorldGenerator regional;

    public MetadataRegionalDomainWorldGenerator(WorldMetadataGenerator? metadata=null)
    {
        this.metadata=metadata??new();
        regional=new MetadataRegionalWorldGenerator(this.metadata);
    }

    public MetadataRegionalDomainWorldDocument Generate(MetadataRegionalDomainWorldDefinition definition,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||
            !WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||
            (definition.RegionalWorld is null)==(definition.RegionalDesign is null)||
            definition.AdditionalDomains is not {Count:>=1 and <=14}||
            definition.Transitions is null||definition.Transitions.Count>128)
            throw new ArgumentException("METADATA_REGIONAL_DOMAIN_DEFINITION_INVALID");
        var regionIds=definition.RegionalWorld?.Regions?.Select(r=>r?.Id).ToArray()??
            definition.RegionalDesign?.Template?.Regions?.Select(r=>r?.Id).ToArray();
        if(regionIds is not {Length:>=2 and <=15}||regionIds.Length+definition.AdditionalDomains.Count>16)
            throw new ArgumentException("METADATA_REGIONAL_DOMAIN_DEFINITION_INVALID");
        var domainIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in regionIds.Concat(definition.AdditionalDomains.Select(d=>d?.Id)))
            if(!WorldEnvironmentQuery.Id(id)||!domainIds.Add(id!))throw new ArgumentException("METADATA_REGIONAL_DOMAIN_ID_INVALID");
        definition=MetadataExecutionCache.Clone(definition);
        if(definition.RegionalWorld is {} direct)return Compose(definition,regional.Generate(direct,token),null,token);
        MetadataRegionalDomainWorldDocument? selected=null;
        var design=new MetadataRegionalDesignGenerator(metadata).GenerateConstrained(definition.RegionalDesign!,world=>{
            try{selected=Compose(definition,world,null,token);return null;}
            catch(ArgumentException error)when(DomainCandidateRefusal(error)){return error.Message;}
        },token);
        return selected! with{Selection=new(design.Receipt.Attempts,design.Receipt.SeedDerivations)};
    }

    private static bool DomainCandidateRefusal(ArgumentException error)=>new[]{"METADATA_DOMAIN_UNREACHABLE:",
        "METADATA_DOMAIN_JOURNEY_UNREACHABLE:","METADATA_DOMAIN_JOURNEY_MILESTONE_BYPASS:","METADATA_DOMAIN_JOURNEY_MILESTONE_ORDER:",
        "METADATA_PROGRESSION_UNREACHABLE:","METADATA_PROGRESSION_MILESTONE_BYPASS:","METADATA_PROGRESSION_MILESTONE_ORDER:","METADATA_PROGRESSION_RETURN_UNREACHABLE:","METADATA_REGIONAL_PROGRESSION_SELECTOR_COLLISION:"}
        .Any(prefix=>error.Message.StartsWith(prefix,StringComparison.Ordinal))||error is MetadataRegionalProgressionSelectorException;

    private MetadataRegionalDomainWorldDocument Compose(MetadataRegionalDomainWorldDefinition definition,MetadataRegionalWorldDocument world,
        MetadataRegionalDomainSelection? selection,CancellationToken token)
    {
        var regions=world.Definition.Regions.Select(r=>new MetadataWorldDomain(r.Id,r.Purpose,r.SourceRuleId,
            MetadataRegionalWorldGenerator.EffectiveRecipe(world.Definition,r,token),r.Catalog)).ToArray();
        var links=new List<MetadataDomainTransition>();var sources=new List<MetadataRegionalDomainRoute>();int index=0;
        foreach(var route in world.Connections.OrderBy(r=>r.Definition.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var c=route.Definition;
            if(c.From.Selector is not null||c.To.Selector is not null||
                c.From.RegionId==c.To.RegionId||route.Forward.Status!=WorldRouteStatus.Found||route.Forward.Path is null||
                (c.Bidirectional&&(route.Reverse?.Status!=WorldRouteStatus.Found||route.Reverse.Path is null)))
                throw new ArgumentException("METADATA_REGIONAL_DOMAIN_ROUTE_UNSUPPORTED:"+c.Id);
            var id="regional-route-"+(index++).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var from=new MetadataDomainEndpoint(c.From.RegionId,c.From.NodeId,c.From.PortId,Profile:c.Profile,UseNodePosition:c.From.PortId is null);
            var to=new MetadataDomainEndpoint(c.To.RegionId,c.To.NodeId,c.To.PortId,Profile:c.Profile,UseNodePosition:c.To.PortId is null);
            links.Add(new(id,c.Kind,c.Purpose,c.SourceRuleId,from,to,c.Bidirectional));
            sources.Add(new(id,c.Id,route.Forward,route.Reverse));
        }
        var input=new MetadataWorldDomainDefinition(1,definition.Id,definition.Purpose,definition.SourceRuleId,
            [..regions,..definition.AdditionalDomains],[..links,..definition.Transitions],definition.Reachability,
            definition.MaximumTotalNodes,definition.MaximumTotalConnections,definition.MaximumTransitionWork,definition.MaximumGraphWork,definition.Journeys,definition.Progression);
        var domains=metadata.GenerateDomains(input,token);
        foreach(var region in world.Regions)
        {
            token.ThrowIfCancellationRequested();var generated=domains.Domains.Single(d=>d.Id==region.RegionId);
            if(!MetadataPopulationSources.Equal(region.World,generated.World)||!MetadataPopulationSources.Equal(region.Dependencies,generated.Dependencies))
                throw new ArgumentException("METADATA_REGIONAL_DOMAIN_SOURCE_MISMATCH:"+region.RegionId);
        }
        foreach(var source in sources)
        {
            var link=domains.Transitions.Single(t=>t.Definition.Id==source.TransitionId);
            var from=new WorldPoint(link.From.Position![0],link.From.Position[1]);
            var to=new WorldPoint(link.To.Position![0],link.To.Position[1]);
            bool Matches(WorldRouteResult route,WorldPoint a,WorldPoint b)=>route.Path!.Points.Count>=2&&route.Path.Points[0]==a&&route.Path.Points[^1]==b;
            if(!Matches(source.Forward,from,to)||(source.Reverse is not null&&!Matches(source.Reverse,to,from)))
                throw new ArgumentException("METADATA_REGIONAL_DOMAIN_ROUTE_ENDPOINT_MISMATCH:"+source.RegionalConnectionId);
        }
        return new(1,definition,MetadataExecutionCache.Hash(definition),world,domains,sources.AsReadOnly(),selection);
    }

    public void Validate(MetadataRegionalDomainWorldDocument document,CancellationToken token=default)
    {
        if(document is null||document.Version!=1||document.Definition is null||document.DefinitionSha256!=MetadataExecutionCache.Hash(document.Definition))
            throw new ArgumentException("METADATA_REGIONAL_DOMAIN_DOCUMENT_INVALID");
        if(!MetadataPopulationSources.Equal(Generate(document.Definition,token),document))
            throw new ArgumentException("METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");
    }

    private static string JourneyHtml(MetadataRegionalDomainWorldDocument document)
    {
        if(document.Domains.Journeys is not {Count:>0} journeys)return "";
        static string E(string? value)=>System.Net.WebUtility.HtmlEncode(value??"");
        var regionalRoutes=document.RegionalRoutes.ToDictionary(r=>r.TransitionId,StringComparer.Ordinal);
        var transitions=document.Domains.Transitions.ToDictionary(t=>t.Definition.Id,StringComparer.Ordinal);
        var html=new System.Text.StringBuilder("<h2>Qualified journeys</h2><p>Regional paths have generated geometry. Explicit domain changes are game-defined and do not provide connector geometry. Each local segment refers to its saved source.</p>");
        foreach(var journey in journeys)
        {
            html.Append("<h3>").Append(E(journey.Requirement.Purpose)).Append("</h3>");
            void Direction(string title,IReadOnlyList<MetadataDomainJourneyStep> steps,IReadOnlyList<IReadOnlyList<string>>? states)
            {
                html.Append("<h4>").Append(title).Append("</h4><ol>");
                for(int i=0;i<Math.Min(steps.Count,128);i++)
                {
                    var step=steps[i];
                    html.Append("<li>");
                    if(step.Kind=="transition"&&regionalRoutes.TryGetValue(step.SourceId,out var route))
                    {
                        var path=step.Reverse?route.Reverse:route.Forward;
                        html.Append("Regional route ").Append(E(route.RegionalConnectionId)).Append(" — ").Append(E(step.DomainId))
                            .Append(" → ").Append(E(step.ToDomainId)).Append("; ").Append(path?.Path?.Points.Count??0).Append(" path points");
                    }
                    else if(step.Kind=="transition"&&transitions.TryGetValue(step.SourceId,out var transition))
                        html.Append("Explicit domain change ").Append(E(transition.Definition.Kind)).Append(" — ").Append(E(step.DomainId)).Append(" → ").Append(E(step.ToDomainId));
                    else if(step.Kind=="corridor-segment")
                        html.Append("Qualified corridor ").Append(E(step.CorridorGroupId)).Append(" / ").Append(E(step.CorridorSegmentId)).Append(" in ").Append(E(step.DomainId));
                    else html.Append(E(step.Kind)).Append(" ").Append(E(step.SourceId)).Append(step.SegmentIndex>=0?" / "+step.SegmentIndex:"").Append(" in ").Append(E(step.DomainId));
                    if(step.Reverse)html.Append(" (reverse)");
                    if(states is not null&&states[i].Count>0)html.Append(" — capabilities: ").Append(E(string.Join(", ",states[i])));
                    html.Append("</li>");
                }
                html.Append("</ol>");
                if(steps.Count>128)html.Append("<p>Showing 128 of ").Append(steps.Count).Append(" steps; complete evidence is in world.json.</p>");
            }
            Direction("Forward",journey.Forward,journey.ForwardCapabilityStates);
            if(journey.Return is {} back)Direction("Return",back,journey.ReturnCapabilityStates);
        }
        return html.ToString();
    }

    private static string ProgressionHtml(MetadataRegionalDomainWorldDocument document)
    {
        if(document.Domains.Progression is not {} plan)return "";
        static string E(string? value)=>System.Net.WebUtility.HtmlEncode(value??"");
        var html=new System.Text.StringBuilder("<h2>Gameplay progression</h2><p>Logical capability and milestone evidence; see qualified journeys for physical routes.</p>");
        foreach(var result in plan.Results)
        {
            html.Append("<h3>").Append(E(result.RuleId)).Append("</h3>");
            foreach(var path in result.Paths)
            {
                html.Append("<p>Goal: ").Append(E(path.Goal.DomainId)).Append(" / ").Append(E(path.Goal.NodeId)).Append("</p><ol>");
                for(int i=0;i<Math.Min(path.Nodes.Count,128);i++)
                {
                    var node=path.Nodes[i];html.Append("<li>").Append(E(node.DomainId)).Append(" / ").Append(E(node.NodeId));
                    if(path.CapabilityStates is {} states&&states[i].Count>0)html.Append(" — capabilities: ").Append(E(string.Join(", ",states[i])));
                    html.Append("</li>");
                }
                html.Append("</ol>");
                if(path.Nodes.Count>128)html.Append("<p>Showing 128 of ").Append(path.Nodes.Count).Append(" nodes; complete evidence is in world.json.</p>");
            }
        }
        return html.ToString();
    }

    /// <summary>Atomically publishes the complete composition and both existing inspectors.</summary>
    public async Task PublishAsync(MetadataRegionalDomainWorldDocument document,string outputDirectory,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        string target=Path.GetFullPath(outputDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_REGIONAL_DOMAIN_OUTPUT_EXISTS");
        Validate(document,token);
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".regional-domains-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);Exception? failure=null;
        try
        {
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"world.json"),document,token);
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"definition.json"),document.Definition,token);
            await regional.PublishAsync(document.RegionalWorld,Path.Combine(staging,"regions"),token);
            await metadata.PublishDomainsAsync(document.Domains,Path.Combine(staging,"domains"),null,token);
            string selectionHtml="";
            if(document.Selection is {} selection)
            {
                await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"selection.json"),selection,token);
                var rows=selection.Attempts.Select(a=>"<li>Attempt "+a.Ordinal+": "+(a.Accepted?"selected":
                    "rejected — "+System.Net.WebUtility.HtmlEncode(a.Rejection))+"</li>");
                selectionHtml="<h2>Program selection</h2><ol>"+string.Concat(rows)+"</ol><p><a href=\"selection.json\">Selection evidence</a></p>";
            }
            await File.WriteAllTextAsync(Path.Combine(staging,"index.html"),
                "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>World composition</title><h1>World composition</h1><p><a href=\"regions/schematic.html\">Regional world</a></p><p><a href=\"domains/index.html\">Independent domains and journeys</a></p>"+selectionHtml+JourneyHtml(document)+ProgressionHtml(document)+"<p><a href=\"world.json\">Composition source and route evidence</a></p></html>",token);
            await MetadataDirectoryPublication.CommitAsync(staging,target,ct=>{
                foreach(var domain in document.Domains.Domains)
                {
                    var source=document.Domains.Definition.Domains.Single(d=>d.Id==domain.Id);
                    if(!MetadataPopulationSources.Equal(domain.Dependencies,metadata.Dependencies(source.Recipe,source.Catalog,ct)))
                        throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
                }
            },token);
        }
        catch(Exception error){failure=error;throw;}
        finally{MetadataDirectoryPublication.Cleanup(staging,parent,".regional-domains-",failure);}
    }

    public async Task<MetadataRegionalDomainWorldDocument> ReadAsync(string path,CancellationToken token=default)
    {
        var document=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDocument>(path,token);
        Validate(document,token);return document;
    }
}
