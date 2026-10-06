using UnityEngine;

namespace Game.Tracer
{
    /// <summary>
    /// Zelda-style camera: snaps to the centre of the screen that contains the target.
    /// Letterboxes to the screen's aspect ratio so exactly one full screen is visible.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 screenSize = ScreenMath.DefaultScreenSize;

        private Camera cam;
        private int lastWidth = -1;
        private int lastHeight = -1;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector2Int CurrentScreen { get; private set; }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = screenSize.y * 0.5f;
        }

        private void Start()
        {
            Snap();
        }

        private void LateUpdate()
        {
            Snap();
            UpdateLetterbox();
        }

        private void Snap()
        {
            if (target == null) return;
            CurrentScreen = ScreenMath.ScreenIndex(target.position, screenSize);
            Vector2 centre = ScreenMath.ScreenCenter(CurrentScreen, screenSize);
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
        }

        private void UpdateLetterbox()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            float targetAspect = screenSize.x / screenSize.y;
            float windowAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (windowAspect > targetAspect)
            {
                float w = targetAspect / windowAspect;
                cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = windowAspect / targetAspect;
                cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }
    }
}
