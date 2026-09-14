using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Only an explicit interaction commits a door choice.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorTarget : MonoBehaviour
    {
        public bool IsForward;
        public bool IsFinalExit;
        public string InteractionLabel => IsFinalExit ? "666号扉を開ける" : IsForward ? "前へ進む" : "引き返す";
    }
}
