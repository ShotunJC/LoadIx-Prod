namespace LoadIx.Application.MasterData;

public interface IMasterDataService
{
    Task<MasterDataResult<PagedResponse<BranchItem>>> ListBranchesAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<BranchItem>> GetBranchAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<BranchItem>> CreateBranchAsync(
        BranchWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<BranchItem>> UpdateBranchAsync(
        Guid id, BranchWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<BranchItem>> SetBranchActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<CustomerItem>>> ListCustomersAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<CustomerItem>> GetCustomerAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<CustomerItem>> CreateCustomerAsync(
        CustomerWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<CustomerItem>> UpdateCustomerAsync(
        Guid id, CustomerWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<CustomerItem>> SetCustomerActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<LocationItem>>> ListLocationsAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<LocationItem>> GetLocationAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<LocationItem>> CreateLocationAsync(
        LocationWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<LocationItem>> UpdateLocationAsync(
        Guid id, LocationWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<LocationItem>> SetLocationActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<CommodityItem>>> ListCommoditiesAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<CommodityItem>> GetCommodityAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<CommodityItem>> CreateCommodityAsync(
        CommodityWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<CommodityItem>> UpdateCommodityAsync(
        Guid id, CommodityWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<CommodityItem>> SetCommodityActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<DriverItem>>> ListDriversAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<DriverItem>> GetDriverAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<DriverItem>> CreateDriverAsync(
        DriverWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<DriverItem>> UpdateDriverAsync(
        Guid id, DriverWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<DriverItem>> SetDriverActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<VehicleItem>>> ListVehiclesAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<VehicleItem>> GetVehicleAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<VehicleItem>> CreateVehicleAsync(
        VehicleWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<VehicleItem>> UpdateVehicleAsync(
        Guid id, VehicleWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<VehicleItem>> SetVehicleActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);

    Task<MasterDataResult<PagedResponse<TrailerItem>>> ListTrailersAsync(
        MasterDataQuery query, CancellationToken cancellationToken);
    Task<MasterDataResult<TrailerItem>> GetTrailerAsync(
        Guid id, CancellationToken cancellationToken);
    Task<MasterDataResult<TrailerItem>> CreateTrailerAsync(
        TrailerWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<TrailerItem>> UpdateTrailerAsync(
        Guid id, TrailerWriteModel model, CancellationToken cancellationToken);
    Task<MasterDataResult<TrailerItem>> SetTrailerActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken);
}
