using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 用户外部身份实体配置。
/// </summary>
internal class UserExternalIdentityTypeConfiguration : IEntityTypeConfiguration<UserExternalIdentity>
{
    /// <summary>
    /// 配置用户外部身份表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("user_external_identities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("外部身份唯一标识");

        builder.Property(x => x.UserAccountId)
            .HasColumnName("user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("逻辑关联的用户账户标识");

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasColumnType("int")
            .HasDefaultValue(UserExternalIdentityProvider.Apple)
            .HasComment("外部身份提供方");

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(255)")
            .HasDefaultValue(string.Empty)
            .HasComment("提供方返回的稳定用户标识");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("身份绑定时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("身份最近更新时间");

        builder.Property(x => x.Deleted)
            .HasColumnName("deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .HasComment("软删除标记");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("int")
            .HasDefaultValue(new RowVersion(0))
            .HasComment("乐观并发控制版本");

        builder.Property(x => x.RevocationTokenCiphertext)
            .HasColumnName("revocation_token_ciphertext")
            .HasColumnType("varchar(2048)")
            .HasDefaultValue(string.Empty)
            .HasComment("加密后的平台撤销令牌，账户注销撤销成功后清空；禁止写入日志");
    }
}
