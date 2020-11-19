// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alba.CsConsoleFormat;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Shared;
using osu_database_reader.BinaryFiles;
using osu_database_reader.Components.Player;

namespace PerformanceCalculator.LocalScores
{
    [Command(Name = "localscores", Description = "Recalcs all of your local scores")]
    public class LocalScoresCommand : ProcessorCommand
    {
        private const int top_scores_count = 500;
        private const int max_name_length = 120;

        [UsedImplicitly]
        [Required]
        [FileExists]
        [Argument(0, Name = "osuDb", Description = "Path to your osu!.db file. Copy and rename it to remove the '!' in the filename if you have issues.")]
        public string OsuDbPath { get; }

        [UsedImplicitly]
        [Required]
        [FileExists]
        [Argument(1, Name = "scoresDb", Description = "Path to your scores.db file.")]
        public string ScoreDbPath { get; }

        [UsedImplicitly]
        [Required]
        [DirectoryExists]
        [Argument(2, Name = "songsFolderPath", Description = "Path to your osu Songs folder.")]
        public string SongsFolderPath { get; }

        [Option(CommandOptionType.MultipleValue, Template = "-u|--user <username>", Description = "Extra usernames to process")]
        public string[] ExtraUsernames { get; set; }

        [Option(CommandOptionType.MultipleValue, Template = "-c|--columns <attribute_name>", Description = "Extra columns to display from category attribs, for example 'Tap Rhythm pp'")]
        public string[] ExtraColumns { get; set; }

        [Option(CommandOptionType.SingleValue, Template = "-s|--sort <attribute_name>", Description = "What column to sort by (defaults to pp of the play)")]
        public string SortColumnName { get; set; }

        [Option(Description = "Only run on 20 beatmaps to test your arguments")]
        public bool TestRun { get; set; }

        [Option(Description = "Remove chokes from scores")]
        public bool NoChokes { get; set; }

        public override void Execute()
        {
            var currentRuleset = LegacyHelper.GetRulesetFromLegacyID(0);
            var allowedUsers = new List<string>();

            if (ExtraUsernames != null)
            {
                allowedUsers.AddRange(ExtraUsernames);
            }

            OsuDb osuDb = OsuDb.Read(OsuDbPath);
            ScoresDb scoresDb = ScoresDb.Read(ScoreDbPath);

            Dictionary<string, string> checkSumToOsuFile = new Dictionary<string, string>();

            foreach (var beatmap in osuDb.Beatmaps.Where(beatmap => beatmap.BeatmapChecksum != null && beatmap.RankedStatus == SubmissionStatus.Ranked))
            {
                if (!checkSumToOsuFile.TryAdd(beatmap.BeatmapChecksum, SongsFolderPath + "/" + beatmap.FolderName + "/" + beatmap.BeatmapFileName))
                {
                    Console.WriteLine("WARNING: beatmap " + beatmap.BeatmapFileName + " found multiple times in osu db");
                }
            }

            string[] keys = scoresDb.Beatmaps.Keys.ToArray();
            var allScoresBag = new ConcurrentBag<ReplayPPValues>();

            string[] keysToProcess = TestRun ? keys[..20] : keys;
            ConcurrentDictionary<string, List<Replay>> replayDictionary = new ConcurrentDictionary<string, List<Replay>>(scoresDb.Beatmaps);
            Console.WriteLine("Processing " + keysToProcess.Length + " beatmaps (this may take a while!)");

            Parallel.ForEach(keysToProcess, md5 =>
            {
                List<Replay> replays = replayDictionary[md5];
                List<ReplayPPValues> replayPPValuesOnThisMap = new List<ReplayPPValues>();

                checkSumToOsuFile.TryGetValue(md5, out var beatmapPath);

                if (beatmapPath == null)
                {
                    // Probably an unranked map
                    // Console.WriteLine("Couldn't find beatmap with hash " + md5);
                    return;
                }

                if (!File.Exists(beatmapPath))
                {
                    Console.WriteLine("WARNING: Couldn't find map " + beatmapPath);
                    return;
                }

                foreach (var replayEntry in replays.Where(replayEntry =>
                    replayEntry.GameMode == 0 && (allowedUsers.Count == 0 || allowedUsers.Contains(replayEntry.PlayerName))))
                {
                    try
                    {
                        string beatmapFileName = Path.GetFileName(beatmapPath).Substring(0);
                        string beatmapName = beatmapFileName.Substring(0, beatmapFileName.Length - 4);

                        var workingBeatmap = new ProcessorWorkingBeatmap(beatmapPath);
                        var scoreParser = new ProcessorScoreDecoder(workingBeatmap);

                        RulesetInfo rulesetInfo = LegacyHelper.GetRulesetFromLegacyID(0).RulesetInfo;
                        Ruleset ruleset = rulesetInfo.CreateInstance();

                        // Convert + process beatmap
                        var scoreMods = currentRuleset.ConvertFromLegacyMods((LegacyMods)replayEntry.Mods).ToArray();

                        var difficultyCalculator = (OsuDifficultyCalculator)ruleset.CreateDifficultyCalculator(workingBeatmap);
                        var difficultyAttributes = difficultyCalculator.Calculate(scoreMods);

                        ScoreInfo scoreInfo = new ScoreInfo
                        {
                            Statistics =
                            {
                                [HitResult.Good] = replayEntry.Count100,
                                [HitResult.Great] = !NoChokes ? replayEntry.Count300
                                    : difficultyAttributes.MaxCombo - replayEntry.Count100 - replayEntry.Count50,
                                [HitResult.Meh] = replayEntry.Count50,
                                [HitResult.Miss] = !NoChokes ? replayEntry.CountMiss : 0
                            },
                            Mods = scoreMods,
                            MaxCombo = !NoChokes ? replayEntry.Combo : difficultyAttributes.MaxCombo,
                            Ruleset = rulesetInfo,
                        };

                        var score = scoreParser.Parse(scoreInfo);

                        var performanceCalculator = (OsuPerformanceCalculator)ruleset.CreatePerformanceCalculator(difficultyAttributes, score.ScoreInfo);
                        // ReSharper disable once PossibleNullReferenceException
                        scoreInfo.Combo = performanceCalculator.Attributes.MaxCombo;

                        var categoryAttribs = new Dictionary<string, double>();

                        var pp = performanceCalculator.Calculate(categoryAttribs);
                        replayPPValuesOnThisMap.Add(new ReplayPPValues(pp, categoryAttribs, score.ScoreInfo, beatmapName));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                if (replayPPValuesOnThisMap.Count <= 0) return;

                ReplayPPValues best = null;

                foreach (var replayValues in replayPPValuesOnThisMap.Where(replayValues => best == null || best.TotalPP < replayValues.TotalPP))
                {
                    best = replayValues;
                }

                allScoresBag.Add(best);
                Console.WriteLine("Done with " + replayPPValuesOnThisMap[0].MapName);
            });

            Console.WriteLine("Done recalcing replays");
            List<ReplayPPValues> allScores = allScoresBag.ToList();

            if (allScores.Count == 0)
            {
                Console.WriteLine("No replays for the current user found!");
                return;
            }

            allScores.Sort((s1, s2) =>
                SortColumnName == null
                    ? s2.TotalPP.CompareTo(s1.TotalPP)
                    : s2.CategoryAttribs[SortColumnName].CompareTo(s1.CategoryAttribs[SortColumnName]));

            List<ReplayPPValues> localOrdered = allScores.GetRange(0, Math.Min(top_scores_count, allScores.Count));

            foreach (var values in localOrdered.Where(values => values.MapName.Length > max_name_length))
            {
                values.MapName = "..." + values.MapName.Substring(values.MapName.Length - max_name_length);
            }

            int index = 0;
            double totalLocalPP = localOrdered.Sum(play => Math.Pow(0.95, index++) * play.TotalPP);
            double bonusPP = 416.6667 * (1 - Math.Pow(0.9994, allScores.Count));

            Grid grid = new Grid();
            grid.Columns.Add(createColumns(10 + (ExtraColumns?.Length ?? 0)));
            grid.Children.Add(
                new Cell("#") { Align = Align.Center },
                new Cell("beatmap") { Align = Align.Center },
                new Cell("mods") { Align = Align.Center },
                new Cell("local pp") { Align = Align.Center },
                new Cell("acc") { Align = Align.Center },
                new Cell("miss") { Align = Align.Center },
                new Cell("combo") { Align = Align.Center },
                new Cell("Total Aim pp") { Align = Align.Center },
                new Cell("Total Tap pp") { Align = Align.Center },
                new Cell("Accuracy pp") { Align = Align.Center }
            );

            if (ExtraColumns != null)
            {
                foreach (var extraColumn in ExtraColumns)
                {
                    grid.Children.Add(new Cell(extraColumn) { Align = Align.Center });
                }
            }

            grid.Children.Add(localOrdered.Select((item, cellIndex) =>
            {
                List<Cell> cells = new List<Cell>
                {
                    new Cell(cellIndex + 1) { Align = Align.Left },
                    new Cell($"{item.MapName.ToString().Substring(0, Math.Min(max_name_length + 3, item.MapName.ToString().Length))}"),
                    new Cell(getMods(item.ScoreInfo)) { Align = Align.Right },
                    new Cell($"{item.TotalPP:F1}") { Align = Align.Right },
                    new Cell($"{item.ScoreInfo.Accuracy * 100f:F2}" + " %") { Align = Align.Right },
                    new Cell($"{item.ScoreInfo.Statistics[HitResult.Miss]}") { Align = Align.Right },
                    new Cell($"{item.ScoreInfo.MaxCombo}/{item.ScoreInfo.Combo}") { Align = Align.Right },
                };

                if (item.CategoryAttribs.TryGetValue("Aim", out _))
                {
                    // Delta attributes
                    cells.AddRange(new List<Cell>
                    {
                        new Cell($"{item.CategoryAttribs["Aim"]:F1}") { Align = Align.Right },
                        new Cell($"{item.CategoryAttribs["Tap"]:F1}") { Align = Align.Right },
                        new Cell($"{item.CategoryAttribs["Accuracy"]:F1}") { Align = Align.Right }
                    });
                }
                else
                {
                    // Xexxar attributes
                    cells.AddRange(new List<Cell>
                    {
                        new Cell($"{item.CategoryAttribs["Total Aim pp"]:F1}") { Align = Align.Right },
                        new Cell($"{item.CategoryAttribs["Total Tap pp"]:F1}") { Align = Align.Right },
                        new Cell($"{item.CategoryAttribs["Accuracy pp"]:F1}") { Align = Align.Right }
                    });
                }

                if (ExtraColumns != null)
                {
                    cells.AddRange(ExtraColumns.Select(extraColumn => new Cell($"{item.CategoryAttribs[extraColumn]:F1}") { Align = Align.Right }));
                }

                return cells;
            }));

            Console.WriteLine();
            OutputDocument(new Document(
                new Span($"Local PP: {(totalLocalPP + bonusPP):F1} (including {bonusPP:F1}pp from playcount)"), "\n",
                grid
            ));
        }

        private string getMods(ScoreInfo scoreInfo)
        {
            return scoreInfo.Mods.Length > 0
                ? scoreInfo.Mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}|{n}")
                : "";
        }

        private string getScoreLine(ReplayPPValues values)
        {
            string result = values.MapName + " - " + getMods(values.ScoreInfo) + values.TotalPP + "pp";
            result = result.PadRight(100) + "| ";

            return values.CategoryAttribs.Aggregate(result,
                (current, kvp) => current + (kvp.Key + "= " + kvp.Value.ToString(CultureInfo.InvariantCulture).PadRight(3) + " | "));
        }

        private List<Column> createColumns(int size)
        {
            List<Column> result = new List<Column>();

            for (int i = 0; i < size; i++)
            {
                result.Add(new Column { Width = GridLength.Auto });
            }

            return result;
        }
    }
}
