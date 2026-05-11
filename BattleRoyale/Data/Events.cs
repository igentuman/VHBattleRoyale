using UnityEngine;

namespace BattleRoyale
{
    public struct MatchStartedEvent : IBREvent
    {
        public string MatchId;
        public int Seed;
        public int PlayerCount;
        public long StartedAt;
    }

    public struct MatchEndedEvent : IBREvent
    {
        public string MatchId;
        public string WinnerName;
        public float DurationSeconds;
        public long EndedAt;
    }

    public struct PlayerKilledEvent : IBREvent
    {
        public string MatchId;
        public string KillerName;
        public string VictimName;
        public Vector3 Position;
        public long Timestamp;
    }

    public struct ZoneUpdatedEvent : IBREvent
    {
        public string  MatchId;
        public float   Radius;
        public Vector3 Center;
        public float   DamagePerSecond;
        public float   NextRadius;
        public Vector3 NextCenter;
        public int     PhaseNumber;
        public long    Timestamp;
        // Shrink interpolation — lets clients reproduce the server lerp locally
        public bool    IsShrinking;
        public float   ShrinkStartRadius;
        public Vector3 ShrinkStartCenter;
        public float   ShrinkDuration;
        public float   ShrinkElapsed;
    }

    public struct LootSpawnedEvent : IBREvent
    {
        public string MatchId;
        public Vector3 Position;
        public string ItemName;
    }
}
