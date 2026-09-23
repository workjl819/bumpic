using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 商店通知收件记录实体配置。
/// </summary>
internal class StoreNotificationReceiptTypeConfiguration
    : IEntityTypeConfiguration<StoreNotificationReceipt>
{
    /// <summary>
    /// 配置可重放通知收件表结构。
    /// </summary>
    public void Configure(EntityTypeBuilder<StoreNotificationReceipt> builder)
    {
        builder.ToTable("store_notification_receipts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Store, x.ExternalNotificationId });
        builder.HasIndex(x => new { x.Status, x.NextRetryAt });
        builder.HasIndex(x => new { x.Status, x.LeaseExpiresAt });

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("通知收件记录标识");
        builder.Property(x => x.Store)
            .HasColumnName("store")
            .HasColumnType("int")
            .HasComment("通知来源商店");
        builder.Property(x => x.ExternalNotificationId)
            .HasColumnName("external_notification_id")
            .HasColumnType("varchar(255)")
            .IsRequired()
            .HasComment("平台通知唯一标识");
        builder.Property(x => x.NotificationType)
            .HasColumnName("notification_type")
            .HasColumnType("varchar(100)")
            .IsRequired()
            .HasComment("平台通知类型");
        builder.Property(x => x.SchemaVersion)
            .HasColumnName("schema_version")
            .HasColumnType("int")
            .HasComment("规范化载荷版本");
        builder.Property(x => x.ParserVersion)
            .HasColumnName("parser_version")
            .HasColumnType("int")
            .HasComment("首次解析器版本");
        builder.Property(x => x.RawPayload)
            .HasColumnName("raw_payload")
            .HasColumnType("longtext")
            .IsRequired()
            .HasComment("原始通知载荷");
        builder.Property(x => x.NormalizedPayload)
            .HasColumnName("normalized_payload")
            .HasColumnType("longtext")
            .HasComment("版本化规范通知载荷");
        builder.Property(x => x.PayloadHash)
            .HasColumnName("payload_hash")
            .HasColumnType("binary(32)")
            .IsRequired()
            .HasComment("原始载荷 SHA-256");
        ConfigureTimestamp(builder, x => x.SourceVerifiedAt, "source_verified_at", "来源验证时间");
        builder.Property(x => x.SourcePrincipal)
            .HasColumnName("source_principal")
            .HasColumnType("varchar(255)")
            .IsRequired()
            .HasComment("已验证平台主体摘要");
        builder.Property(x => x.SourceAudience)
            .HasColumnName("source_audience")
            .HasColumnType("varchar(500)")
            .HasComment("已验证 Google Push Audience");
        ConfigureTimestamp(builder, x => x.OccurredAt, "occurred_at", "平台事件发生时间");
        ConfigureNullableTimestamp(builder, x => x.ProcessedAt, "processed_at", "处理完成时间");
        ConfigureNullableTimestamp(builder, x => x.LastReplayedAt, "last_replayed_at", "最近重放时间");
        builder.Property(x => x.LastReplayParserVersion)
            .HasColumnName("last_replay_parser_version")
            .HasColumnType("int")
            .HasComment("最近重放解析器版本");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(StoreNotificationReceiptStatus.Received)
            .HasComment("收件处理状态");
        builder.Property(x => x.FailureCode)
            .HasColumnName("failure_code")
            .HasColumnType("varchar(100)")
            .HasComment("最近失败码");
        builder.Property(x => x.AttemptCount)
            .HasColumnName("attempt_count")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("处理尝试次数");
        ConfigureNullableTimestamp(builder, x => x.NextRetryAt, "next_retry_at", "下一次重试时间");
        builder.Property(x => x.LeaseOwner)
            .HasColumnName("lease_owner")
            .HasColumnType("varchar(255)")
            .HasComment("处理租约所有者");
        ConfigureNullableTimestamp(builder, x => x.LeaseExpiresAt, "lease_expires_at", "处理租约截止时间");
        ConfigureTimestamp(builder, x => x.CreatedAt, "created_at", "创建时间");
        ConfigureTimestamp(builder, x => x.UpdatedAt, "updated_at", "更新时间");
        builder.Property(x => x.Deleted)
            .HasColumnName("deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .HasComment("软删除标记");
        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("int")
            .HasDefaultValue(new RowVersion(0))
            .HasComment("乐观并发版本")
            .IsConcurrencyToken();
    }

    private static void ConfigureTimestamp(
        EntityTypeBuilder<StoreNotificationReceipt> builder,
        System.Linq.Expressions.Expression<Func<StoreNotificationReceipt, DateTimeOffset>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }

    private static void ConfigureNullableTimestamp(
        EntityTypeBuilder<StoreNotificationReceipt> builder,
        System.Linq.Expressions.Expression<Func<StoreNotificationReceipt, DateTimeOffset?>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }
}
