#load "..\Lib\SeasonDataEntity.csx"

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

public class TournamentEntity : TableEntity
{
    public TournamentEntity()
    {

    }

    public TournamentEntity(int season, string tour, string tournamentNumber)
    {
        PartitionKey = $"{season}:{tour}";
        RowKey = tournamentNumber;
        Season = season;
        Tour = tour;
    }

    public int Season { get; set; }
    public string Tour { get; set; }
    public string PermanentNumber { get; set; }
    public string Name { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Champion { get; set; }
    public string ChampionData { get; set; }
    public int WeekNumber { get; set; }
    public bool Open { get; set; }

    public string Course { get; set; }
    public string CourseData { get; set; }
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