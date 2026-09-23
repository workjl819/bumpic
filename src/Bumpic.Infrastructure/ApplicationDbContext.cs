using MediatR;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetCorePal.Extensions.DistributedTransactions.CAP.Persistence;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

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
