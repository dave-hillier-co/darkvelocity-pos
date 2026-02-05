using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace DarkVelocity.Host.Domains.System;

/// <summary>
/// Registry grain for tracking all scheduled jobs in an organization.
/// </summary>
public class JobRegistryGrain : Grain, IJobRegistryGrain
{
    private readonly IPersistentState<JobRegistryState> _state;
    private readonly ILogger<JobRegistryGrain> _logger;

    public JobRegistryGrain(
        [PersistentState("job-registry", "OrleansStorage")]
        IPersistentState<JobRegistryState> state,
        ILogger<JobRegistryGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid orgId)
    {
        if (_state.State.OrganizationId != Guid.Empty)
            return; // Already initialized

        _state.State = new JobRegistryState
        {
            OrganizationId = orgId,
            Version = 1
        };

        await _state.WriteStateAsync();

        _logger.LogInformation("Job registry initialized for organization {OrgId}", orgId);
    }

    public async Task RegisterJobAsync(Guid jobId, string name, JobTriggerType triggerType)
    {
        EnsureInitialized();

        var entry = _state.State.Jobs.FirstOrDefault(j => j.JobId == jobId);
        if (entry != null)
        {
            // Update existing
            var index = _state.State.Jobs.IndexOf(entry);
            _state.State.Jobs[index] = entry with
            {
                Name = name,
                TriggerType = triggerType
            };
        }
        else
        {
            // Add new
            _state.State.Jobs.Add(new JobRegistryEntryRecord
            {
                JobId = jobId,
                Name = name,
                TriggerType = triggerType,
                Status = JobStatus.Scheduled
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogDebug("Registered job {JobId} '{Name}' in registry", jobId, name);
    }

    public async Task UnregisterJobAsync(Guid jobId)
    {
        EnsureInitialized();

        var entry = _state.State.Jobs.FirstOrDefault(j => j.JobId == jobId);
        if (entry != null)
        {
            _state.State.Jobs.Remove(entry);
            _state.State.Version++;
            await _state.WriteStateAsync();

            _logger.LogDebug("Unregistered job {JobId} from registry", jobId);
        }
    }

    public Task<IReadOnlyList<JobRegistryEntry>> GetJobsAsync(JobStatus? status = null)
    {
        EnsureInitialized();

        var query = _state.State.Jobs.AsEnumerable();

        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        var entries = query
            .OrderByDescending(j => j.NextRunAt ?? DateTime.MinValue)
            .Select(j => new JobRegistryEntry
            {
                JobId = j.JobId,
                Name = j.Name,
                TriggerType = j.TriggerType,
                Status = j.Status,
                NextRunAt = j.NextRunAt,
                LastRunAt = j.LastRunAt
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<JobRegistryEntry>>(entries);
    }

    public async Task UpdateJobStatusAsync(Guid jobId, JobStatus status, DateTime? nextRunAt = null)
    {
        EnsureInitialized();

        var entry = _state.State.Jobs.FirstOrDefault(j => j.JobId == jobId);
        if (entry != null)
        {
            var index = _state.State.Jobs.IndexOf(entry);
            _state.State.Jobs[index] = entry with
            {
                Status = status,
                NextRunAt = nextRunAt,
                LastRunAt = status == JobStatus.Running ? DateTime.UtcNow : entry.LastRunAt
            };

            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.OrganizationId != Guid.Empty);

    private void EnsureInitialized()
    {
        if (_state.State.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("Job registry not initialized");
    }
}

/// <summary>
/// State for the job registry grain.
/// </summary>
[GenerateSerializer]
public sealed class JobRegistryState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public List<JobRegistryEntryRecord> Jobs { get; set; } = [];
    [Id(2)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed record JobRegistryEntryRecord
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public JobTriggerType TriggerType { get; init; }
    [Id(3)] public JobStatus Status { get; init; }
    [Id(4)] public DateTime? NextRunAt { get; init; }
    [Id(5)] public DateTime? LastRunAt { get; init; }
}
