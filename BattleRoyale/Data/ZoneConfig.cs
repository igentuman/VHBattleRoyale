using UnityEngine;

namespace BattleRoyale
{
    public class ZoneConfig
    {
        public float[]  PhaseRadii;           // radius per phase, e.g. [5500,4000,2000,1000,200,10]
        public float    PhaseWaitDuration;    // seconds to show next zone before shrinking
        public float    PhaseShrinkDuration;  // seconds to complete each shrink
        public float    BaseDamagePerSecond;  // phase 1 damage; doubles each phase
        public Vector3  InitialCenter;
    }
}
