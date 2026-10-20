namespace EduCore.Api.Security;

/// <summary>Names for platform-only capabilities; none grant access to school academic records.</summary>
public static class PlatformPermissions
{
    public const string DashboardView = "platform.dashboard.view";
    public const string SchoolsView = "platform.schools.view";
    public const string SchoolsManage = "platform.schools.manage";
    public const string UsersView = "platform.users.view";
    public const string UsersManage = "platform.users.manage";
    public const string RolesView = "platform.roles.view";
    public const string RolesManage = "platform.roles.manage";
    public const string AuditView = "platform.audit.view";
    public const string IdentityManage = "platform.identity.manage";

    /// <summary>Permissions ordinary administrators may explicitly delegate.</summary>
    public static IReadOnlySet<string> Delegable { get; } = new HashSet<string>(
        StringComparer.Ordinal)
    {
        DashboardView,
        SchoolsView,
        SchoolsManage,
        UsersView,
        UsersManage,
        RolesView,
        AuditView
    };

    /// <summary>Permissions installed only by the explicit trusted bootstrap operation.</summary>
    public static IReadOnlyList<string> Bootstrap { get; } =
    [
        DashboardView,
        SchoolsView,
        SchoolsManage,
        UsersView,
        UsersManage,
        RolesView,
        RolesManage,
        AuditView,
        IdentityManage
    ];
}
