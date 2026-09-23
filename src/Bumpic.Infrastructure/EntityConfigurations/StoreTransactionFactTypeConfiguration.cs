using Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 商店交易事实实体配置。
/// </summary>
internal class StoreTransactionFactTypeConfiguration : IEntityTypeConfiguration<StoreTransactionFact>
{
    /// <summary>
    /// 配置只追加商店交易事实表结构。
    /// </summary>
    public void Configure(EntityTypeBuilder<StoreTransactionFact> builder)
    {
        builder.ToTable("store_transaction_facts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Store, x.FactKey });
        builder.HasIndex(x => new { x.Store, x.ExternalEventId });
        builder.HasIndex(x => new { x.Store, x.ExternalTransactionId, x.PlatformVersionAt });
        builder.HasIndex(x => x.StoreNotificationReceiptId);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("交易事实标识");
        builder.Property(x => x.StoreNotificationReceiptId)
            .HasColumnName("store_notification_receipt_id")
            .HasColumnType("char(36)")
            .HasComment("来源通知收件记录标识");
        builder.Property(x => x.StoreTransactionId)
            .HasColumnName("store_transaction_id")
            .HasColumnType("char(36)")
            .HasComment("已应用的内部交易标识");
        builder.Property(x => x.SourceType)
            .HasColumnName("source_type")
            .HasColumnType("int")
            .HasComment("交易事实来源");
        builder.Property(x => x.Store)
            .HasColumnName("store")
            .HasColumnType("int")
            .HasComment("交易来源商店");
        builder.Property(x => x.ExternalTransactionId)
            .HasColumnName("external_transaction_id")
            .HasColumnType("varchar(1000)")
            .HasCharSet("ascii")
            .IsRequired()
            .HasComment("平台原始交易标识；Apple transactionId 或 Google purchaseToken，仅含 ASCII 字符");
        builder.Property(x => x.StoreOrderId)
            .HasColumnName("store_order_id")
            .HasColumnType("varchar(255)")
            .HasCharSet("ascii")
            .HasComment("商店订单标识审计快照");
        builder.Property(x => x.ExternalEventId)
            .HasColumnName("external_event_id")
            .HasColumnType("varchar(512)")
            .IsRequired()
            .HasComment("来源事件或快照稳定标识");
        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasColumnType("int")
            .HasComment("交易事实类型");
        ConfigureTimestamp(builder, x => x.OccurredAt, "occurred_at", "业务事件发生时间");
        ConfigureNullableTimestamp(builder, x => x.PlatformVersionAt, "platform_version_at", "平台快照版本时间");
        ConfigureTimestamp(builder, x => x.ObservedAt, "observed_at", "服务端观察时间");
        builder.Property(x => x.FactKey)
            .HasColumnName("fact_key")
            .HasColumnType("binary(32)")
            .IsRequired()
            .HasComment("确定性事实幂等键");
        builder.Property(x => x.PayloadHash)
            .HasColumnName("payload_hash")
            .HasColumnType("binary(32)")
            .IsRequired()
            .HasComment("权威事实载荷摘要");
        ConfigureNullableTimestamp(builder, x => x.AppliedAt, "applied_at", "事实应用时间");
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
        EntityTypeBuilder<StoreTransactionFact> builder,
        System.Linq.Expressions.Expression<Func<StoreTransactionFact, DateTimeOffset>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }

    private static void ConfigureNullableTimestamp(
        EntityTypeBuilder<StoreTransactionFact> builder,
        System.Linq.Expressions.Expression<Func<StoreTransactionFact, DateTimeOffset?>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }
}
