using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.EntityConfigurations;

/// <summary>
/// 商店交易实体配置。
/// </summary>
internal class StoreTransactionTypeConfiguration : IEntityTypeConfiguration<StoreTransaction>
{
    /// <summary>
    /// 配置商店交易表结构。
    /// </summary>
    public void Configure(EntityTypeBuilder<StoreTransaction> builder)
    {
        builder.ToTable("store_transactions");

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Store, x.ExternalTransactionId });
        builder.HasIndex(x => x.UserAccountId);
        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .IsRequired()
            .UseGuidVersion7ValueGenerator()
            .HasComment("商店交易唯一标识");

        builder.Property(x => x.Store)
            .HasColumnName("store")
            .HasColumnType("int")
            .HasDefaultValue(AppStore.AppleAppStore)
            .HasComment("交易来源商店");

        builder.Property(x => x.IsTestPurchase)
            .HasColumnName("is_test_purchase")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .HasComment("是否为 Google 测试购买");

        builder.Property(x => x.ExternalTransactionId)
            .HasColumnName("external_transaction_id")
            .HasColumnType("varchar(1000)")
            .HasCharSet("ascii")
            .IsRequired()
            .HasComment("平台原始交易标识；Apple transactionId 或 Google purchaseToken，仅含 ASCII 字符");

        builder.Property(x => x.UserAccountId)
            .HasColumnName("user_account_id")
            .HasColumnType("char(36)")
            .HasComment("获得点数的用户账户标识");

        builder.Property(x => x.OwnershipStatus)
            .HasColumnName("ownership_status")
            .HasColumnType("int")
            .HasDefaultValue(StoreTransactionOwnershipStatus.Unlinked)
            .HasComment("交易账户归属状态");

        builder.Property(x => x.StoreProductId)
            .HasColumnName("store_product_id")
            .HasColumnType("char(36)")
            .HasComment("服务端商品标识");

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("varchar(255)")
            .HasComment("商店商品标识快照");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,4)")
            .HasComment("权威验单金额快照");

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasColumnType("char(3)")
            .HasComment("权威验单币种快照");

        builder.Property(x => x.PointsSnapshot)
            .HasColumnName("points_snapshot")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("商品点数配置快照");

        builder.Property(x => x.GrantedPoints)
            .HasColumnName("granted_points")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("历史实际发放点数");

        builder.Property(x => x.ReversedPoints)
            .HasColumnName("reversed_points")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("当前累计冲正点数");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("int")
            .HasDefaultValue(StoreTransactionStatus.PendingVerification)
            .HasComment("商店交易状态");

        builder.Property(x => x.VerificationPayloadHash)
            .HasColumnName("verification_payload_hash")
            .HasColumnType("varchar(128)")
            .HasComment("验单证据摘要");

        builder.Property(x => x.FailureCode)
            .HasColumnName("failure_code")
            .HasColumnType("varchar(100)")
            .HasComment("稳定失败原因");

        builder.Property(x => x.ConsumptionAttemptCount)
            .HasColumnName("consumption_attempt_count")
            .HasColumnType("int")
            .HasDefaultValue(0)
            .HasComment("Google 消费尝试次数");

        ConfigureTimestamp(builder, x => x.NextRetryAt, "next_retry_at", "下一次补偿时间");
        ConfigureTimestamp(builder, x => x.LastPlatformVersionAt, "last_platform_version_at", "最新平台快照版本时间");
        builder.Property(x => x.LastAppleFactPriority)
            .HasColumnName("last_apple_fact_priority")
            .HasColumnType("int")
            .HasComment("Apple 同一快照时间下的最新事实优先级");
        ConfigureTimestamp(builder, x => x.PurchasedAt, "purchased_at", "商店确认购买时间");
        ConfigureTimestamp(builder, x => x.RefundedAt, "refunded_at", "商店确认退款时间");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("交易记录创建时间");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .HasComment("交易记录最近更新时间");

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
        EntityTypeBuilder<StoreTransaction> builder,
        System.Linq.Expressions.Expression<Func<StoreTransaction, DateTimeOffset?>> property,
        string columnName,
        string comment)
    {
        builder.Property(property)
            .HasColumnName(columnName)
            .HasColumnType("datetime(6)")
            .HasComment(comment);
    }
}
