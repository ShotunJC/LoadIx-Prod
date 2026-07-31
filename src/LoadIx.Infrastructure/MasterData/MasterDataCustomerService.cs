using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Locations;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.MasterData;

public sealed partial class MasterDataService
{
    public async Task<MasterDataResult<PagedResponse<CustomerItem>>> ListCustomersAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<CustomerItem>>(LoadIxPermissions.CustomerView);
        var invalid = ValidateQuery<CustomerItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        IQueryable<Customer> source = dbContext.Customers.AsNoTracking()
            .Include(item => item.Contacts);
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.LegalName.Contains(search)
                || item.TradingName.Contains(search)
                || item.RegistrationNumber.Contains(search));
        }

        var descending = query.Direction == "desc";
        source = (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("reference", true) => source.OrderByDescending(item => item.RegistrationNumber)
                .ThenByDescending(item => item.Id),
            ("reference", false) => source.OrderBy(item => item.RegistrationNumber)
                .ThenBy(item => item.Id),
            ("tradingname", true) => source.OrderByDescending(item => item.TradingName)
                .ThenByDescending(item => item.Id),
            ("tradingname", false) => source.OrderBy(item => item.TradingName)
                .ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.LegalName)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.LegalName).ThenBy(item => item.Id),
        };
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return MasterDataResult<PagedResponse<CustomerItem>>.Success(
            Page(entities.Select(item => MapCustomer(item)).ToArray(), query, total));
    }

    public async Task<MasterDataResult<CustomerItem>> GetCustomerAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<CustomerItem>(LoadIxPermissions.CustomerView);
        if (denied is not null) return denied;
        var entity = await dbContext.Customers.AsNoTracking()
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null
            ? NotFound<CustomerItem>("Customer")
            : MasterDataResult<CustomerItem>.Success(MapCustomer(entity));
    }

    public async Task<MasterDataResult<CustomerItem>> CreateCustomerAsync(
        CustomerWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<CustomerItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var address = CreateAddress(model.BillingAddress);
        if (!address.IsSuccess)
            return MasterDataResult<CustomerItem>.Fail(
                address.Failure, address.Code, address.Message);
        var result = Customer.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.LegalName,
            model.TradingName,
            model.RegistrationNumber,
            model.TaxNumber,
            model.PaymentTermsDays,
            address.Value!,
            model.Notes);
        if (result.IsFailure) return Validation<CustomerItem>(result.Error!);

        var contacts = BuildContacts(result.Value.Id, model.Contacts);
        if (!contacts.IsSuccess)
            return MasterDataResult<CustomerItem>.Fail(
                contacts.Failure, contacts.Code, contacts.Message);
        dbContext.Customers.Add(result.Value);
        dbContext.ContactPeople.AddRange(contacts.Value!);
        return await SaveAsync(
            () => MapCustomer(result.Value, contacts.Value!),
            "Created",
            "Customer",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<CustomerItem>> UpdateCustomerAsync(
        Guid id, CustomerWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<CustomerItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Customers.Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<CustomerItem>("Customer");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<CustomerItem>();
        var address = CreateAddress(model.BillingAddress);
        if (!address.IsSuccess)
            return MasterDataResult<CustomerItem>.Fail(
                address.Failure, address.Code, address.Message);
        var result = entity.Update(
            model.LegalName,
            model.TradingName,
            model.RegistrationNumber,
            model.TaxNumber,
            model.PaymentTermsDays,
            address.Value!,
            model.Notes);
        if (result.IsFailure) return Validation<CustomerItem>(result.Error!);

        foreach (var existing in entity.Contacts)
        {
            var supplied = model.Contacts.SingleOrDefault(item => item.Id == existing.Id);
            if (supplied is null)
            {
                existing.Deactivate();
                continue;
            }

            if (!Enum.TryParse<ContactCategory>(supplied.Category, true, out var category))
                return InvalidContactCategory<CustomerItem>();
            var contactResult = existing.Update(
                supplied.Name,
                supplied.Email,
                supplied.PhoneNumber,
                category);
            if (contactResult.IsFailure)
                return Validation<CustomerItem>(contactResult.Error!);
            if (supplied.IsActive) existing.Activate(); else existing.Deactivate();
        }

        foreach (var supplied in model.Contacts.Where(item => !item.Id.HasValue))
        {
            var contact = BuildContact(id, supplied);
            if (!contact.IsSuccess)
                return MasterDataResult<CustomerItem>.Fail(
                    contact.Failure, contact.Code, contact.Message);
            dbContext.ContactPeople.Add(contact.Value!);
        }

        return await SaveAsync(
            () => MapCustomer(entity),
            "Updated",
            "Customer",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<CustomerItem>> SetCustomerActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<CustomerItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Customers.Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<CustomerItem>("Customer");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<CustomerItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        return await SaveAsync(
            () => MapCustomer(entity),
            active ? "Activated" : "Deactivated",
            "Customer",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<PagedResponse<LocationItem>>> ListLocationsAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<LocationItem>>(LoadIxPermissions.CustomerView);
        var invalid = ValidateQuery<LocationItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        var source = dbContext.Locations.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (query.CustomerId.HasValue)
            source = source.Where(item => item.CustomerId == query.CustomerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.Name.Contains(search) || item.Code.Contains(search));
        }

        var descending = query.Direction == "desc";
        source = (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("reference", true) => source.OrderByDescending(item => item.Code)
                .ThenByDescending(item => item.Id),
            ("reference", false) => source.OrderBy(item => item.Code)
                .ThenBy(item => item.Id),
            ("type", true) => source.OrderByDescending(item => item.Type)
                .ThenByDescending(item => item.Id),
            ("type", false) => source.OrderBy(item => item.Type).ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.Name)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.Name).ThenBy(item => item.Id),
        };
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var customerIds = entities.Where(item => item.CustomerId.HasValue)
            .Select(item => item.CustomerId!.Value)
            .Distinct()
            .ToArray();
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(item => customerIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.TradingName, cancellationToken);
        return MasterDataResult<PagedResponse<LocationItem>>.Success(
            Page(
                entities.Select(item => MapLocation(
                    item,
                    item.CustomerId.HasValue
                        && customers.TryGetValue(item.CustomerId.Value, out var name)
                            ? name
                            : null)).ToArray(),
                query,
                total));
    }

    public async Task<MasterDataResult<LocationItem>> GetLocationAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<LocationItem>(LoadIxPermissions.CustomerView);
        if (denied is not null) return denied;
        var entity = await dbContext.Locations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<LocationItem>("Location");
        var customerName = entity.CustomerId.HasValue
            ? await dbContext.Customers.AsNoTracking()
                .Where(item => item.Id == entity.CustomerId)
                .Select(item => item.TradingName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        return MasterDataResult<LocationItem>.Success(MapLocation(entity, customerName));
    }

    public async Task<MasterDataResult<LocationItem>> CreateLocationAsync(
        LocationWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<LocationItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var validation = await ValidateLocationWriteAsync(model, cancellationToken);
        if (!validation.IsSuccess) return validation.Result!;
        var result = Location.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.CustomerId,
            model.Name,
            model.Code,
            validation.Type,
            validation.Address!,
            model.Latitude,
            model.Longitude,
            model.GeofenceRadiusMetres,
            model.SiteInstructions,
            model.ContactName,
            model.ContactPhoneNumber,
            model.OperatingHours);
        if (result.IsFailure) return Validation<LocationItem>(result.Error!);
        dbContext.Locations.Add(result.Value);
        return await SaveAsync(
            () => MapLocation(result.Value, validation.CustomerName),
            "Created",
            "Location",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<LocationItem>> UpdateLocationAsync(
        Guid id, LocationWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<LocationItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<LocationItem>("Location");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<LocationItem>();
        var validation = await ValidateLocationWriteAsync(model, cancellationToken);
        if (!validation.IsSuccess) return validation.Result!;
        var result = entity.Update(
            model.CustomerId,
            model.Name,
            model.Code,
            validation.Type,
            validation.Address!,
            model.Latitude,
            model.Longitude,
            model.GeofenceRadiusMetres,
            model.SiteInstructions,
            model.ContactName,
            model.ContactPhoneNumber,
            model.OperatingHours);
        if (result.IsFailure) return Validation<LocationItem>(result.Error!);
        return await SaveAsync(
            () => MapLocation(entity, validation.CustomerName),
            "Updated",
            "Location",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<LocationItem>> SetLocationActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<LocationItem>(LoadIxPermissions.CustomerManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<LocationItem>("Location");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<LocationItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        return await SaveAsync(
            () => MapLocation(entity, null),
            active ? "Activated" : "Deactivated",
            "Location",
            id,
            cancellationToken);
    }

    private MasterDataResult<IReadOnlyList<ContactPerson>> BuildContacts(
        Guid customerId,
        IReadOnlyList<ContactModel> models)
    {
        var contacts = new List<ContactPerson>();
        foreach (var model in models)
        {
            var result = BuildContact(customerId, model);
            if (!result.IsSuccess)
                return MasterDataResult<IReadOnlyList<ContactPerson>>.Fail(
                    result.Failure, result.Code, result.Message);
            contacts.Add(result.Value!);
        }

        return MasterDataResult<IReadOnlyList<ContactPerson>>.Success(contacts);
    }

    private MasterDataResult<ContactPerson> BuildContact(
        Guid customerId, ContactModel model)
    {
        if (!Enum.TryParse<ContactCategory>(model.Category, true, out var category))
            return InvalidContactCategory<ContactPerson>();
        var result = ContactPerson.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            customerId,
            model.Name,
            model.Email,
            model.PhoneNumber,
            category);
        return result.IsSuccess
            ? MasterDataResult<ContactPerson>.Success(result.Value)
            : Validation<ContactPerson>(result.Error!);
    }

    private static MasterDataResult<T> InvalidContactCategory<T>() =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.Validation,
            "contact.invalid_category",
            "Contact category must be Operational or Finance.");

    private async Task<(
        bool IsSuccess,
        LocationType Type,
        Address? Address,
        string? CustomerName,
        MasterDataResult<LocationItem>? Result)> ValidateLocationWriteAsync(
            LocationWriteModel model,
            CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LocationType>(model.Type, true, out var type))
            return (
                false,
                default,
                null,
                null,
                MasterDataResult<LocationItem>.Fail(
                    MasterDataFailure.Validation,
                    "location.invalid_type",
                    "The location type is not supported."));
        var address = CreateAddress(model.Address);
        if (!address.IsSuccess)
            return (
                false,
                type,
                null,
                null,
                MasterDataResult<LocationItem>.Fail(
                    address.Failure, address.Code, address.Message));
        string? customerName = null;
        if (model.CustomerId.HasValue)
        {
            customerName = await dbContext.Customers
                .Where(item => item.Id == model.CustomerId && item.IsActive)
                .Select(item => item.TradingName)
                .SingleOrDefaultAsync(cancellationToken);
            if (customerName is null)
                return (
                    false,
                    type,
                    address.Value,
                    null,
                    MasterDataResult<LocationItem>.Fail(
                        MasterDataFailure.Validation,
                        "location.customer_invalid",
                        "The selected active customer was not found."));
        }

        return (true, type, address.Value, customerName, null);
    }

    private static CustomerItem MapCustomer(
        Customer entity,
        IEnumerable<ContactPerson>? contacts = null) =>
        new(
            entity.Id,
            entity.RegistrationNumber,
            entity.LegalName,
            entity.TradingName,
            entity.RegistrationNumber,
            entity.TaxNumber,
            entity.PaymentTermsDays,
            MapAddress(entity.BillingAddress),
            (contacts ?? entity.Contacts).Select(MapContact).ToArray(),
            entity.Notes,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);

    private static ContactModel MapContact(ContactPerson entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Email,
            entity.PhoneNumber,
            entity.Category.ToString(),
            entity.IsActive);

    private static LocationItem MapLocation(Location entity, string? customerName) =>
        new(
            entity.Id,
            entity.Code,
            entity.CustomerId,
            customerName,
            entity.Name,
            entity.Type.ToString(),
            MapAddress(entity.PhysicalAddress),
            entity.Latitude,
            entity.Longitude,
            entity.GeofenceRadiusMetres,
            entity.OperatingInstructions,
            entity.ContactName,
            entity.ContactPhoneNumber,
            entity.OperatingHours,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);
}
