using Xunit;

namespace Lunaria.Tests.Support;

/// <summary>Serialize these tests because the static Resources.Loader would mix their dumps.</summary>
[CollectionDefinition(Name)]
public sealed class AssetsCollection : ICollectionFixture<TestAssets>
{
    public const string Name = "assets";
}
