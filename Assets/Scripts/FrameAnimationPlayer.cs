using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FrameAnimationPlayer : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 30f;
    public bool loop = false;

    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (frames != null && frames.Length > 0)
        {
            StartCoroutine(PlayAnimation());
        }
    }

    IEnumerator PlayAnimation()
    {
        int frameIndex = 0;
        float delay = 1f / fps;

        while (frameIndex < frames.Length)
        {
            image.sprite = frames[frameIndex];
            frameIndex++;

            if (frameIndex >= frames.Length && !loop)
            {
                break;
            }

            if (loop && frameIndex >= frames.Length)
            {
                frameIndex = 0;
            }

            yield return new WaitForSeconds(delay);
        }
    }
}
