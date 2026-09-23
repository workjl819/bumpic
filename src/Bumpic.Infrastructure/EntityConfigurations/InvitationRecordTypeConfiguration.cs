using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 邀请记录实体配置。
/// </summary>
internal class InvitationRecordTypeConfiguration : IEntityTypeConfiguration<InvitationRecord>
{
    /// <summary>
    /// 配置邀请记录表结构。
    /// </summary>
    /// <param name="builder">实体类型构建器。</param>
    public void Configure(EntityTypeBuilder<InvitationRecord> builder)
    {
        builder.ToTable("invitation_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("邀请记录唯一标识");

        builder.Property(x => x.InvitationCode)
            .HasColumnName("invitation_code")
            .HasColumnType("varchar(64)")
            .HasDefaultValue(string.Empty)
            .HasComment("建立邀请关系时使用的邀请码快照");

        builder.Property(x => x.InviterUserAccountId)
            .HasColumnName("inviter_user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("邀请人用户账户标识");

        builder.Property(x => x.InviteeUserAccountId)
            .HasColumnName("invitee_user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("受邀用户账户标识");

        builder.Property(x => x.RewardPoints)
            .HasColumnName("reward_points")
            .HasColumnType("int")
            .HasDefaultValue(10)
            .HasComment("分别发放给邀请双方的点数");

        builder.Property(x => x.EstablishedAt)
            .HasColumnName("established_at")
            .HasColumnType("datetime(6)")
            .HasComment("邀请关系建立时间");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("邀请记录创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("邀请记录最近更新时间");

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
    }
}
