using TMPro;
using UnityEngine;

public class FPSCounterView : MonoBehaviour
{
    public TMP_Text FpsText;
    public float UpdateInterval = 1f;

    private float AccumulatedTime;
    private int FrameCount;
    private float TimeUntilUpdate;

    private void Update()
    {
        AccumulatedTime += Time.unscaledDeltaTime;
        FrameCount++;
        TimeUntilUpdate -= Time.unscaledDeltaTime;

        if (TimeUntilUpdate <= 0f)
        {
            float averageFps = FrameCount / AccumulatedTime;
            FpsText.text = Mathf.RoundToInt(averageFps).ToString();

            AccumulatedTime = 0f;
            FrameCount = 0;
            TimeUntilUpdate = UpdateInterval;
        }
    }
}
