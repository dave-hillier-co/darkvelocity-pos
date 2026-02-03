using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class CustomerGrain : JournaledGrain<CustomerState, ICustomerEvent>, ICustomerGrain
{
    private Lazy<IAsyncStream<IStreamEvent>>? _customerStream;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _customerStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.CustomerStreamNamespace, State.OrganizationId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(CustomerState state, ICustomerEvent @event)
    {
        switch (@event)
        {
            case CustomerCreated e:
                state.Id = e.CustomerId;
                state.OrganizationId = e.OrganizationId;
                state.FirstName = e.FirstName ?? "";
                state.LastName = e.LastName ?? "";
                state.DisplayName = e.DisplayName;
                state.Contact = new ContactInfo { Email = e.Email, Phone = e.Phone };
                state.Source = Enum.TryParse<CustomerSource>(e.Source, out var src) ? src : CustomerSource.Direct;
                state.Status = CustomerStatus.Active;
                state.Stats = new CustomerStats { Segment = CustomerSegment.New };
                state.CreatedAt = e.OccurredAt;
                break;

            case CustomerProfileUpdated e:
                if (e.FirstName != null) state.FirstName = e.FirstName;
                if (e.LastName != null) state.LastName = e.LastName;
                if (e.DisplayName != null) state.DisplayName = e.DisplayName;
                if (e.Email != null || e.Phone != null)
                {
                    state.Contact = state.Contact with
                    {
                        Email = e.Email ?? state.Contact.Email,
                        Phone = e.Phone ?? state.Contact.Phone
                    };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerTagAdded e:
                state.Tags.TryAddTag(e.Tag);
                break;

            case CustomerTagRemoved e:
                state.Tags.Remove(e.Tag);
                break;

            case CustomerNoteAdded e:
                state.Notes.Add(new CustomerNote
                {
                    Id = e.NoteId,
                    Content = e.Content,
                    CreatedBy = e.CreatedBy,
                    CreatedAt = e.OccurredAt
                });
                break;

            case CustomerLoyaltyEnrolled e:
                state.Loyalty = new LoyaltyStatus
                {
                    EnrolledAt = e.OccurredAt,
                    ProgramId = e.ProgramId,
                    MemberNumber = e.MemberNumber,
                    TierId = e.InitialTierId,
                    TierName = e.TierName,
                    PointsBalance = e.InitialPointsBalance,
                    LifetimePoints = 0,
                    YtdPoints = 0,
                    PointsToNextTier = 0
                };
                break;

            case CustomerPointsEarned e:
                if (state.Loyalty != null)
                {
                    state.Loyalty = state.Loyalty with
                    {
                        PointsBalance = e.NewBalance,
                        LifetimePoints = state.Loyalty.LifetimePoints + e.Points,
                        YtdPoints = state.Loyalty.YtdPoints + e.Points
                    };
                    if (e.SpendAmount.HasValue)
                    {
                        state.Stats = state.Stats with { TotalSpend = state.Stats.TotalSpend + e.SpendAmount.Value };
                    }
                }
                break;

            case CustomerPointsRedeemed e:
                if (state.Loyalty != null)
                {
                    state.Loyalty = state.Loyalty with { PointsBalance = e.NewBalance };
                }
                break;

            case CustomerPointsAdjusted e:
                if (state.Loyalty != null)
                {
                    state.Loyalty = state.Loyalty with { PointsBalance = e.NewBalance };
                    if (e.Adjustment > 0)
                    {
                        state.Loyalty = state.Loyalty with { LifetimePoints = state.Loyalty.LifetimePoints + e.Adjustment };
                    }
                }
                break;

            case CustomerPointsExpired e:
                if (state.Loyalty != null)
                {
                    state.Loyalty = state.Loyalty with { PointsBalance = e.NewBalance };
                }
                break;

            case CustomerTierChanged e:
                if (state.Loyalty != null)
                {
                    state.Loyalty = state.Loyalty with
                    {
                        TierId = e.NewTierId,
                        TierName = e.NewTierName
                    };
                }
                break;

            case CustomerRewardIssued e:
                var reward = new CustomerReward
                {
                    Id = e.RewardId,
                    RewardDefinitionId = Guid.Empty, // Not in event, preserved from command context
                    Name = e.RewardName,
                    Status = RewardStatus.Available,
                    PointsSpent = 0, // Not in event
                    IssuedAt = e.OccurredAt,
                    ExpiresAt = e.ExpiryDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue
                };
                state.Rewards.Add(reward);
                break;

            case CustomerRewardRedeemed e:
                var rewardIdx = state.Rewards.FindIndex(r => r.Id == e.RewardId);
                if (rewardIdx >= 0)
                {
                    state.Rewards[rewardIdx] = state.Rewards[rewardIdx] with
                    {
                        Status = RewardStatus.Redeemed,
                        RedeemedAt = e.OccurredAt,
                        RedemptionOrderId = e.OrderId
                    };
                }
                break;

            case CustomerRewardsExpired e:
                foreach (var rewardId in e.ExpiredRewardIds)
                {
                    var idx = state.Rewards.FindIndex(r => r.Id == rewardId);
                    if (idx >= 0)
                    {
                        state.Rewards[idx] = state.Rewards[idx] with { Status = RewardStatus.Expired };
                    }
                }
                break;

            case CustomerVisitRecorded e:
                state.Stats = state.Stats with
                {
                    TotalVisits = e.VisitNumber,
                    TotalSpend = state.Stats.TotalSpend + e.SpendAmount,
                    AverageCheck = (state.Stats.TotalSpend + e.SpendAmount) / e.VisitNumber,
                    LastVisitSiteId = e.SiteId,
                    DaysSinceLastVisit = 0
                };
                state.LastVisitAt = e.OccurredAt;
                break;

            case CustomerPreferencesUpdated e:
                state.Preferences = state.Preferences with
                {
                    DietaryRestrictions = e.DietaryRestrictions ?? state.Preferences.DietaryRestrictions,
                    Allergens = e.Allergens ?? state.Preferences.Allergens,
                    SeatingPreference = e.SeatingPreference ?? state.Preferences.SeatingPreference,
                    Notes = e.Notes ?? state.Preferences.Notes
                };
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerDietaryRestrictionAdded e:
                var restrictions = state.Preferences.DietaryRestrictions.ToList();
                if (!restrictions.Contains(e.Restriction))
                {
                    restrictions.Add(e.Restriction);
                    state.Preferences = state.Preferences with { DietaryRestrictions = restrictions };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerDietaryRestrictionRemoved e:
                var restrictionsList = state.Preferences.DietaryRestrictions.ToList();
                restrictionsList.Remove(e.Restriction);
                state.Preferences = state.Preferences with { DietaryRestrictions = restrictionsList };
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerAllergenAdded e:
                var allergens = state.Preferences.Allergens.ToList();
                if (!allergens.Contains(e.Allergen))
                {
                    allergens.Add(e.Allergen);
                    state.Preferences = state.Preferences with { Allergens = allergens };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerAllergenRemoved e:
                var allergensList = state.Preferences.Allergens.ToList();
                allergensList.Remove(e.Allergen);
                state.Preferences = state.Preferences with { Allergens = allergensList };
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerSeatingPreferenceSet e:
                state.Preferences = state.Preferences with { SeatingPreference = e.Preference };
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerReferralCodeGenerated e:
                state.ReferralCode = e.ReferralCode;
                break;

            case CustomerReferredBySet e:
                state.ReferredBy = e.ReferrerId;
                break;

            case CustomerReferralCompleted e:
                state.SuccessfulReferrals++;
                break;

            case CustomerMerged e:
                state.MergedFrom.Add(e.SourceCustomerId);
                break;

            case CustomerDeactivated e:
                state.Status = CustomerStatus.Inactive;
                break;

            case CustomerReactivated e:
                state.Status = CustomerStatus.Active;
                break;

            case CustomerAnonymized e:
                state.FirstName = "REDACTED";
                state.LastName = "REDACTED";
                state.DisplayName = "REDACTED";
                state.Contact = new ContactInfo();
                state.DateOfBirth = null;
                state.Status = CustomerStatus.Inactive;
                break;
        }
    }

    public async Task<CustomerCreatedResult> CreateAsync(CreateCustomerCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Customer already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, customerId) = GrainKeys.ParseOrgEntity(key);

        RaiseEvent(new CustomerCreated
        {
            CustomerId = customerId,
            OrganizationId = command.OrganizationId,
            FirstName = command.FirstName,
            LastName = command.LastName,
            DisplayName = $"{command.FirstName} {command.LastName}".Trim(),
            Email = command.Email,
            Phone = command.Phone,
            Source = command.Source.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish customer created event
        await _customerStream!.Value.OnNextAsync(new CustomerCreatedEvent(
            customerId,
            State.DisplayName,
            command.Email,
            command.Phone,
            command.Source.ToString(),
            ReferredByCustomerId: null)
        {
            OrganizationId = command.OrganizationId
        });

        return new CustomerCreatedResult(customerId, State.DisplayName, State.CreatedAt);
    }

    public Task<CustomerState> GetStateAsync() => Task.FromResult(State);

    public async Task UpdateAsync(UpdateCustomerCommand command)
    {
        EnsureExists();

        var changedFields = new List<string>();
        string? newFirstName = null;
        string? newLastName = null;
        string? newEmail = null;
        string? newPhone = null;

        if (command.FirstName != null)
        {
            newFirstName = command.FirstName;
            changedFields.Add("FirstName");
        }
        if (command.LastName != null)
        {
            newLastName = command.LastName;
            changedFields.Add("LastName");
        }
        if (command.Email != null)
        {
            newEmail = command.Email;
            changedFields.Add("Email");
        }
        if (command.Phone != null)
        {
            newPhone = command.Phone;
            changedFields.Add("Phone");
        }
        if (command.DateOfBirth != null)
        {
            changedFields.Add("DateOfBirth");
        }
        if (command.Preferences != null)
        {
            changedFields.Add("Preferences");
        }

        if (changedFields.Count == 0) return;

        var displayName = $"{newFirstName ?? State.FirstName} {newLastName ?? State.LastName}".Trim();

        RaiseEvent(new CustomerProfileUpdated
        {
            CustomerId = State.Id,
            FirstName = newFirstName,
            LastName = newLastName,
            DisplayName = displayName,
            Email = newEmail,
            Phone = newPhone,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish customer profile updated event
        await _customerStream!.Value.OnNextAsync(new CustomerProfileUpdatedEvent(
            State.Id,
            State.DisplayName,
            changedFields,
            UpdatedBy: null)
        {
            OrganizationId = State.OrganizationId
        });
    }

    public async Task AddTagAsync(string tag)
    {
        EnsureExists();
        if (!State.Tags.Contains(tag))
        {
            RaiseEvent(new CustomerTagAdded
            {
                CustomerId = State.Id,
                Tag = tag,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            // Publish customer tag added event
            await _customerStream!.Value.OnNextAsync(new CustomerTagAddedEvent(
                State.Id,
                tag,
                AddedBy: null)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task RemoveTagAsync(string tag)
    {
        EnsureExists();
        if (State.Tags.Contains(tag))
        {
            RaiseEvent(new CustomerTagRemoved
            {
                CustomerId = State.Id,
                Tag = tag,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            // Publish customer tag removed event
            await _customerStream!.Value.OnNextAsync(new CustomerTagRemovedEvent(
                State.Id,
                tag,
                RemovedBy: null)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task AddNoteAsync(string content, Guid createdBy)
    {
        EnsureExists();
        RaiseEvent(new CustomerNoteAdded
        {
            CustomerId = State.Id,
            NoteId = Guid.NewGuid(),
            Content = content,
            CreatedBy = createdBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task EnrollInLoyaltyAsync(EnrollLoyaltyCommand command)
    {
        EnsureExists();
        if (State.Loyalty != null)
            throw new InvalidOperationException("Customer already enrolled in loyalty");

        RaiseEvent(new CustomerLoyaltyEnrolled
        {
            CustomerId = State.Id,
            ProgramId = command.ProgramId,
            MemberNumber = command.MemberNumber,
            InitialTierId = command.InitialTierId,
            TierName = command.TierName,
            InitialPointsBalance = 0,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish customer enrolled in loyalty event
        await _customerStream!.Value.OnNextAsync(new CustomerEnrolledInLoyaltyEvent(
            State.Id,
            command.ProgramId,
            command.MemberNumber,
            command.InitialTierId,
            command.TierName,
            InitialPointsBalance: 0)
        {
            OrganizationId = State.OrganizationId
        });
    }

    public async Task<PointsResult> EarnPointsAsync(EarnPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var newBalance = State.Loyalty!.PointsBalance + command.Points;

        RaiseEvent(new CustomerPointsEarned
        {
            CustomerId = State.Id,
            Points = command.Points,
            NewBalance = newBalance,
            OrderId = command.OrderId,
            SpendAmount = command.SpendAmount,
            Reason = command.Reason ?? "",
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new PointsResult(State.Loyalty.PointsBalance, State.Loyalty.LifetimePoints);
    }

    public async Task<PointsResult> RedeemPointsAsync(RedeemPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        if (State.Loyalty!.PointsBalance < command.Points)
            throw new InvalidOperationException("Insufficient points");

        var newBalance = State.Loyalty.PointsBalance - command.Points;

        RaiseEvent(new CustomerPointsRedeemed
        {
            CustomerId = State.Id,
            Points = command.Points,
            NewBalance = newBalance,
            OrderId = command.OrderId,
            DiscountValue = command.DiscountValue ?? 0,
            RewardType = command.RewardType ?? "",
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new PointsResult(State.Loyalty.PointsBalance, State.Loyalty.LifetimePoints);
    }

    public async Task<PointsResult> AdjustPointsAsync(AdjustPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var newBalance = State.Loyalty!.PointsBalance + command.Points;
        if (newBalance < 0)
            throw new InvalidOperationException("Adjustment would result in negative balance");

        RaiseEvent(new CustomerPointsAdjusted
        {
            CustomerId = State.Id,
            Adjustment = command.Points,
            NewBalance = newBalance,
            Reason = command.Reason ?? "",
            AdjustedBy = command.AdjustedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new PointsResult(State.Loyalty.PointsBalance, State.Loyalty.LifetimePoints);
    }

    public async Task ExpirePointsAsync(int points, DateTime expiryDate)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var newBalance = Math.Max(0, State.Loyalty!.PointsBalance - points);

        RaiseEvent(new CustomerPointsExpired
        {
            CustomerId = State.Id,
            ExpiredPoints = points,
            NewBalance = newBalance,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task PromoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        RaiseEvent(new CustomerTierChanged
        {
            CustomerId = State.Id,
            OldTierId = State.Loyalty!.TierId,
            OldTierName = State.Loyalty.TierName,
            NewTierId = newTierId,
            NewTierName = tierName,
            CumulativeSpend = State.Stats.TotalSpend,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task DemoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier)
    {
        await PromoteTierAsync(newTierId, tierName, pointsToNextTier);
    }

    public async Task<RewardResult> IssueRewardAsync(IssueRewardCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var rewardId = Guid.NewGuid();

        RaiseEvent(new CustomerRewardIssued
        {
            CustomerId = State.Id,
            RewardId = rewardId,
            RewardType = "",
            RewardName = command.RewardName,
            Value = null,
            ExpiryDate = command.ExpiresAt.HasValue ? DateOnly.FromDateTime(command.ExpiresAt.Value) : null,
            Reason = null,
            OccurredAt = DateTime.UtcNow
        });

        if (command.PointsCost > 0)
        {
            RaiseEvent(new CustomerPointsRedeemed
            {
                CustomerId = State.Id,
                Points = command.PointsCost,
                NewBalance = State.Loyalty!.PointsBalance - command.PointsCost,
                OrderId = Guid.Empty,
                DiscountValue = 0,
                RewardType = "reward_issue",
                OccurredAt = DateTime.UtcNow
            });
        }

        await ConfirmEvents();

        var issuedReward = State.Rewards.FirstOrDefault(r => r.Id == rewardId);
        return new RewardResult(rewardId, issuedReward?.ExpiresAt);
    }

    public async Task RedeemRewardAsync(RedeemRewardCommand command)
    {
        EnsureExists();

        var reward = State.Rewards.FirstOrDefault(r => r.Id == command.RewardId)
            ?? throw new InvalidOperationException("Reward not found");

        if (reward.Status != RewardStatus.Available)
            throw new InvalidOperationException($"Reward is not available: {reward.Status}");

        if (reward.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Reward has expired");

        RaiseEvent(new CustomerRewardRedeemed
        {
            CustomerId = State.Id,
            RewardId = command.RewardId,
            OrderId = command.OrderId,
            RedeemedValue = null,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task ExpireRewardsAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var expiredIds = State.Rewards
            .Where(r => r.Status == RewardStatus.Available && r.ExpiresAt < now)
            .Select(r => r.Id)
            .ToList();

        if (expiredIds.Count > 0)
        {
            RaiseEvent(new CustomerRewardsExpired
            {
                CustomerId = State.Id,
                ExpiredRewardIds = expiredIds,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task RecordVisitAsync(RecordVisitCommand command)
    {
        EnsureExists();

        // Calculate days since previous visit before updating
        var daysSincePreviousVisit = State.LastVisitAt.HasValue
            ? (int)(DateTime.UtcNow - State.LastVisitAt.Value).TotalDays
            : 0;

        var visitNumber = State.Stats.TotalVisits + 1;

        // Add to visit history
        var visitRecord = new CustomerVisitRecord
        {
            Id = Guid.NewGuid(),
            SiteId = command.SiteId,
            SiteName = command.SiteName,
            VisitedAt = DateTime.UtcNow,
            OrderId = command.OrderId,
            BookingId = command.BookingId,
            SpendAmount = command.SpendAmount,
            PartySize = command.PartySize,
            PointsEarned = command.PointsEarned,
            Notes = command.Notes
        };

        State.VisitHistory.Insert(0, visitRecord);
        // Keep only last 50 visits
        if (State.VisitHistory.Count > 50)
        {
            State.VisitHistory.RemoveAt(State.VisitHistory.Count - 1);
        }

        RaiseEvent(new CustomerVisitRecorded
        {
            CustomerId = State.Id,
            SiteId = command.SiteId,
            OrderId = command.OrderId,
            SpendAmount = command.SpendAmount,
            VisitNumber = visitNumber,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish customer visited event
        await _customerStream!.Value.OnNextAsync(new CustomerVisitedEvent(
            State.Id,
            command.SiteId,
            command.OrderId,
            command.SpendAmount,
            State.Stats.TotalVisits,
            State.Stats.TotalSpend,
            daysSincePreviousVisit)
        {
            OrganizationId = State.OrganizationId
        });
    }

    public Task<IReadOnlyList<CustomerVisitRecord>> GetVisitHistoryAsync(int limit = 50)
    {
        EnsureExists();
        var visits = State.VisitHistory.Take(limit).ToList();
        return Task.FromResult<IReadOnlyList<CustomerVisitRecord>>(visits);
    }

    public Task<IReadOnlyList<CustomerVisitRecord>> GetVisitsBySiteAsync(Guid siteId, int limit = 20)
    {
        EnsureExists();
        var visits = State.VisitHistory
            .Where(v => v.SiteId == siteId)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<CustomerVisitRecord>>(visits);
    }

    public Task<CustomerVisitRecord?> GetLastVisitAsync()
    {
        EnsureExists();
        return Task.FromResult(State.VisitHistory.FirstOrDefault());
    }

    public async Task UpdatePreferencesAsync(UpdatePreferencesCommand command)
    {
        EnsureExists();

        RaiseEvent(new CustomerPreferencesUpdated
        {
            CustomerId = State.Id,
            DietaryRestrictions = command.DietaryRestrictions,
            Allergens = command.Allergens,
            SeatingPreference = command.SeatingPreference,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AddDietaryRestrictionAsync(string restriction)
    {
        EnsureExists();
        if (!State.Preferences.DietaryRestrictions.Contains(restriction))
        {
            RaiseEvent(new CustomerDietaryRestrictionAdded
            {
                CustomerId = State.Id,
                Restriction = restriction,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task RemoveDietaryRestrictionAsync(string restriction)
    {
        EnsureExists();
        if (State.Preferences.DietaryRestrictions.Contains(restriction))
        {
            RaiseEvent(new CustomerDietaryRestrictionRemoved
            {
                CustomerId = State.Id,
                Restriction = restriction,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task AddAllergenAsync(string allergen)
    {
        EnsureExists();
        if (!State.Preferences.Allergens.Contains(allergen))
        {
            RaiseEvent(new CustomerAllergenAdded
            {
                CustomerId = State.Id,
                Allergen = allergen,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task RemoveAllergenAsync(string allergen)
    {
        EnsureExists();
        if (State.Preferences.Allergens.Contains(allergen))
        {
            RaiseEvent(new CustomerAllergenRemoved
            {
                CustomerId = State.Id,
                Allergen = allergen,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task SetSeatingPreferenceAsync(string preference)
    {
        EnsureExists();
        RaiseEvent(new CustomerSeatingPreferenceSet
        {
            CustomerId = State.Id,
            Preference = preference,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task SetReferralCodeAsync(string code)
    {
        EnsureExists();
        RaiseEvent(new CustomerReferralCodeGenerated
        {
            CustomerId = State.Id,
            ReferralCode = code,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task SetReferredByAsync(Guid referrerId)
    {
        EnsureExists();
        RaiseEvent(new CustomerReferredBySet
        {
            CustomerId = State.Id,
            ReferrerId = referrerId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task IncrementReferralCountAsync()
    {
        EnsureExists();
        RaiseEvent(new CustomerReferralCompleted
        {
            CustomerId = State.Id,
            ReferredCustomerId = Guid.Empty, // Would need to be passed in
            BonusPointsAwarded = null,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task MergeFromAsync(Guid sourceCustomerId)
    {
        EnsureExists();
        RaiseEvent(new CustomerMerged
        {
            CustomerId = State.Id,
            SourceCustomerId = sourceCustomerId,
            CombinedLifetimeSpend = State.Stats.TotalSpend,
            CombinedTotalPoints = State.Loyalty?.LifetimePoints ?? 0,
            CombinedVisits = State.Stats.TotalVisits,
            MergedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        RaiseEvent(new CustomerDeactivated
        {
            CustomerId = State.Id,
            Reason = "Deleted",
            DeactivatedBy = null,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AnonymizeAsync()
    {
        EnsureExists();
        RaiseEvent(new CustomerAnonymized
        {
            CustomerId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<bool> IsLoyaltyMemberAsync() => Task.FromResult(State.Loyalty != null);
    public Task<int> GetPointsBalanceAsync() => Task.FromResult(State.Loyalty?.PointsBalance ?? 0);
    public Task<IReadOnlyList<CustomerReward>> GetAvailableRewardsAsync()
        => Task.FromResult<IReadOnlyList<CustomerReward>>(State.Rewards.Where(r => r.Status == RewardStatus.Available && r.ExpiresAt > DateTime.UtcNow).ToList());

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Customer does not exist");
    }

    private void EnsureLoyaltyEnrolled()
    {
        if (State.Loyalty == null)
            throw new InvalidOperationException("Customer not enrolled in loyalty program");
    }
}
