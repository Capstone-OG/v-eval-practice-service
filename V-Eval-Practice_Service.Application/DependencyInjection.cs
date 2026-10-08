using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using V_Eval_Practice_Service.Application.Common.Behaviors;

namespace V_Eval_Practice_Service.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Động cơ đồ thị Graph Engine (Core Flow 2)
        services.AddTransient<Common.Graph.TarjanCycleDetector>();
        services.AddTransient<Common.Graph.PathPruner>();
        services.AddTransient<Common.Graph.TopologicalSorter>();
        services.AddTransient<Common.Graph.MilestoneBinder>();
        services.AddTransient<Common.Graph.IStudentKMeansClusterer, Common.Graph.StudentKMeansClusterer>();

        // Động cơ Luyện tập thích ứng Adaptive Engine (Core Flow 3)
        services.AddTransient<Common.Adaptive.IZpdQuestionSelector, Common.Adaptive.ZpdQuestionSelector>();

        return services;
    }
}
