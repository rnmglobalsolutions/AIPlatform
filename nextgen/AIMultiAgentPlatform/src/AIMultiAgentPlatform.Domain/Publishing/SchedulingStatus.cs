namespace AIMultiAgentPlatform.Domain.Publishing;

public enum SchedulingStatus
{
    Pending = 1,
    Scheduled = 2,
    Blocked = 3,
    PartiallyPublished = 4,
    Published = 5,
    Failed = 6
}
