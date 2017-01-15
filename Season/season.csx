#load "..\Lib\SeasonDataEntity.csx"
#load "..\Lib\TournamentEntity.csx"

#r "..\Common\PppPool.Common.dll"
#r "..\Common\Microsoft.WindowsAzure.Storage.dll"
#r "System.Xml.Linq"
#r "Newtonsoft.Json"

using System;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using PppPool.Common;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var jwt = await req.GetJwt("admin");
    if (jwt == null)
        return req.CreateError(HttpStatusCode.Unauthorized);

    var connectionString = "TournamentStorage".GetEnvVar();
    var tableService = new TableService(connectionString);
    SeasonDataEntity seasonDataEntity = await tableService.GetEntityAsync<SeasonDataEntity>("season", "data", "season");

    IDictionary<string, string> query = req.GetQueryNameValuePairs().ToDictionary(pair => pair.Key, pair => pair.Value);

    if(query.Keys.Count > 0)
    {
        var season = Convert.ToInt32(query["season"]);
        seasonDataEntity.Season = season;
        await tableService.UpsertEntityAsync("season", seasonDataEntity);
    }

    return req.CreateOk(seasonDataEntity);
}