using MediatR;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetCorePal.Extensions.DistributedTransactions.CAP.Persistence;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;

namespace Bumpic.Infrastructure;

public partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IMediator mediator)
    : AppDbContextBase(options, mediator),IDataProtectionKeyContext
    , IMySqlCapDataStorage
{
    /// <summary>
    /// 用户账户集合。
    /// </summary>
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    /// <summary>
    /// 外部身份集合。
    /// </summary>
    public DbSet<UserExternalIdentity> UserExternalIdentities => Set<UserExternalIdentity>();

    /// <summary>邀请记录集合。</summary>
    public DbSet<InvitationRecord> InvitationRecords => Set<InvitationRecord>();

    /// <summary>积分账户集合。</summary>
    public DbSet<PointAccount> PointAccounts => Set<PointAccount>();

    /// <summary>积分流水集合。</summary>
    public DbSet<AccountPointRecord> AccountPointRecords => Set<AccountPointRecord>();

    /// <summary>商店商品集合。</summary>
    public DbSet<StoreProduct> StoreProducts => Set<StoreProduct>();

    /// <summary>商店交易集合。</summary>
    public DbSet<StoreTransaction> StoreTransactions => Set<StoreTransaction>();

    /// <summary>商店通知回执集合。</summary>
    public DbSet<StoreNotificationReceipt> StoreNotificationReceipts => Set<StoreNotificationReceipt>();

    /// <summary>商店交易事实集合。</summary>
    public DbSet<StoreTransactionFact> StoreTransactionFacts => Set<StoreTransactionFact>();

    /// <summary>幂等请求集合。</summary>
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();
    
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (modelBuilder is null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }



    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ConfigureStronglyTypedIdValueConverter(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
    }

}
