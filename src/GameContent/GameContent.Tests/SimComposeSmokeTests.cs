using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

public sealed class SimComposeSmokeTests
{
    [Fact]
    public void TheCorePackComposesIntoASimEngineWorld()
    {
        var set = CatalogLoader.Load(new DirectoryCatalogSource(Path.Combine(AppContext.BaseDirectory, "Packs")), "core").Required();
        var conversion = CatalogToSim.Convert(set);
        var game = SimWorld.Compose(conversion);
        Assert.NotNull(game);
    }
}
