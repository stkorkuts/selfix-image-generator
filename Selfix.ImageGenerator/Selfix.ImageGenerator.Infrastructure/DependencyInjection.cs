using System.Globalization;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Selfix.ImageGenerator.Application.Abstractions;
using Selfix.ImageGenerator.Infrastructure.EventStreaming;
using Selfix.ImageGenerator.Infrastructure.ImageGeneration;
using Selfix.ImageGenerator.Infrastructure.ObjectStorage;
using Selfix.ImageGenerator.Shared.Settings;
using Selfix.Schema.Kafka.Jobs.Images.V1.ImageGeneration;
using Serilog;

namespace Selfix.ImageGenerator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection collection, KafkaSettings kafkaSettings) =>
        collection
            .AddTransient<IResourceCleaner, ResourceCleaner>()
            .AddSingleton<IDirectoryService, DirectoryService>()
            .AddSingleton<IFileService, FileService>()
            .AddSingleton<IImageGenerator, ComfyUiImageGenerator>()
            .AddSingleton<IObjectStorage, AmazonS3ObjectStorage>()
            .AddSingleton<IWorkflowGenerator, ComfyUiWorkflowGenerator>()
            .AddSingleton<IJsonSerializer, SystemJsonSerializer>(_ =>
                new SystemJsonSerializer(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                }))
            .AddAmazonS3Client()
            .AddKafka(kafkaSettings);

    private static IServiceCollection AddAmazonS3Client(this IServiceCollection collection) =>
        collection.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            S3Settings settings = serviceProvider.GetRequiredService<IOptions<S3Settings>>().Value;

            var config = new AmazonS3Config
            {
                AuthenticationRegion = settings.Region,
                ServiceURL = settings.Endpoint,
                ForcePathStyle = true
            };

            return new AmazonS3Client(settings.AccessKey, settings.SecretKey, config);
        });

    private static IServiceCollection AddKafka(this IServiceCollection collection, KafkaSettings kafkaSettings) =>
        collection.AddMassTransit(configurator =>
        {
            configurator.SetKebabCaseEndpointNameFormatter();
            configurator.AddSerilog();
            
            configurator.AddConfigureEndpointsCallback((_,_,cfg) =>
            {
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });
            
            configurator.UsingInMemory();
            configurator.AddRider(rider =>
            {
                rider.AddConsumer<ImageGenerationConsumer>();
                rider.AddProducer<GenerateImageResponseEvent>(kafkaSettings.TopicOutput);

                rider.UsingKafka((context, kafka) =>
                {
                    kafka.Host(kafkaSettings.BootstrapServer, host => host.UseSasl(sasl =>
                    {
                        SaslSettings saslSettings = kafkaSettings.Sasl;
                        
                        sasl.Username = saslSettings.Username;
                        sasl.Password = saslSettings.Password;
                        sasl.Mechanism = Enum.Parse<SaslMechanism>(saslSettings.KafkaSaslMechanism, true);
                        sasl.SecurityProtocol = Enum.Parse<SecurityProtocol>(saslSettings.KafkaSecurityProtocol, true);
                    }));

                    kafka.TopicEndpoint<GenerateImageRequestEvent>(kafkaSettings.TopicInput, 
                        $"{kafkaSettings.GroupId}-{kafkaSettings.TopicInput}",
                        endpointConfigurator =>
                        {
                            endpointConfigurator.ConfigureConsumer<ImageGenerationConsumer>(context);
                            endpointConfigurator.AutoOffsetReset = AutoOffsetReset.Earliest;
                            endpointConfigurator.MaxPollInterval = TimeSpan.FromMinutes(3);
                        });
                });
            });
        });
}