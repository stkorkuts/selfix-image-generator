using Microsoft.Extensions.DependencyInjection;
using Selfix.ImageGenerator.Application.UseCases.GenerateImage;

namespace Selfix.ImageGenerator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection collection)
    {
        return collection.AddTransient<GenerateImageUseCase>();
    }
}