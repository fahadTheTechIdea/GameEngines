using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Catalogs.Modern;

internal static class WorldDependencyTests
{
    private sealed class InvalidIdentity:IWorldGenerationDependency
    {
        public string ConfigurationSha256=>"unmeasured-version";
    }
    private sealed class FixedResolver(WorldMetadataGenerator? generator):IMetadataGenerationDependencyResolver
    {
        public WorldMetadataGenerator Resolve(IReadOnlyList<MetadataGenerationDependency> dependencies)=>generator!;
    }
    private sealed class VersionedLine(double step):IMetadataLayout,IWorldGenerationDependency
    {
        public double Step {get;set;}=step;
        public bool MutateDuringRun {get;set;}
        public string Id=>"line";
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Step});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {
            var result=new MetadataLineLayout().Plan(MetadataLineLayout.Parameters(new double[dimensions],Enumerable.Range(0,dimensions).Select(i=>i==0?Step:0).ToArray()).Parameters,dimensions,count,token);
            if(MutateDuringRun&&count>0)Step++;
            return result;
        }
    }
    private sealed class Resolver(IReadOnlyDictionary<string,WorldMetadataGenerator> versions):IMetadataGenerationDependencyResolver
    {
        public WorldMetadataGenerator Resolve(IReadOnlyList<MetadataGenerationDependency> dependencies)
            =>versions[dependencies.Single(d=>d.Kind=="layout").Implementation.ConfigurationSha256];
    }
    private sealed class RevisedCatalog(int extraCapacity):ISettlementCatalogProvider
    {
        private readonly ModernCatalog source=new();
        public SettlementCatalogDescriptor Descriptor=>source.Descriptor;
        public IReadOnlyList<BuildingDefinition> Structures=>source.Structures.Select(d=>d.Id=="village-house"?d with{
            SizeVariants=d.SizeVariants!.Select(v=>v.Id=="cottage"?v with{ServiceCapacity=v.ServiceCapacity+extraCapacity}:v).ToArray()}:d).ToArray();
        public IReadOnlyList<FacilityDefinition> Facilities=>source.Facilities;
        public IReadOnlyList<SiteObjectDefinition> Objects=>((ISettlementCatalogProvider)source).Objects;
        public IReadOnlyList<SiteAreaDefinition> Areas=>((ISettlementCatalogProvider)source).Areas;
        public ISettlementCatalogPolicy Policy=>source.Policy;
        public ISettlementDemandPolicy Demand=>source.Demand;
        public IInfrastructureDesignStrategy Infrastructure=>source.Infrastructure;
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) LayoutExample()
    {
        var root=new WorldMetadataArchetype("root","journey","Required journey steps",new Dictionary<string,JsonElement>(),
            [new("steps","step",4,"Required encounters",Layout:MetadataLineLayout.Parameters([0,0],[1,0]))],[],[]);
        var leaf=new WorldMetadataArchetype("step","encounter","Required encounter",new Dictionary<string,JsonElement>(),[],[],[]);
        return(new(1,"versioned-journey","exploration","versioned-catalog","root","level",MetadataCoverage.PartialScope,["Layout dependency fixture only"],new Dictionary<string,int>(),new(["x","y"],"meters"),[0,0],true),
            new(1,"versioned-catalog",[root,leaf]));
    }
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot before,WorldMetadataRecipe? recipe=null)
        =>new(id,"Explicit dependency/edit change","test:"+id,before.World.RecipeSha256,before.World.CatalogSha256,recipe??before.Recipe,before.Catalog);
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        var e=WorldContextTests.Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"protections.json"),Json(new MetadataInputProtection[]{new("source-selection",MetadataInputSource.Catalog,"","Keep source references and required program","developer:source")}));
        var edit=new MetadataEditRequest("review-label","Review the registered source world","developer:review",Hash(e.Recipe),Hash(e.Catalog),e.Recipe with{GameProfile="registered-review"},e.Catalog);
        await File.WriteAllTextAsync(Path.Combine(directory,"edit.json"),Json(edit));
    }
    public static async Task RunAsync()
    {
        await RejectAsync(()=>{WorldGenerationDependencyIdentity.Describe(new InvalidIdentity());return Task.CompletedTask;},"METADATA_GENERATION_DEPENDENCY_DIGEST_INVALID");
        var e=LayoutExample();var one=new VersionedLine(2);var two=new VersionedLine(4);
        var gen1=new WorldMetadataGenerator(layouts:[one]);var gen2=new WorldMetadataGenerator(layouts:[two]);
        var session=new MetadataGenerationSession(gen1,e.Recipe,e.Catalog);var initial=session.Snapshot;
        var unchanged=session.Apply(Edit("same",initial));Check(unchanged.Report.Stages.All(s=>s.Reused),"stable custom version reuses cache");
        var changed=session.Apply(Edit("version-two",unchanged),generator:gen2);
        Check(changed.Report.Changes.Count==0&&Json(changed.Report.Dependencies)!=Json(initial.Report.Dependencies),"dependency-only change recorded independently of unchanged input JSON");
        Check(changed.Report.Stages.Single(s=>s.Id=="surface").Reused&&!changed.Report.Stages.Single(s=>s.Id=="content").Reused,"custom implementation configuration invalidates content and its dependents");
        Check(Json(changed.World)==Json(gen2.Generate(e.Recipe,e.Catalog))&&changed.World.Nodes.Count==5,"custom version equals full output with every required child");
        Check(changed.World.Nodes.Last().Position![0]==12&&initial.World.Nodes.Last().Position![0]==6,"new configuration actually changes positions");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-dependencies-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        string packet=Path.Combine(temp,"accepted");await session.PublishAsync(packet);string checkpoint=Path.Combine(packet,"generation-session.json");
        await RejectAsync(()=>MetadataGenerationSession.RestoreAsync(gen2,checkpoint),"METADATA_CHECKPOINT_DEPENDENCY_UNAVAILABLE:initial");
        await RejectAsync(()=>MetadataGenerationSession.RestoreAsync(gen1,checkpoint,resolver:new FixedResolver(null)),"METADATA_CHECKPOINT_DEPENDENCY_UNAVAILABLE:initial");
        await RejectAsync(()=>MetadataGenerationSession.RestoreAsync(gen1,checkpoint,resolver:new FixedResolver(gen2)),"METADATA_CHECKPOINT_DEPENDENCY_UNAVAILABLE:initial");
        var resolver=new Resolver(new Dictionary<string,WorldMetadataGenerator>{{one.ConfigurationSha256,gen1},{two.ConfigurationSha256,gen2}});
        var restored=await MetadataGenerationSession.RestoreAsync(gen2,checkpoint,resolver:resolver);
        Check(Json(restored.Snapshot)==Json(changed),"exact historical implementations replay version transition");
        var mutable=new VersionedLine(6){MutateDuringRun=true};var unstable=new WorldMetadataGenerator(layouts:[mutable]);
        try{session.Apply(Edit("unstable",changed),generator:unstable);throw new Exception("Expected unstable dependency refusal");}
        catch(MetadataRegenerationException x){Check(x.ReasonCode=="METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN","mid-run dependency change is rejected");}
        Check(Json(session.Snapshot)==Json(changed),"failed implementation change cannot replace accepted world/cache/generator");
        var again=session.Apply(Edit("after-failure",changed));Check(again.Report.Stages.All(s=>s.Reused),"accepted generator retained after rejected replacement");
        var publishingLayout=new VersionedLine(7);var publishing=new MetadataGenerationSession(new WorldMetadataGenerator(layouts:[publishingLayout]),e.Recipe,e.Catalog);
        var before=publishing.Snapshot;bool observed=false;
        using(var watcher=new FileSystemWatcher(temp,"generation-session.json"){IncludeSubdirectories=true,EnableRaisingEvents=true})
        {
            watcher.Created+=(_,_)=>{observed=true;publishingLayout.Step=8;};
            await RejectAsync(()=>publishing.PublishAsync(Path.Combine(temp,"unstable-publication")),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
        }
        Check(observed&&!Directory.Exists(Path.Combine(temp,"unstable-publication"))&&Json(publishing.Snapshot)==Json(before),"changed dependency during real staging prevents publication and preserves snapshot");
        Console.WriteLine("PASS: custom configuration invalidation, full-generation equivalence, same-version reuse, exact historical resolver, missing-version refusal, mid-run/mid-publication rejection and accepted-generator rollback.");
        RegisteredCases();
    }
    private static void RegisteredCases()
    {
        var e=WorldContextTests.Example();var provider1=new RevisedCatalog(0);var provider2=new RevisedCatalog(1);
        var registry1=new SettlementCatalogRegistry([provider1]);var registry2=new SettlementCatalogRegistry([provider2]);
        var gen1=new WorldMetadataGenerator(registry1);var gen2=new WorldMetadataGenerator(registry2);
        var session=new MetadataGenerationSession(gen1,e.Recipe,e.Catalog);var before=session.Snapshot;
        Check(before.Report.Dependencies.Any(d=>d.Kind=="catalog"&&d.Id=="modern"),"real registered source provenance included");
        var repeated=session.Apply(Edit("same-source",before));Check(repeated.Report.Stages.All(s=>s.Reused),"registered source/policy stable reuse");
        var changed=session.Apply(Edit("source-update",repeated),generator:gen2);
        Check(Json(changed.World)==Json(gen2.Generate(e.Recipe,e.Catalog)),"changed registered source equals full generation");
        Check(changed.World.StructureDefinitions.Values.Single().Variant.ServiceCapacity==before.World.StructureDefinitions.Values.Single().Variant.ServiceCapacity+1,"new source variant capacity retained exactly");
        Check(Json(changed.Report.Dependencies)!=Json(before.Report.Dependencies)&&!changed.Report.Stages.Single(s=>s.Id=="services").Reused,"source update invalidates downstream catalog services/context");
        Check(changed.World.Nodes.Count==before.World.Nodes.Count&&Json(changed.World.AccessPlan!.Sites)==Json(before.World.AccessPlan!.Sites),"capacity update preserves exact required physical geometry");
        var definition=new CatalogCompositionDefinition("composed-dependency","Imported source",[new("modern",["village-house"],[])]);
        var first=new SettlementCatalogComposition(definition,registry1);var second=new SettlementCatalogComposition(definition,registry2);
        Check(WorldGenerationDependencyIdentity.Describe(first.Policy).ConfigurationSha256!=WorldGenerationDependencyIdentity.Describe(second.Policy).ConfigurationSha256,"composed policy fingerprint includes imported source version");
        var composedRegistry=new SettlementCatalogRegistry([provider1,first]);
        var composedCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Structure is {} reference?a with{Structure=reference with{Catalog=new("composed-dependency",first.Descriptor.Capabilities)}}:a).ToArray()};
        var composed=new MetadataGenerationSession(new WorldMetadataGenerator(composedRegistry),e.Recipe,composedCatalog);
        Check(composed.Snapshot.Report.Dependencies.Any(d=>d.Id=="composed-dependency")&&composed.Snapshot.World.Nodes.Count==before.World.Nodes.Count,"composed catalog uses the same session path");
        Console.WriteLine("PASS: registered Modern content/configuration identities, source-capacity update, actual service/context replay, unchanged physical members and transitive composed-catalog identity.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static string Hash<T>(T value)=>WorldGenerationDependencyIdentity.Digest(value);
    private static void Check(bool value,string message){if(!value)throw new Exception("Dependencies: "+message);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
