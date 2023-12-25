using Microsoft.EntityFrameworkCore;
using MyPlanner.API;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.UnitOfWork;
using MyPlanner.Service;

var builder = WebApplication.CreateBuilder(args);
ConfigureServices(builder.Services);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();


void ConfigureServices(IServiceCollection services){
    var connectionString = ApplicationDbContext.GetConnectionString();

    services.AddDbContext<DbContext, ApplicationDbContext>(
            dbContextOptions => dbContextOptions
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                // The following three options help with debugging, but should
                // be changed or removed for production.
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddTransient<ITodoFolderService, TodoFolderService>();
    services.AddTransient<ITodoListService, TodoListService>();
    services.AddTransient<ITodoTaskService, TodoTaskService>();
}
