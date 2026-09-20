using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>
    /// The hand that appears only while striking (Hit): it thrusts from below the view toward the crosshair, stops on what it
    /// hits with a knock and a small kick of the view, and withdraws out of sight. It sits under the camera in
    /// Assets/Prefabs/Player.prefab, where its poses and timing are tuned. Rituals take the hit when the button is pressed;
    /// this is only how it looks and sounds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonHand : MonoBehaviour
    {
        private enum Phase { Idle, Thrust, Hold, Withdraw }

        [Tooltip("手の見た目（子）。叩いていない間は非表示にする。")]
        [SerializeField] private GameObject model;

        [Header("姿勢（カメラから見た位置と角度）")]
        [Tooltip("隠れているときの位置。画面の下の外に置く。")]
        [SerializeField] private Vector3 hiddenPosition = new Vector3(.30f, -.55f, .12f);
        [SerializeField] private Vector3 hiddenAngles = new Vector3(-35f, -20f, 0f);
        [Tooltip("叩いたときに拳が届く位置。物に当たるときは、その表面の手前で止まる。")]
        [SerializeField] private Vector3 strikePosition = new Vector3(.12f, -.13f, .52f);
        [SerializeField] private Vector3 strikeAngles = new Vector3(-10f, -14f, 0f);
        [Tooltip("拳を物の表面からどれだけ手前で止めるか（m）。")]
        [SerializeField, Min(0)] private float contactGap = .06f;

        [Header("時間（秒）")]
        [Tooltip("画面の下から拳が届くまで。")]
        [SerializeField, Min(.01f)] private float thrustSeconds = .09f;
        [Tooltip("拳を当てたまま止める時間。")]
        [SerializeField, Min(0)] private float holdSeconds = .07f;
        [Tooltip("画面の下へ引っ込めるまで。")]
        [SerializeField, Min(.01f)] private float withdrawSeconds = .24f;

        [Header("手応え（物に当たったときだけ）")]
        [Tooltip("視点が跳ね上がる角度（度）。")]
        [SerializeField, Min(0)] private float impactKick = 1.2f;
        [Tooltip("跳ね上がった視点が戻るまでの時間（秒）。")]
        [SerializeField, Min(.01f)] private float kickRecoverSeconds = .18f;
        [Tooltip("叩く音（SoundLibrary の knock）の音量。")]
        [SerializeField, Range(0f, 1f)] private float impactVolume = .55f;

        private Phase phase;
        private float clock;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private Vector3 reachPosition;
        private bool contact;
        private Vector3 contactPoint;

        /// <summary>Degrees the latest impact kicks the view up; the rig adds it to its pitch.</summary>
        public float ViewKick { get; private set; }
        public bool IsStriking => phase != Phase.Idle;

        private void Awake() => Hide();

        /// <param name="target">What the crosshair ray hit within reach; a hit without a collider swings at the air.</param>
        public void Strike(RaycastHit target)
        {
            // A strike during another starts from where the hand is, so rapid clicks chain without snapping.
            bool fromHidden = phase == Phase.Idle;
            fromPosition = fromHidden ? hiddenPosition : transform.localPosition;
            fromRotation = fromHidden ? Quaternion.Euler(hiddenAngles) : transform.localRotation;
            reachPosition = strikePosition;
            contact = target.collider != null;
            if (contact)
            {
                contactPoint = target.point;
                // Stop the fist on the surface instead of sinking into it.
                float surface = transform.parent != null ? transform.parent.InverseTransformPoint(target.point).z : target.distance;
                reachPosition.z = Mathf.Clamp(surface - contactGap, .2f, strikePosition.z);
            }
            phase = Phase.Thrust;
            clock = 0;
            transform.localPosition = fromPosition;
            transform.localRotation = fromRotation;
            if (model != null) model.SetActive(true);
        }

        /// <summary>Puts the hand away at once, for example when the player is moved to the next round.</summary>
        public void Hide()
        {
            phase = Phase.Idle;
            ViewKick = 0;
            transform.localPosition = hiddenPosition;
            transform.localRotation = Quaternion.Euler(hiddenAngles);
            if (model != null) model.SetActive(false);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (ViewKick > 0) ViewKick = Mathf.MoveTowards(ViewKick, 0, impactKick / kickRecoverSeconds * deltaTime);
            if (phase == Phase.Idle) return;
            clock += deltaTime;
            switch (phase)
            {
                case Phase.Thrust:
                    float thrust = Mathf.Clamp01(clock / thrustSeconds);
                    float eased = 1 - (1 - thrust) * (1 - thrust);
                    transform.localPosition = Vector3.Lerp(fromPosition, reachPosition, eased);
                    transform.localRotation = Quaternion.Slerp(fromRotation, Quaternion.Euler(strikeAngles), eased);
                    if (thrust < 1) break;
                    Impact();
                    Advance(Phase.Hold);
                    break;
                case Phase.Hold:
                    if (clock >= holdSeconds) Advance(Phase.Withdraw);
                    break;
                case Phase.Withdraw:
                    float withdraw = Mathf.Clamp01(clock / withdrawSeconds);
                    float smooth = withdraw * withdraw * (3 - 2 * withdraw);
                    transform.localPosition = Vector3.Lerp(reachPosition, hiddenPosition, smooth);
                    transform.localRotation = Quaternion.Slerp(Quaternion.Euler(strikeAngles), Quaternion.Euler(hiddenAngles), smooth);
                    if (withdraw >= 1) Hide();
                    break;
            }
        }

        private void Impact()
        {
            if (!contact) return;
            ViewKick = impactKick;
            SpatialAudio.Emit(null, contactPoint, "knock", impactVolume);
        }

        private void Advance(Phase next)
        {
            phase = next;
            clock = 0;
        }
    }
}
