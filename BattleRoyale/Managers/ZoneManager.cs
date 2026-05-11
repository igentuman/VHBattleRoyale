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
        private float           _dmgTimer;
        private bool            _warned60;
        private bool            _warned30;
        private bool            _warned10;
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
            _warned60       = false;
            _warned30       = false;
            _warned10       = false;
            GenerateNextZone();
            BroadcastZone();
            ClientSync.BroadcastSystemMessage($"Zone is active! Phase 1/{_config.PhaseRadii.Length - 1} — closes in {_config.PhaseWaitDuration:F0}s. Radius: {_currentRadius:F0}m → {_targetRadius:F0}m");
            _log.LogInfo($"[ZoneManager] Zone active - phase 1/{_config.PhaseRadii.Length}, radius: {_currentRadius}m, next: {_targetRadius}m @ {_targetCenter}");
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
                    ClientSync.BroadcastSystemMessage($"Zone closing! Phase {_phaseIndex + 1}/{_config.PhaseRadii.Length - 1} — shrinking from {_currentRadius:F0}m to {_targetRadius:F0}m");
                    _log.LogInfo($"[ZoneManager] Phase {_phaseIndex + 1} shrinking → {_targetRadius}m @ {_targetCenter}");
                }
                else
                {
                    float timeLeft = _config.PhaseWaitDuration - _phaseElapsed;
                    if (!_warned60 && timeLeft <= 60f) { _warned60 = true; ClientSync.BroadcastSystemMessage("Zone closes in 60 seconds!"); }
                    if (!_warned30 && timeLeft <= 30f) { _warned30 = true; ClientSync.BroadcastSystemMessage("Zone closes in 30 seconds!"); }
                    if (!_warned10 && timeLeft <= 10f) { _warned10 = true; ClientSync.BroadcastSystemMessage("Zone closes in 10 seconds!"); }
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
                        ClientSync.BroadcastSystemMessage($"Final zone reached! Radius: {_currentRadius:F0}m — last players standing wins!");
                        _log.LogInfo($"[ZoneManager] Final zone reached: {_currentRadius}m");
                    }
                    else
                    {
                        _phaseElapsed = 0f;
                        _shrinking    = false;
                        _warned60     = false;
                        _warned30     = false;
                        _warned10     = false;
                        GenerateNextZone();
                        ClientSync.BroadcastSystemMessage($"Phase {_phaseIndex + 1} — zone stable. Closes again in {_config.PhaseWaitDuration:F0}s. Next radius: {_targetRadius:F0}m");
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

            _dmgTimer += dt;
            if (_dmgTimer < 1f) return;
            _dmgTimer -= 1f;

            float dmg = GetDamagePerSecond();
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                float dist = Vector3.Distance(player.transform.position, _currentCenter);
                if (dist > _currentRadius)
                {
                    var hit = new HitData();
                    hit.m_damage.m_blunt = dmg;
                    hit.m_point          = player.transform.position;
                    hit.m_dir            = Vector3.up;
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
