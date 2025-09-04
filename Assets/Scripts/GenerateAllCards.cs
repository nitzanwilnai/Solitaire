using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GenerateAllCards : MonoBehaviour
{
#if UNITY_EDITOR
    public GenerateCards[] m_cards;
    int cardIndex = 0;
    int numCards = 0;

    private void Awake()
    {
        numCards = m_cards.Length;
        for (int i = 0; i < numCards; i++)
            m_cards[i].gameObject.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        m_cards[cardIndex].gameObject.SetActive(true);
        m_cards[cardIndex].RunGenerateCards();
    }

    private void Update()
    {
        if(m_cards[cardIndex].Finished)
        {
            if (cardIndex >= numCards - 1)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            m_cards[cardIndex].gameObject.SetActive(false);
            cardIndex++;
            m_cards[cardIndex].gameObject.SetActive(true);
            m_cards[cardIndex].RunGenerateCards();
        }


    }
#endif
}
