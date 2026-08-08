using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class TransportContract : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<ContractRoute> _routes = [];
    private readonly List<ContractRate> _rates = [];
    private readonly List<ContractCommodity> _commodities = [];

    private TransportContract(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        string defaultCurrency,
        int paymentTermsDays,
        decimal weightTolerancePercent,
        string? notes)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        ContractNumber = contractNumber;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Status = ContractStatus.Draft;
        DefaultCurrency = defaultCurrency;
        PaymentTermsDays = paymentTermsDays;
        WeightTolerancePercent = weightTolerancePercent;
        Notes = notes;
    }

    private TransportContract()
    {
        ContractNumber = string.Empty;
        Name = string.Empty;
        DefaultCurrency = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string ContractNumber { get; private set; }

    public string Name { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public ContractStatus Status { get; private set; }

    public string DefaultCurrency { get; private set; }

    public int PaymentTermsDays { get; private set; }

    public decimal WeightTolerancePercent { get; private set; }

    public string? Notes { get; private set; }

    public bool ManualQuantityPermitted { get; private set; }

    public IReadOnlyCollection<ContractRoute> Routes => _routes.AsReadOnly();

    public IReadOnlyCollection<ContractRate> Rates => _rates.AsReadOnly();

    public IReadOnlyCollection<ContractCommodity> Commodities =>
        _commodities.AsReadOnly();

    public static Result<TransportContract> Create(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        string defaultCurrency,
        int paymentTermsDays,
        decimal weightTolerancePercent,
        string? notes,
        IReadOnlyCollection<ContractRouteDefinition> routes,
        IReadOnlyCollection<Guid> commodityIds,
        IReadOnlyCollection<ContractRateDefinition> rates,
        bool manualQuantityPermitted = false)
    {
        var validation = ValidateHeader(
            id, tenantId, customerId, contractNumber, name, startDate, endDate,
            defaultCurrency, paymentTermsDays, weightTolerancePercent, notes);
        if (validation is not null)
        {
            return Result<TransportContract>.Failure(validation);
        }

        var contract = new TransportContract(
            id,
            tenantId,
            customerId,
            contractNumber.Trim().ToUpperInvariant(),
            name.Trim(),
            startDate,
            endDate,
            defaultCurrency.Trim().ToUpperInvariant(),
            paymentTermsDays,
            weightTolerancePercent,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
        contract.ManualQuantityPermitted = manualQuantityPermitted;
        var children = contract.ReplaceChildren(routes, commodityIds, rates);
        return children.IsFailure
            ? Result<TransportContract>.Failure(children.Error!)
            : Result<TransportContract>.Success(contract);
    }

    public Result Update(
        Guid customerId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        string defaultCurrency,
        int paymentTermsDays,
        decimal weightTolerancePercent,
        string? notes,
        IReadOnlyCollection<ContractRouteDefinition> routes,
        IReadOnlyCollection<Guid> commodityIds,
        IReadOnlyCollection<ContractRateDefinition> rates,
        bool manualQuantityPermitted = false)
    {
        if (Status == ContractStatus.Archived)
        {
            return Result.Failure(new DomainError(
                "contract.archived",
                "Archived contracts cannot be changed."));
        }

        var candidate = Create(
            Id, TenantId, customerId, contractNumber, name, startDate, endDate,
            defaultCurrency, paymentTermsDays, weightTolerancePercent, notes,
            routes, commodityIds, rates, manualQuantityPermitted);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        CustomerId = candidate.Value.CustomerId;
        ContractNumber = candidate.Value.ContractNumber;
        Name = candidate.Value.Name;
        StartDate = candidate.Value.StartDate;
        EndDate = candidate.Value.EndDate;
        DefaultCurrency = candidate.Value.DefaultCurrency;
        PaymentTermsDays = candidate.Value.PaymentTermsDays;
        WeightTolerancePercent = candidate.Value.WeightTolerancePercent;
        Notes = candidate.Value.Notes;
        ManualQuantityPermitted = candidate.Value.ManualQuantityPermitted;
        var routeDefinitions = routes.ToDictionary(item => item.Id!.Value);
        _routes.RemoveAll(item => !routeDefinitions.ContainsKey(item.Id));
        foreach (var route in _routes)
        {
            var updated = route.Update(routeDefinitions[route.Id]);
            if (updated.IsFailure) return updated;
        }
        _routes.AddRange(candidate.Value._routes.Where(candidateRoute =>
            _routes.All(existing => existing.Id != candidateRoute.Id)));

        var rateDefinitions = rates.ToDictionary(item => item.Id!.Value);
        _rates.RemoveAll(item => !rateDefinitions.ContainsKey(item.Id));
        foreach (var rate in _rates)
        {
            var updated = rate.Update(rateDefinitions[rate.Id]);
            if (updated.IsFailure) return updated;
        }
        _rates.AddRange(candidate.Value._rates.Where(candidateRate =>
            _rates.All(existing => existing.Id != candidateRate.Id)));

        var commoditySet = commodityIds.ToHashSet();
        _commodities.RemoveAll(item => !commoditySet.Contains(item.CommodityId));
        _commodities.AddRange(candidate.Value._commodities.Where(candidateCommodity =>
            _commodities.All(existing =>
                existing.CommodityId != candidateCommodity.CommodityId)));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status is ContractStatus.Archived or ContractStatus.Expired)
        {
            return Result.Failure(new DomainError(
                "contract.activation_status_invalid",
                "Archived or expired contracts cannot be activated."));
        }

        if (_routes.Count == 0 || _commodities.Count == 0 || _rates.Count == 0)
        {
            return Result.Failure(new DomainError(
                "contract.activation_incomplete",
                "Activation requires a route, a commodity, and a rate."));
        }

        if (_routes.Any(route => !_rates.Any(rate => rate.ContractRouteId == route.Id)))
        {
            return Result.Failure(new DomainError(
                "contract.activation_route_rate_missing",
                "Every contract route must have at least one rate."));
        }

        foreach (var rate in _rates)
        {
            var route = _routes.Single(item => item.Id == rate.ContractRouteId);
            var weightSourceAvailable = rate.RateType switch
            {
                ContractRateType.FixedPerTrip => true,
                ContractRateType.SourceNetTonne => route.SourceWeightCaptureRequired,
                ContractRateType.DestinationNetTonne =>
                    route.DestinationWeightCaptureRequired,
                ContractRateType.ReconciledTonne or ContractRateType.BasePlusPerTonne =>
                    route.SourceWeightCaptureRequired
                    || route.DestinationWeightCaptureRequired,
                ContractRateType.ManualQuantityPerTonne => ManualQuantityPermitted,
                _ => false,
            };
            if (!weightSourceAvailable)
            {
                return Result.Failure(new DomainError(
                    "contract.activation_quantity_source_missing",
                    "A rate depends on a quantity source that its route does not capture."));
            }
        }

        Status = ContractStatus.Active;
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status != ContractStatus.Active)
        {
            return Result.Failure(new DomainError(
                "contract.suspension_status_invalid",
                "Only active contracts can be suspended."));
        }

        Status = ContractStatus.Suspended;
        return Result.Success();
    }

    public Result Archive()
    {
        if (Status == ContractStatus.Active)
        {
            return Result.Failure(new DomainError(
                "contract.archive_active",
                "Suspend an active contract before archiving it."));
        }

        Status = ContractStatus.Archived;
        return Result.Success();
    }

    public void MarkExpired() => Status = ContractStatus.Expired;

    private Result ReplaceChildren(
        IReadOnlyCollection<ContractRouteDefinition> routes,
        IReadOnlyCollection<Guid> commodityIds,
        IReadOnlyCollection<ContractRateDefinition> rates)
    {
        if (routes.Select(route => route.Reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != routes.Count)
        {
            return Result.Failure(new DomainError(
                "contract.route_reference_duplicate",
                "Route references must be unique within a contract."));
        }

        if (commodityIds.Distinct().Count() != commodityIds.Count)
        {
            return Result.Failure(new DomainError(
                "contract.commodity_duplicate",
                "A commodity can be permitted only once."));
        }

        foreach (var definition in routes)
        {
            var route = ContractRoute.Create(
                definition.Id ?? Guid.NewGuid(), TenantId,
                definition.SourceLocationId, definition.DestinationLocationId,
                definition.Reference,
                definition.SourceWeightCaptureRequired,
                definition.DestinationWeightCaptureRequired,
                definition.CollectionInstructions,
                definition.DeliveryInstructions);
            if (route.IsFailure) return Result.Failure(route.Error!);
            _routes.Add(route.Value);
        }

        var routeIds = _routes.Select(route => route.Id).ToHashSet();
        foreach (var definition in rates)
        {
            if (!routeIds.Contains(definition.ContractRouteId))
            {
                return Result.Failure(new DomainError(
                    "contract.rate_route_invalid",
                    "Every rate must reference a route in the contract."));
            }

            if (definition.EffectiveFrom < StartDate
                || EndDate is not null
                && (definition.EffectiveTo is null || definition.EffectiveTo > EndDate))
            {
                return Result.Failure(new DomainError(
                    "contract.rate_outside_contract_dates",
                    "Rate effective dates must fall within the contract period."));
            }

            var rate = ContractRate.Create(
                definition.Id ?? Guid.NewGuid(), TenantId, definition);
            if (rate.IsFailure) return Result.Failure(rate.Error!);
            _rates.Add(rate.Value);
        }

        if (HasOverlappingRates(_rates))
        {
            return Result.Failure(new DomainError(
                "contract.rate_dates_overlap",
                "Rates for the same route cannot have overlapping effective dates."));
        }

        foreach (var commodityId in commodityIds)
        {
            var commodity = ContractCommodity.Create(Guid.NewGuid(), TenantId, commodityId);
            if (commodity.IsFailure) return Result.Failure(commodity.Error!);
            _commodities.Add(commodity.Value);
        }

        return Result.Success();
    }

    private static bool HasOverlappingRates(IEnumerable<ContractRate> rates) =>
        rates.GroupBy(rate => rate.ContractRouteId).Any(group =>
        {
            var ordered = group.OrderBy(rate => rate.EffectiveFrom).ToArray();
            return ordered.Zip(ordered.Skip(1)).Any(pair =>
                pair.First.EffectiveTo is null
                || pair.Second.EffectiveFrom <= pair.First.EffectiveTo);
        });

    private static DomainError? ValidateHeader(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        string defaultCurrency,
        int paymentTermsDays,
        decimal weightTolerancePercent,
        string? notes)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || customerId == Guid.Empty)
        {
            return new DomainError(
                "contract.identifiers_required",
                "Contract, tenant, and customer identifiers are required.");
        }

        var error = DomainValidation.Required(contractNumber, "contract.number", 60)
            ?? DomainValidation.Required(name, "contract.name", 200)
            ?? DomainValidation.Required(defaultCurrency, "contract.currency", 3);
        if (error is not null) return error;
        if (notes?.Trim().Length > 2000)
        {
            return new DomainError(
                "contract.notes.too_long",
                "Contract notes cannot exceed 2000 characters.");
        }
        if (defaultCurrency.Trim().Length != 3
            || !defaultCurrency.Trim().All(char.IsAsciiLetter))
        {
            return new DomainError(
                "contract.currency_invalid",
                "Default currency must be a three-letter ISO-style code.");
        }

        if (endDate < startDate)
        {
            return new DomainError(
                "contract.dates_invalid",
                "Contract end date cannot precede its start date.");
        }

        if (paymentTermsDays is < 0 or > 365)
        {
            return new DomainError(
                "contract.payment_terms_invalid",
                "Payment terms must be between 0 and 365 days.");
        }

        return weightTolerancePercent is < 0 or > 100
            ? new DomainError(
                "contract.tolerance_invalid",
                "Weight tolerance percentage must be between 0 and 100.")
            : null;
    }
}
