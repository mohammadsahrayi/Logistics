using Xunit;

namespace Logistics.UnitTests
{
    [CollectionDefinition("Postgres integration", DisableParallelization = true)]
    public sealed class PostgresCollection : ICollectionFixture<PostgresCollectionFixture>
    {
    }

    public sealed class PostgresCollectionFixture
    {
    }
}