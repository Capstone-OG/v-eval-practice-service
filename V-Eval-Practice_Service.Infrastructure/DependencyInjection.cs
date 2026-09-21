using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Infrastructure.GrpcClients;
using V_Eval_Practice_Service.Infrastructure.Persistence;
using V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

namespace V_Eval_Practice_Service.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<PracticeDbContext>(options =>
            options.UseNpgsql(connectionString, b =>
                b.MigrationsHistoryTable("__EFMigrationsHistory", "practice")));

        services.AddScoped<IExamSubmissionRepository, ExamSubmissionRepository>();
        services.AddScoped<IIdentityGrpcClient, IdentityGrpcClient>();
        services.AddScoped<IContentGrpcClient, ContentGrpcClient>();

        return services;
    }
}
