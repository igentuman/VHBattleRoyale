using BepInEx.Logging;

namespace BattleRoyale
{
    // Mock implementation - logs events instead of real HTTP.
    // Real HTTP implementation lives in the separate API project.
    // Set API.Enabled = true and wire HttpApiClient when ready.
    public static class ApiClient
    {
        private static string _baseUrl;
        private static bool _enabled;
        private static ManualLogSource _log;

        public static void Init(string baseUrl, bool enabled, ManualLogSource log)
        {
            _baseUrl = baseUrl;
            _enabled = enabled;
            _log = log;

            BREventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
            BREventBus.Subscribe<MatchEndedEvent>(OnMatchEnded);
            BREventBus.Subscribe<PlayerKilledEvent>(OnPlayerKilled);
            BREventBus.Subscribe<ZoneUpdatedEvent>(OnZoneUpdated);
        }

        private static void Post(string endpoint, string json)
        {
            if (!_enabled)
            {
                _log.LogInfo($"[ApiClient:mock] POST {endpoint} {json}");
                return;
            }
            // TODO: replace with real HTTP once backend project is live
            _log.LogWarning($"[ApiClient] API enabled but HTTP client not implemented — POST {_baseUrl}{endpoint}");
        }

        private static void OnMatchStarted(MatchStartedEvent e) =>
            Post("/api/match/start",
                $"{{\"matchId\":\"{e.MatchId}\",\"seed\":{e.Seed},\"playerCount\":{e.PlayerCount},\"startedAt\":{e.StartedAt}}}");

        private static void OnMatchEnded(MatchEndedEvent e) =>
            Post("/api/match/end",
                $"{{\"matchId\":\"{e.MatchId}\",\"winner\":\"{e.WinnerName}\",\"durationSeconds\":{e.DurationSeconds},\"endedAt\":{e.EndedAt}}}");

        private static void OnPlayerKilled(PlayerKilledEvent e) =>
            Post("/api/events/kill",
                $"{{\"matchId\":\"{e.MatchId}\",\"killerName\":\"{e.KillerName}\",\"victimName\":\"{e.VictimName}\",\"timestamp\":{e.Timestamp}}}");

        private static void OnZoneUpdated(ZoneUpdatedEvent e) =>
            Post("/api/events/zone",
                $"{{\"matchId\":\"{e.MatchId}\",\"radius\":{e.Radius},\"center\":{{\"x\":{e.Center.x},\"y\":{e.Center.y},\"z\":{e.Center.z}}},\"timestamp\":{e.Timestamp}}}");
    }
}
