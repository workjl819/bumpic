using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 商店商品实体配置。
/// </summary>
internal class StoreProductTypeConfiguration : IEntityTypeConfiguration<StoreProduct>
{
    /// <summary>
    /// 配置商店商品表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<StoreProduct> builder)
    {
        builder.ToTable("store_products");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.Store, x.ProductId });

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("商店商品唯一标识");

        builder.Property(x => x.Store)
            .HasColumnName("store")
            .HasColumnType("int")
            .HasDefaultValue(AppStore.AppleAppStore)
            .HasComment("商品所属商店");

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("varchar(255)")
            .HasDefaultValue(string.Empty)
            .HasComment("商店后台配置的商品标识");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,4)")
            .HasDefaultValue(0m)
            .HasComment("商品展示和配置金额");

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasColumnType("char(3)")
            .HasDefaultValue(string.Empty)
            .HasComment("ISO 4217 商品金额币种代码");

        builder.Property(x => x.Points)
            .HasColumnName("points")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("验单成功后增加的固定点数");

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .HasComment("是否允许客户端查询和购买");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("同一商店内的展示顺序");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("商品记录创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("商品记录最近更新时间");

        builder.Property(x => x.Deleted)
            .HasColumnName("deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .HasComment("软删除标记");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("int")
            .HasDefaultValue(new RowVersion(0))
            .HasComment("乐观并发控制版本")
            .IsConcurrencyToken();
    }
}
