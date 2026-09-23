using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 幂等请求实体配置。
/// </summary>
internal class IdempotentRequestTypeConfiguration : IEntityTypeConfiguration<IdempotentRequest>
{
    /// <summary>
    /// 配置幂等请求表结构。
    /// </summary>
    public void Configure(EntityTypeBuilder<IdempotentRequest> builder)
    {
        builder.ToTable("idempotent_requests");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserAccountId, x.Operation, x.IdempotencyKey });
        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("幂等请求唯一标识");
        builder.Property(x => x.UserAccountId)
            .HasColumnName("user_account_id")
            .HasColumnType("char(36)")
            .HasDefaultValue(new UserAccountId(Guid.Empty))
            .HasComment("发起请求的用户标识");
        builder.Property(x => x.Operation)
            .HasColumnName("operation")
            .HasColumnType("varchar(100)")
            .IsRequired()
            .HasComment("幂等操作名称");
        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasColumnType("varchar(255)")
            .IsRequired()
            .HasComment("客户端幂等键");
        builder.Property(x => x.RequestHash)
            .HasColumnName("request_hash")
            .HasColumnType("binary(32)")
            .IsRequired()
            .HasComment("规范化请求摘要");
        builder.Property(x => x.RequestHashVersion)
            .HasColumnName("request_hash_version")
            .HasColumnType("int")
            .HasComment("请求摘要算法版本");
        builder.Property(x => x.StoreTransactionId)
            .HasColumnName("store_transaction_id")
            .HasColumnType("char(36)")
            .HasComment("已关联内部商店交易标识");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(IdempotentRequestStatus.Processing)
            .HasComment("请求处理状态");
        builder.Property(x => x.LeaseToken)
            .HasColumnName("lease_token")
            .HasColumnType("char(36)")
            .HasComment("当前执行 fencing 令牌");
        ConfigureTimestamp(builder, x => x.LeaseExpiresAt, "lease_expires_at", "当前执行租约截止时间");
        builder.Property(x => x.AttemptCount)
            .HasColumnName("attempt_count")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("成功领取执行权次数");
        ConfigureTimestamp(builder, x => x.NextRetryAt, "next_retry_at", "最早再次领取时间");
        builder.Property(x => x.ResultCode)
            .HasColumnName("result_code")
            .HasColumnType("varchar(100)")
            .HasComment("最近稳定或诊断结果码");
        ConfigureTimestamp(builder, x => x.CompletedAt, "completed_at", "完成时间");
        ConfigureTimestamp(builder, x => x.LastAttemptAt, "last_attempt_at", "最近领取执行权时间");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("创建时间");
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("最近更新时间");
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

    private static void ConfigureTimestamp(
        EntityTypeBuilder<IdempotentRequest> builder,
        System.Linq.Expressions.Expression<Func<IdempotentRequest, DateTimeOffset?>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }
}
