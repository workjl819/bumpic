using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 用户账户实体配置。
/// </summary>
internal class UserAccountTypeConfiguration : IEntityTypeConfiguration<UserAccount>
{
    /// <summary>
    /// 配置用户账户表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.PurchaseAccountToken);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("用户账户唯一标识");

        builder.Property(x => x.EmailAddress)
            .HasColumnName("email_address")
            .HasColumnType("varchar(320)")
            .HasDefaultValue(string.Empty)
            .HasComment("规范化后的登录邮箱");

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("varchar(512)")
            .HasDefaultValue(string.Empty)
            .HasComment("安全哈希后的登录密码");

        builder.Property(x => x.InvitationCode)
            .HasColumnName("invitation_code")
            .HasColumnType("varchar(64)")
            .HasDefaultValue(string.Empty)
            .HasComment("长期有效的邀请码");

        builder.Property(x => x.PurchaseAccountToken)
            .HasColumnName("purchase_account_token")
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("商店购买账户标识");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(UserAccountStatus.Active)
            .HasComment("用户账户状态");

        builder.Property(x => x.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("连续登录失败次数");

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("datetime(6)")
            .HasComment("安全锁定截止时间");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("账户创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("账户最近更新时间");

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

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .HasComment("账户删除时间");

        builder.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("datetime(6)")
            .HasComment("最近一次成功登录时间");

        builder.Property(x => x.DeletionRequestedAt)
            .HasColumnName("deletion_requested_at")
            .HasColumnType("datetime(6)")
            .HasComment("注销申请时间；定时任务据此扫描到期账户");
    }
}
