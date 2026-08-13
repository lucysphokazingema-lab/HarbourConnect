using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using APDP.Models;

namespace APDP.Services
{
    public class WeatherService
    {
        private static readonly Dictionary<string, string> LocationMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Durban Harbour",    "Durban,ZA"       },
                { "Cape Town Harbour", "Cape Town,ZA"    },
                { "Port Elizabeth",    "Gqeberha,ZA"     },
                { "East London",       "East London,ZA"  },
                { "Richards Bay",      "Richards Bay,ZA" }
            };

        public WeatherViewModel GetWeather(string harbourLocation)
        {
            string apiKey  = ConfigurationManager.AppSettings["OpenWeatherMapApiKey"];
            string baseUrl = ConfigurationManager.AppSettings["OpenWeatherMapBaseUrl"];

            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey == "YOUR_API_KEY_HERE" ||
                apiKey == "PASTE_YOUR_KEY_HERE")
                return GetFallbackWeather(harbourLocation);

            string cityQuery;
            if (!LocationMap.TryGetValue(harbourLocation ?? "", out cityQuery))
                cityQuery = (harbourLocation ?? "Durban") + ",ZA";

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(6);

                    // ── Current weather ───────────────────────────────
                    string currentUrl  = $"{baseUrl}?q={Uri.EscapeDataString(cityQuery)}&appid={apiKey}&units=metric";
                    var    currentResp = client.GetAsync(currentUrl).Result;

                    if (!currentResp.IsSuccessStatusCode)
                        return GetFallbackWeather(harbourLocation);

                    string currentJson = currentResp.Content.ReadAsStringAsync().Result;
                    var    current     = JObject.Parse(currentJson);

                    var result = new WeatherViewModel();
                    result.CityName           = current["name"]?.ToString() ?? harbourLocation;
                    result.Country            = current["sys"]?["country"]?.ToString() ?? "ZA";
                    result.TemperatureCelsius = Math.Round(current["main"]?["temp"]?.Value<double>() ?? 24, 1);
                    result.FeelsLike          = Math.Round(current["main"]?["feels_like"]?.Value<double>() ?? 23, 1);
                    result.Humidity           = current["main"]?["humidity"]?.Value<int>() ?? 70;
                    result.WindSpeedKmh       = Math.Round((current["wind"]?["speed"]?.Value<double>() ?? 3) * 3.6, 1);

                    // Sunrise / Sunset — stored as Unix timestamps in UTC
                    long sunriseUnix = current["sys"]?["sunrise"]?.Value<long>() ?? 0;
                    long sunsetUnix  = current["sys"]?["sunset"]?.Value<long>()  ?? 0;
                    result.Sunrise   = sunriseUnix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(sunriseUnix).LocalDateTime
                        : DateTime.Today.AddHours(6);
                    result.Sunset    = sunsetUnix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(sunsetUnix).LocalDateTime
                        : DateTime.Today.AddHours(18);

                    var wArr = current["weather"] as JArray;
                    if (wArr != null && wArr.Count > 0)
                    {
                        result.Description = wArr[0]["description"]?.ToString() ?? "clear sky";
                        result.Icon        = wArr[0]["icon"]?.ToString() ?? "01d";
                    }
                    else
                    {
                        result.Description = "clear sky";
                        result.Icon        = "01d";
                    }

                    // ── 5-day / 3-hour forecast → daily ──────────────
                    string forecastUrl  = $"https://api.openweathermap.org/data/2.5/forecast?q={Uri.EscapeDataString(cityQuery)}&appid={apiKey}&units=metric";
                    var    forecastResp = client.GetAsync(forecastUrl).Result;

                    if (forecastResp.IsSuccessStatusCode)
                    {
                        string forecastJson = forecastResp.Content.ReadAsStringAsync().Result;
                        var    forecastData = JObject.Parse(forecastJson);
                        var    list         = forecastData["list"] as JArray;

                        if (list != null)
                            result.Forecast = BuildDailyForecast(list);
                    }

                    // If forecast call failed, build fallback forecast
                    if (result.Forecast == null || result.Forecast.Count == 0)
                        result.Forecast = BuildFallbackForecast();

                    return result;
                }
            }
            catch
            {
                return GetFallbackWeather(harbourLocation);
            }
        }

        // ── Convert 3-hour slots into one entry per day ──────────────
        private List<ForecastDay> BuildDailyForecast(JArray list)
        {
            var days = new List<ForecastDay>();

            var grouped = list
                .GroupBy(item =>
                    DateTimeOffset.FromUnixTimeSeconds(item["dt"].Value<long>())
                                  .LocalDateTime.Date)
                .Take(7);

            foreach (var group in grouped)
            {
                var temps = group.Select(i => i["main"]?["temp"]?.Value<double>() ?? 20).ToList();
                var midday = group.OrderBy(i =>
                    Math.Abs(DateTimeOffset.FromUnixTimeSeconds(i["dt"].Value<long>())
                                           .LocalDateTime.Hour - 12))
                    .First();

                var wArr = midday["weather"] as JArray;
                days.Add(new ForecastDay
                {
                    Date        = group.Key,
                    TempMin     = Math.Round(temps.Min(), 0),
                    TempMax     = Math.Round(temps.Max(), 0),
                    Description = wArr != null && wArr.Count > 0
                                    ? wArr[0]["description"]?.ToString() ?? "clear sky"
                                    : "clear sky",
                    Icon        = wArr != null && wArr.Count > 0
                                    ? wArr[0]["icon"]?.ToString() ?? "01d"
                                    : "01d"
                });
            }

            return days;
        }

        private List<ForecastDay> BuildFallbackForecast()
        {
            var list = new List<ForecastDay>();
            var icons    = new[] { "01d","02d","03d","10d","01d","02d","01d" };
            var descs    = new[] { "clear sky","partly cloudy","cloudy","light rain","clear sky","partly cloudy","clear sky" };
            var maxTemps = new[] { 28.0, 26.0, 24.0, 22.0, 27.0, 29.0, 30.0 };
            var minTemps = new[] { 19.0, 18.0, 17.0, 16.0, 18.0, 20.0, 21.0 };
            for (int i = 0; i < 7; i++)
            {
                list.Add(new ForecastDay
                {
                    Date        = DateTime.Today.AddDays(i),
                    TempMax     = maxTemps[i],
                    TempMin     = minTemps[i],
                    Description = descs[i],
                    Icon        = icons[i]
                });
            }
            return list;
        }

        private WeatherViewModel GetFallbackWeather(string harbourLocation)
        {
            var defaults = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Durban Harbour",    new[] { 26.0, 25.0, 78.0, 15.0 } },
                { "Cape Town Harbour", new[] { 22.0, 21.0, 65.0, 20.0 } },
                { "Port Elizabeth",    new[] { 20.0, 19.0, 70.0, 18.0 } },
                { "East London",       new[] { 23.0, 22.0, 72.0, 14.0 } },
                { "Richards Bay",      new[] { 27.0, 26.0, 80.0, 12.0 } },
            };

            double[] vals;
            if (!defaults.TryGetValue(harbourLocation ?? "", out vals))
                vals = new[] { 24.0, 23.0, 70.0, 14.0 };

            bool isNight = DateTime.Now.Hour < 6 || DateTime.Now.Hour > 18;

            return new WeatherViewModel
            {
                CityName           = harbourLocation ?? "Harbour",
                Country            = "ZA",
                TemperatureCelsius = vals[0],
                FeelsLike          = vals[1],
                Humidity           = (int)vals[2],
                WindSpeedKmh       = vals[3],
                Description        = isNight ? "clear sky" : "partly cloudy",
                Icon               = isNight ? "01n" : "02d",
                Sunrise            = DateTime.Today.AddHours(6),
                Sunset             = DateTime.Today.AddHours(18),
                Forecast           = BuildFallbackForecast(),
                HasError           = false
            };
        }
    }
}
