using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 账户点数记录实体配置。
/// </summary>
internal class AccountPointRecordTypeConfiguration : IEntityTypeConfiguration<AccountPointRecord>
{
    /// <summary>
    /// 配置账户点数记录表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<AccountPointRecord> builder)
    {
        builder.ToTable("account_point_records");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.BusinessReference);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("账户点数记录唯一标识");

        builder.Property(x => x.PointAccountId)
            .HasColumnName("point_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new PointAccountId(Guid.Empty))
            .HasComment("逻辑关联的点数账户标识");

        builder.Property(x => x.UserAccountId)
            .HasColumnName("user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("冗余保存的用户账户标识");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasColumnType("int")
            .HasDefaultValue(AccountPointRecordType.RegistrationGranted)
            .HasComment("点数流水类型");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("本次点数变化值");

        builder.Property(x => x.BusinessReference)
            .HasColumnName("business_reference")
            .HasColumnType("varchar(255)")
            .HasDefaultValue(string.Empty)
            .HasComment("关联业务的唯一引用");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("流水创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("流水更新时间");

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
