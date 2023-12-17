using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.Data.DBContexts;

public class ApplicationDbContext : DbContext
{
    public DbSet<TodoFolder> Folder { get; set; }
    public DbSet<TodoList> Lists { get; set; }
    public DbSet<TodoTask> Tasks { get; set; }

    public ApplicationDbContext(DbContextOptions options):base(options)
    {
        Database.Migrate();
    }

    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // {
    //     var connectionString = GetConnectionString();
    //     optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
    // }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<TodoList>()
            .HasOne<TodoFolder>()
            .WithMany(x => x.Lists).HasForeignKey(x => x.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TodoTask>()
            .HasOne<TodoList>()
            .WithMany(x => x.Tasks).HasForeignKey(x => x.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(builder);
    }
    public static string GetConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("MYSQLCONNSTR_localdb");

        if (connectionString == null)
            return string.Empty;

        // WRONG: Database = localdb; Data Source = 127.0.0.1:50249; User Id = azure; Password = ****
        //CORRECT: server=127.0.0.1;userid=azure;password=XXXX;database=localdb;Port=nnnnn
        var builder = new System.Data.Common.DbConnectionStringBuilder();
        builder.ConnectionString = connectionString;

        // separate DataSource => server and port
        if (builder.TryGetValue("Data Source", out object? dataSourceValue) && dataSourceValue != null)
        {
            var parts = dataSourceValue.ToString().Split(":");
            builder.Remove("Data Source");
            builder.Add("server", parts[0]);
            if (parts.Count() > 1)
                builder.Add("Port", parts[1]);
        }
        // replace databaseName
        if (builder.TryGetValue("database", out object? databaseValue))
        {
            var databaseName = "myplanner";
            builder.Remove("database");
            builder.Add("database", databaseName);
        }
        return builder.ConnectionString;
    }
}
