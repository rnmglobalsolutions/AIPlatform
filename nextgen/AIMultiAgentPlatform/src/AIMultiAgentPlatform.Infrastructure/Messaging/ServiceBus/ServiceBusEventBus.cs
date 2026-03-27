using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Messaging.ServiceBus;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.ServiceBus;

public sealed class ServiceBusEventBus : IEventBus, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private readonly bool _ownsClient;

    public ServiceBusEventBus(ServiceBusOptions options, ServiceBusClient? client = null)
    {
        _options = options;
        _client = client ?? new ServiceBusClient(options.ConnectionString);
        _ownsClient = client is null;
    }

    public async Task PublishAsync(string eventName, MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var sender = _client.CreateSender(BuildEntityName(_options.EventEntityPrefix, eventName));
        try
        {
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(envelope.PayloadJson))
            {
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                Subject = envelope.MessageType,
                ContentType = "application/json"
            };

            message.ApplicationProperties["tenantId"] = envelope.TenantId;
            message.ApplicationProperties["messageType"] = envelope.MessageType;
            message.ApplicationProperties["createdUtc"] = envelope.CreatedUtc.ToString("O");

            if (envelope.Properties is not null)
            {
                foreach (var pair in envelope.Properties)
                {
                    message.ApplicationProperties[pair.Key] = pair.Value;
                }
            }

            await sender.SendMessageAsync(message, cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsClient)
        {
            await _client.DisposeAsync();
        }
    }

    private static string BuildEntityName(string prefix, string name) =>
        $"{prefix}-{NormalizeName(name)}";

    private static string NormalizeName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}
