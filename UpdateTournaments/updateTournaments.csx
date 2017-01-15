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

public static async Task Run(TimerInfo timer, TraceWriter log)
{
    var startTime = DateTime.UtcNow;

    var connectionString = "TournamentStorage".GetEnvVar();
    var tableService = new TableService(connectionString);

    SeasonDataEntity seasonDataEntity = await tableService.GetEntityAsync<SeasonDataEntity>("season", "data", "season");
    int currentSeason = seasonDataEntity.Season;

    // General data from tournaments is retrieved from the "Schedule.xml" file. It doesn't update very often, so 6 hours is enough tolerance.
    XDocument xSchedule = await RefreshFileService.RefreshXmlFile(connectionString, "data", "r/current/schedule.xml", TimeSpan.FromHours(6));

    // The current week is also determined by the schedule.xml file. It updates right after the weekend tournament is complete.
    int currentWeek = GetCurrentWeek(xSchedule);
    if(seasonDataEntity.CurrentWeek != currentWeek)
    {
        seasonDataEntity.CurrentWeek = currentWeek;
        await tableService.UpsertEntityAsync("season", seasonDataEntity);
    }

    // right now we only use the PGA TOUR tour, but if other tours are ever used, this is where they will be entered.
    string[] tourNames = seasonDataEntity.Tours.Split(new[] { ',' });

    // go through each applicable tournament (current year, useable tour) and add it's info to the table
    foreach (var year in xSchedule.Element("schedule").Element("years").Elements("year"))
    {
        var yearNumber = Convert.ToInt32(year.Attribute("year").Value);
        if (yearNumber != currentSeason)
            continue;

        foreach (var tour in year.Element("tours").Elements("tour"))
        {
            var tourName = tour.Attribute("desc").Value;
            if (!tourNames.Contains(tour.Attribute("desc").Value))
                continue;

            foreach (var trn in tour.Element("trns").Elements("trn"))
            {
                if (trn.Attribute("official").Value.ToLower().Contains("no"))
                    continue;
                if (trn.Attribute("FedExCup") == null || trn.Attribute("FedExCup").Value.ToLower().Contains("no"))
                    continue;
                if (trn.Attribute("trnType").Value == "ALT")
                    continue;

                TournamentEntity entity = await tableService.GetEntityAsync<TournamentEntity>("tournaments", $"{currentSeason}:{tourName}", $"{trn.Attribute("trnNum").Value}");
                var newEntity = false;
                if(entity == null)
                {
                    entity = new TournamentEntity(currentSeason, tourName, trn.Attribute("trnNum").Value);
                    newEntity = true;
                }

                var testDateElement = trn.Element("date");
                var start = new DateTime(
                    Convert.ToInt32(testDateElement.Element("start").Element("yr").Value),
                    Convert.ToInt32(testDateElement.Element("start").Element("month").Value),
                    Convert.ToInt32(testDateElement.Element("start").Element("day").Value), 13, 0, 0, DateTimeKind.Utc);
                var end = new DateTime(
                    Convert.ToInt32(testDateElement.Element("end").Element("yr").Value),
                    Convert.ToInt32(testDateElement.Element("end").Element("month").Value),
                    Convert.ToInt32(testDateElement.Element("end").Element("day").Value), 23, 59, 0, DateTimeKind.Utc);
                entity.Name = trn.Element("trnName").Element("official").Value;
                entity.WeekNumber = Convert.ToInt32(trn.Element("date").Attribute("weeknumber").Value);
                entity.Start = start;
                entity.End = end;
                entity.PermanentNumber = trn.Attribute("permNum").Value;
                entity.Open = start > DateTime.UtcNow && start - DateTime.UtcNow < TimeSpan.FromDays(4);
                entity.Course = trn.Element("courses").Elements("course").First().Attribute("number").Value;
                entity.IsMajor = trn.Attribute("trnType").Value == "MJR" && !entity.Name.ToLower().Contains("players");
                entity.IsPlayoff = trn.Attribute("trnType").Value == "PLF" || trn.Attribute("trnType").Value == "PLS";
                entity.TournamentType = trn.Attribute("trnType").Value;
                entity.Used = true;

                var courseElement = trn.Element("courses").Elements("course").First();
                var courseData = JObject.FromObject(new
                {
                    Host = courseElement.Attribute("host").Value.ToLower() == "yes" ? true : false,
                    IsTpc = courseElement.Attribute("isTpc").Value.ToLower() == "no" ? false : true,
                    Number = courseElement.Attribute("number").Value,
                    Rank = courseElement.Attribute("rank").Value,
                    Name = courseElement.Element("courseName").Value,
                    Designer = courseElement.Element("designer").Value,
                    Established = courseElement.Element("established").Value,
                    Country = courseElement.Element("location").Element("country").Value,
                    State = courseElement.Element("location").Element("state").Value,
                    City = courseElement.Element("location").Element("city").Value,
                });
                entity.CourseData = courseData.ToString(Formatting.Indented);

                var championElement = trn.Element("champion");
                entity.Champion = championElement.Attribute("plrNum").Value;
                var championData = JObject.FromObject(new
                {
                    IsMember = championElement.Attribute("isMember").Value.ToLower() == "yes" ? true : false,
                    Id = championElement.Attribute("plrNum").Value,
                    FirstName = championElement.Element("playerName").Element("first").Value,
                    MiddleName = championElement.Element("playerName").Element("middle").Value,
                    LastName = championElement.Element("playerName").Element("last").Value,
                });
                entity.ChampionData = championData.ToString(Formatting.Indented);

                var fedExPurse = trn.Element("FedExCupPurse").Value.Replace(",", "");
                entity.FedExPurse = !string.IsNullOrEmpty(fedExPurse) ? Convert.ToInt32(fedExPurse) : 0;

                var fedExWinnerShare = trn.Element("FedExCupWinnerPoints").Value.Replace(",", "");
                entity.FedExWinnerShare = !string.IsNullOrEmpty(fedExWinnerShare) ? Convert.ToInt32(fedExWinnerShare) : 0;

                var moneyPurse = trn.Element("Purse").Value.Replace(",", "");
                entity.MoneyPurse = !string.IsNullOrEmpty(moneyPurse) ? Convert.ToInt32(moneyPurse) : 0;

                var moneyWinnerShare = trn.Element("winnersShare").Value.Replace(",", "");
                entity.MoneyWinnerShare = !string.IsNullOrEmpty(moneyWinnerShare) ? Convert.ToInt32(moneyWinnerShare) : 0;

                DateTime startRange = start.AddDays(-14);
                DateTime endRange = start.AddDays(14);

                if (newEntity || (DateTime.UtcNow > startRange && DateTime.UtcNow < endRange))
                    await tableService.UpsertEntityAsync("tournaments", entity);
            }
        }
    }

    // future = tournament starts in a week after the current week.
    // picking = tournament's week is the current week, but start date hasn't arrived.
    // progressing = tournament's week is current week, and start date is passed.
    // completed = tournament is done and the tournament of the current week is open
    // dequeued = tournament is done and the tournament of the current week is progressing
    List<TournamentEntity> allTournaments = await tableService.GetPartitionAsync<TournamentEntity>("tournaments", $"{currentSeason}:PGA TOUR");
    var maxWeek = allTournaments.Max(x => x.WeekNumber);
    foreach (TournamentEntity tEntity in allTournaments)
    {
        TournamentEntity nextWeekEntity = allTournaments.SingleOrDefault(x => x.WeekNumber == tEntity.WeekNumber + 1 && x.TournamentType != "ALT");
        if (tEntity.WeekNumber == maxWeek)
            nextWeekEntity = allTournaments.SingleOrDefault(x => x.WeekNumber == 1 && x.TournamentType != "ALT");
        string state = "";
        if (tEntity.WeekNumber > currentWeek)
            state = "future";
        if (tEntity.WeekNumber == currentWeek && DateTime.UtcNow <= tEntity.Start)
            state = "picking";
        if (tEntity.WeekNumber == currentWeek && DateTime.UtcNow > tEntity.Start)
            state = "progressing";
        if (nextWeekEntity != null && nextWeekEntity.Open && tEntity.WeekNumber < currentWeek)
            state = "completed";
        if (nextWeekEntity != null && DateTime.UtcNow > nextWeekEntity.Start)
            state = "dequeued";
        tEntity.State = state;
        await tableService.UpsertEntityAsync("tournaments", tEntity);
    }

    var endTime = DateTime.UtcNow;
    log.Info($"Execution Time: {endTime - startTime}");
}


public static int GetCurrentWeek(XDocument scheduleDocument)
{
    var dateElement = scheduleDocument.Element("schedule").Element("thisWeek").Element("date");
    var thisWeekNumberValue = dateElement.Attribute("weeknumber").Value;
    return Convert.ToInt32(thisWeekNumberValue);
}