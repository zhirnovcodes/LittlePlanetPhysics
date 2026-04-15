using TMPro;
using UnityEngine;

public class FPSCounterView : MonoBehaviour
{
    public TMP_Text FpsText;

    private void Update()
    {
        int fps = Mathf.RoundToInt(1f / Time.smoothDeltaTime);
        FpsText.text = fps.ToString();
    }
}
