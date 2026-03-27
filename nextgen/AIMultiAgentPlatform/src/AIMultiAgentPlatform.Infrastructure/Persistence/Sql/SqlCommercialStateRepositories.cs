using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Booking;
using AIMultiAgentPlatform.Domain.FollowUps;
using AIMultiAgentPlatform.Domain.Leads;
using AIMultiAgentPlatform.Domain.Reminders;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Voice;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlLeadProfileRepository : SqlAggregateDocumentRepositoryBase, ILeadProfileRepository
{
    private const string AggregateType = "LeadProfile";

    public SqlLeadProfileRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(LeadProfile leadProfile, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            leadProfile.LeadProfileId,
            leadProfile.TenantId.Value,
            leadProfile,
            leadProfile.UpdatedUtc,
            lookupKey: Normalize(leadProfile.ManyChatContactId),
            lookupKey2: Normalize(leadProfile.Channel),
            lookupKey3: leadProfile.CurrentStage.ToString(),
            sortUtc: leadProfile.UpdatedUtc,
            cancellationToken: cancellationToken);

    public Task<LeadProfile?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        FindFirstAsync<LeadProfile>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(manyChatContactId)),
            cancellationToken);
}

public sealed class SqlManyChatContactStateRepository : SqlAggregateDocumentRepositoryBase, IManyChatContactStateRepository
{
    private const string AggregateType = "ManyChatContactState";

    public SqlManyChatContactStateRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ManyChatContactState contactState, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            contactState.ManyChatContactStateId,
            contactState.TenantId.Value,
            contactState,
            contactState.UpdatedUtc,
            lookupKey: Normalize(contactState.ManyChatContactId),
            lookupKey2: Normalize(contactState.TriggeredFlow),
            sortUtc: contactState.UpdatedUtc,
            cancellationToken: cancellationToken);

    public Task<ManyChatContactState?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        FindFirstAsync<ManyChatContactState>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(manyChatContactId)),
            cancellationToken);
}

public sealed class SqlBookingRecordRepository : SqlAggregateDocumentRepositoryBase, IBookingRecordRepository
{
    private const string AggregateType = "BookingRecord";

    public SqlBookingRecordRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(BookingRecord bookingRecord, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            bookingRecord.BookingRecordId,
            bookingRecord.TenantId.Value,
            bookingRecord,
            bookingRecord.UpdatedUtc,
            lookupKey: Normalize(bookingRecord.ManyChatContactId),
            lookupKey2: Normalize(bookingRecord.LeadProfileId),
            lookupKey3: bookingRecord.Status.ToString(),
            sortUtc: bookingRecord.UpdatedUtc,
            cancellationToken: cancellationToken);

    public Task<BookingRecord?> FindByContactAsync(string tenantId, string manyChatContactId, CancellationToken cancellationToken) =>
        FindFirstAsync<BookingRecord>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(manyChatContactId)),
            cancellationToken);
}

public sealed class SqlReminderScheduleRepository : SqlAggregateDocumentRepositoryBase, IReminderScheduleRepository
{
    private const string AggregateType = "ReminderSchedule";

    public SqlReminderScheduleRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ReminderSchedule reminderSchedule, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            reminderSchedule.ReminderScheduleId,
            reminderSchedule.TenantId.Value,
            reminderSchedule,
            reminderSchedule.CreatedUtc,
            lookupKey: Normalize(reminderSchedule.BookingRecordId),
            lookupKey2: reminderSchedule.Status.ToString(),
            sortUtc: reminderSchedule.CreatedUtc,
            cancellationToken: cancellationToken);

    public Task<ReminderSchedule?> FindByBookingRecordAsync(string bookingRecordId, CancellationToken cancellationToken) =>
        FindFirstAsync<ReminderSchedule>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(bookingRecordId)),
            cancellationToken);
}

public sealed class SqlFollowUpSequenceRepository : SqlAggregateDocumentRepositoryBase, IFollowUpSequenceRepository
{
    private const string AggregateType = "FollowUpSequence";

    public SqlFollowUpSequenceRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(FollowUpSequence followUpSequence, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            followUpSequence.FollowUpSequenceId,
            followUpSequence.TenantId.Value,
            followUpSequence,
            followUpSequence.CreatedUtc,
            lookupKey: Normalize(followUpSequence.LeadProfileId),
            lookupKey2: followUpSequence.Status.ToString(),
            sortUtc: followUpSequence.CreatedUtc,
            cancellationToken: cancellationToken);

    public Task<FollowUpSequence?> FindByLeadProfileAsync(string leadProfileId, CancellationToken cancellationToken) =>
        FindFirstAsync<FollowUpSequence>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(leadProfileId)),
            cancellationToken);
}

public sealed class SqlVoiceCallSessionRepository : SqlAggregateDocumentRepositoryBase, IVoiceCallSessionRepository
{
    private const string AggregateType = "VoiceCallSession";

    public SqlVoiceCallSessionRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(VoiceCallSession voiceCallSession, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            voiceCallSession.VoiceCallSessionId,
            voiceCallSession.TenantId.Value,
            voiceCallSession,
            voiceCallSession.StartedUtc,
            lookupKey: Normalize(voiceCallSession.LeadProfileId),
            lookupKey2: Normalize(voiceCallSession.ManyChatContactId),
            lookupKey3: Normalize(voiceCallSession.ExternalCallId),
            sortUtc: voiceCallSession.CompletedUtc == default ? voiceCallSession.StartedUtc : voiceCallSession.CompletedUtc,
            cancellationToken: cancellationToken);

    public Task<VoiceCallSession?> FindByIdAsync(string voiceCallSessionId, CancellationToken cancellationToken) =>
        FindByIdAsync<VoiceCallSession>(AggregateType, Normalize(voiceCallSessionId), cancellationToken);
}

public sealed class SqlMonthlyPerformanceSnapshotRepository : SqlAggregateDocumentRepositoryBase, IMonthlyPerformanceSnapshotRepository
{
    private const string AggregateType = "MonthlyPerformanceSnapshot";

    public SqlMonthlyPerformanceSnapshotRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(MonthlyPerformanceSnapshot snapshot, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            snapshot.MonthlyPerformanceSnapshotId,
            snapshot.TenantId.Value,
            snapshot,
            snapshot.GeneratedUtc,
            lookupKey: Normalize(snapshot.MonthKey),
            sortUtc: snapshot.GeneratedUtc,
            cancellationToken: cancellationToken);

    public Task<MonthlyPerformanceSnapshot?> FindAsync(string tenantId, string monthKey, CancellationToken cancellationToken) =>
        FindFirstAsync<MonthlyPerformanceSnapshot>(
            AggregateType,
            query => query.Where(item =>
                item.TenantId == Normalize(tenantId) &&
                item.LookupKey == Normalize(monthKey)),
            cancellationToken);
}
