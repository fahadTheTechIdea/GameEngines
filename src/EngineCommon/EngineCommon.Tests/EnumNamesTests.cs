using Xunit;

namespace EngineCommon.Tests;

public sealed class EnumNamesTests
{
    public enum Sample { Plain, NonAggression, ThreeWordName }

    [Theory]
    [InlineData(Sample.Plain, "plain")]
    [InlineData(Sample.NonAggression, "non_aggression")]
    [InlineData(Sample.ThreeWordName, "three_word_name")]
    public void AMemberIsItsSnakeCaseName(Sample member, string name)
    {
        Assert.Equal(name, EnumNames.Name(member));
        Assert.Equal(member, EnumNames.Parse<Sample>(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Plain")]
    [InlineData("nonaggression")]
    [InlineData("other")]
    public void ANameThatIsNoMemberIsNull(string name) => Assert.Null(EnumNames.Parse<Sample>(name));

    [Fact]
    public void ChoicesListsEveryMemberInOrder() => Assert.Equal("plain, non_aggression, three_word_name", EnumNames.Choices<Sample>());
}
