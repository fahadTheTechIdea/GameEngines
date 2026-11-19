using System.Net;
using System.Text;
using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataWorldDomain(string Id,string Purpose,string SourceRuleId,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog);
public sealed record MetadataInteriorOpeningSelector(string ConnectionKind,int ConnectionIndex=0);
public sealed record MetadataDomainEndpoint(string DomainId,string NodeId,string? AccessPortId=null,string? InteriorConnectionId=null,WorldTraversalProfile? Profile=null,string? NetworkRuleId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataInteriorOpeningSelector? InteriorOpening=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool UseNodePosition=false);
public sealed record MetadataDomainTransition(string Id,string Kind,string Purpose,string SourceRuleId,MetadataDomainEndpoint From,MetadataDomainEndpoint To,bool Bidirectional=false);
public sealed record MetadataDomainReachability(string Id,string Purpose,string SourceRuleId,string FromDomainId,string ToDomainId,bool RequireReturn=false);
public sealed record MetadataWorldDomainDefinition(int Version,string Id,string Purpose,string SourceRuleId,IReadOnlyList<MetadataWorldDomain> Domains,
    IReadOnlyList<MetadataDomainTransition> Transitions,IReadOnlyList<MetadataDomainReachability>? Reachability=null,
    int MaximumTotalNodes=50000,int MaximumTotalConnections=100000,long MaximumTransitionWork=20000000,long MaximumGraphWork=1000000,
    IReadOnlyList<MetadataDomainJourney>? Journeys=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataDomainProgressionRule>? Progression=null);
public sealed record MetadataGeneratedDomain(string Id,WorldMetadataDocument World,IReadOnlyList<MetadataGenerationDependency> Dependencies);
public sealed record MetadataDomainEndpointResult(MetadataDomainEndpoint Definition,IReadOnlyList<double>? Position,double? AvailableWidth,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ResolvedInteriorConnectionId=null);
public sealed record MetadataDomainTransitionResult(MetadataDomainTransition Definition,MetadataDomainEndpointResult From,MetadataDomainEndpointResult To);
public sealed record MetadataDomainReachabilityResult(MetadataDomainReachability Requirement,IReadOnlyList<string> ForwardTransitions,IReadOnlyList<string>? ReturnTransitions);
public sealed record MetadataWorldDomainDocument(int Version,MetadataWorldDomainDefinition Definition,string DefinitionSha256,
    IReadOnlyList<MetadataGeneratedDomain> Domains,IReadOnlyList<MetadataDomainTransitionResult> Transitions,
    IReadOnlyList<MetadataDomainReachabilityResult> Reachability,long TransitionWork,long GraphWork,IReadOnlyList<MetadataDomainJourneyResult>? Journeys=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataDomainProgressionPlan? Progression=null);

public sealed partial class WorldMetadataGenerator
{
    /// <summary>Generates independent local domains and qualifies explicit transitions.
    /// Local coordinates are never merged; graph reachability does not invent local journeys or 3D connectors.</summary>
    public MetadataWorldDomainDocument GenerateDomains(MetadataWorldDomainDefinition definition,CancellationToken token=default)
        =>GenerateDomainsCore(definition,null,token);

    internal MetadataWorldDomainDocument GenerateDomainsCore(MetadataWorldDomainDefinition definition,
        IDictionary<string,MetadataExecutionCache>? execution,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||
            !WorldEnvironmentQuery.Text(definition.SourceRuleId)||definition.Domains is not {Count:>=2 and <=16}||definition.Transitions is not {Count:>=1 and <=128}||
            definition.Reachability is {Count:0 or >32}||definition.MaximumTotalNodes is <1 or >100000||definition.MaximumTotalConnections is <1 or >200000||
            definition.MaximumTransitionWork is <1 or >200000000||definition.MaximumGraphWork is <1 or >10000000)
            throw new ArgumentException("METADATA_DOMAIN_DEFINITION_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var domain in definition.Domains)
            if(domain is null||!WorldEnvironmentQuery.Id(domain.Id)||!ids.Add(domain.Id)||!WorldEnvironmentQuery.Text(domain.Purpose)||
                !WorldEnvironmentQuery.Text(domain.SourceRuleId)||domain.Recipe is null||domain.Catalog is null)throw new ArgumentException("METADATA_DOMAIN_INVALID");
        var transitionIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var transition in definition.Transitions)
        {
            if(transition is null||!WorldEnvironmentQuery.Id(transition.Id)||!transitionIds.Add(transition.Id)||!WorldEnvironmentQuery.Id(transition.Kind)||
                !WorldEnvironmentQuery.Text(transition.Purpose)||!WorldEnvironmentQuery.Text(transition.SourceRuleId)||transition.From is null||transition.To is null||
                transition.From.DomainId==transition.To.DomainId)throw new ArgumentException("METADATA_DOMAIN_TRANSITION_INVALID");
            foreach(var endpoint in new[]{transition.From,transition.To})
                if(!ids.Contains(endpoint.DomainId)||!WorldEnvironmentQuery.Text(endpoint.NodeId)||
                    (endpoint.AccessPortId is not null&&!WorldEnvironmentQuery.Id(endpoint.AccessPortId))||
                    (endpoint.InteriorConnectionId is not null&&!WorldEnvironmentQuery.Text(endpoint.InteriorConnectionId))||
                    (endpoint.NetworkRuleId is not null&&!WorldEnvironmentQuery.Id(endpoint.NetworkRuleId))||
                    !ValidInteriorOpeningSelector(endpoint.InteriorOpening)||
                    EndpointSelectorCount(endpoint)>1)throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_INVALID");
        }
        var requirementIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var requirement in definition.Reachability??[])
            if(requirement is null||!WorldEnvironmentQuery.Id(requirement.Id)||!requirementIds.Add(requirement.Id)||!WorldEnvironmentQuery.Text(requirement.Purpose)||
                !WorldEnvironmentQuery.Text(requirement.SourceRuleId)||!ids.Contains(requirement.FromDomainId)||!ids.Contains(requirement.ToDomainId)||
                requirement.FromDomainId==requirement.ToDomainId)throw new ArgumentException("METADATA_DOMAIN_REACHABILITY_INVALID");
        ValidateDomainJourneys(definition,ids);
        if(definition.Progression is {} progressionRules)MetadataDomainProgressionPlanner.ValidateRules(progressionRules,ids);
        definition=MetadataExecutionCache.Clone(definition);
        definition=definition with{Domains=definition.Domains.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray(),
            Transitions=definition.Transitions.OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray(),Reachability=definition.Reachability?.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray(),
            Journeys=definition.Journeys?.OrderBy(j=>j.Id,StringComparer.Ordinal).ToArray(),
            Progression=definition.Progression?.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray()};
        if(JsonSerializer.SerializeToUtf8Bytes(definition,WorldJson.CreateOptions()).Length>16777216)throw new ArgumentException("METADATA_DOMAIN_SOURCE_SIZE_LIMIT");
        var generated=new List<MetadataGeneratedDomain>();long nodes=0,connections=definition.Transitions.Count;
        foreach(var domain in definition.Domains)
        {
            token.ThrowIfCancellationRequested();var dependencies=Dependencies(domain.Recipe,domain.Catalog,token);
            MetadataExecutionCache? cache=null;
            if(execution is not null&&!execution.TryGetValue(domain.Id,out cache))execution.Add(domain.Id,cache=new());
            var world=GenerateCore(domain.Recipe,domain.Catalog,cache,token);
            if(execution is not null&&execution.Values.Sum(c=>c.SizeBytes)>134217728)throw new ArgumentException("METADATA_DOMAIN_CACHE_SIZE_LIMIT");
            if(!MetadataPopulationSources.Equal(dependencies,Dependencies(domain.Recipe,domain.Catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
            nodes+=world.Nodes.Count;connections+=world.Connections.Count;
            if(nodes>definition.MaximumTotalNodes||connections>definition.MaximumTotalConnections)throw new ArgumentException("METADATA_DOMAIN_CONTENT_BUDGET");
            generated.Add(new(domain.Id,world,dependencies));
        }
        var worlds=generated.ToDictionary(d=>d.Id,d=>d.World,StringComparer.Ordinal);
        var budget=new WorldSpatialWorkBudget(definition.MaximumTransitionWork,token,failureCode:"METADATA_DOMAIN_TRANSITION_WORK_BUDGET");
        var transitions=new List<MetadataDomainTransitionResult>();
        foreach(var transition in definition.Transitions)
            transitions.Add(new(transition,ResolveDomainEndpoint(transition.From,worlds[transition.From.DomainId],budget,token),
                ResolveDomainEndpoint(transition.To,worlds[transition.To.DomainId],budget,token)));
        var graphBudget=new WorldSpatialWorkBudget(definition.MaximumGraphWork,token,failureCode:"METADATA_DOMAIN_GRAPH_WORK_BUDGET");
        var graph=definition.Domains.ToDictionary(d=>d.Id,_=>new List<(string To,string Transition)>(),StringComparer.Ordinal);
        foreach(var transition in definition.Transitions)
        {
            graphBudget.Charge();graph[transition.From.DomainId].Add((transition.To.DomainId,transition.Id));
            if(transition.Bidirectional)graph[transition.To.DomainId].Add((transition.From.DomainId,transition.Id));
        }
        IReadOnlyList<string> Path(string from,string to,string requirement)
        {
            var queue=new Queue<string>();queue.Enqueue(from);var seen=new HashSet<string>(StringComparer.Ordinal){from};
            var previous=new Dictionary<string,(string From,string Transition)>(StringComparer.Ordinal);
            while(queue.TryDequeue(out var current))
            {
                graphBudget.Charge();if(current==to){var path=new List<string>();while(current!=from){var p=previous[current];path.Add(p.Transition);current=p.From;}path.Reverse();return path;}
                foreach(var edge in graph[current]){graphBudget.Charge();if(seen.Add(edge.To)){previous.Add(edge.To,(current,edge.Transition));queue.Enqueue(edge.To);}}
            }
            throw new ArgumentException("METADATA_DOMAIN_UNREACHABLE:"+requirement);
        }
        var reachability=new List<MetadataDomainReachabilityResult>();
        foreach(var requirement in definition.Reachability??[])
            reachability.Add(new(requirement,Path(requirement.FromDomainId,requirement.ToDomainId,requirement.Id),
                requirement.RequireReturn?Path(requirement.ToDomainId,requirement.FromDomainId,requirement.Id):null));
        var journeys=PlanDomainJourneys(definition,worlds,transitions,budget,graphBudget,token);
        var progression=definition.Progression is {} progressionInput?new MetadataDomainProgressionPlanner().Plan(progressionInput,generated,transitions,token):null;
        foreach(var domain in generated)
        {
            var source=definition.Domains.Single(d=>d.Id==domain.Id);
            if(!MetadataPopulationSources.Equal(domain.Dependencies,Dependencies(source.Recipe,source.Catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        }
        return new(1,definition,MetadataExecutionCache.Hash(definition),generated,transitions,reachability,budget.Used,graphBudget.Used,journeys,progression);
    }

    private static int EndpointSelectorCount(MetadataDomainEndpoint endpoint)
        =>(endpoint.AccessPortId is null?0:1)+(endpoint.InteriorConnectionId is null?0:1)+(endpoint.NetworkRuleId is null?0:1)+(endpoint.InteriorOpening is null?0:1)+(endpoint.UseNodePosition?1:0);

    private static bool ValidInteriorOpeningSelector(MetadataInteriorOpeningSelector? selector)
        =>selector is null||(WorldEnvironmentQuery.Id(selector.ConnectionKind)&&selector.ConnectionIndex is >=0 and <=127);

    internal static string SelectDomainOpeningConnection(string nodeId,MetadataInteriorOpeningSelector selector,
        IReadOnlyList<WorldMetadataConnection> connections,WorldSpatialWorkBudget budget)
    {
        if(!ValidInteriorOpeningSelector(selector))throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_INVALID");
        // Select the declared incident connection, not the first feasible opening.
        // The existing opening proof must then qualify this exact choice.
        var prefix=new SortedSet<string>(StringComparer.Ordinal);
        foreach(var connection in connections)
        {
            budget.Charge();
            if(connection.Kind!=selector.ConnectionKind||(connection.FromId!=nodeId&&connection.ToId!=nodeId))continue;
            budget.Charge(1+(long)Math.Ceiling(Math.Log2(selector.ConnectionIndex+2)));
            if(!prefix.Add(connection.Id))throw new ArgumentException("METADATA_DOMAIN_OPENING_AMBIGUOUS");
            if(prefix.Count>selector.ConnectionIndex+1)prefix.Remove(prefix.Max!);
        }
        if(prefix.Count<=selector.ConnectionIndex)throw new ArgumentException("METADATA_DOMAIN_OPENING_REQUIRED");
        return prefix.Max!;
    }

    // Identity includes all movement/cost fields, excluding editorial text.
    internal static string DomainTraversalSignature(WorldTraversalProfile p)=>MetadataExecutionCache.Hash(new{p.Id,p.Radius,p.MaximumSlope,
        KindCosts=p.KindCosts.OrderBy(c=>c.Key,StringComparer.Ordinal).ToArray(),p.AscentCost,p.DescentCost,p.MaximumHeightStep,p.Guidance});

    internal static MetadataDomainEndpointResult ResolveDomainEndpoint(MetadataDomainEndpoint endpoint,WorldMetadataDocument world,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        budget.Charge(world.Nodes.Count+1);var node=world.Nodes.SingleOrDefault(n=>n.Id==endpoint.NodeId)??throw new ArgumentException("METADATA_DOMAIN_NODE_REQUIRED:"+endpoint.NodeId);
        if(world.Surface is null)
        {
            if(endpoint.Profile is not null||EndpointSelectorCount(endpoint)!=0)throw new ArgumentException("METADATA_DOMAIN_LOGICAL_ENDPOINT_INVALID");
            return new(endpoint,node.Position,null);
        }
        if(endpoint.Profile is null||EndpointSelectorCount(endpoint)!=1)throw new ArgumentException("METADATA_DOMAIN_PHYSICAL_ENDPOINT_REQUIRED");
        var surface=MetadataInteriorPlanner.WithWalls(world.Surface,world.InteriorPlan);
        MetadataPhysicalAccessPlanner.ValidateSolidSet(surface,world.AccessPlan,token);
        var traversal=new WorldSurfaceTraversal(new WorldSurfaceQuery(surface,token),endpoint.Profile,token);var profile=traversal.Profile;
        if(profile.Radius<=0)throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_RADIUS_REQUIRED");
        if(endpoint.UseNodePosition)
        {
            var point=new MetadataPhysicalAccessPlanner().Endpoint(node,null,world.AccessPlan,surface,profile,budget,token);
            if(!traversal.AllowsEnvelope(WorldSurfaceTraversal.Envelope(point,point,profile.Radius),budget,token)||
                MetadataPhysicalAccessPlanner.IntersectsSolids(world.AccessPlan,point,point,profile.Radius,budget,token))
                throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_BLOCKED");
            return new(endpoint,new[]{point.X,point.Y},null);
        }
        if(endpoint.NetworkRuleId is {} networkId)
        {
            budget.Charge((world.NetworkPlan?.Networks.Count??0)+1);
            var network=world.NetworkPlan?.Networks.SingleOrDefault(n=>n.RuleId==networkId)
                ??throw new ArgumentException("METADATA_DOMAIN_NETWORK_REQUIRED:"+networkId);
            budget.Charge(network.Plan.Destinations.Count+world.NetworkPlan!.Rules.Count+1);
            var destination=network.Plan.Destinations.SingleOrDefault(d=>d.Id==node.Id)
                ??throw new ArgumentException("METADATA_DOMAIN_NETWORK_DESTINATION_REQUIRED:"+node.Id);
            var rule=world.NetworkPlan.Rules.Single(r=>r.Id==networkId);
            if(DomainTraversalSignature(profile)!=DomainTraversalSignature(network.Plan.Options.Profile))
                throw new ArgumentException("METADATA_DOMAIN_NETWORK_PROFILE_MISMATCH");
            // Reuse the network owner's access policy: markers have no invented
            // doorway; solid destinations must still use their declared port.
            var point=new MetadataPhysicalAccessPlanner().Endpoint(node,rule.PortId,world.AccessPlan,surface,profile,budget,token);
            if(point!=destination.Position)throw new ArgumentException("METADATA_DOMAIN_NETWORK_ENDPOINT_MISMATCH");
            double? available=rule.PortId is {} port?world.AccessPlan!.Sites.Single(s=>s.NodeId==node.Id).Ports.Single(p=>p.Id==port).Width:null;
            return new(endpoint,new[]{point.X,point.Y},available);
        }
        WorldPoint position;double width;string? resolved=null;
        if(endpoint.AccessPortId is {} portId)
        {
            position=new MetadataPhysicalAccessPlanner().Endpoint(node,portId,world.AccessPlan,surface,profile,budget,token);
            width=world.AccessPlan!.Sites.Single(s=>s.NodeId==node.Id).Ports.Single(p=>p.Id==portId).Width;
        }
        else
        {
            string? connectionId=endpoint.InteriorConnectionId;
            if(endpoint.InteriorOpening is {} selector)connectionId=resolved=SelectDomainOpeningConnection(node.Id,selector,world.Connections,budget);
            var openings=new List<MetadataInteriorOpening>();
            foreach(var result in world.InteriorPlan?.Results??[])
            foreach(var candidate in result.Openings)
            {
                budget.Charge();
                if(candidate.ConnectionId==connectionId&&(candidate.FromId==node.Id||candidate.ToId==node.Id))openings.Add(candidate);
                if(openings.Count>1)throw new ArgumentException("METADATA_DOMAIN_OPENING_REQUIRED");
            }
            if(openings.Count!=1)throw new ArgumentException("METADATA_DOMAIN_OPENING_REQUIRED");
            var opening=openings[0];width=opening.Width;
            if(profile.Radius*2>width+1e-8)throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_TOO_WIDE");
            position=opening.FromId==node.Id?opening.FromApproach:opening.ToApproach;
            var middle=new WorldPoint((opening.From.X+opening.To.X)/2,(opening.From.Y+opening.To.Y)/2);
            if(traversal.Evaluate(position,middle,budget,token) is null||traversal.Evaluate(middle,position,budget,token) is null||
                MetadataPhysicalAccessPlanner.IntersectsSolids(world.AccessPlan,position,middle,profile.Radius,budget,token))
                throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_BLOCKED");
        }
        return new(endpoint,new[]{position.X,position.Y},width,resolved);
    }

    public void ValidateDomains(MetadataWorldDomainDocument document,CancellationToken token=default)
    {
        if(document is null||document.Version!=1||document.Definition is null||document.DefinitionSha256!=MetadataExecutionCache.Hash(document.Definition))
            throw new ArgumentException("METADATA_DOMAIN_DOCUMENT_INVALID");
        var expected=GenerateDomains(document.Definition,token);
        if(!MetadataPopulationSources.Equal(expected,document))throw new ArgumentException("METADATA_DOMAIN_DOCUMENT_MISMATCH");
    }

    public async Task<MetadataWorldDomainDocument> GenerateDomainFilesAsync(string definitionFile,string newOutputDirectory,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        string target=Path.GetFullPath(newOutputDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_DOMAIN_OUTPUT_EXISTS");
        var definition=await ReadAsync<MetadataWorldDomainDefinition>(definitionFile,token);var document=GenerateDomains(definition,token);
        await PublishDomainsAsync(document,newOutputDirectory,null,token);return document;
    }

    internal async Task PublishDomainsAsync(MetadataWorldDomainDocument document,string newOutputDirectory,MetadataDomainSessionCheckpoint? checkpoint,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string target=Path.GetFullPath(newOutputDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("METADATA_DOMAIN_OUTPUT_EXISTS");
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".world-domains-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        Exception? publicationFailure=null;
        try
        {
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"world-domains.json"),document,token);
            if(checkpoint is not null)await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"domain-session.json"),checkpoint,token);
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"definition.json"),document.Definition,token);
            var entries=Array.AsReadOnly(document.Domains.Select((domain,index)=>new SchematicDomainEntry(domain.Id,
                document.Definition.Domains.Single(d=>d.Id==domain.Id).Purpose,
                "../domain-"+index.ToString("D4",System.Globalization.CultureInfo.InvariantCulture)+"/schematic.html",
                domain.World.Id,domain.World.RecipeSha256,domain.World.CatalogSha256)).ToArray());
            var transitions=Array.AsReadOnly(document.Transitions.Select(t=>new SchematicDomainTransition(t.Definition.Id,t.Definition.Kind,
                t.Definition.Purpose,t.Definition.SourceRuleId,t.From.Definition.DomainId,t.From.Definition.NodeId,
                t.To.Definition.DomainId,t.To.Definition.NodeId,t.Definition.Bidirectional)).ToArray());
            var html=new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>World domains</title><style>body{font:16px system-ui;background:#13242c;color:#eaf1f2;margin:28px}a{color:#77ddd0}td,th{padding:10px;border-bottom:1px solid #49626e;text-align:left}</style><h1>World domains</h1><p>Local coordinates stay independent. Transitions qualify endpoints and domain graph reachability; they do not create continuous 3D connectors or runtime scene changes.</p><table><tr><th>Domain</th><th>Purpose</th><th>Nodes</th><th>Scope</th></tr>");
            for(int i=0;i<document.Domains.Count;i++)
            {
                var domain=document.Domains[i];var source=document.Definition.Domains.Single(d=>d.Id==domain.Id);string folder="domain-"+i.ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
                var navigation=new SchematicDomainNavigation(1,document.Definition.Id,document.DefinitionSha256,domain.Id,entries,transitions);
                await PublishAsync(source.Recipe,source.Catalog,domain.World,Path.Combine(staging,folder),null,token,navigation:navigation);
                html.Append($"<tr><td><a href=\"{folder}/schematic.html\">{WebUtility.HtmlEncode(domain.Id)}</a></td><td>{WebUtility.HtmlEncode(source.Purpose)}</td><td>{domain.World.Nodes.Count}</td><td>{domain.World.Coverage}</td></tr>");
                if(!MetadataPopulationSources.Equal(domain.Dependencies,Dependencies(source.Recipe,source.Catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
            }
            html.Append("</table><h2>Transitions</h2><table><tr><th>Kind</th><th>From</th><th>To</th><th>Direction</th></tr>");
            foreach(var transition in document.Transitions)
            {
                string from=WebUtility.HtmlEncode(transition.From.Definition.DomainId+" / "+transition.From.Definition.NodeId);
                string to=WebUtility.HtmlEncode(transition.To.Definition.DomainId+" / "+transition.To.Definition.NodeId);
                string direction=transition.Definition.Bidirectional?"both":"forward";
                html.Append($"<tr><td>{WebUtility.HtmlEncode(transition.Definition.Kind)}</td><td>{from}</td><td>{to}</td><td>{direction}</td></tr>");
            }
            html.Append("</table>");
            if(document.Journeys is {Count:>0} journeys)
            {
                html.Append("<h2>Qualified journeys</h2><p>Each local step references a source-qualified route segment or logical connection. Transitions remain explicit domain changes.</p><table><tr><th>Journey</th><th>From</th><th>To</th><th>Forward steps</th><th>Return steps</th></tr>");
                foreach(var journey in journeys)html.Append($"<tr><td>{WebUtility.HtmlEncode(journey.Requirement.Purpose)}</td><td>{WebUtility.HtmlEncode(journey.From.Definition.DomainId)}</td><td>{WebUtility.HtmlEncode(journey.To.Definition.DomainId)}</td><td>{journey.Forward.Count}</td><td>{journey.Return?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)??"not required"}</td></tr>");
                html.Append("</table>");
            }
            html.Append("<p><a href=\"world-domains.json\">Complete domain data and proofs</a> · <a href=\"definition.json\">Reusable source definition</a></p></html>");
            await File.WriteAllTextAsync(Path.Combine(staging,"index.html"),html.ToString(),token);
            await MetadataDirectoryPublication.CommitAsync(staging,target,commitToken=>{
                foreach(var domain in document.Domains)
                {
                    var source=document.Definition.Domains.Single(d=>d.Id==domain.Id);
                    if(!MetadataPopulationSources.Equal(domain.Dependencies,Dependencies(source.Recipe,source.Catalog,commitToken)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
                }
            },token);
        }
        catch(Exception error){publicationFailure=error;throw;}
        finally
        {
            MetadataDirectoryPublication.Cleanup(staging,parent,".world-domains-",publicationFailure);
        }
    }
}
