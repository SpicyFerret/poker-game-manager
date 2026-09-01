using System.Text.Json.Serialization;
using Application.Abstractions.Realtime;
using Web.Api.Infrastructure;
using Web.Api.Realtime;

namespace Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // REMARK: If you want to use Controllers, you'll need this.
        services.AddControllers();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddCorsInternal(configuration);

        // Enums travel as strings ("Pix", "Running", "InviteOnly") instead of
        // integers. The client stays readable, and reordering an enum member
        // stops being a silent breaking change for anyone already deployed.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSignalR();
        services.AddScoped<IChampionshipActivityNotifier, ChampionshipActivityNotifier>();

        return services;
    }

    private static void AddCorsInternal(this IServiceCollection services, IConfiguration configuration)
    {
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
        }));
    }
}
