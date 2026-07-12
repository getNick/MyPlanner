using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities;
using MyPlanner.Data.Entities.Common;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Data.Entities.Notes;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Data.DBContexts;

public class ApplicationDbContext : DbContext
{
    public DbSet<Page> Pages { get; set; }
    public DbSet<PageContent> PageContent { get; set; }
    public DbSet<PageSharing> PageSharing { get; set; }
    public DbSet<TodoList> TodoLists { get; set; }
    public DbSet<TodoTask> TodoTasks { get; set; }
    public DbSet<TodoTaskSession> TodoTasksSessions { get; set; }
    public DbSet<Note> Notes { get; set; }

    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionItem> TransactionItems { get; set; }

    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }

    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // {
    //     var connectionString = GetConnectionString();
    //     optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
    // }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // page to page
        builder.Entity<Page>()
            .HasMany(x => x.IncludePages).WithOne()
            .HasForeignKey(x => x.ParentPage)
            .OnDelete(DeleteBehavior.Cascade);

        // page to content
        builder.Entity<Page>()
            .HasOne(x => x.Content).WithOne()
            .HasForeignKey<PageContent>(x => x.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Page>()
            .HasMany(x => x.Sharing).WithOne()
            .HasForeignKey(x => x.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        // page content
        builder.Entity<PageContent>()
            .Property(x => x.Type)
            .HasConversion<string>();

        //  TodoList and Tasks
        builder.Entity<TodoList>().ToTable("TodoLists");
        builder.Entity<Note>().ToTable("Notes");

        builder.Entity<TodoTask>()
            .HasOne<TodoList>()
            .WithMany(x => x.Tasks).HasForeignKey(x => x.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TodoTaskSession>()
            .HasOne<TodoTask>()
            .WithMany(x => x.Sessions).HasForeignKey(x => x.TodoTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Finance entities - configure property conversions separately
        var paymentMethodBuilder = builder.Entity<PaymentMethod>();
        paymentMethodBuilder.Property(x => x.Type).HasConversion<string>();
        paymentMethodBuilder.Property(x => x.Currency).HasConversion<string>();
        paymentMethodBuilder.HasIndex(x => x.UserId);

        var transactionBuilder = builder.Entity<Transaction>();
        transactionBuilder.Property(x => x.Type).HasConversion<string>();
        transactionBuilder.Property(x => x.Currency).HasConversion<string>();
        transactionBuilder.Property(x => x.DataOrigin).HasConversion<string>();
        transactionBuilder.HasIndex(x => x.UserId);

        builder.Entity<TransactionItem>()
            .Property(x => x.Origin)
            .HasConversion<string>();

        // Configure foreign key relationships for finance entities
        builder.Entity<Transaction>()
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Transaction>()
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.ToPaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Entity<TransactionItem>()
            .HasOne<Transaction>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(builder);
    }
    public static string GetConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MYSQLCONNSTR_LOCALDB");

        if (connectionString == null)
            return string.Empty;

        return connectionString;
    }
}
