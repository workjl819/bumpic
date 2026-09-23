using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bumpic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StorePointsMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_point_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "账户点数记录唯一标识", collation: "ascii_general_ci"),
                    point_account_id = table.Column<Guid>(type: "char(36)", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"), comment: "逻辑关联的点数账户标识", collation: "ascii_general_ci"),
                    user_account_id = table.Column<Guid>(type: "char(36)", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"), comment: "冗余保存的用户账户标识", collation: "ascii_general_ci"),
                    type = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "点数流水类型"),
                    amount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "本次点数变化值"),
                    business_reference = table.Column<string>(type: "varchar(255)", nullable: false, defaultValue: "", comment: "关联业务的唯一引用")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "流水创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "流水更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_point_records", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FriendlyName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Xml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "idempotent_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "幂等请求唯一标识", collation: "ascii_general_ci"),
                    user_account_id = table.Column<Guid>(type: "char(36)", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"), comment: "发起请求的用户标识", collation: "ascii_general_ci"),
                    operation = table.Column<string>(type: "varchar(100)", nullable: false, comment: "幂等操作名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    idempotency_key = table.Column<string>(type: "varchar(255)", nullable: false, comment: "客户端幂等键")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    request_hash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "规范化请求摘要"),
                    request_hash_version = table.Column<int>(type: "int", nullable: false, comment: "请求摘要算法版本"),
                    store_transaction_id = table.Column<Guid>(type: "char(36)", nullable: true, comment: "已关联内部商店交易标识", collation: "ascii_general_ci"),
                    status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "请求处理状态"),
                    lease_token = table.Column<Guid>(type: "char(36)", nullable: true, comment: "当前执行 fencing 令牌", collation: "ascii_general_ci"),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "当前执行租约截止时间"),
                    attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "成功领取执行权次数"),
                    next_retry_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "最早再次领取时间"),
                    result_code = table.Column<string>(type: "varchar(100)", nullable: true, comment: "最近稳定或诊断结果码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "完成时间"),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "最近领取执行权时间"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotent_requests", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "point_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "点数账户唯一标识", collation: "ascii_general_ci"),
                    user_account_id = table.Column<Guid>(type: "char(36)", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"), comment: "所属用户账户标识", collation: "ascii_general_ci"),
                    available_points = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "当前可用点数"),
                    frozen_points = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "当前冻结点数"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "点数账户创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "点数账户最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_notification_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "通知收件记录标识", collation: "ascii_general_ci"),
                    store = table.Column<int>(type: "int", nullable: false, comment: "通知来源商店"),
                    external_notification_id = table.Column<string>(type: "varchar(255)", nullable: false, comment: "平台通知唯一标识")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notification_type = table.Column<string>(type: "varchar(100)", nullable: false, comment: "平台通知类型")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    schema_version = table.Column<int>(type: "int", nullable: false, comment: "规范化载荷版本"),
                    parser_version = table.Column<int>(type: "int", nullable: false, comment: "首次解析器版本"),
                    raw_payload = table.Column<string>(type: "longtext", nullable: false, comment: "原始通知载荷")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_payload = table.Column<string>(type: "longtext", nullable: true, comment: "版本化规范通知载荷")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payload_hash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "原始载荷 SHA-256"),
                    source_verified_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "来源验证时间"),
                    source_principal = table.Column<string>(type: "varchar(255)", nullable: false, comment: "已验证平台主体摘要")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_audience = table.Column<string>(type: "varchar(500)", nullable: true, comment: "已验证 Google Push Audience")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "平台事件发生时间"),
                    processed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "处理完成时间"),
                    last_replayed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "最近重放时间"),
                    last_replay_parser_version = table.Column<int>(type: "int", nullable: true, comment: "最近重放解析器版本"),
                    status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "收件处理状态"),
                    failure_code = table.Column<string>(type: "varchar(100)", nullable: true, comment: "最近失败码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "处理尝试次数"),
                    next_retry_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "下一次重试时间"),
                    lease_owner = table.Column<string>(type: "varchar(255)", nullable: true, comment: "处理租约所有者")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "处理租约截止时间"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_notification_receipts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "商店商品唯一标识", collation: "ascii_general_ci"),
                    store = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "商品所属商店"),
                    product_id = table.Column<string>(type: "varchar(255)", nullable: false, defaultValue: "", comment: "商店后台配置的商品标识")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m, comment: "商品展示和配置金额"),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "", comment: "ISO 4217 商品金额币种代码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    points = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "验单成功后增加的固定点数"),
                    enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否允许客户端查询和购买"),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "同一商店内的展示顺序"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "商品记录创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "商品记录最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_products", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_transaction_facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "交易事实标识", collation: "ascii_general_ci"),
                    store_notification_receipt_id = table.Column<Guid>(type: "char(36)", nullable: true, comment: "来源通知收件记录标识", collation: "ascii_general_ci"),
                    store_transaction_id = table.Column<Guid>(type: "char(36)", nullable: true, comment: "已应用的内部交易标识", collation: "ascii_general_ci"),
                    source_type = table.Column<int>(type: "int", nullable: false, comment: "交易事实来源"),
                    store = table.Column<int>(type: "int", nullable: false, comment: "交易来源商店"),
                    external_transaction_id = table.Column<string>(type: "varchar(1000)", nullable: false, comment: "平台原始交易标识；Apple transactionId 或 Google purchaseToken，仅含 ASCII 字符")
                        .Annotation("MySql:CharSet", "ascii"),
                    store_order_id = table.Column<string>(type: "varchar(255)", nullable: true, comment: "商店订单标识审计快照")
                        .Annotation("MySql:CharSet", "ascii"),
                    external_event_id = table.Column<string>(type: "varchar(512)", nullable: false, comment: "来源事件或快照稳定标识")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: false, comment: "交易事实类型"),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "业务事件发生时间"),
                    platform_version_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "平台快照版本时间"),
                    observed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "服务端观察时间"),
                    fact_key = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "确定性事实幂等键"),
                    payload_hash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "权威事实载荷摘要"),
                    applied_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "事实应用时间"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_transaction_facts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "商店交易唯一标识", collation: "ascii_general_ci"),
                    store = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "交易来源商店"),
                    is_test_purchase = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否为 Google 测试购买"),
                    external_transaction_id = table.Column<string>(type: "varchar(1000)", nullable: false, comment: "平台原始交易标识；Apple transactionId 或 Google purchaseToken，仅含 ASCII 字符")
                        .Annotation("MySql:CharSet", "ascii"),
                    user_account_id = table.Column<Guid>(type: "char(36)", nullable: true, comment: "获得点数的用户账户标识", collation: "ascii_general_ci"),
                    ownership_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "交易账户归属状态"),
                    store_product_id = table.Column<Guid>(type: "char(36)", nullable: true, comment: "服务端商品标识", collation: "ascii_general_ci"),
                    product_id = table.Column<string>(type: "varchar(255)", nullable: true, comment: "商店商品标识快照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,4)", nullable: true, comment: "权威验单金额快照"),
                    currency_code = table.Column<string>(type: "char(3)", nullable: true, comment: "权威验单币种快照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    points_snapshot = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "商品点数配置快照"),
                    granted_points = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "历史实际发放点数"),
                    reversed_points = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "当前累计冲正点数"),
                    status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "商店交易状态"),
                    verification_payload_hash = table.Column<string>(type: "varchar(128)", nullable: true, comment: "验单证据摘要")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    failure_code = table.Column<string>(type: "varchar(100)", nullable: true, comment: "稳定失败原因")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    consumption_attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "Google 消费尝试次数"),
                    next_retry_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "下一次补偿时间"),
                    last_platform_version_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "最新平台快照版本时间"),
                    last_apple_fact_priority = table.Column<int>(type: "int", nullable: true, comment: "Apple 同一快照时间下的最新事实优先级"),
                    purchased_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "商店确认购买时间"),
                    refunded_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "商店确认退款时间"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "交易记录创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "交易记录最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_transactions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "用户账户唯一标识", collation: "ascii_general_ci"),
                    last_login_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "最近一次成功登录时间"),
                    deletion_requested_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true, comment: "注销申请时间；定时任务据此扫描到期账户"),
                    email_address = table.Column<string>(type: "varchar(320)", nullable: false, defaultValue: "", comment: "规范化后的登录邮箱")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(512)", nullable: false, defaultValue: "", comment: "安全哈希后的登录密码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invitation_code = table.Column<string>(type: "varchar(64)", nullable: false, defaultValue: "", comment: "长期有效的邀请码")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purchase_account_token = table.Column<Guid>(type: "char(36)", nullable: false, comment: "商店购买账户标识", collation: "ascii_general_ci"),
                    status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "用户账户状态"),
                    failed_login_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "连续登录失败次数"),
                    locked_until = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "安全锁定截止时间"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "账户创建时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "账户最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本"),
                    deleted_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, comment: "账户删除时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "外部身份唯一标识", collation: "ascii_general_ci"),
                    user_account_id = table.Column<Guid>(type: "char(36)", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"), comment: "逻辑关联的用户账户标识", collation: "ascii_general_ci"),
                    provider = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "外部身份提供方"),
                    subject_id = table.Column<string>(type: "varchar(255)", nullable: false, defaultValue: "", comment: "提供方返回的稳定用户标识")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "身份绑定时间"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)", comment: "身份最近更新时间"),
                    deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "软删除标记"),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "乐观并发控制版本"),
                    revocation_token_ciphertext = table.Column<string>(type: "varchar(2048)", nullable: false, defaultValue: "", comment: "加密后的平台撤销令牌，账户注销撤销成功后清空；禁止写入日志")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_identities", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_account_point_records_business_reference",
                table: "account_point_records",
                column: "business_reference");

            migrationBuilder.CreateIndex(
                name: "IX_idempotent_requests_status_next_retry_at",
                table: "idempotent_requests",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotent_requests_user_account_id_operation_idempotency_key",
                table: "idempotent_requests",
                columns: new[] { "user_account_id", "operation", "idempotency_key" });

            migrationBuilder.CreateIndex(
                name: "IX_point_accounts_user_account_id",
                table: "point_accounts",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_notification_receipts_status_lease_expires_at",
                table: "store_notification_receipts",
                columns: new[] { "status", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_store_notification_receipts_status_next_retry_at",
                table: "store_notification_receipts",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_store_notification_receipts_store_external_notification_id",
                table: "store_notification_receipts",
                columns: new[] { "store", "external_notification_id" });

            migrationBuilder.CreateIndex(
                name: "IX_store_products_store_product_id",
                table: "store_products",
                columns: new[] { "store", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transaction_facts_store_external_event_id",
                table: "store_transaction_facts",
                columns: new[] { "store", "external_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transaction_facts_store_external_transaction_id_platfo~",
                table: "store_transaction_facts",
                columns: new[] { "store", "external_transaction_id", "platform_version_at" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transaction_facts_store_fact_key",
                table: "store_transaction_facts",
                columns: new[] { "store", "fact_key" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transaction_facts_store_notification_receipt_id",
                table: "store_transaction_facts",
                column: "store_notification_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_transactions_status_next_retry_at",
                table: "store_transactions",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transactions_store_external_transaction_id",
                table: "store_transactions",
                columns: new[] { "store", "external_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transactions_user_account_id",
                table: "store_transactions",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_purchase_account_token",
                table: "user_accounts",
                column: "purchase_account_token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_point_records");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "idempotent_requests");

            migrationBuilder.DropTable(
                name: "point_accounts");

            migrationBuilder.DropTable(
                name: "store_notification_receipts");

            migrationBuilder.DropTable(
                name: "store_products");

            migrationBuilder.DropTable(
                name: "store_transaction_facts");

            migrationBuilder.DropTable(
                name: "store_transactions");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropTable(
                name: "user_external_identities");
        }
    }
}
