namespace LoadIx.Application.Trips;

public interface ITripService
{
    Task<TripResult<TripPagedResponse<TripListItem>>> ListAsync(
        TripQuery query,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> CreateAsync(
        CreateTripModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> AssignAsync(
        Guid id,
        AssignTripModel model,
        bool reassign,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> ExecuteAsync(
        Guid id,
        string command,
        TripCommandModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> OverrideAsync(
        Guid id,
        TripOverrideModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> RecordWeighbridgeAsync(
        Guid id,
        bool source,
        TripWeighbridgeModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> AddNoteAsync(
        Guid id,
        TripNoteModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TripResult<TripDetail>> RecordLocationAsync(
        Guid id,
        TripLocationUpdateModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
