﻿#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using BTCPayServer.HostedServices;
using BTCPayServer.Lightning;
using Network = NBitcoin.Network;

namespace BTCPayServer.Plugins.Blink;


public class BlinkLightningConnectionStringHandler : ILightningConnectionStringHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NBXplorerDashboard _nbXplorerDashboard;

    public BlinkLightningConnectionStringHandler(IHttpClientFactory httpClientFactory, NBXplorerDashboard nbXplorerDashboard)
    {
        _httpClientFactory = httpClientFactory;
        _nbXplorerDashboard = nbXplorerDashboard;
    }
        
        
    public ILightningClient? Create(string connectionString, Network network, out string? error)
    {
        var kv = LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
        if (type != "blink")
        {
            error = null;
            return null;
        }
        if (!kv.TryGetValue("server", out var server))
        {
            error = $"The key 'server' is mandatory for blink connection strings";
            return null;
        }
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
            || uri.Scheme != "http" && uri.Scheme != "https")
        {
            error = "The key 'server' should be an URI starting by http:// or https://";
            return null;
        }
        bool allowInsecure = false;
        if (kv.TryGetValue("allowinsecure", out var allowinsecureStr))
        {
            var allowedValues = new[] {"true", "false"};
            if (!allowedValues.Any(v => v.Equals(allowinsecureStr, StringComparison.OrdinalIgnoreCase)))
            {
                error = "The key 'allowinsecure' should be true or false";
                return null;
            }

            allowInsecure = allowinsecureStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
            
        if (!LightningConnectionStringHelper.VerifySecureEndpoint(uri, allowInsecure))
        {
            error = "The key 'allowinsecure' is false, but server's Uri is not using https";
            return null;
        }
        if (!kv.TryGetValue("api-key", out var apiKey))
        {
            error = "The key 'api-key' is not found";
            return null;
        }
        if (!kv.TryGetValue("wallet-id", out var walletId))
        {
            error = "The key 'wallet-id' is not found";
            return null;
        }

        error = null;
            
        var client = _httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        client.BaseAddress = uri;
        
        network = Network.Main;
        var bclient =  new BlinkLightningClient( apiKey, uri, walletId, network, _nbXplorerDashboard, client);
        var result = bclient.GetBalance().GetAwaiter().GetResult();
        if (result is null)
        {
            error = "Invalid credentials";
            return null;
        }
       return bclient;
            
    }
}