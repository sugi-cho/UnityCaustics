using System.Linq;
using UnityEngine;

namespace CausticsReflective
{
    [DisallowMultipleComponent]
    public class ReflectiveCausticsDebug : MonoBehaviour
    {
        private enum DebugView
        {
            None,
            Caustics,
            Fresnel,
            Jacobian,
            PlaneDistance,
            ShadowMask
        }

        private static readonly string[] ViewLabels =
        {
            "None",
            "CausticsRT",
            "Fresnel R(θ)",
            "Jacobian detJ",
            "Plane distance",
            "Shadow mask"
        };

        [Header("Global Texture Property Names")]
        [SerializeField] private string fresnelTextureProperty = "_RC_Fresnel";
        [SerializeField] private string jacobianTextureProperty = "_RC_Jacobian";
        [SerializeField] private string planeDistanceTextureProperty = "_RC_PlaneMask";
        [SerializeField] private string shadowMaskTextureProperty = "_RC_ShadowMask";

        [Header("UI Settings")]
        [SerializeField] private bool startHidden = false;
        [SerializeField] private float windowWidth = 320f;
        private DebugView _view = DebugView.None;
        private bool _windowVisible;
        private Rect _windowRect = new(12f, 12f, 320f, 10f);
        private int _receiverIndex;
        private Vector2 _scrollPosition;

        private void Awake()
        {
            _windowVisible = !startHidden;
        }

        private void OnEnable()
        {
            if (!IsDebugBuild())
            {
                enabled = false;
                return;
            }
        }

        private static bool IsDebugBuild()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        private void OnGUI()
        {
            if (!enabled)
            {
                return;
            }

            if (!_windowVisible)
            {
                if (GUI.Button(new Rect(12f, 12f, 140f, 32f), "Caustics Debug"))
                {
                    _windowVisible = true;
                }

                return;
            }

            var height = Mathf.Clamp(_windowRect.height, 100f, Screen.height - 24f);
            _windowRect = GUILayout.Window(GetInstanceID(), new Rect(_windowRect.x, _windowRect.y, windowWidth, height), DrawWindowContents, "Reflective Caustics Debug");
        }

        private void DrawWindowContents(int id)
        {
            GUILayout.BeginVertical();

            DrawViewSelection();

            GUILayout.Space(6f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.Height(220f));
            DrawSelectedView();
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            DrawManagerControls();

            GUILayout.Space(10f);
            if (GUILayout.Button("閉じる"))
            {
                _windowVisible = false;
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawViewSelection()
        {
            GUILayout.Label("Debug View", GUI.skin.label);
            int selected = GUILayout.Toolbar((int)_view, ViewLabels);
            _view = (DebugView)Mathf.Clamp(selected, 0, ViewLabels.Length - 1);
        }

        private void DrawSelectedView()
        {
            switch (_view)
            {
                case DebugView.None:
                    GUILayout.Label("バッファのプレビューを選択してください。", GUI.skin.box);
                    break;
                case DebugView.Caustics:
                    DrawCausticsRT();
                    break;
                case DebugView.Fresnel:
                    DrawGlobalTexture(fresnelTextureProperty);
                    break;
                case DebugView.Jacobian:
                    DrawGlobalTexture(jacobianTextureProperty);
                    break;
                case DebugView.PlaneDistance:
                    DrawGlobalTexture(planeDistanceTextureProperty);
                    break;
                case DebugView.ShadowMask:
                    DrawGlobalTexture(shadowMaskTextureProperty);
                    break;
                default:
                    GUILayout.Label("未対応のビューです。", GUI.skin.box);
                    break;
            }
        }

        private void DrawCausticsRT()
        {
            if (ReflectiveCausticsManager.Instance == null)
            {
                GUILayout.Label("ReflectiveCausticsManager が見つかりません。", GUI.skin.box);
                return;
            }

            var receivers = ReflectiveCausticsManager.Receivers;
            if (receivers == null || receivers.Count == 0)
            {
                GUILayout.Label("登録されている受光面がありません。", GUI.skin.box);
                return;
            }

            _receiverIndex = Mathf.Clamp(_receiverIndex, 0, receivers.Count - 1);

            GUILayout.Label("Caustics Receiver", GUI.skin.label);

            var names = receivers.Select(r => r != null ? r.name : "(null)").ToArray();
            int newIndex = GUILayout.SelectionGrid(_receiverIndex, names, 1);
            _receiverIndex = Mathf.Clamp(newIndex, 0, receivers.Count - 1);

            var selectedReceiver = receivers[_receiverIndex];
            if (selectedReceiver == null)
            {
                GUILayout.Label("選択された受光面が無効です。", GUI.skin.box);
                return;
            }

            DrawTexturePreview(selectedReceiver.CausticsRT);
        }

        private void DrawGlobalTexture(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                GUILayout.Label("プロパティ名が未設定です。", GUI.skin.box);
                return;
            }

            var texture = Shader.GetGlobalTexture(propertyName);
            if (texture == null)
            {
                GUILayout.Label($"GlobalTexture '{propertyName}' は現在割り当てられていません。", GUI.skin.box);
                return;
            }

            DrawTexturePreview(texture);
        }

        private static void DrawTexturePreview(Texture texture)
        {
            if (texture == null)
            {
                GUILayout.Label("テクスチャが無効です。", GUI.skin.box);
                return;
            }

            float aspect = texture.width > 0 && texture.height > 0 ? (float)texture.width / texture.height : 1f;
            Rect previewRect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(previewRect, texture, ScaleMode.ScaleToFit, false);
        }

        private static float SliderWithLabel(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120f));
            float newValue = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(newValue.ToString("F3"), GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return newValue;
        }

        private static Color ColorSliders(string label, Color value)
        {
            GUILayout.Label(label);
            value.r = SliderWithLabel("R", value.r, 0f, 1f);
            value.g = SliderWithLabel("G", value.g, 0f, 1f);
            value.b = SliderWithLabel("B", value.b, 0f, 1f);
            value.a = SliderWithLabel("A", value.a, 0f, 1f);
            return value;
        }

        private void DrawManagerControls()
        {
            var manager = ReflectiveCausticsManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("ReflectiveCausticsManager が見つかりません。", GUI.skin.box);
                return;
            }

            GUILayout.Label("Manager Parameters", GUI.skin.label);
            manager.Intensity = SliderWithLabel("Intensity", manager.Intensity, 0f, 5f);
            manager.F0 = SliderWithLabel("F0", manager.F0, 0f, 1f);
            manager.JacobianGain = SliderWithLabel("JacobianGain", manager.JacobianGain, 0f, 5f);
            manager.Tint = ColorSliders("Tint", manager.Tint);
        }
    }
}
