using System;
using System.Collections.Generic;

namespace APDP.Models
{
    public class WeatherViewModel
    {
        // ── Current weather ──────────────────────────────────────────
        public string CityName             { get; set; }
        public string Country              { get; set; }
        public double TemperatureCelsius   { get; set; }
        public double FeelsLike            { get; set; }
        public int    Humidity             { get; set; }
        public double WindSpeedKmh         { get; set; }
        public string Description          { get; set; }
        public string Icon                 { get; set; }
        public string IconUrl              => $"https://openweathermap.org/img/wn/{Icon}@2x.png";

        // ── Day / Night ──────────────────────────────────────────────
        public DateTime Sunrise            { get; set; }
        public DateTime Sunset             { get; set; }
        public bool IsNight
        {
            get
            {
                var now = DateTime.Now.TimeOfDay;
                return now < Sunrise.TimeOfDay || now > Sunset.TimeOfDay;
            }
        }

        // ── 7-day forecast ───────────────────────────────────────────
        public List<ForecastDay> Forecast  { get; set; } = new List<ForecastDay>();

        // ── Sky type for animation ───────────────────────────────────
        public string SkyType
        {
            get
            {
                string d = (Description ?? "").ToLower();
                if (d.Contains("thunderstorm"))  return "thunder";
                if (d.Contains("rain") || d.Contains("drizzle")) return "rain";
                if (d.Contains("snow"))          return "snow";
                if (d.Contains("fog") || d.Contains("mist") || d.Contains("haze")) return "fog";
                if (d.Contains("cloud"))         return IsNight ? "cloudy-night" : "cloudy";
                return IsNight ? "clear-night" : "clear";
            }
        }

        // ── Sailing advice ───────────────────────────────────────────
        public string SailingAdvice
        {
            get
            {
                string desc = (Description ?? "").ToLower();
                if (desc.Contains("thunderstorm") || desc.Contains("tornado"))
                    return "Dangerous — Do not sail. Severe weather.";
                if (WindSpeedKmh > 50)
                    return "Very High Winds — Sailing not recommended.";
                if (desc.Contains("heavy rain") || desc.Contains("heavy snow"))
                    return "Poor Conditions — Consider rescheduling.";
                if (WindSpeedKmh > 30 || desc.Contains("rain") || desc.Contains("drizzle"))
                    return "Moderate Conditions — Sail with caution.";
                return "Good Conditions — Suitable for sailing.";
            }
        }

        public string SailingAdviceCssClass
        {
            get
            {
                string a = SailingAdvice;
                if (a.StartsWith("Dangerous") || a.StartsWith("Very High")) return "danger";
                if (a.StartsWith("Poor") || a.StartsWith("Moderate"))       return "warning";
                return "success";
            }
        }

        public bool   HasError     { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ForecastDay
    {
        public DateTime Date        { get; set; }
        public string   DayName     => Date.DayOfWeek == DateTime.Today.DayOfWeek
                                        ? "Today"
                                        : Date.ToString("ddd");
        public double   TempMin     { get; set; }
        public double   TempMax     { get; set; }
        public string   Description { get; set; }
        public string   Icon        { get; set; }
        public string   IconUrl     => $"https://openweathermap.org/img/wn/{Icon}@2x.png";
    }
}
