using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class DialogueTextEffects : MonoBehaviour
{
    [Header("Letter Pop")]
    public float popScale = 1.35f;
    public float popDuration = 0.08f;

    [Header("Shake")]
    public float shakeStrength = 2f;
    public float shakeSpeed = 18f;

    [Header("Wave")]
    public float waveAmplitude = 3f;
    public float waveSpeed = 5f;
    public float waveFrequency = 0.6f;

    [Header("Special Colours")]
    public Color itemColor = new Color(1f, 0.8f, 0.15f);
    public Color dangerColor = new Color(1f, 0.2f, 0.2f);

    private TMP_Text text;
    private List<EffectRange> shakeRanges = new List<EffectRange>();
    private List<EffectRange> waveRanges = new List<EffectRange>();
    private float[] revealTimes;

    private struct OpenEffect
    {
        public int startIndex;
        public float multiplier;

        public OpenEffect(int startIndex, float multiplier)
        {
            this.startIndex = startIndex;
            this.multiplier = multiplier;
        }
    }

    private class EffectRange
    {
        public int startIndex;
        public int endIndex;
        public float multiplier;

        public EffectRange(int startIndex, int endIndex, float multiplier)
        {
            this.startIndex = startIndex;
            this.endIndex = endIndex;
            this.multiplier = multiplier;
        }
    }

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    public void PrepareText(string rawText)
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        string processedText = ParseCustomTags(rawText);

        text.text = processedText;

        TMP_TextInfo info = text.GetTextInfo(processedText);
        Debug.Log("TMP PARSED COUNT: " + info.characterCount);

        revealTimes = new float[info.characterCount];

        for (int i = 0; i < revealTimes.Length; i++)
            revealTimes[i] = -1000f;

        text.maxVisibleCharacters = 0;

        text.ForceMeshUpdate(true, true);
    }

    public void RevealCharacter(int index)
    {
        if (index < 0 || index >= text.textInfo.characterCount)
            return;

        text.maxVisibleCharacters = index + 1;

        if (revealTimes != null && index < revealTimes.Length)
            revealTimes[index] = Time.unscaledTime;
    }

    public void ShowAll()
    {
        text.maxVisibleCharacters = int.MaxValue;

        if (revealTimes != null)
        {
            for (int i = 0; i < revealTimes.Length; i++)
            {
                revealTimes[i] = -1000f;
            }
        }

        text.ForceMeshUpdate();
    }

    public void Clear()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        text.text = "";
        text.maxVisibleCharacters = int.MaxValue;
        shakeRanges.Clear();
        waveRanges.Clear();
    }

    public int GetCharacterCount()
    {
        return revealTimes != null ? revealTimes.Length : 0;
    }

    public char GetCharacter(int index)
    {
        if (index < 0 || index >= text.textInfo.characterCount)
            return ' ';

        return text.textInfo.characterInfo[index].character;
    }

    public bool IsCharacterVisible(int index)
    {
        if (index < 0 || index >= text.textInfo.characterCount)
            return false;

        return text.textInfo.characterInfo[index].isVisible;
    }

    private void LateUpdate()
    {
        if (text == null || string.IsNullOrEmpty(text.text))
            return;

        text.ForceMeshUpdate();

        TMP_TextInfo textInfo = text.textInfo;
        int visibleLimit = Mathf.Min(text.maxVisibleCharacters, textInfo.characterCount);

        for (int i = 0; i < visibleLimit; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];

            if (!characterInfo.isVisible)
                continue;

            int materialIndex = characterInfo.materialReferenceIndex;
            int vertexIndex = characterInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) / 2f;

            float scale = 1f;

            if (revealTimes != null && i < revealTimes.Length && revealTimes[i] > 0f && popDuration > 0f)
            {
                float age = Time.unscaledTime - revealTimes[i];

                if (age < popDuration)
                {
                    float t = Mathf.Clamp01(age / popDuration);
                    t = 1f - Mathf.Pow(1f - t, 3f);
                    scale = Mathf.Lerp(popScale, 1f, t);
                }
            }

            Vector3 offset = Vector3.zero;

            float shakeMultiplier = GetEffectMultiplier(shakeRanges, i);

            if (shakeMultiplier > 0f)
            {
                float jitterSpeed = 35f;
                float jitterStep = Mathf.Floor(Time.unscaledTime * jitterSpeed);

                float shakeX = Mathf.PerlinNoise(i * 17.13f, jitterStep * 0.37f) * 2f - 1f;
                float shakeY = Mathf.PerlinNoise(i * 29.71f + 50f, jitterStep * 0.41f) * 2f - 1f;

                offset.x += shakeX * shakeStrength * shakeMultiplier;
                offset.y += shakeY * shakeStrength * shakeMultiplier;
            }

            float waveMultiplier = GetEffectMultiplier(waveRanges, i);

            if (waveMultiplier > 0f)
                offset.y += Mathf.Sin(Time.unscaledTime * waveSpeed + i * waveFrequency) * waveAmplitude * waveMultiplier;

            for (int j = 0; j < 4; j++)
            {
                Vector3 vertex = vertices[vertexIndex + j];
                vertex -= center;
                vertex *= scale;
                vertex += center;
                vertex += offset;
                vertices[vertexIndex + j] = vertex;
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private float GetEffectMultiplier(List<EffectRange> ranges, int characterIndex)
    {
        float multiplier = 0f;

        foreach (EffectRange range in ranges)
        {
            if (characterIndex >= range.startIndex && characterIndex <= range.endIndex)
                multiplier = Mathf.Max(multiplier, range.multiplier);
        }

        return multiplier;
    }

    private string ParseCustomTags(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return "";

        shakeRanges.Clear();
        waveRanges.Clear();

        Stack<OpenEffect> shakeStack = new Stack<OpenEffect>();
        Stack<OpenEffect> waveStack = new Stack<OpenEffect>();
        StringBuilder output = new StringBuilder();

        int visibleIndex = 0;
        int i = 0;

        while (i < rawText.Length)
        {
            if (TryReadEffectTag(rawText, i, "shake", out int shakeLength, out float shakeMultiplier))
            {
                shakeStack.Push(new OpenEffect(visibleIndex, shakeMultiplier));
                i += shakeLength;
                continue;
            }

            if (StartsWithAt(rawText, i, "[/shake]"))
            {
                if (shakeStack.Count > 0)
                {
                    OpenEffect open = shakeStack.Pop();

                    if (visibleIndex > open.startIndex)
                        shakeRanges.Add(new EffectRange(open.startIndex, visibleIndex - 1, open.multiplier));
                }

                i += "[/shake]".Length;
                continue;
            }

            if (TryReadEffectTag(rawText, i, "wave", out int waveLength, out float waveMultiplier))
            {
                waveStack.Push(new OpenEffect(visibleIndex, waveMultiplier));
                i += waveLength;
                continue;
            }

            if (StartsWithAt(rawText, i, "[/wave]"))
            {
                if (waveStack.Count > 0)
                {
                    OpenEffect open = waveStack.Pop();

                    if (visibleIndex > open.startIndex)
                        waveRanges.Add(new EffectRange(open.startIndex, visibleIndex - 1, open.multiplier));
                }

                i += "[/wave]".Length;
                continue;
            }

            if (StartsWithAt(rawText, i, "[item]"))
            {
                string hex = ColorUtility.ToHtmlStringRGB(itemColor);
                output.Append("<color=#" + hex + ">");
                i += "[item]".Length;
                continue;
            }

            if (StartsWithAt(rawText, i, "[/item]"))
            {
                output.Append("</color>");
                i += "[/item]".Length;
                continue;
            }

            if (StartsWithAt(rawText, i, "[danger]"))
            {
                string hex = ColorUtility.ToHtmlStringRGB(dangerColor);
                output.Append("<color=#" + hex + ">");
                i += "[danger]".Length;
                continue;
            }

            if (StartsWithAt(rawText, i, "[/danger]"))
            {
                output.Append("</color>");
                i += "[/danger]".Length;
                continue;
            }

            if (StartsWithAt(rawText, i, "[color="))
            {
                int closingBracket = rawText.IndexOf(']', i);

                if (closingBracket != -1)
                {
                    string colour = rawText.Substring(i + 7, closingBracket - (i + 7));
                    output.Append("<color=" + colour + ">");
                    i = closingBracket + 1;
                    continue;
                }
            }

            if (StartsWithAt(rawText, i, "[/color]"))
            {
                output.Append("</color>");
                i += "[/color]".Length;
                continue;
            }

            output.Append(rawText[i]);
            visibleIndex++;
            i++;
        }

        while (shakeStack.Count > 0)
        {
            OpenEffect open = shakeStack.Pop();

            if (visibleIndex > open.startIndex)
                shakeRanges.Add(new EffectRange(open.startIndex, visibleIndex - 1, open.multiplier));
        }

        while (waveStack.Count > 0)
        {
            OpenEffect open = waveStack.Pop();

            if (visibleIndex > open.startIndex)
                waveRanges.Add(new EffectRange(open.startIndex, visibleIndex - 1, open.multiplier));
        }

        return output.ToString();
    }

    private bool TryReadEffectTag(string source, int index, string effectName, out int tagLength, out float multiplier)
    {
        tagLength = 0;
        multiplier = 1f;

        string normalTag = "[" + effectName + "]";

        if (StartsWithAt(source, index, normalTag))
        {
            tagLength = normalTag.Length;
            return true;
        }

        string startTag = "[" + effectName + "=";

        if (!StartsWithAt(source, index, startTag))
            return false;

        int closingBracket = source.IndexOf(']', index);

        if (closingBracket == -1)
            return false;

        int valueStart = index + startTag.Length;
        string valueText = source.Substring(valueStart, closingBracket - valueStart);

        float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier);

        if (multiplier <= 0f)
            multiplier = 1f;

        tagLength = closingBracket - index + 1;

        return true;
    }

    private bool StartsWithAt(string source, int index, string value)
    {
        if (index + value.Length > source.Length)
            return false;

        return string.Compare(source, index, value, 0, value.Length, true, CultureInfo.InvariantCulture) == 0;
    }
}