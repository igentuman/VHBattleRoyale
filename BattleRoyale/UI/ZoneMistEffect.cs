using UnityEngine;

namespace BattleRoyale.UI
{
    public class ZoneMistEffect : MonoBehaviour
    {
        private const int EmitterCount = 24;
        private const float MaxEmitterSpacing = 30f;
        private const float ActivationDistance = 500f;
        private const float EmitterCenterY = 10f; // center box 10m above terrain → spans 0–20m

        private static readonly int TerrainLayer = LayerMask.GetMask("terrain");

        private ParticleSystem[] _emitters;
        private Vector3[]        _terrainCache;
        private Texture2D        _softCircle;

        private void Awake()
        {
            _softCircle   = BuildSoftCircleTexture(64);
            _emitters     = new ParticleSystem[EmitterCount];
            _terrainCache = new Vector3[EmitterCount];

            for (int i = 0; i < EmitterCount; i++)
            {
                _emitters[i]     = CreateEmitter(i);
                _terrainCache[i] = new Vector3(float.MaxValue, 0f, float.MaxValue);
            }
        }

        private static Texture2D BuildSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep — soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        private ParticleSystem CreateEmitter(int index)
        {
            var go = new GameObject($"ZoneMist_{index}");
            go.transform.SetParent(transform);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 80;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize       = new ParticleSystem.MinMaxCurve(10f, 20f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(0.63f, 0.18f, 0.13f, 0.45f),
                new Color(0.28f, 0.14f, 0.07f, 0.25f)
            );

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(28f, 20f, 1f); // wide horizontal, max 20m tall

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.y       = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.61f, 0.25f, 0.16f), 0f), new GradientColorKey(new Color(0.78f, 0.24f, 0.15f), 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0.0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1.0f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;

            var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                      ?? Shader.Find("Particles/Alpha Blended")
                      ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = _softCircle;
            rend.material   = mat;

            return ps;
        }

        private void Update()
        {
            if (ClientSync.Phase == MatchPhase.Lobby)
            {
                SetAllPlaying(false);
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null)
            {
                SetAllPlaying(false);
                return;
            }

            Vector3 pPos   = player.transform.position;
            Vector3 center = ClientSync.ZoneCenter;
            float   radius = ClientSync.ZoneRadius;

            float dx = pPos.x - center.x;
            float dz = pPos.z - center.z;
            float distToCenter = Mathf.Sqrt(dx * dx + dz * dz);

            if (Mathf.Abs(distToCenter - radius) > ActivationDistance)
            {
                SetAllPlaying(false);
                return;
            }

            float playerAngle = Mathf.Atan2(dz, dx);

            // Arc half-width: pack EmitterCount emitters at MaxEmitterSpacing, capped at 162° (0.9π)
            float halfArc = Mathf.Min(
                Mathf.PI * 0.9f,
                (EmitterCount - 1) * MaxEmitterSpacing * 0.5f / Mathf.Max(radius, 1f)
            );
            float step = halfArc * 2f / (EmitterCount - 1);

            for (int i = 0; i < EmitterCount; i++)
            {
                float angle = playerAngle - halfArc + step * i;
                float bx    = center.x + Mathf.Cos(angle) * radius;
                float bz    = center.z + Mathf.Sin(angle) * radius;
                float by    = SampleTerrainY(bx, bz, i);

                _emitters[i].transform.position = new Vector3(bx, by + EmitterCenterY, bz);
                _emitters[i].transform.rotation = Quaternion.Euler(0f, (-angle * Mathf.Rad2Deg) + 90f, 0f);

                if (!_emitters[i].isPlaying)
                    _emitters[i].Play();
            }
        }

        private void SetAllPlaying(bool playing)
        {
            foreach (var ps in _emitters)
            {
                if (ps == null) continue;
                if (!playing && ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else if (playing && !ps.isPlaying)
                    ps.Play();
            }
        }

        private float SampleTerrainY(float x, float z, int idx)
        {
            ref Vector3 cached = ref _terrainCache[idx];
            if (Vector2.Distance(new Vector2(x, z), new Vector2(cached.x, cached.z)) < 5f)
                return cached.y;

            float y = 0f;
            if (Physics.Raycast(new Ray(new Vector3(x, 5000f, z), Vector3.down), out RaycastHit hit, 6000f, TerrainLayer))
                y = hit.point.y;

            cached = new Vector3(x, y, z);
            return y;
        }
    }
}
