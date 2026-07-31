namespace LoadIx.Domain.Trips;

public enum TripStatus
{
    Planned,
    Assigned,
    Dispatched,
    AtCollection,
    Loading,
    CollectionComplete,
    InTransit,
    AtDelivery,
    Unloading,
    DeliveryComplete,
    ReconciliationRequired,
    Completed,
    Cancelled,
    Rejected,
}
