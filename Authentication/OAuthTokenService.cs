using BCETL.Configuration;
using Microsoft.Identity.Client;

namespace BCETL.Authentication;

public sealed class OAuthTokenService
{
    private readonly IConfidentialClientApplication _app;
    private static readonly string[] Scopes =
        ["https://api.businesscentral.dynamics.com/.default"];

    public OAuthTokenService(BusinessCentralOptions options)
    {
        _app = ConfidentialClientApplicationBuilder.Create(options.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}")
            .WithClientSecret(options.ClientSecret)
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        AuthenticationResult result = await _app.AcquireTokenForClient(Scopes)
            .ExecuteAsync(cancellationToken);
        return result.AccessToken;
    }
}
