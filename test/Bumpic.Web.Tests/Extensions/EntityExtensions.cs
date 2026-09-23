using System.Reflection;

namespace Bumpic.Web.Tests.Extensions;

/// <summary>
/// 测试用实体扩展。
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// 为实体写入强类型标识并返回同一实例。
    /// </summary>
    /// <remarks>
    /// 领域模型按规范不手动为 ID 赋值，主键由 EF Core 在实体被跟踪时生成；单元测试中实体不被跟踪，
    /// 因此需要这一辅助方法补齐标识，以验证依赖标识的业务引用。
    /// </remarks>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TId">强类型标识类型。</typeparam>
    /// <param name="entity">目标实体。</param>
    /// <param name="id">要写入的标识。</param>
    /// <returns>同一实体实例。</returns>
    public static TEntity WithId<TEntity, TId>(this TEntity entity, TId id)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var idProperty = typeof(TEntity).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} 不存在 Id 属性");
        idProperty.SetValue(entity, id);

        return entity;
    }
}
