using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Messaging.ServiceBus;

namespace AIMultiAgentPlatform.Infrastructure.Messaging.ServiceBus;

public sealed class ServiceBusCommandBus : ICommandBus, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private readonly bool _ownsClient;

    public ServiceBusCommandBus(ServiceBusOptions options, ServiceBusClient? client = null)
    {
        _options = options;
        _client = client ?? new ServiceBusClient(options.ConnectionString);
        _ownsClient = client is null;
    }

    public async Task SendAsync(string commandName, MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var sender = _client.CreateSender(BuildEntityName(_options.CommandEntityPrefix, commandName));
        try
        {
            var message = BuildMessage(envelope);
            if (envelope.ScheduledEnqueueUtc.HasValue)
            {
                message.ScheduledEnqueueTime = envelope.ScheduledEnqueueUtc.Value;
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

    private static ServiceBusMessage BuildMessage(MessageEnvelope envelope)
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

        return message;
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
