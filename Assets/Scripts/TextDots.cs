using System.Collections;
using TMPro;
using UnityEngine;

public class TextDots : MonoBehaviour
{
    private TextMeshProUGUI m_TextMeshPro;
    [SerializeField] float duration = 0.5f;

    private string baseText;

    private void Awake()
    {
        m_TextMeshPro = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        baseText = m_TextMeshPro.text;
        StartCoroutine(CreateDots());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator CreateDots()
    {
        int count = 0;

        while (true)
        {
            int dotCount = count % 4; 
            string dots = new string('.', dotCount);

            m_TextMeshPro.text = baseText + dots;

            count++;
            yield return new WaitForSeconds(duration);
        }
    }
}
