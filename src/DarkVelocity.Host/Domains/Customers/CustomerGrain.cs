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

            case CustomerNoShowRecorded e:
                state.NoShowCount++;
                state.LastNoShowAt = e.OccurredAt;
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
                state.IsAnonymized = true;
                state.AnonymizedAt = e.OccurredAt;
                state.AnonymizedHash = e.AnonymizedHash;
                // Clear PII but retain aggregate stats if requested
                if (!e.RetainAggregateStats)
                {
                    state.Notes.Clear();
                    state.Preferences = new CustomerPreferences();
                }
                break;

            // RFM Segmentation Events
            case CustomerRfmScoreCalculated e:
                state.RfmScore = new RfmScore
                {
                    RecencyScore = e.RecencyScore,
                    FrequencyScore = e.FrequencyScore,
                    MonetaryScore = e.MonetaryScore,
                    CalculatedAt = e.OccurredAt,
                    DaysSinceLastVisit = e.DaysSinceLastVisit,
                    VisitCount = e.VisitCount,
                    TotalSpend = e.TotalSpend
                };
                break;

            case CustomerSegmentChanged e:
                var previousSegment = state.Stats.Segment;
                if (Enum.TryParse<CustomerSegment>(e.NewSegment, out var newSegment))
                {
                    state.Stats = state.Stats with { Segment = newSegment };
                    state.SegmentHistory.Add(new SegmentChange
                    {
                        PreviousSegment = previousSegment,
                        NewSegment = newSegment,
                        RfmScore = state.RfmScore,
                        ChangedAt = e.OccurredAt,
                        Reason = e.Reason
                    });
                    // Keep only last 20 segment changes
                    if (state.SegmentHistory.Count > 20)
                        state.SegmentHistory.RemoveAt(0);
                }
                break;

            // GDPR Consent Events
            case CustomerConsentUpdated e:
                var consent = state.Consent;
                consent = e.ConsentType switch
                {
                    "MarketingEmail" => consent with { MarketingEmail = e.NewValue, MarketingEmailConsentedAt = e.NewValue ? e.OccurredAt : consent.MarketingEmailConsentedAt },
                    "Sms" => consent with { Sms = e.NewValue, SmsConsentedAt = e.NewValue ? e.OccurredAt : consent.SmsConsentedAt },
                    "DataRetention" => consent with { DataRetention = e.NewValue, DataRetentionConsentedAt = e.NewValue ? e.OccurredAt : consent.DataRetentionConsentedAt },
                    "Profiling" => consent with { Profiling = e.NewValue, ProfilingConsentedAt = e.NewValue ? e.OccurredAt : consent.ProfilingConsentedAt },
                    _ => consent
                };
                consent = consent with { ConsentVersion = e.ConsentVersion ?? consent.ConsentVersion, LastUpdated = e.OccurredAt };
                state.Consent = consent;
                state.ConsentHistory.Add(new ConsentChange
                {
                    ConsentType = e.ConsentType,
                    PreviousValue = e.PreviousValue,
                    NewValue = e.NewValue,
                    ChangedAt = e.OccurredAt,
                    IpAddress = e.IpAddress,
                    UserAgent = e.UserAgent
                });
                // Keep only last 50 consent changes
                if (state.ConsentHistory.Count > 50)
                    state.ConsentHistory.RemoveAt(0);
                break;

            // VIP Events
            case CustomerVipStatusGranted e:
                state.VipStatus = new VipStatus
                {
                    IsVip = true,
                    VipSince = e.OccurredAt,
                    VipReason = e.Reason,
                    SpendAtVipGrant = e.SpendAtGrant,
                    VisitsAtVipGrant = e.VisitsAtGrant,
                    ManuallyAssigned = e.ManuallyAssigned
                };
                state.Tags.TryAddTag("VIP");
                break;

            case CustomerVipStatusRevoked e:
                state.VipStatus = new VipStatus
                {
                    IsVip = false,
                    VipSince = state.VipStatus.VipSince,
                    VipReason = e.Reason
                };
                state.Tags.Remove("VIP");
                break;

            // Birthday Events
            case CustomerBirthdaySet e:
                state.DateOfBirth = e.Birthday;
                state.UpdatedAt = e.OccurredAt;
                break;

            case CustomerBirthdayRewardIssued e:
                var birthdayReward = new BirthdayRewardStatus
                {
                    CurrentRewardId = e.RewardId,
                    IssuedAt = e.OccurredAt,
                    Year = e.Year
                };
                state.CurrentBirthdayReward = birthdayReward;
                // Also issue as a regular reward
                state.Rewards.Add(new CustomerReward
                {
                    Id = e.RewardId,
                    Name = e.RewardName,
                    Status = RewardStatus.Available,
                    IssuedAt = e.OccurredAt,
                    ExpiresAt = e.ExpiresAt?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue
                });
                break;

            case CustomerBirthdayRewardRedeemed e:
                if (state.CurrentBirthdayReward != null)
                {
                    state.CurrentBirthdayReward = state.CurrentBirthdayReward with { RedeemedAt = e.OccurredAt };
                    state.BirthdayRewardHistory.Add(state.CurrentBirthdayReward);
                }
                // Mark the reward as redeemed
                var birthdayRewardIdx = state.Rewards.FindIndex(r => r.Id == e.RewardId);
                if (birthdayRewardIdx >= 0)
                {
                    state.Rewards[birthdayRewardIdx] = state.Rewards[birthdayRewardIdx] with
                    {
                        Status = RewardStatus.Redeemed,
                        RedeemedAt = e.OccurredAt,
                        RedemptionOrderId = e.OrderId
                    };
                }
                break;

            // Enhanced Referral Events
            case CustomerReferralRewardAwarded e:
                state.Referral = state.Referral with
                {
                    SuccessfulReferrals = e.TotalReferrals,
                    TotalPointsEarnedFromReferrals = state.Referral.TotalPointsEarnedFromReferrals + e.PointsAwarded,
                    ReferredCustomers = state.Referral.ReferredCustomers.Append(e.ReferredCustomerId).ToList()
                };
                // Also update legacy fields for compatibility
                state.SuccessfulReferrals = e.TotalReferrals;
                break;

            case CustomerReferralCapReached e:
                // Cap reached - no more referral rewards
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

        // Generate a hash of the original email for reference (if needed for legal/audit purposes)
        var anonymizedHash = State.Contact.Email != null
            ? Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(State.Contact.Email + State.Id.ToString())))
            : null;

        RaiseEvent(new CustomerAnonymized
        {
            CustomerId = State.Id,
            OccurredAt = DateTime.UtcNow,
            AnonymizedHash = anonymizedHash,
            RetainAggregateStats = true // Keep aggregate stats for business reporting
        });
        await ConfirmEvents();
    }

    public async Task<bool> RequestDataDeletionAsync(string? requestedBy = null, string? reason = null)
    {
        EnsureExists();

        RaiseEvent(new CustomerDataDeletionRequested
        {
            CustomerId = State.Id,
            RequestedBy = requestedBy,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Then anonymize the data
        await AnonymizeAsync();
        return true;
    }

    // ==================== GDPR Consent Management ====================

    public async Task UpdateConsentAsync(UpdateConsentCommand command)
    {
        EnsureExists();

        var now = DateTime.UtcNow;

        if (command.MarketingEmail.HasValue && command.MarketingEmail != State.Consent.MarketingEmail)
        {
            RaiseEvent(new CustomerConsentUpdated
            {
                CustomerId = State.Id,
                ConsentType = "MarketingEmail",
                PreviousValue = State.Consent.MarketingEmail,
                NewValue = command.MarketingEmail.Value,
                ConsentVersion = command.ConsentVersion,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                OccurredAt = now
            });
        }

        if (command.Sms.HasValue && command.Sms != State.Consent.Sms)
        {
            RaiseEvent(new CustomerConsentUpdated
            {
                CustomerId = State.Id,
                ConsentType = "Sms",
                PreviousValue = State.Consent.Sms,
                NewValue = command.Sms.Value,
                ConsentVersion = command.ConsentVersion,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                OccurredAt = now
            });
        }

        if (command.DataRetention.HasValue && command.DataRetention != State.Consent.DataRetention)
        {
            RaiseEvent(new CustomerConsentUpdated
            {
                CustomerId = State.Id,
                ConsentType = "DataRetention",
                PreviousValue = State.Consent.DataRetention,
                NewValue = command.DataRetention.Value,
                ConsentVersion = command.ConsentVersion,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                OccurredAt = now
            });
        }

        if (command.Profiling.HasValue && command.Profiling != State.Consent.Profiling)
        {
            RaiseEvent(new CustomerConsentUpdated
            {
                CustomerId = State.Id,
                ConsentType = "Profiling",
                PreviousValue = State.Consent.Profiling,
                NewValue = command.Profiling.Value,
                ConsentVersion = command.ConsentVersion,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                OccurredAt = now
            });
        }

        await ConfirmEvents();
    }

    public Task<ConsentStatus> GetConsentStatusAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Consent);
    }

    public Task<IReadOnlyList<ConsentChange>> GetConsentHistoryAsync()
    {
        EnsureExists();
        return Task.FromResult<IReadOnlyList<ConsentChange>>(State.ConsentHistory);
    }

    // ==================== RFM Segmentation ====================

    public async Task<RfmScore> CalculateRfmScoreAsync(SegmentationThresholds? thresholds = null)
    {
        EnsureExists();

        thresholds ??= new SegmentationThresholds();

        var daysSinceLastVisit = State.LastVisitAt.HasValue
            ? (int)(DateTime.UtcNow - State.LastVisitAt.Value).TotalDays
            : 365; // Default to 1 year if never visited

        var visitCount = State.Stats.TotalVisits;
        var totalSpend = State.Stats.TotalSpend;

        // Calculate Recency Score (5 = best, 1 = worst)
        var recencyScore = daysSinceLastVisit <= thresholds.RecencyDaysExcellent ? 5 :
                          daysSinceLastVisit <= thresholds.RecencyDaysGood ? 4 :
                          daysSinceLastVisit <= thresholds.RecencyDaysFair ? 3 :
                          daysSinceLastVisit <= thresholds.RecencyDaysPoor ? 2 : 1;

        // Calculate Frequency Score
        var frequencyScore = visitCount >= thresholds.FrequencyCountExcellent ? 5 :
                            visitCount >= thresholds.FrequencyCountGood ? 4 :
                            visitCount >= thresholds.FrequencyCountFair ? 3 :
                            visitCount >= thresholds.FrequencyCountPoor ? 2 : 1;

        // Calculate Monetary Score
        var monetaryScore = totalSpend >= thresholds.MonetaryValueExcellent ? 5 :
                           totalSpend >= thresholds.MonetaryValueGood ? 4 :
                           totalSpend >= thresholds.MonetaryValueFair ? 3 :
                           totalSpend >= thresholds.MonetaryValuePoor ? 2 : 1;

        RaiseEvent(new CustomerRfmScoreCalculated
        {
            CustomerId = State.Id,
            RecencyScore = recencyScore,
            FrequencyScore = frequencyScore,
            MonetaryScore = monetaryScore,
            DaysSinceLastVisit = daysSinceLastVisit,
            VisitCount = visitCount,
            TotalSpend = totalSpend,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return State.RfmScore!;
    }

    public Task<CustomerSegment> GetSegmentAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Stats.Segment);
    }

    public Task<IReadOnlyList<SegmentChange>> GetSegmentHistoryAsync()
    {
        EnsureExists();
        return Task.FromResult<IReadOnlyList<SegmentChange>>(State.SegmentHistory);
    }

    public async Task RecalculateSegmentAsync(SegmentationThresholds? thresholds = null)
    {
        EnsureExists();

        // First calculate RFM score
        var rfmScore = await CalculateRfmScoreAsync(thresholds);

        // Determine segment based on RFM scores
        var newSegment = DetermineSegment(rfmScore);

        if (newSegment != State.Stats.Segment)
        {
            RaiseEvent(new CustomerSegmentChanged
            {
                CustomerId = State.Id,
                PreviousSegment = State.Stats.Segment.ToString(),
                NewSegment = newSegment.ToString(),
                RecencyScore = rfmScore.RecencyScore,
                FrequencyScore = rfmScore.FrequencyScore,
                MonetaryScore = rfmScore.MonetaryScore,
                Reason = "RFM recalculation",
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    private CustomerSegment DetermineSegment(RfmScore rfm)
    {
        var r = rfm.RecencyScore;
        var f = rfm.FrequencyScore;
        var m = rfm.MonetaryScore;
        var combined = rfm.CombinedScore;

        // Champions: High R, F, M (all 4-5)
        if (r >= 4 && f >= 4 && m >= 4)
            return CustomerSegment.Champion;

        // Loyal: Good frequency and monetary (F >= 4 or M >= 4), decent recency
        if (r >= 3 && (f >= 4 || m >= 4))
            return CustomerSegment.Loyal;

        // Potential Loyal: Recent customers with good frequency or monetary
        if (r >= 4 && (f >= 2 && f <= 3) && m >= 3)
            return CustomerSegment.PotentialLoyal;

        // New: Recent but low frequency (based on visit count)
        if (r >= 4 && f <= 2 && rfm.VisitCount <= 2)
            return CustomerSegment.New;

        // At Risk: Were good customers but haven't visited recently
        if (r <= 2 && (f >= 3 || m >= 3))
            return CustomerSegment.AtRisk;

        // Hibernating: Low recency, low frequency, but had some monetary value
        if (r <= 2 && f <= 2 && m >= 2)
            return CustomerSegment.Hibernating;

        // Lost: Very low across the board
        if (combined <= 5)
            return CustomerSegment.Lost;

        // Default to Regular for everyone else
        return CustomerSegment.Regular;
    }

    // ==================== VIP Detection ====================

    public Task<bool> IsVipAsync()
    {
        EnsureExists();
        return Task.FromResult(State.VipStatus.IsVip);
    }

    public async Task GrantVipStatusAsync(GrantVipStatusCommand command)
    {
        EnsureExists();

        if (State.VipStatus.IsVip)
            throw new InvalidOperationException("Customer is already VIP");

        RaiseEvent(new CustomerVipStatusGranted
        {
            CustomerId = State.Id,
            Reason = command.Reason,
            SpendAtGrant = State.Stats.TotalSpend,
            VisitsAtGrant = State.Stats.TotalVisits,
            ManuallyAssigned = true,
            GrantedBy = command.GrantedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RevokeVipStatusAsync(RevokeVipStatusCommand command)
    {
        EnsureExists();

        if (!State.VipStatus.IsVip)
            throw new InvalidOperationException("Customer is not VIP");

        RaiseEvent(new CustomerVipStatusRevoked
        {
            CustomerId = State.Id,
            Reason = command.Reason,
            RevokedBy = command.RevokedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task CheckAndUpdateVipStatusAsync(VipThresholds? thresholds = null)
    {
        EnsureExists();

        if (State.VipStatus.IsVip && State.VipStatus.ManuallyAssigned)
            return; // Don't auto-revoke manually assigned VIP

        thresholds ??= new VipThresholds();

        var meetsSpendThreshold = !thresholds.MinimumSpend.HasValue || State.Stats.TotalSpend >= thresholds.MinimumSpend.Value;
        var meetsVisitThreshold = !thresholds.MinimumVisits.HasValue || State.Stats.TotalVisits >= thresholds.MinimumVisits.Value;

        var shouldBeVip = thresholds.RequireBoth
            ? meetsSpendThreshold && meetsVisitThreshold
            : meetsSpendThreshold || meetsVisitThreshold;

        if (shouldBeVip && !State.VipStatus.IsVip)
        {
            var reason = new List<string>();
            if (meetsSpendThreshold && thresholds.MinimumSpend.HasValue)
                reason.Add($"Spend >= {thresholds.MinimumSpend.Value:C}");
            if (meetsVisitThreshold && thresholds.MinimumVisits.HasValue)
                reason.Add($"Visits >= {thresholds.MinimumVisits.Value}");

            RaiseEvent(new CustomerVipStatusGranted
            {
                CustomerId = State.Id,
                Reason = string.Join(", ", reason),
                SpendAtGrant = State.Stats.TotalSpend,
                VisitsAtGrant = State.Stats.TotalVisits,
                ManuallyAssigned = false,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public Task<VipStatus> GetVipStatusAsync()
    {
        EnsureExists();
        return Task.FromResult(State.VipStatus);
    }

    // ==================== Birthday Rewards ====================

    public async Task SetBirthdayAsync(SetBirthdayCommand command)
    {
        EnsureExists();

        RaiseEvent(new CustomerBirthdaySet
        {
            CustomerId = State.Id,
            Birthday = command.Birthday,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<RewardResult> IssueBirthdayRewardAsync(IssueBirthdayRewardCommand command)
    {
        EnsureExists();

        var currentYear = DateTime.UtcNow.Year;

        // Check if already issued this year
        if (State.CurrentBirthdayReward?.Year == currentYear)
            throw new InvalidOperationException("Birthday reward already issued this year");

        if (!State.DateOfBirth.HasValue)
            throw new InvalidOperationException("Customer birthday not set");

        var rewardId = Guid.NewGuid();
        var expiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(command.ValidDays ?? 30));

        RaiseEvent(new CustomerBirthdayRewardIssued
        {
            CustomerId = State.Id,
            RewardId = rewardId,
            RewardName = command.RewardName,
            Year = currentYear,
            ExpiresAt = expiresAt,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new RewardResult(rewardId, expiresAt.ToDateTime(TimeOnly.MinValue));
    }

    public Task<bool> HasBirthdayRewardThisYearAsync()
    {
        EnsureExists();
        var currentYear = DateTime.UtcNow.Year;
        return Task.FromResult(State.CurrentBirthdayReward?.Year == currentYear);
    }

    public Task<BirthdayRewardStatus?> GetCurrentBirthdayRewardAsync()
    {
        EnsureExists();
        return Task.FromResult(State.CurrentBirthdayReward);
    }

    // ==================== Enhanced Referral Tracking ====================

    public async Task<string> GenerateReferralCodeAsync(GenerateReferralCodeCommand? command = null)
    {
        EnsureExists();

        if (!string.IsNullOrEmpty(State.Referral.ReferralCode))
            return State.Referral.ReferralCode;

        var prefix = command?.Prefix ?? State.FirstName?.ToUpperInvariant().Replace(" ", "")[..Math.Min(4, State.FirstName.Length)] ?? "REF";
        var code = $"{prefix}{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        RaiseEvent(new CustomerReferralCodeGenerated
        {
            CustomerId = State.Id,
            ReferralCode = code,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Update legacy field
        State.ReferralCode = code;
        State.Referral = State.Referral with { ReferralCode = code };

        return code;
    }

    public async Task<ReferralResult> CompleteReferralAsync(CompleteReferralCommand command)
    {
        EnsureExists();

        var totalReferrals = State.Referral.SuccessfulReferrals + 1;
        var capReached = totalReferrals >= State.Referral.ReferralRewardsCap;

        if (capReached && State.Referral.SuccessfulReferrals >= State.Referral.ReferralRewardsCap)
        {
            // Already at cap, no more rewards
            return new ReferralResult(false, 0, State.Referral.SuccessfulReferrals, true);
        }

        var pointsToAward = capReached ? 0 : command.PointsToAward;

        RaiseEvent(new CustomerReferralRewardAwarded
        {
            CustomerId = State.Id,
            ReferredCustomerId = command.ReferredCustomerId,
            PointsAwarded = pointsToAward,
            TotalReferrals = totalReferrals,
            OccurredAt = DateTime.UtcNow
        });

        if (capReached)
        {
            RaiseEvent(new CustomerReferralCapReached
            {
                CustomerId = State.Id,
                TotalReferrals = totalReferrals,
                TotalPointsEarned = State.Referral.TotalPointsEarnedFromReferrals + pointsToAward,
                OccurredAt = DateTime.UtcNow
            });
        }

        await ConfirmEvents();

        // Add points to loyalty if enrolled
        if (pointsToAward > 0 && State.Loyalty != null)
        {
            await EarnPointsAsync(new EarnPointsCommand(pointsToAward, "Referral bonus"));
        }

        return new ReferralResult(true, pointsToAward, totalReferrals, capReached);
    }

    public Task<ReferralStatus> GetReferralStatusAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Referral);
    }

    public Task<bool> HasReachedReferralCapAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Referral.SuccessfulReferrals >= State.Referral.ReferralRewardsCap);
    }

    public async Task RecordNoShowAsync(DateTime bookingTime, Guid? bookingId = null)
    {
        EnsureExists();
        RaiseEvent(new CustomerNoShowRecorded
        {
            CustomerId = State.Id,
            BookingTime = bookingTime,
            BookingId = bookingId,
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
