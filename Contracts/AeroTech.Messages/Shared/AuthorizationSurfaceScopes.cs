using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Shared
{
    public static class AuthorizationSurfaceScopes
    {
        public const string Backoffice = "pss.backoffice";
        public const string OtaPanel = "pss.otapanel";
        public const string Ibe = "pss.ibe";
        public const string Api = "pss.api";
        public const string Service = "pss.service";
         public const string Account = "pss.account";


        public static string Canonical(AuthorizationSurface surface) => surface switch
        {
            AuthorizationSurface.Backoffice => Backoffice,
            AuthorizationSurface.OtaPanel => OtaPanel,
            AuthorizationSurface.Ibe => Ibe,
            AuthorizationSurface.Api => Api,
            AuthorizationSurface.Service => Service,
            AuthorizationSurface.Account => Account,
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
        };
    }
}
