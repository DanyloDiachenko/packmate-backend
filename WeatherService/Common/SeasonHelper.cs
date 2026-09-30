

using System;

namespace WeatherService.Common;

public static class SeasonHelper
{
    public static string GetSeason(DateTime date) => date.Month switch
    {
        12 or 1 or 2 => "Winter",
        3 or 4 or 5 => "Spring",
        6 or 7 or 8 => "Summer",
        9 or 10 or 11 => "Autumn",
        _ => "Unknown"
    };
}