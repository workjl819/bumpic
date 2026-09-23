using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 点数账户实体配置。
/// </summary>
internal class PointAccountTypeConfiguration : IEntityTypeConfiguration<PointAccount>
{
    /// <summary>
    /// 配置点数账户表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<PointAccount> builder)
    {
        builder.ToTable("point_accounts");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserAccountId);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("点数账户唯一标识");

        builder.Property(x => x.UserAccountId)
            .HasColumnName("user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("所属用户账户标识");

        builder.Property(x => x.AvailablePoints)
            .HasColumnName("available_points")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("当前可用点数");

        builder.Property(x => x.FrozenPoints)
            .HasColumnName("frozen_points")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("当前冻结点数");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("点数账户创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("点数账户最近更新时间");

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
