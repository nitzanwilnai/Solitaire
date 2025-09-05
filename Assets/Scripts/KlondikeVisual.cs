using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ColumnType { WASTE, PILE, STACK };

public struct SelectedCard
{
    public sbyte Index;
    public int Column;
    public float DistanceX;
    public float DistanceY;
    public Vector3 Pos;
    public ColumnType DestType;
    public ColumnType SrcType;
}

public class KlondikeVisual : MonoBehaviour
{
    [Header("UI")]
    public GameObject m_victoryText;
    public GameObject m_autoCompleteText;
    public GameObject m_undoButton;

    [Header("Card Sprites")]
    public GameObject m_cardPrefab;
    public Transform m_cardPoolParent;
    public SpriteRenderer[] m_cardPiles;
    public GameObject[] m_cardPilesBkg;
    public SpriteRenderer m_cardStock;
    public SpriteRenderer[] m_stacks;
    public GameObject[] m_stacksBkg;
    public SpriteRenderer[] m_waste;

    Vector3[] m_cardPosition;
    Vector3[] m_cardPosTarget;
    Vector3[] m_cardOffset;
    float[] m_cardVelocity;
    float[] m_cardMoving;

    SpriteRenderer[] m_cards;
    SpriteRenderer[] m_cardShadows;
    Animation[] m_cardAnimations;
    Transform[] m_cardTransforms;

    Camera m_mainCamera;

    // Input
    sbyte[] m_tempSelectedCards = new sbyte[52];
    int m_tempSelectedCardsCount = 0;

    sbyte m_wasteSelectedCard = -1;

    Vector3 m_mouseDownPosition;
    sbyte[] m_selectedCardsIndices;
    int m_selectedCardsCount = 0;
    int m_selectedCardColumn = -1;
    SelectedCard m_selectedCard = new SelectedCard();

    bool m_cardStockClicked = false;

    float YOFFSET = 0.25f;

    Color m_darkColor = Color.gray;

    public void Init(Camera camera)
    {
        m_mainCamera = camera;

        m_cards = new SpriteRenderer[52];
        m_cardShadows = new SpriteRenderer[52];
        m_cardAnimations = new Animation[52];
        m_cardTransforms = new Transform[52];
        m_cardPosition = new Vector3[52];
        m_cardPosTarget = new Vector3[52];
        m_cardOffset = new Vector3[52];
        m_selectedCardsIndices = new sbyte[52];
        m_cardMoving = new float[52];
        m_cardVelocity = new float[52];

        for (int i = 0; i < 52; i++)
        {
            GameObject card = Instantiate(m_cardPrefab);
            card.transform.SetParent(m_cardPoolParent);
            card.name = ((CARD_STATE)(i / 13)).ToString() + (i % 13).ToString();
            card.transform.localScale = m_cardPrefab.transform.localScale;
            m_cardTransforms[i] = card.transform;
            m_cards[i] = card.GetComponentInChildren<SpriteRenderer>();
            m_cardShadows[i] = m_cards[i].transform.Find("Shadow").GetComponent<SpriteRenderer>();
            m_cardShadows[i].enabled = false;
            m_cardAnimations[i] = card.GetComponent<Animation>();
        }
    }

    public void ShowVictory(bool show)
    {
        m_victoryText.SetActive(show);
    }

    public void ShowAutoComplete(bool show)
    {
        m_autoCompleteText.SetActive(show);
    }

    public void StartGame(byte[] cardsShuffled, CardPackScriptableObject cardPack)
    {
        for (int i = 0; i < 52; i++)
        {
            int index = cardsShuffled[i];
            m_cardPosTarget[index].x = m_cardPosition[index].x = m_cardStock.transform.position.x;
            m_cardPosTarget[index].y = m_cardPosition[index].y = m_cardStock.transform.position.y;
            m_cardPosTarget[index].z = m_cardPosition[index].z = -i - 1;
            m_cards[i].sprite = cardPack.Back;
            m_cards[i].color = Color.white;
            m_cards[i].enabled = true;
            m_cardMoving[i] = 0.0f;
            m_cardVelocity[i] = 0.0f;
        }
        for (int i = 0; i < 7; i++)
            m_cardPilesBkg[i].SetActive(false);
        for (int i = 0; i < 4; i++)
            m_stacksBkg[i].SetActive(false);

        ShowVictory(false);

        GameManager.Instance.SaveMove();
    }

    public void HandleInput(ref GameData gameData, ref ReplayData replayData)
    {
#if UNITY_EDITOR
        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseMove = Input.GetMouseButton(0);
        bool mouseUp = Input.GetMouseButtonUp(0);
        Vector3 mousePosition = Input.mousePosition;
#else
            bool mouseDown = (Input.touchCount > 0) && Input.GetTouch(0).phase == TouchPhase.Began;
            bool mouseMove = (Input.touchCount > 0) && Input.GetTouch(0).phase == TouchPhase.Moved;
            bool mouseUp = (Input.touchCount > 0) && (Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(0).phase == TouchPhase.Canceled);
            Vector3 mousePosition = Vector3.zero;
            if (Input.touchCount > 0)
                mousePosition = Input.GetTouch(0).position;
#endif

        Vector3 worldPosition = m_mainCamera.ScreenToWorldPoint(mousePosition);
        if (mouseDown)
        {
            m_mouseDownPosition = worldPosition;

            m_tempSelectedCardsCount = 0;
            m_selectedCardsCount = 0;
            m_selectedCardColumn = -1;
            m_cardStockClicked = false;

            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < gameData.CardPileCount[i]; j++)
                {
                    byte index = gameData.CardPileIndices[i][j];
                    CARD_STATE cardState = gameData.CardPileState[i][j];
                    float distance = Vector3.Distance(worldPosition, m_cards[index].transform.position);
                    if (IsPositionInSprite(worldPosition, m_cards[index]) && cardState == CARD_STATE.REVEALED)
                    {
                        m_tempSelectedCards[m_tempSelectedCardsCount++] = (sbyte)index;
                        m_selectedCardColumn = i;
                    }
                }
            }

            float lowestZ = 0.0f;
            sbyte selectedCardIndex = -1;
            for (int i = 0; i < m_tempSelectedCardsCount; i++)
            {
                sbyte index = m_tempSelectedCards[i];
                if (m_cardPosTarget[index].z < lowestZ)
                {
                    lowestZ = m_cardPosTarget[index].z;
                    selectedCardIndex = index;
                }
            }


            if (selectedCardIndex > -1)
            {
                m_selectedCardsIndices[m_selectedCardsCount++] = selectedCardIndex;
                for (int i = 0; i < gameData.CardPileCount[m_selectedCardColumn]; i++)
                {
                    byte index = gameData.CardPileIndices[m_selectedCardColumn][i];
                    if (index != selectedCardIndex && m_cardPosTarget[index].z < m_cardPosTarget[selectedCardIndex].z)
                        m_selectedCardsIndices[m_selectedCardsCount++] = (sbyte)index;
                }

                m_cardShadows[m_selectedCardsIndices[m_selectedCardsCount - 1]].enabled = true;

                Debug.LogFormat("card {0} clicked!", m_cards[selectedCardIndex].name);
            }

            m_wasteSelectedCard = -1;
            if (gameData.CardStockIndex >= 0 && IsPositionInSprite(worldPosition, m_waste[0]))
            {
                m_wasteSelectedCard = (sbyte)gameData.CardStockIndices[gameData.CardStockIndex];
                m_cardShadows[m_wasteSelectedCard].enabled = true;
            }

            if (IsPositionInSprite(worldPosition, m_cardStock))
                m_cardStockClicked = true;

            for (int i = 0; i < 52; i++)
                m_cardOffset[i] = Vector3.zero;

            KlondikeLogic.DebugPrint(gameData, "Mouse Down\n");
        }
        if (mouseMove)
        {
            for (int i = 0; i < m_selectedCardsCount; i++)
            {
                int index = m_selectedCardsIndices[i];
                m_cardOffset[index] = worldPosition - m_mouseDownPosition;
                m_cardOffset[index].z = -53.0f;
            }
            if (m_wasteSelectedCard > -1)
            {
                m_cardOffset[m_wasteSelectedCard] = worldPosition - m_mouseDownPosition;
                m_cardOffset[m_wasteSelectedCard].z = -53.0f;
            }

            for (int i = 0; i < 52; i++)
                m_cards[i].color = Color.white;
            for (int i = 0; i < 7; i++)
                m_cardPilesBkg[i].SetActive(false);
            for (int i = 0; i < 4; i++)
                m_stacksBkg[i].SetActive(false);


            if (m_wasteSelectedCard > -1 || m_selectedCardsCount > 0)
            {
                m_selectedCard.DistanceX = m_selectedCard.DistanceY = 100.0f;
                gameData.HighlightedIndex = -1;

                if (m_selectedCardsCount > 0)
                {
                    m_selectedCard.SrcType = ColumnType.PILE;
                    GetClosestPosition(gameData, worldPosition, (byte)m_selectedCardsIndices[0], m_selectedCardsIndices.Length, ref m_selectedCard);
                    if (m_selectedCard.DistanceX < 0.5f && m_selectedCard.DistanceY < 1.0f)
                    {
                        if (m_selectedCard.Index > -1)
                            m_cards[m_selectedCard.Index].color = m_darkColor;
                        else if (m_selectedCard.DestType == ColumnType.PILE)
                            m_cardPilesBkg[m_selectedCard.Column].SetActive(true);
                        else if (m_selectedCard.DestType == ColumnType.STACK)
                            m_stacksBkg[m_selectedCard.Column].SetActive(true);
                    }
                }
                else if (m_wasteSelectedCard > -1)
                {
                    m_selectedCard.SrcType = ColumnType.WASTE;
                    GetClosestPosition(gameData, worldPosition, (byte)m_wasteSelectedCard, 1, ref m_selectedCard);
                    if (m_selectedCard.DistanceX < 0.5f && m_selectedCard.DistanceY < 1.0f)
                    {
                        if (m_selectedCard.Index > -1)
                            m_cards[m_selectedCard.Index].color = m_darkColor;
                        else if (m_selectedCard.DestType == ColumnType.PILE)
                            m_cardPilesBkg[m_selectedCard.Column].SetActive(true);
                        else if (m_selectedCard.DestType == ColumnType.STACK)
                            m_stacksBkg[m_selectedCard.Column].SetActive(true);
                    }
                }

            }
        }
        if (mouseUp)
        {
            Vector3 offset = worldPosition - m_mouseDownPosition;
            float clickDistance = offset.magnitude;
            offset.z = -53.0f;

            if (m_wasteSelectedCard > -1 || m_selectedCardsCount > 0)
            {

                sbyte selectedCardIndex = (m_wasteSelectedCard > -1) ? m_wasteSelectedCard : m_selectedCardsIndices[m_selectedCardsCount - 1];
                m_selectedCard.SrcType = (m_wasteSelectedCard > -1) ? ColumnType.WASTE : ColumnType.PILE;

                if (clickDistance < 0.2f)
                {
                    bool cardMoved = false;
                    for (int stackIdx = 0; stackIdx < 4; stackIdx++)
                    {
                        if (KlondikeLogic.CanPlaceCardInStack(gameData, selectedCardIndex, stackIdx))
                        {
                            if (m_selectedCard.SrcType == ColumnType.PILE)
                            {
                                KlondikeLogic.MoveCardFromColumnToStack(ref gameData, (byte)selectedCardIndex, m_selectedCardColumn, stackIdx);
                                replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_STACK, (byte)selectedCardIndex, (byte)m_selectedCardColumn, (byte)stackIdx, gameData.GameTime);
                                GameManager.Instance.SaveMove();
                                m_cardPosition[selectedCardIndex] += offset;
                                cardMoved = true;
                            }
                            else if (m_selectedCard.SrcType == ColumnType.WASTE)
                            {
                                KlondikeLogic.MoveCardFromWasteToStack(ref gameData, (byte)selectedCardIndex, stackIdx);
                                replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_STACK, (byte)selectedCardIndex, 0, (byte)stackIdx, gameData.GameTime);
                                GameManager.Instance.SaveMove();
                                m_cardPosition[selectedCardIndex] += offset;
                                cardMoved = true;
                            }
                            break;
                        }
                    }
                    if (!cardMoved)
                        if (m_wasteSelectedCard > -1)
                            m_cardAnimations[m_wasteSelectedCard].Play("card shake");
                        else if (m_selectedCardsCount == 1)
                            m_cardAnimations[m_selectedCardsIndices[0]].Play("card shake");
                }
                else
                {
                    selectedCardIndex = (m_wasteSelectedCard > -1) ? m_wasteSelectedCard : m_selectedCardsIndices[0];
                    int numCards = (m_wasteSelectedCard > -1) ? 1 : m_selectedCardsCount;
                    GetClosestPosition(gameData, worldPosition, (byte)selectedCardIndex, numCards, ref m_selectedCard);
                    if (m_selectedCard.DistanceX < 0.5f && m_selectedCard.DistanceY < 1.0f)
                    {
                        if (m_selectedCard.SrcType == ColumnType.PILE && m_selectedCard.DestType == ColumnType.PILE)
                        {
                            for (int i = 0; i < m_selectedCardsCount; i++)
                            {
                                byte selectedIndex = (byte)m_selectedCardsIndices[i];
                                KlondikeLogic.MoveCardFromColumnToColumn(ref gameData, selectedIndex, m_selectedCardColumn, m_selectedCard.Column);
                                m_cardPosition[selectedIndex] += new Vector3(offset.x, offset.y, offset.z + m_cardPosition[selectedIndex].z);
                            }
                            replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_COLUMN, (byte)m_selectedCardsIndices[0], (byte)m_selectedCardColumn, (byte)m_selectedCard.Column, gameData.GameTime);
                            GameManager.Instance.SaveMove();
                        }
                        else if (m_selectedCard.SrcType == ColumnType.PILE && m_selectedCard.DestType == ColumnType.STACK)
                        {
                            KlondikeLogic.MoveCardFromColumnToStack(ref gameData, (byte)m_selectedCardsIndices[0], (int)m_selectedCardColumn, (int)m_selectedCard.Column);
                            replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_STACK, (byte)m_selectedCardsIndices[0], (byte)m_selectedCardColumn, (byte)m_selectedCard.Column, gameData.GameTime);
                            GameManager.Instance.SaveMove();
                            m_cardPosition[m_selectedCardsIndices[0]] += offset;
                        }
                        else if (m_selectedCard.SrcType == ColumnType.WASTE && m_selectedCard.DestType == ColumnType.PILE)
                        {
                            KlondikeLogic.MoveCardFromWasteToColumn(ref gameData, (byte)m_wasteSelectedCard, m_selectedCard.Column);
                            replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_COLUMN, (byte)m_wasteSelectedCard, 0, (byte)m_selectedCard.Column, gameData.GameTime);
                            GameManager.Instance.SaveMove();
                            m_cardPosition[m_wasteSelectedCard] += offset;
                        }
                        else if (m_selectedCard.SrcType == ColumnType.WASTE && m_selectedCard.DestType == ColumnType.STACK)
                        {
                            KlondikeLogic.MoveCardFromWasteToStack(ref gameData, (byte)m_wasteSelectedCard, (int)m_selectedCard.Column);
                            replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_STACK, (byte)m_wasteSelectedCard, 0, (byte)m_selectedCard.Column, gameData.GameTime);
                            GameManager.Instance.SaveMove();
                            m_cardPosition[m_wasteSelectedCard] += offset;
                        }
                    }
                }
            }

            if (m_cardStockClicked)
            {
                Debug.Log("m_cardStock clicked");
                KlondikeLogic.MoveCardsToWaste(ref gameData);
                replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.MOVE_TO_WASTE, (byte)gameData.CardStockIndex, 0, 0, gameData.GameTime);
                GameManager.Instance.SaveMove();

                KlondikeLogic.DebugPrint(gameData, "MoveCardsToWaste");
            }

            if (m_selectedCardsCount > 0)
                m_cardShadows[m_selectedCardsIndices[m_selectedCardsCount - 1]].enabled = false;
            m_selectedCardsCount = 0;

            if (m_wasteSelectedCard > -1)
                m_cardShadows[m_wasteSelectedCard].enabled = false;
            m_wasteSelectedCard = -1;

            for (int i = 0; i < 52; i++)
                m_cardOffset[i] = Vector3.zero;

            for (int i = 0; i < 52; i++)
                m_cards[i].color = Color.white;
            for (int i = 0; i < 7; i++)
                m_cardPilesBkg[i].SetActive(false);
            for (int i = 0; i < 4; i++)
                m_stacksBkg[i].SetActive(false);

            gameData.HighlightedIndex = -1;

            KlondikeLogic.DebugPrint(gameData, "Mouse Up\n");
        }

    }

    public void GetClosestPosition(GameData gameData, Vector3 worldPosition, byte selectedCardIndex, int numCards, ref SelectedCard selectedCard)
    {
        float minDistance = float.MaxValue;
        for (int i = 0; i < 7; i++)
        {
            if (gameData.CardPileCount[i] == 0 && KlondikeLogic.CanPlaceCardInEmptyColumn(gameData, selectedCardIndex, i))
            {
                float distance = Vector2.Distance(worldPosition, m_cardPiles[i].transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    selectedCard.Column = i;
                    selectedCard.Index = -1;
                    selectedCard.DistanceX = Mathf.Abs((worldPosition - m_cardPiles[i].transform.position).x);
                    selectedCard.DistanceY = Mathf.Abs((worldPosition - m_cardPiles[i].transform.position).y);
                    selectedCard.Pos = m_cardPiles[i].transform.position;
                    selectedCard.DestType = ColumnType.PILE;
                }
            }
            else if (gameData.CardPileCount[i] > 0)
            {
                byte dropIndex = gameData.CardPileIndices[i][gameData.CardPileCount[i] - 1];
                if (selectedCardIndex != dropIndex)
                {
                    float distance = Vector2.Distance(worldPosition, m_cards[dropIndex].transform.position);
                    if (distance < minDistance && KlondikeLogic.CanPlaceCardInColumn(selectedCardIndex, dropIndex))
                    {
                        minDistance = distance;
                        selectedCard.Column = i;
                        selectedCard.Index = (sbyte)dropIndex;
                        selectedCard.DistanceX = Mathf.Abs((worldPosition - m_cards[dropIndex].transform.position).x);
                        selectedCard.DistanceY = Mathf.Abs((worldPosition - m_cards[dropIndex].transform.position).y);
                        selectedCard.Pos = m_cards[dropIndex].transform.position;
                        selectedCard.DestType = ColumnType.PILE;
                    }
                }
            }
        }

        if (numCards == 1)
        {
            for (int stackIdx = 0; stackIdx < 4; stackIdx++)
            {
                float distance = Vector2.Distance(worldPosition, m_stacks[stackIdx].transform.position);
                if (distance < minDistance && KlondikeLogic.CanPlaceCardInStack(gameData, selectedCardIndex, stackIdx))
                {
                    minDistance = distance;
                    selectedCard.Column = stackIdx;
                    selectedCard.Index = gameData.CardStackCount[stackIdx] > 0 ? (sbyte)gameData.CardStacks[stackIdx][gameData.CardStackCount[stackIdx] - 1] : (sbyte)-1;
                    selectedCard.DistanceX = Mathf.Abs((worldPosition - m_stacks[stackIdx].transform.position).x);
                    selectedCard.DistanceY = Mathf.Abs((worldPosition - m_stacks[stackIdx].transform.position).y);
                    selectedCard.Pos = m_stacks[stackIdx].transform.position;
                    selectedCard.DestType = ColumnType.STACK;
                }
            }
        }
    }

    public void SyncVisuals(GameData gameData, bool showUndo, CardPackScriptableObject cardPack)
    {
        MoveCards(Time.deltaTime);

        int index = -1;
        for (int i = 0; i < 7; i++)
        {
            float yOffset = 0.0f;
            for (int j = 0; j < gameData.CardPileCount[i]; j++)
            {
                index = gameData.CardPileIndices[i][j];
                m_cardPosTarget[index].x = m_cardPiles[i].transform.position.x;
                m_cardPosTarget[index].y = m_cardPiles[i].transform.position.y - yOffset;
                m_cardPosTarget[index].z = -j - 1;
                m_cardVelocity[index] = 2.5f;

                m_cards[index].sprite = gameData.CardPileState[i][j] == CARD_STATE.HIDDEN ? cardPack.Back : cardPack.Cards[index];

                yOffset += gameData.CardPileState[i][j] == CARD_STATE.HIDDEN ? YOFFSET / 2.0f : YOFFSET;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < gameData.CardStackCount[i]; j++)
            {
                index = gameData.CardStacks[i][j];
                m_cardPosTarget[index].x = m_stacks[i].transform.position.x;
                m_cardPosTarget[index].y = m_stacks[i].transform.position.y;
                m_cardPosTarget[index].z = -j - 30;
                m_cards[index].sprite = cardPack.Cards[index];
                m_cardVelocity[index] = 2.5f;
            }
        }

        for (int i = 0; i <= gameData.CardStockIndex - 3; i++)
        {
            index = gameData.CardStockIndices[i];
            m_cardPosTarget[index].x = m_waste[2].transform.position.x;
            m_cardPosTarget[index].y = m_waste[2].transform.position.y;
            m_cardPosTarget[index].z = i - 3;
            m_cards[index].enabled = false;
            m_cardVelocity[index] = 2.5f;
        }

        int wasteIndex = 0;
        for (int i = gameData.CardStockIndex; i > gameData.CardStockIndex - 3; i--)
        {
            if (i >= 0 && i < gameData.CardStockCount)
            {
                index = gameData.CardStockIndices[i];
                m_cards[index].sprite = cardPack.Cards[index];
                m_cardPosTarget[index].x = m_waste[wasteIndex].transform.position.x;
                m_cardPosTarget[index].y = m_waste[wasteIndex].transform.position.y;
                m_cardPosTarget[index].z = wasteIndex - 3;
                m_cards[index].enabled = true;
                wasteIndex++;
                m_cardVelocity[index] = 2.5f + wasteIndex;
            }
        }

        for (int i = gameData.CardStockIndex + 1; i < gameData.CardStockCount; i++)
        {
            index = gameData.CardStockIndices[i];
            m_cards[index].sprite = cardPack.Back;
            m_cardPosTarget[index].x = m_cardStock.transform.position.x;
            m_cardPosTarget[index].y = m_cardStock.transform.position.y;
            m_cardPosTarget[index].z = -i - 1;
            m_cards[index].enabled = true;
            m_cardVelocity[index] = 2.5f + i;
        }

        for (int i = 0; i < 52; i++)
        {
            Vector3 cardPosition = m_cardPosition[i] + m_cardOffset[i];
            cardPosition.z += (m_cardMoving[i] * -50.0f);
            m_cardTransforms[i].position = cardPosition;

        }

        if (m_undoButton.activeSelf != showUndo)
            m_undoButton.SetActive(showUndo);
    }

    void MoveCards(float dt)
    {
        for (int i = 0; i < 52; i++)
        {
            m_cardPosition[i].z = m_cardPosTarget[i].z;
            Vector3 diff = m_cardPosTarget[i] - m_cardPosition[i];
            float distance = diff.magnitude;
            float velocity = dt * m_cardVelocity[i] * 2.0f;

            if (distance > velocity)
            {
                Vector3 cardPosition = m_cardPosition[i] + (diff.normalized * velocity);
                m_cardPosition[i] = cardPosition;
                m_cardMoving[i] = 1.0f;
            }
            else
            {
                m_cardPosition[i] = m_cardPosTarget[i];
                m_cardMoving[i] = 0.0f;
            }
        }
    }

    bool IsPositionInSprite(Vector3 position, SpriteRenderer spriteRenderer)
    {
        Vector3 center = spriteRenderer.transform.position;
        Vector3 size = spriteRenderer.bounds.size * 0.5f;
        if (position.x > (center.x - size.x) && position.x < (center.x + size.x))
            if (position.y > (center.y - size.y) && position.y < (center.y + size.y))
                return true;
        return false;
    }
}
