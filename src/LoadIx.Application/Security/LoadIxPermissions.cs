namespace LoadIx.Application.Security;

public static class LoadIxPermissions
{
    public const string TenantView = "Tenant.View";
    public const string TenantManage = "Tenant.Manage";
    public const string UserView = "User.View";
    public const string UserManage = "User.Manage";
    public const string CustomerView = "Customer.View";
    public const string CustomerManage = "Customer.Manage";
    public const string FleetView = "Fleet.View";
    public const string FleetManage = "Fleet.Manage";
    public const string ContractView = "Contract.View";
    public const string ContractManage = "Contract.Manage";
    public const string TripView = "Trip.View";
    public const string TripManage = "Trip.Manage";
    public const string TripDispatch = "Trip.Dispatch";
    public const string TripComplete = "Trip.Complete";
    public const string TripOverride = "Trip.Override";
    public const string TripDriverExecute = "Trip.Driver.Execute";
    public const string TripLocationUpdate = "Trip.Location.Update";
    public const string DocumentView = "Document.View";
    public const string DocumentManage = "Document.Manage";
    public const string DocumentDriver = "Document.Driver";
    public const string FinanceView = "Finance.View";
    public const string FinanceManage = "Finance.Manage";
    public const string ReportView = "Report.View";
    public const string AuditView = "Audit.View";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            TenantView,
            TenantManage,
            UserView,
            UserManage,
            CustomerView,
            CustomerManage,
            FleetView,
            FleetManage,
            ContractView,
            ContractManage,
            TripView,
            TripManage,
        TripDispatch,
        TripComplete,
        TripOverride,
        TripDriverExecute,
        TripLocationUpdate,
            DocumentView,
            DocumentManage,
            DocumentDriver,
            FinanceView,
            FinanceManage,
            ReportView,
            AuditView,
        ],
        StringComparer.Ordinal);
}
