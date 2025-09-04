using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteScaler : MonoBehaviour
{
    int m_oldWidth;

    private void Awake()
    {
        m_oldWidth = Screen.width;
        ScaleScreen();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Screen.width != m_oldWidth)
        {
            ScaleScreen();
        }
    }

    void ScaleScreen()
    {
        float width = Screen.width;
        float height = Screen.height;
        float ratio = width / height;
        if (ratio < (9.0f / 16.0f))
        {
            float defaultWidth = 1080.0f;
            float newWidth = ratio * 1920.0f;
            Debug.LogFormat("defaultWidth {0} newWidth {1}", defaultWidth, newWidth);

            transform.localScale = Vector3.one * (newWidth / 1080.0f);
        }
        else
            transform.localScale = Vector3.one;

        m_oldWidth = Screen.width;
    }
}
