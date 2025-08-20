using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MyPlanner.API;

public class FixedOpenIdConnectConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string authority, IDocumentRetriever retriever, CancellationToken cancel)
    {
        string address = $"{authority}/.well-known/openid-configuration";
        var config = await OpenIdConnectConfigurationRetriever.GetAsync(address, retriever, cancel);

        if (config.JwksUri == null)// If JwksUri is not set, fetch it
        {
            config.JwksUri = $"{authority}/.well-known/jwks.json";
            using var httpClient = new HttpClient();
            string jwksJson = await httpClient.GetStringAsync(config.JwksUri);
            var jwksKeys = new JsonWebKeySet(jwksJson).GetSigningKeys();
            foreach (var key in jwksKeys)
            {
                config.SigningKeys.Add(key);
            }
        }
        return config;
    }
}