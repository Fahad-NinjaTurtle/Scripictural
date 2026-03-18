using UnityEngine;

public class QrAnimationLine : MonoBehaviour
{
    [SerializeField] float height = 5f;
    [SerializeField] float speed = 2f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * height;
        transform.position = startPos + new Vector3(0, y, 0);
    }
}