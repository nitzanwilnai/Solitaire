using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GenerateCards : MonoBehaviour
{
    public string m_cardName;
    public Camera m_RTCamera;
    public Camera m_RTCameraHD;

    public TextMeshPro[] m_symbols;
    public TextMeshPro[] m_number;
    public SpriteRenderer m_card;

    public bool Finished = false;

    // Start is called before the first frame update
    public void RunGenerateCards()
    {
        StartCoroutine(GenerateAndSaveCards());
    }

    WaitForSeconds waitTime = new WaitForSeconds(0.5F);
    WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();

    public Color[] m_cardColors;
    public Color[] m_symbolColors;
    string[] SYMBOLS = { "\u2663", "\u2666", "\u2665", "\u2660" };
    string[] NUMBERS = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    IEnumerator GenerateAndSaveCards()
    {
        Finished = false;

        yield return waitTime;
        yield return frameEnd;

        string path = Application.dataPath + "/../GeneratedCards/HD/";

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);


        RenderTexture rtHD = m_RTCameraHD.targetTexture;
        int widthHD = rtHD.width;
        int heightHD = rtHD.height;
        Texture2D texHD = new Texture2D(widthHD, heightHD, TextureFormat.RGBA32, false);
        RenderTexture.active = m_RTCameraHD.targetTexture;

        for (int i = 0; i < 52; i++)
        {
            RenderCard(i, m_RTCameraHD);
            texHD.ReadPixels(new Rect(0, 0, widthHD, heightHD), 0, 0);
            texHD.Apply();
            byte[] bytesHD = texHD.EncodeToPNG();
            File.WriteAllBytes(path + m_cardName + "HD"+i+".png", bytesHD);
        }

        //RenderCard(26, m_RTCameraHD);
        //texHD.ReadPixels(new Rect(0, 0, widthHD, heightHD), 0, 0);
        //texHD.Apply();
        //byte[] bytesHD = texHD.EncodeToPNG();
        //File.WriteAllBytes(path + m_cardName + "HD1.png", bytesHD);

        //RenderCard(0, m_RTCameraHD);
        //texHD.ReadPixels(new Rect(0, 0, widthHD, heightHD), 0, 0);
        //texHD.Apply();
        //bytesHD = texHD.EncodeToPNG();
        //File.WriteAllBytes(path + m_cardName + "HD2.png", bytesHD);

        path = Application.dataPath + "/../GeneratedCards/" + m_cardName + "/";
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        RenderTexture rt = m_RTCamera.targetTexture;
        int width = rt.width;
        int height = rt.height;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = m_RTCamera.targetTexture;
        for (int i = 0; i < 52; i++)
        {
            RenderCard(i, m_RTCamera);

            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();

            File.WriteAllBytes(path + i + ".png", bytes);
        }
        Object.Destroy(tex);
        RenderTexture.active = null;

        Finished = true;
    }

    void RenderCard(int i, Camera camera)
    {
        for (int j = 0; j < m_number.Length; j++)
        {
            m_number[j].text = NUMBERS[i % 13];
            m_number[j].color = m_symbolColors[i / 13];
        }
        for (int j = 0; j < m_symbols.Length; j++)
        {
            m_symbols[j].text = SYMBOLS[i / 13];
            m_symbols[j].color = m_symbolColors[i / 13];
        }

        m_card.color = m_cardColors[i / 13];

        camera.Render();

    }
}
