using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for optimizing table assignments for bookings.
/// </summary>
public class TableAssignmentOptimizerGrain : Grain, ITableAssignmentOptimizerGrain
{
    private readonly IPersistentState<TableAssignmentOptimizerState> _state;
    private readonly IGrainFactory _grainFactory;

    public TableAssignmentOptimizerGrain(
        [PersistentState("tableoptimizer", "OrleansStorage")]
        IPersistentState<TableAssignmentOptimizerState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new TableAssignmentOptimizerState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<TableAssignmentResult> GetRecommendationsAsync(TableAssignmentRequest request)
    {
        EnsureExists();

        var recommendations = new List<TableRecommendation>();
        var availableTables = _state.State.Tables.Where(t => !t.IsOccupied).ToList();

        // Score each table
        foreach (var table in availableTables)
        {
            var score = CalculateTableScore(table, request);
            if (score > 0)
            {
                var reasons = GetScoreReasons(table, request);
                var serverSection = _state.State.ServerSections
                    .FirstOrDefault(s => s.TableIds.Contains(table.TableId));

                recommendations.Add(new TableRecommendation
                {
                    TableId = table.TableId,
                    TableNumber = table.TableNumber,
                    Capacity = table.MaxCapacity,
                    Score = score,
                    Reasons = reasons,
                    RequiresCombination = false,
                    ServerId = serverSection?.ServerId,
                    ServerName = serverSection?.ServerName
                });
            }
        }

        // Check for table combinations for large parties
        if (request.PartySize > availableTables.Where(t => t.MaxCapacity >= request.PartySize).Count())
        {
            var combinations = FindTableCombinations(availableTables, request.PartySize);
            foreach (var combo in combinations)
            {
                var totalCapacity = combo.Sum(t => t.MaxCapacity);
                var combinedScore = combo.Sum(t => CalculateTableScore(t, request)) / combo.Count;

                recommendations.Add(new TableRecommendation
                {
                    TableId = combo.First().TableId,
                    TableNumber = string.Join("+", combo.Select(t => t.TableNumber)),
                    Capacity = totalCapacity,
                    Score = combinedScore,
                    Reasons = ["Combined tables for large party"],
                    RequiresCombination = true,
                    CombinedTableIds = combo.Select(t => t.TableId).ToList()
                });
            }
        }

        // Sort by score descending
        var sortedRecommendations = recommendations
            .OrderByDescending(r => r.Score)
            .Take(5)
            .ToList();

        return Task.FromResult(new TableAssignmentResult
        {
            Success = sortedRecommendations.Count > 0,
            Recommendations = sortedRecommendations,
            Message = sortedRecommendations.Count > 0 ? null : "No suitable tables available"
        });
    }

    public async Task<TableRecommendation?> AutoAssignAsync(TableAssignmentRequest request)
    {
        var result = await GetRecommendationsAsync(request);
        return result.Recommendations.FirstOrDefault();
    }

    public async Task RegisterTableAsync(Guid tableId, string tableNumber, int minCapacity, int maxCapacity, bool isCombinable, IReadOnlyList<string>? tags = null)
    {
        EnsureExists();

        var existingIndex = _state.State.Tables.FindIndex(t => t.TableId == tableId);
        var table = new OptimizableTable
        {
            TableId = tableId,
            TableNumber = tableNumber,
            MinCapacity = minCapacity,
            MaxCapacity = maxCapacity,
            IsCombinable = isCombinable,
            Tags = tags?.ToList() ?? [],
            IsOccupied = false,
            CurrentCovers = 0
        };

        if (existingIndex >= 0)
        {
            _state.State.Tables[existingIndex] = table;
        }
        else
        {
            _state.State.Tables.Add(table);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UnregisterTableAsync(Guid tableId)
    {
        EnsureExists();

        var removed = _state.State.Tables.RemoveAll(t => t.TableId == tableId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateServerSectionAsync(UpdateServerSectionCommand command)
    {
        EnsureExists();

        var existingIndex = _state.State.ServerSections.FindIndex(s => s.ServerId == command.ServerId);
        var section = new ServerSectionRecord
        {
            ServerId = command.ServerId,
            ServerName = command.ServerName,
            TableIds = command.TableIds.ToList(),
            MaxCovers = command.MaxCovers,
            CurrentCovers = existingIndex >= 0 ? _state.State.ServerSections[existingIndex].CurrentCovers : 0
        };

        if (existingIndex >= 0)
        {
            _state.State.ServerSections[existingIndex] = section;
        }
        else
        {
            _state.State.ServerSections.Add(section);
        }

        // Update table server assignments
        foreach (var tableId in command.TableIds)
        {
            var tableIndex = _state.State.Tables.FindIndex(t => t.TableId == tableId);
            if (tableIndex >= 0)
            {
                _state.State.Tables[tableIndex] = _state.State.Tables[tableIndex] with
                {
                    CurrentServerId = command.ServerId
                };
            }
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveServerSectionAsync(Guid serverId)
    {
        EnsureExists();

        var removed = _state.State.ServerSections.RemoveAll(s => s.ServerId == serverId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ServerSection>> GetServerSectionsAsync()
    {
        EnsureExists();

        var sections = _state.State.ServerSections.Select(s => new ServerSection
        {
            ServerId = s.ServerId,
            ServerName = s.ServerName,
            TableIds = s.TableIds,
            CurrentCoverCount = s.CurrentCovers,
            MaxCovers = s.MaxCovers
        }).ToList();

        return Task.FromResult<IReadOnlyList<ServerSection>>(sections);
    }

    public async Task RecordTableUsageAsync(Guid tableId, Guid serverId, int covers)
    {
        EnsureExists();

        var tableIndex = _state.State.Tables.FindIndex(t => t.TableId == tableId);
        if (tableIndex >= 0)
        {
            _state.State.Tables[tableIndex] = _state.State.Tables[tableIndex] with
            {
                IsOccupied = true,
                CurrentCovers = covers,
                CurrentServerId = serverId
            };
        }

        var sectionIndex = _state.State.ServerSections.FindIndex(s => s.ServerId == serverId);
        if (sectionIndex >= 0)
        {
            _state.State.ServerSections[sectionIndex].CurrentCovers += covers;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ClearTableUsageAsync(Guid tableId)
    {
        EnsureExists();

        var tableIndex = _state.State.Tables.FindIndex(t => t.TableId == tableId);
        if (tableIndex >= 0)
        {
            var table = _state.State.Tables[tableIndex];
            var serverId = table.CurrentServerId;
            var covers = table.CurrentCovers;

            _state.State.Tables[tableIndex] = table with
            {
                IsOccupied = false,
                CurrentCovers = 0
            };

            if (serverId.HasValue)
            {
                var sectionIndex = _state.State.ServerSections.FindIndex(s => s.ServerId == serverId.Value);
                if (sectionIndex >= 0)
                {
                    _state.State.ServerSections[sectionIndex].CurrentCovers =
                        Math.Max(0, _state.State.ServerSections[sectionIndex].CurrentCovers - covers);
                }
            }

            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ServerWorkload>> GetServerWorkloadsAsync()
    {
        EnsureExists();

        var workloads = _state.State.ServerSections.Select(s =>
        {
            var sectionTables = _state.State.Tables.Where(t => s.TableIds.Contains(t.TableId)).ToList();

            return new ServerWorkload
            {
                ServerId = s.ServerId,
                ServerName = s.ServerName,
                CurrentCovers = s.CurrentCovers,
                MaxCovers = s.MaxCovers,
                TableCount = sectionTables.Count,
                OccupiedTableCount = sectionTables.Count(t => t.IsOccupied),
                LoadPercentage = s.MaxCovers > 0 ? (decimal)s.CurrentCovers / s.MaxCovers * 100 : 0
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<ServerWorkload>>(workloads);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private int CalculateTableScore(OptimizableTable table, TableAssignmentRequest request)
    {
        var score = 100;

        // Size match scoring
        if (table.MaxCapacity < request.PartySize)
            return 0; // Table too small

        var sizeOverage = table.MaxCapacity - request.PartySize;
        if (sizeOverage == 0)
            score += 50; // Perfect match
        else if (sizeOverage <= 2)
            score += 30; // Good match
        else
            score -= sizeOverage * 5; // Penalize wasted capacity

        // VIP preference
        if (request.IsVip && table.Tags.Contains("VIP"))
            score += 40;

        // Seating preference matching
        if (!string.IsNullOrEmpty(request.SeatingPreference))
        {
            if (table.Tags.Any(t => t.Equals(request.SeatingPreference, StringComparison.OrdinalIgnoreCase)))
                score += 30;
        }

        // Preferred table
        if (request.PreferredTableId.HasValue && request.PreferredTableId.Value == table.TableId)
            score += 100;

        // Server workload balancing
        if (request.PreferredServerId.HasValue)
        {
            if (table.CurrentServerId == request.PreferredServerId.Value)
                score += 20;
        }
        else
        {
            // Balance workload - prefer less busy servers
            var serverSection = _state.State.ServerSections.FirstOrDefault(s => s.ServerId == table.CurrentServerId);
            if (serverSection != null)
            {
                var loadPercentage = serverSection.MaxCovers > 0
                    ? (double)serverSection.CurrentCovers / serverSection.MaxCovers
                    : 0;
                score += (int)((1 - loadPercentage) * 20); // Up to 20 points for less busy servers
            }
        }

        return Math.Max(0, score);
    }

    private static List<string> GetScoreReasons(OptimizableTable table, TableAssignmentRequest request)
    {
        var reasons = new List<string>();

        var sizeOverage = table.MaxCapacity - request.PartySize;
        if (sizeOverage == 0)
            reasons.Add("Perfect capacity match");
        else if (sizeOverage <= 2)
            reasons.Add("Good capacity match");

        if (request.IsVip && table.Tags.Contains("VIP"))
            reasons.Add("VIP table");

        if (!string.IsNullOrEmpty(request.SeatingPreference) &&
            table.Tags.Any(t => t.Equals(request.SeatingPreference, StringComparison.OrdinalIgnoreCase)))
            reasons.Add($"Matches preference: {request.SeatingPreference}");

        if (request.PreferredTableId.HasValue && request.PreferredTableId.Value == table.TableId)
            reasons.Add("Guest's preferred table");

        return reasons;
    }

    private List<List<OptimizableTable>> FindTableCombinations(List<OptimizableTable> availableTables, int targetCapacity)
    {
        var combinations = new List<List<OptimizableTable>>();
        var combinableTables = availableTables.Where(t => t.IsCombinable).ToList();

        if (combinableTables.Count < 2)
            return combinations;

        // Simple 2-table combinations
        for (int i = 0; i < combinableTables.Count; i++)
        {
            for (int j = i + 1; j < combinableTables.Count; j++)
            {
                var totalCapacity = combinableTables[i].MaxCapacity + combinableTables[j].MaxCapacity;
                if (totalCapacity >= targetCapacity && totalCapacity <= targetCapacity + 4)
                {
                    combinations.Add([combinableTables[i], combinableTables[j]]);
                }
            }
        }

        return combinations;
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Table optimizer not initialized");
    }
}
