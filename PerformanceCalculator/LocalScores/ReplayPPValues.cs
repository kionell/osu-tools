// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Scoring;

namespace PerformanceCalculator.LocalScores
{
    public class ReplayPPValues
    {
        public double TotalPP;
        public Dictionary<string, double> CategoryAttribs;
        public ScoreInfo ScoreInfo;
        public string MapName;

        public ReplayPPValues(double totalPP, Dictionary<string, double> categoryAttribs, ScoreInfo scoreInfo, string mapName)
        {
            TotalPP = totalPP;
            CategoryAttribs = categoryAttribs;
            ScoreInfo = scoreInfo;
            MapName = mapName;
        }
    }

}
