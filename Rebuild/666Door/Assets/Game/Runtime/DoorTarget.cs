using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Only an explicit interaction commits a door choice.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorTarget : MonoBehaviour
    {
        public bool IsForward;
        public bool IsFinalExit;
    }
}
