namespace WebApplicationInAspire.Security;

// Felles rollenavn. Vanlig registrering gir bare ResourceProvider.
public static class RoleNames
{
    public const string ResourceProvider = "ResourceProvider";
    public const string PublicUser = "PublicUser";
    public const string Admin = "Admin";

    public static readonly string[] All = [ResourceProvider, PublicUser, Admin];
}
