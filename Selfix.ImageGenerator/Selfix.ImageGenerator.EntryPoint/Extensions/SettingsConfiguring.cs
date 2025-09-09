using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Selfix.ImageGenerator.EntryPoint.Extensions;

public static class SettingsConfiguring
{
    public static IHostApplicationBuilder AddSettings<T>(this IHostApplicationBuilder builder, string sectionName)
        where T : class
    {
        IConfigurationSection section = builder.Configuration.GetSection(sectionName);

        builder.Services
            .AddOptions<T>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return builder;
    }
}