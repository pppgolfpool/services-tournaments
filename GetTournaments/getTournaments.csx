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

// future = tournament starts in a week after the current week.
// picking = tournament's week is the current week, but start date hasn't arrived.
// progressing = tournament's week is current week, and start date is passed.
// completed = tournament is done and the tournament of the current week is open
// dequeued = tournament is done and the tournament of the current week is progressing

// season, tour, key(index, id, state(see above), all, week), value=query parameter
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var jwt = await req.GetJwt("submitter");
    if (jwt == null)
        return req.CreateError(HttpStatusCode.Unauthorized);

    var connectionString = "TournamentStorage".GetEnvVar();
    var tableService = new TableService(connectionString);

    IDictionary<string, string> query = req.GetQueryNameValuePairs().ToDictionary(pair => pair.Key, pair => pair.Value);

    SeasonDataEntity seasonDataEntity = null;

    if(query["season"].ToLower() == "current")
    {
        seasonDataEntity = await tableService.GetEntityAsync<SeasonDataEntity>("season", "data", "season");
        query["season"] = $"{seasonDataEntity.Season}";
    }

    var season = Convert.ToInt32(query["season"]);
    var tour = query["tour"];
    var key = query["key"].ToLower();
    var value = string.Empty;
    if(key != "all")
        value = query["value"].ToLower();

    List<TournamentEntity> partition = await tableService.GetPartitionAsync<TournamentEntity>("tournaments", $"{season}:{tour}");

    if(key == "index")
        return req.CreateOk(ConvertTournament(partition.Where(x => x.RowKey == value)).FirstOrDefault());

    if (key == "id")
        return req.CreateOk(ConvertTournament(partition.Where(x => x.PermanentNumber == value)).FirstOrDefault());

    if(key == "state")
    {
        var states = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        List<TournamentEntity> list = new List<TournamentEntity>();
        foreach (var state in states)
        {
            list.AddRange(partition.Where(x => x.State == state));
        }
        return req.CreateOk(ConvertTournament(list));
    }

    if(key == "week")
    {
        if (value == "current")
        {
            if(seasonDataEntity == null)
                seasonDataEntity = await tableService.GetEntityAsync<SeasonDataEntity>("season", "data", "season");
            int currentWeek = seasonDataEntity.CurrentWeek;
            return req.CreateOk(ConvertTournament(partition.Where(x => x.WeekNumber == currentWeek)).FirstOrDefault());
        }
        else
        {
            return req.CreateOk(ConvertTournament(partition.Where(x => x.WeekNumber == Convert.ToInt32(value))).FirstOrDefault());
        }
    }

    if(key == "all")
    {
        return req.CreateOk(ConvertTournament(partition));
    }

    return req.CreateError(HttpStatusCode.BadRequest);
}

public static List<ResponseTournament> ConvertTournament(IEnumerable<TournamentEntity> tournaments)
{
    return tournaments.Select(x => new ResponseTournament
    {
        Index = x.RowKey,
        Season = x.Season,
        Tour = x.Tour,
        PermanentNumber = x.PermanentNumber,
        Name = x.Name,
        Start = x.Start,
        End = x.End,
        Champion = x.Champion,
        ChampionData = JObject.Parse(x.ChampionData),
        WeekNumber = x.WeekNumber,
        Open = x.Open,
        Course = x.Course,
        CourseData = JObject.Parse(x.CourseData),
        FedExPurse = x.FedExPurse,
        FedExWinnerShare = x.FedExWinnerShare,
        MoneyPurse = x.MoneyPurse,
        MoneyWinnerShare = x.MoneyWinnerShare,
        IsMajor = x.IsMajor,
        IsPlayoff = x.IsPlayoff,
        TournamentType = x.TournamentType,
        State = x.State,
        Used = x.Used,
    }).ToList();
}

public class ResponseTournament
{
    public ResponseTournament()
    {

    }

    public string Index { get; set; }
    public int Season { get; set; }
    public string Tour { get; set; }
    public string PermanentNumber { get; set; }
    public string Name { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Champion { get; set; }
    public JObject ChampionData { get; set; }
    public int WeekNumber { get; set; }
    public bool Open { get; set; }

    public string Course { get; set; }
    public JObject CourseData { get; set; }
    public int FedExPurse { get; set; }
    public int FedExWinnerShare { get; set; }
    public int MoneyPurse { get; set; }
    public int MoneyWinnerShare { get; set; }

    public bool IsMajor { get; set; }
    public bool IsPlayoff { get; set; }

    public string TournamentType { get; set; }

    public string State { get; set; }

    public bool Used { get; set; }
}