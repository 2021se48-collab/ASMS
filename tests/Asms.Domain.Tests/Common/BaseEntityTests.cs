using Asms.Domain.Common;

namespace Asms.Domain.Tests.Common;

public class BaseEntityTests
{
    [Fact]
    public void New_entity_gets_a_non_empty_Id()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Two_new_entities_get_different_Ids()
    {
        var firstEntity = new TestEntity();
        var secondEntity = new TestEntity();

        Assert.NotEqual(firstEntity.Id, secondEntity.Id);
    }

    [Fact]
    public void New_entity_starts_at_Version_1()
    {
        var entity = new TestEntity();

        Assert.Equal(1, entity.Version);
    }

    [Fact]
    public void New_entity_is_not_deleted()
    {
        var entity = new TestEntity();

        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void New_entity_has_CreatedAtUtc_and_UpdatedAtUtc_in_UTC()
    {
        var entity = new TestEntity();

        Assert.Equal(DateTimeKind.Utc, entity.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, entity.UpdatedAtUtc.Kind);
    }

    [Fact]
    public void Entity_implements_ITenantEntity()
    {
        var entity = new TestEntity();

        Assert.IsAssignableFrom<ITenantEntity>(entity);
    }

    [Fact]
    public void New_entity_has_empty_SchoolId_until_assigned()
    {
        var entity = new TestEntity();

        Assert.Equal(Guid.Empty, entity.SchoolId);
    }

    private sealed class TestEntity : BaseEntity
    {
    }
}
