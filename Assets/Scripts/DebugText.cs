using TMPro;
using UnityEngine;

public class DebugText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;

    public void UpdateRotationTexts(Transform parent, Transform rawImage)
    {
        if (textMeshPro == null || parent == null || rawImage == null)
            return;

        textMeshPro.text =
            $"Parent Local: {parent.localEulerAngles}\n" +
            $"Parent World: {parent.eulerAngles}\n\n" +
            $"Raw Local: {rawImage.localEulerAngles}\n" +
            $"Raw World: {rawImage.eulerAngles}";
    }
}