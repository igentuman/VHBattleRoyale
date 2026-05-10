using System;
using BepInEx.Logging;
using UnityEngine;

namespace BattleRoyale
{
    public interface IZoneSystem
    {
        float   GetCurrentRadius();
        Vector3 GetCenter();
        float   GetDamagePerSecond();
        float   GetNextRadius();
        Vector3 GetNextCenter();
        int     GetPhaseNumber();
        void    Tick(float dt);
    }

    public class ZoneManager : IZoneSystem
    {
        private static ZoneManager _instance;
        public static ZoneManager Instance => _instance;

        private ZoneConfig      _config;
        private int             _phaseIndex;
        private float           _currentRadius;
        private Vector3         _currentCenter;
        private float           _targetRadius;
        private Vector3         _targetCenter;
        private float           _shrinkStartRadius;
        private Vector3         _shrinkStartCenter;
        private bool            _shrinking;
        private bool            _done;
        private float           _phaseElapsed;
        private bool            _active;
        private float           _broadcastTimer;
        private ManualLogSource _log;

        private const float BroadcastInterval = 5f;

        public static void Init(ZoneConfig config, ManualLogSource log)
        {
            _instance = new ZoneManager { _config = config, _log = log };
            BREventBus.Subscribe<MatchStartedEvent>(_instance.OnMatchStarted);
            BREventBus.Subscribe<MatchEndedEvent>(_instance.OnMatchEnded);
        }

        private void OnMatchStarted(MatchStartedEvent e)
        {
            _phaseIndex     = 0;
            _currentRadius  = _config.PhaseRadii[0];
            _currentCenter  = _config.InitialCenter;
            _phaseElapsed   = 0f;
            _broadcastTimer = 0f;
            _shrinking      = false;
            _done           = false;
            _active         = true;
            GenerateNextZone();
            BroadcastZone();
            _log.LogInfo($"[ZoneManager] Zone active — phase 1/{_config.PhaseRadii.Length}, radius: {_currentRadius}m, next: {_targetRadius}m @ {_targetCenter}");
        }

        private void OnMatchEnded(MatchEndedEvent e)
        {
            _active = false;
            _log.LogInfo($"[ZoneManager] Zone deactivated — phase {_phaseIndex + 1}, radius: {_currentRadius:F1}m");
        }

        private void GenerateNextZone()
        {
            int next = _phaseIndex + 1;
            if (next >= _config.PhaseRadii.Length)
            {
                _targetRadius = _currentRadius;
                _targetCenter = _currentCenter;
                return;
            }
            _targetRadius = _config.PhaseRadii[next];
            _targetCenter = RandomCenterWithin(_currentCenter, _currentRadius, _targetRadius);
        }

        private static Vector3 RandomCenterWithin(Vector3 parentCenter, float parentRadius, float childRadius)
        {
            float maxOffset = parentRadius - childRadius;
            if (maxOffset <= 0f) return parentCenter;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist  = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f)) * maxOffset;
            return new Vector3(
                parentCenter.x + Mathf.Cos(angle) * dist,
                parentCenter.y,
                parentCenter.z + Mathf.Sin(angle) * dist);
        }

        public float   GetCurrentRadius()   => _currentRadius;
        public Vector3 GetCenter()          => _currentCenter;
        public float   GetDamagePerSecond() => _config.BaseDamagePerSecond * Mathf.Pow(2f, _phaseIndex);
        public float   GetNextRadius()      => _targetRadius;
        public Vector3 GetNextCenter()      => _targetCenter;
        public int     GetPhaseNumber()     => _phaseIndex + 1;

        public void Tick(float dt)
        {
            if (!_active || _done) return;

            _phaseElapsed += dt;

            if (!_shrinking)
            {
                if (_phaseElapsed >= _config.PhaseWaitDuration)
                {
                    _phaseElapsed      = 0f;
                    _shrinkStartRadius = _currentRadius;
                    _shrinkStartCenter = _currentCenter;
                    _shrinking         = true;
                    _log.LogInfo($"[ZoneManager] Phase {_phaseIndex + 1} shrinking → {_targetRadius}m @ {_targetCenter}");
                }
            }
            else
            {
                float t = Mathf.Clamp01(_phaseElapsed / _config.PhaseShrinkDuration);
                _currentRadius = Mathf.Lerp(_shrinkStartRadius, _targetRadius, t);
                _currentCenter = Vector3.Lerp(_shrinkStartCenter, _targetCenter, t);

                if (t >= 1f)
                {
                    _currentRadius = _targetRadius;
                    _currentCenter = _targetCenter;
                    _phaseIndex++;

                    if (_phaseIndex >= _config.PhaseRadii.Length - 1)
                    {
                        _done = true;
                        _log.LogInfo($"[ZoneManager] Final zone reached: {_currentRadius}m");
                    }
                    else
                    {
                        _phaseElapsed = 0f;
                        _shrinking    = false;
                        GenerateNextZone();
                        _log.LogInfo($"[ZoneManager] Phase {_phaseIndex + 1} started — radius: {_currentRadius}m, next: {_targetRadius}m @ {_targetCenter}");
                    }
                    BroadcastZone();
                }
            }

            ApplyZoneDamage(dt);

            _broadcastTimer += dt;
            if (_broadcastTimer >= BroadcastInterval)
            {
                _broadcastTimer = 0f;
                string status = _done ? "final" : (_shrinking ? "shrinking" : "waiting");
                _log.LogInfo($"[ZoneManager] Zone tick — phase {_phaseIndex + 1}/{_config.PhaseRadii.Length}, radius: {_currentRadius:F1}m, {status}");
                LogPlayersOutsideZone();
                BroadcastZone();
            }
        }

        private void BroadcastZone()
        {
            BREventBus.Emit(new ZoneUpdatedEvent
            {
                MatchId         = MatchManager.Instance?.State?.MatchId ?? "",
                Radius          = _currentRadius,
                Center          = _currentCenter,
                DamagePerSecond = GetDamagePerSecond(),
                NextRadius      = _targetRadius,
                NextCenter      = _targetCenter,
                PhaseNumber     = _phaseIndex + 1,
                Timestamp       = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        private void ApplyZoneDamage(float dt)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            float dmgRate = GetDamagePerSecond();
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                float dist = Vector3.Distance(player.transform.position, _currentCenter);
                if (dist > _currentRadius)
                {
                    float dmg = dmgRate * dt;
                    var hit = new HitData();
                    hit.m_damage.m_blunt = dmg;
                    hit.m_point = player.transform.position;
                    player.Damage(hit);
                    _log.LogInfo($"[ZoneManager] Zone dmg: '{player.GetPlayerName()}' dist={dist:F0}m zone={_currentRadius:F1}m dmg={dmg:F2} hp={player.GetHealth():F1}");
                }
            }
        }

        private void LogPlayersOutsideZone()
        {
            var outside = new System.Collections.Generic.List<string>();
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                float dist = Vector3.Distance(player.transform.position, _currentCenter);
                if (dist > _currentRadius)
                    outside.Add($"{player.GetPlayerName()}({dist:F0}m)");
            }
            if (outside.Count > 0)
                _log.LogInfo($"[ZoneManager] Players outside zone: [{string.Join(", ", outside)}]");
            else
                _log.LogInfo("[ZoneManager] All players inside zone");
        }
    }
}
