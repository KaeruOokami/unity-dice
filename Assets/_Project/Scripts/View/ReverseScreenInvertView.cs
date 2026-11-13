using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.View
{
    /// <summary>
    /// Topmost Overlay canvas that inverts the final frame (world + skybox + UI) via blend.
    /// </summary>
    public sealed class ReverseScreenInvertView : MonoBehaviour
    {
        const string ShaderName = "Dice/ScreenInvertOverlay";
        const int SortingOrder = 32000;

        Canvas canvas;
        Image image;
        Material invertMaterial;
        bool visible;

        public void SetVisible(bool show) {
            EnsureCreated();
            if (visible == show) {
                return;
            }

            visible = show;
            if (canvas != null) {
                canvas.gameObject.SetActive(show);
            }
        }

        void OnDestroy() {
            if (invertMaterial != null) {
                Destroy(invertMaterial);
                invertMaterial = null;
            }
        }

        void EnsureCreated() {
            if (canvas != null) {
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null) {
                Debug.LogError($"ReverseScreenInvertView: Shader '{ShaderName}' not found.");
                return;
            }

            invertMaterial = new Material(shader) {
                name = "ReverseScreenInvertMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };

            var canvasObject = new GameObject("ReverseScreenInvertCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imageObject = new GameObject("Invert");
            imageObject.transform.SetParent(canvasObject.transform, false);
            image = imageObject.AddComponent<Image>();
            image.material = invertMaterial;
            image.color = Color.white;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            canvasObject.SetActive(visible);
        }
    }
}
