using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.StoreProductAggregate;

/// <summary>
/// 商店商品标识。
/// </summary>
public partial record StoreProductId : IGuidStronglyTypedId;

/// <summary>
/// 商店商品聚合根。
/// </summary>
public class StoreProduct : Entity<StoreProductId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected StoreProduct()
    {
    }

    /// <summary>
    /// 商品所属商店。
    /// </summary>
    public AppStore Store { get; private set; } = AppStore.AppleAppStore;

    /// <summary>
    /// 商店后台配置的商品标识。
    /// </summary>
    public string ProductId { get; private set; } = string.Empty;

    /// <summary>
    /// 商品展示和配置金额。
    /// </summary>
    public decimal Amount { get; private set; } = 0m;

    /// <summary>
    /// ISO 4217 商品金额币种代码。
    /// </summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>
    /// 验单成功后增加的固定点数。
    /// </summary>
    public int Points { get; private set; } = 0;

    /// <summary>
    /// 是否允许客户端查询和购买。
    /// </summary>
    public bool Enabled { get; private set; } = false;

    /// <summary>
    /// 同一商店内的展示顺序。
    /// </summary>
    public int SortOrder { get; private set; } = 0;

    /// <summary>
    /// 商品记录创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 商品记录最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 软删除标记。
    /// </summary>
    public bool Deleted { get; private set; } = false;

    /// <summary>
    /// 乐观并发控制版本。
    /// </summary>
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 永久停用当前商品。
    /// </summary>
    public void Disable(DateTimeOffset now)
    {
        if (!Enabled)
        {
            return;
        }

        Enabled = false;
        UpdatedAt = now;
    }
}
