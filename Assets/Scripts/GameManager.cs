using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public enum CARD_STATE { HIDDEN, REVEALED, HIGHLIGHTED };
public enum CARD_SUITE { CLUBS, DIAMONDS, HEARTS, SPADES };

[Serializable]
public struct GameData
{
    public byte[][] CardPileIndices;
    public CARD_STATE[][] CardPileState;
    public byte[] CardPileCount;

    public byte[][] CardStacks;
    public byte[] CardStackCount;

    public byte[] CardStockIndices;
    public byte CardStockCount;
    public sbyte CardStockIndex;

    public sbyte HighlightedIndex;

    public int Score;
    public float GameTime;

    public GAME_STATE GameState;
}

public enum MOVE_TYPE { COLUMN_TO_COLUMN, COLUMN_TO_STACK, WASTE_TO_COLUMN, WASTE_TO_STACK, MOVE_TO_WASTE };
[Serializable]
public struct PlayerMove
{
    public MOVE_TYPE MoveType;
    public byte PickupIndex;
    public byte PickupColumn;
    public byte DropColumn;
    public float Time;

    public PlayerMove(MOVE_TYPE moveType, byte pickupIndex, byte pickupColumn, byte dropColumn, float time)
    {
        MoveType = moveType;
        PickupIndex = pickupIndex;
        PickupColumn = pickupColumn;
        DropColumn = dropColumn;
        Time = time;
    }
}

[Serializable]
public struct ReplayData
{
    public PlayerMove[] PlayerMoves;
    public int PlayerMoveCount;
    public int PlayerMoveIndex;
}


public enum GAME_STATE { DEMO, PRE_GAME, IN_GAME, AUTO_COMPLETE, VICTORY };
public class GameManager : MonoBehaviourSingleton<GameManager>
{
    public enum MENU_STATE { PRIVACY_POLICY, PRIVACY_POLICY_DETAILS, MAIN_MENU, IN_GAME, GAME_OVER, PAUSE, QUIT_CONFIRMATION, RETRY_CONFIRMATION };
    public MENU_STATE MenuState;

    public static int[] SUITE_COLORS = new int[] { 0, 1, 1, 0 };

    public Camera MainCamera;

    [Header("UI")]
    public GameObject UIMainMenu;
    public GameObject UIInGame;
    public GameObject SpritesParent;
    public GameObject UIGameOver;
    public GameObject UIPauseMenu;
    public GameObject UIQuitConfirmation;
    public GameObject UIRetryConfirmation;
    public GameObject UIPrivacyPolicy;
    public GameObject UIPrivacyPolicyDetails;
    public TextMeshProUGUI SeedText;
    public Transform PrevCardTransform;
    public Transform CurrentCardTransform;
    public Transform NextCardTransform;
    Image[] m_prevCardImages;
    Image[] m_currentCardImages;
    Image[] m_nextCardImages;
    public Transform SelectionIndicatorParent;
    public GameObject SelectionIndicator;
    List<Image> m_selectionIndicators = new List<Image>();

    [Header("Card Packs")]
    public CardPackScriptableObject[] CardPacks;
    int CardPackIndex;

    GameData m_gameData;

    public static byte[] TempCardsShuffled = new byte[52];

    int m_seed;
    ReplayData m_replayData;

    public KlondikeVisual KlondikeVisual;

    float m_autoCompleteTimer = 0.0f;
    static float AUTO_COMPLETE_TIME = 0.25f;

    float m_victoryTime = 0.0f;
    static float VICTORY_TIME = 2.0f;

    float m_demoMoveTimer = 0.0f;
    static float DEMO_MOVE_TIME = 0.5f;

    bool m_mouseDown = false;
    float m_mouseDownTime = 0.0f;
    Vector3 m_mouseDownPosition;
    Vector3 m_prevCardsOrigPos;
    Vector3 m_currentCardsOrigPos;
    Vector3 m_nextCardsOrigPos;
    float m_offsetCurrent;
    float m_offsetTarget;

    public static float SHUFFLE_TIME = 2.0f;

#if UNITY_EDITOR
    int screenshotCounter = 0;
#endif

    protected override void Awake()
    {
        base.Awake();

        KlondikeLogic.AllocateGameData(ref m_gameData);
        KlondikeVisual.Init(MainCamera);

        m_replayData.PlayerMoves = new PlayerMove[1024];

        DateTimeOffset dto = DateTimeOffset.Now;
        KlondikeLogic.seed = (int)(dto.ToUnixTimeSeconds() % int.MaxValue);


        int numCardPacks = CardPacks.Length;
        m_selectionIndicators.Add(SelectionIndicator.GetComponent<Image>());
        for (int i = 1; i < numCardPacks; i++)
        {
            GameObject indicator = Instantiate(SelectionIndicator, SelectionIndicatorParent);
            m_selectionIndicators.Add(indicator.GetComponent<Image>());
            m_selectionIndicators[i].color = new Color(1.0f, 1.0f, 1.0f, 0.5f);
        }

        m_prevCardImages = PrevCardTransform.GetComponentsInChildren<Image>();
        m_currentCardImages = CurrentCardTransform.GetComponentsInChildren<Image>();
        m_nextCardImages = NextCardTransform.GetComponentsInChildren<Image>();

        SetMenuState(MENU_STATE.MAIN_MENU);
    }

    // Start is called before the first frame update
    void Start()
    {
        m_prevCardsOrigPos = PrevCardTransform.transform.position;
        m_currentCardsOrigPos = CurrentCardTransform.transform.position;
        m_nextCardsOrigPos = NextCardTransform.transform.position;

#if UNITY_EDITOR
        PlayerPrefs.SetInt("privacy", 0);
#endif

        if (PlayerPrefs.GetInt("privacy") != 1)
            SetMenuState(MENU_STATE.PRIVACY_POLICY);
        else
            SetMenuState(MENU_STATE.MAIN_MENU);
    }

    public void SetMenuState(MENU_STATE newMenuState)
    {
        MenuState = newMenuState;

        UIMainMenu.SetActive(MenuState == MENU_STATE.MAIN_MENU);
        UIInGame.SetActive(MenuState == MENU_STATE.IN_GAME);
        UIGameOver.SetActive(MenuState == MENU_STATE.GAME_OVER);
        UIPauseMenu.SetActive(MenuState == MENU_STATE.PAUSE);
        UIQuitConfirmation.SetActive(MenuState == MENU_STATE.QUIT_CONFIRMATION);
        UIRetryConfirmation.SetActive(MenuState == MENU_STATE.RETRY_CONFIRMATION);
        UIPrivacyPolicy.SetActive(MenuState == MENU_STATE.PRIVACY_POLICY);
        UIPrivacyPolicyDetails.SetActive(MenuState == MENU_STATE.PRIVACY_POLICY_DETAILS);

        if (MenuState == MENU_STATE.MAIN_MENU)
        {
            StartNewDemo(ref m_gameData, ref m_replayData);
        }
    }

    public static void SetGameState(ref GameData gameData, GAME_STATE newGameState)
    {
        gameData.GameState = newGameState;
    }

    public void StartNewDemo(ref GameData gameData, ref ReplayData replayData)
    {
        KlondikeLogic.ResetGameData(ref gameData);

        replayData.PlayerMoveCount = 0;

        // 3, 5 ok
        KlondikeLogic.seed = m_seed = RandomSeeds.ReadRandomSeed();

        Debug.LogFormat("StartNewDemo seed {0}", m_seed);

        KlondikeLogic.ShuffleCards(ref TempCardsShuffled);

        KlondikeVisual.StartGame(TempCardsShuffled, CardPacks[CardPackIndex]);


        // needs to into klondike logic!
        int count = 0;
        for (int i = 0; i < 7; i++)
        {
            gameData.CardPileCount[i] = (byte)(i + 1);
            for (int j = 0; j < gameData.CardPileCount[i]; j++)
            {
                gameData.CardPileIndices[i][j] = TempCardsShuffled[count++];
                gameData.CardPileState[i][j] = CARD_STATE.HIDDEN;
            }
            gameData.CardPileState[i][gameData.CardPileCount[i] - 1] = CARD_STATE.REVEALED;
        }

        gameData.CardStockCount = (byte)(52 - count);
        for (int i = 0; i < gameData.CardStockCount; i++)
            gameData.CardStockIndices[i] = TempCardsShuffled[i + count];
        gameData.CardStockIndex = -1;

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 13; j++)
                gameData.CardStacks[i][j] = 0;

        SetGameState(ref m_gameData, GAME_STATE.DEMO);
        m_demoMoveTimer = DEMO_MOVE_TIME * 4.0f;

    }

    public void StartRandomGame()
    {
        int seed = RandomSeeds.ReadRandomSeed();
        StartGame(ref m_gameData, seed, ref m_replayData);
    }

    public void Retry()
    {
        StartGame(ref m_gameData, m_seed, ref m_replayData);
    }

    void StartGame(ref GameData gameData, int seed, ref ReplayData replayData)
    {
        SetMenuState(MENU_STATE.IN_GAME);

        replayData.PlayerMoveCount = 0;

        m_seed = seed;
        KlondikeLogic.StartGame(ref gameData, seed, TempCardsShuffled);


        KlondikeVisual.StartGame(TempCardsShuffled, CardPacks[CardPackIndex]);


        SetGameState(ref m_gameData, GAME_STATE.IN_GAME);

        m_autoCompleteTimer = 0.0f;
        //LoadReplay();
    }

    // Update is called once per frame
    void Update()
    {
        SeedText.text = m_seed.ToString();

        if (MenuState == MENU_STATE.IN_GAME)
        {
            m_gameData.GameTime += Time.deltaTime;

            if (m_gameData.GameTime > SHUFFLE_TIME)
                KlondikeVisual.HandleInput(ref m_gameData, ref m_replayData);

            bool autoComplete = false;
            if (m_gameData.GameState == GAME_STATE.IN_GAME)
            {
                if (KlondikeLogic.CheckWinCondition(m_gameData))
                {
                    m_victoryTime -= Time.deltaTime;
                    if (m_victoryTime <= 0.0f)
                        SetGameState(ref m_gameData, GAME_STATE.VICTORY);
                }
                else if (KlondikeLogic.CheckAutoCompleteCondition(m_gameData))
                {
                    autoComplete = true;
                    m_autoCompleteTimer -= Time.deltaTime;
                    if (m_autoCompleteTimer <= 0.0f)
                    {
                        m_autoCompleteTimer = AUTO_COMPLETE_TIME;
                        DoAIAutoComplete(ref m_gameData);
                        m_victoryTime = VICTORY_TIME;
                    }
                }
                KlondikeVisual.ShowAutoComplete(autoComplete);
            }
            else if (m_gameData.GameState == GAME_STATE.VICTORY)
            {
                KlondikeVisual.ShowVictory(true);
                SetMenuState(MENU_STATE.GAME_OVER);
            }

            KlondikeVisual.SyncVisuals(m_gameData, m_replayData.PlayerMoveCount > 0, CardPacks[CardPackIndex]);

#if UNITY_EDITOR
            if (Input.GetKeyDown("a"))
            {
                DoAI(ref m_gameData, ref m_replayData);
                SaveMove();
            }
#endif
        }
        else if (MenuState == MENU_STATE.MAIN_MENU)
        {
            if (m_gameData.GameState == GAME_STATE.DEMO)
            {
                m_gameData.GameTime += Time.deltaTime;
                m_demoMoveTimer -= Time.deltaTime;
                if (m_demoMoveTimer < 0.0f)
                {
                    if (m_victoryTime <= 0.0f && KlondikeLogic.CheckWinCondition(m_gameData))
                        m_victoryTime = VICTORY_TIME;
                    else if (KlondikeLogic.CheckAutoCompleteCondition(m_gameData))
                        DoAIAutoComplete(ref m_gameData);
                    else if (m_gameData.GameTime > SHUFFLE_TIME)
                        DoAI(ref m_gameData, ref m_replayData);

                    m_demoMoveTimer = DEMO_MOVE_TIME;
                }
                if (m_victoryTime > 0.0f)
                {
                    m_victoryTime -= Time.deltaTime;
                    if (m_victoryTime < 0.0f)
                        StartNewDemo(ref m_gameData, ref m_replayData);
                }
                KlondikeVisual.SyncVisuals(m_gameData, false, CardPacks[CardPackIndex]);

                KlondikeVisual.ShowVictory(m_gameData.GameState == GAME_STATE.VICTORY);
                if (m_gameData.GameState == GAME_STATE.VICTORY && MenuState != MENU_STATE.GAME_OVER)
                    SetMenuState(MENU_STATE.GAME_OVER);
            }

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

            if (mouseDown)
            {
                m_mouseDownPosition = Input.mousePosition;
                m_offsetCurrent = m_offsetTarget = 0.0f;
                m_mouseDown = true;
                m_mouseDownTime = Time.realtimeSinceStartup;
            }
            if (mouseMove)
            {
                m_offsetTarget = (Input.mousePosition.x - m_mouseDownPosition.x) * 2.0f;
                m_offsetTarget = Mathf.Clamp(m_offsetTarget, m_prevCardsOrigPos.x - m_currentCardsOrigPos.x, m_nextCardsOrigPos.x - m_currentCardsOrigPos.x);
            }
            if (mouseUp)
            {
                m_mouseDown = false;
                float diffX = (Input.mousePosition.x - m_mouseDownPosition.x) * 2.0f;
                float limit = (Time.realtimeSinceStartup - m_mouseDownTime < 1.0f) ? 50.0f : (m_nextCardsOrigPos.x - m_currentCardsOrigPos.x) / 2.0f;
                if (diffX > limit)
                    m_offsetTarget = m_currentCardsOrigPos.x - m_prevCardsOrigPos.x;
                else if (diffX < -limit)
                    m_offsetTarget = m_currentCardsOrigPos.x - m_nextCardsOrigPos.x;
                else
                    m_offsetTarget = 0.0f;

                Debug.LogFormat("mouseUp called m_mouseDown set to {0}", m_mouseDown);
            }

            float diff = m_offsetTarget - m_offsetCurrent;
            if (Mathf.Abs(diff) > 0.0f)
            {
                float velocity = Time.deltaTime * 5000.0f;
                if (diff > velocity)
                {
                    m_offsetCurrent += velocity;
                    if (m_offsetTarget - m_offsetCurrent >= 0.0f)
                        m_offsetCurrent = m_offsetTarget;
                }
                else if (diff < -velocity)
                {
                    m_offsetCurrent -= velocity;
                    if (m_offsetCurrent - m_offsetTarget <= 0.0f)
                        m_offsetCurrent = m_offsetTarget;
                }
                else
                    m_offsetCurrent = m_offsetTarget;
            }

            if (!m_mouseDown) // can't use mouseUp becuase we need to account for velocity!
            {
                if ((Mathf.Abs(diff) > 0.0f && Mathf.Abs(diff) < 100.0f) || Mathf.Abs(diff) < 1.0f && Mathf.Abs(m_offsetCurrent) > 0.0f)
                {
                    if (m_offsetTarget < -10.0f)
                        NextCardPack();
                    if (m_offsetTarget > 10.0f)
                        PrevCardPack();

                    m_offsetTarget = m_offsetCurrent = 0.0f;
                }
            }
            PrevCardTransform.transform.position = m_prevCardsOrigPos + new Vector3(m_offsetCurrent, 0.0f, 0.0f);
            CurrentCardTransform.transform.position = m_currentCardsOrigPos + new Vector3(m_offsetCurrent, 0.0f, 0.0f);
            NextCardTransform.transform.position = m_nextCardsOrigPos + new Vector3(m_offsetCurrent, 0.0f, 0.0f);

            int prevIndex = CardPackIndex > 0 ? CardPackIndex - 1 : CardPacks.Length - 1;
            int nextIndex = (CardPackIndex + 1) % CardPacks.Length;
            for (int i = 0; i < 2; i++)
            {
                m_prevCardImages[i].sprite = CardPacks[prevIndex].HD[i];
                m_currentCardImages[i].sprite = CardPacks[CardPackIndex].HD[i];
                m_nextCardImages[i].sprite = CardPacks[nextIndex].HD[i];
            }

        }

#if UNITY_EDITOR
        if (Input.GetKeyDown("p"))
        {
            string fileName = "screenshot_" + Screen.width + "x" + Screen.height + "_" + screenshotCounter + ".png";
            ScreenCapture.CaptureScreenshot(fileName);
            screenshotCounter++;
        }
#endif
    }

    public void NextCardPack()
    {
        int numCardPacks = CardPacks.Length;

        CardPackIndex = (CardPackIndex + 1) % numCardPacks;
        MainCamera.backgroundColor = CardPacks[CardPackIndex].TableColor;

        for (int i = 0; i < numCardPacks; i++)
            m_selectionIndicators[i].color = (i == CardPackIndex) ? Color.white : new Color(1.0f, 1.0f, 1.0f, 0.5f);
    }
    public void PrevCardPack()
    {
        int numCardPacks = CardPacks.Length;

        CardPackIndex--;
        if (CardPackIndex < 0)
            CardPackIndex = numCardPacks - 1;
        MainCamera.backgroundColor = CardPacks[CardPackIndex].TableColor;

        for (int i = 0; i < numCardPacks; i++)
            m_selectionIndicators[i].color = (i == CardPackIndex) ? Color.white : new Color(1.0f, 1.0f, 1.0f, 0.5f);
    }

    #region BUTTONS
    public void GoToPrivacyPolicy()
    {
        SetMenuState(MENU_STATE.PRIVACY_POLICY);
        m_mouseDown = false;
    }

    public void GoToPrivacyPolicyDetails()
    {
        SetMenuState(MENU_STATE.PRIVACY_POLICY_DETAILS);
        m_mouseDown = false;
    }

    public void HidePrivacyPolicy()
    {
        PlayerPrefs.SetInt("privacy", 1);
        GoToMainMenu();
        m_mouseDown = false;
    }

    public void GoToMainMenu()
    {
        SetMenuState(MENU_STATE.MAIN_MENU);
    }

    public void GoToPauseMenu()
    {
        SetMenuState(MENU_STATE.PAUSE);
    }

    public void GoToQuitConfirmation()
    {
        SetMenuState(MENU_STATE.QUIT_CONFIRMATION);
    }

    public void GoToRetryConfirmation()
    {
        SetMenuState(MENU_STATE.RETRY_CONFIRMATION);
    }

    public void Undo()
    {
        if (m_replayData.PlayerMoveCount <= 0)
            return;
        m_replayData.PlayerMoveCount--;
        LoadMove();
    }

    public void ExitPauseMenu()
    {
        SetMenuState(MENU_STATE.IN_GAME);
    }
    #endregion // BUTTONS

    #region AI
    public void DoAITest()
    {
        DoAI(ref m_gameData, ref m_replayData);
    }

    public static bool DoAI(ref GameData gameData, ref ReplayData replayData)
    {
        //KlondikeLogic.DebugPrint(m_gameData, "Start DoAI()");

        if (DoAIMoveColumnToColumn(ref gameData, ref replayData))
            return true;

        if (DoAIMoveFromWaste(ref gameData, ref replayData))
            return true;

        if (DoAIMoveFromColumnToStack(ref gameData, ref replayData))
            return true;

        DoAIMoveCardsToWaste(ref gameData, ref replayData);

        return false;
        //KlondikeLogic.DebugPrint(m_gameData, "MoveCardsToWaste()");
    }

    static void DoAIMoveCardsToWaste(ref GameData gameData, ref ReplayData replayData)
    {
        KlondikeLogic.MoveCardsToWaste(ref gameData);
        replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.MOVE_TO_WASTE, (byte)gameData.CardStockIndex, 0, 0, gameData.GameTime);
    }

    static bool DoAIMoveColumnToColumn(ref GameData gameData, ref ReplayData replayData)
    {
        for (int pickupColumn = 0; pickupColumn < 7; pickupColumn++)
        {
            for (int i = 0; i < gameData.CardPileCount[pickupColumn]; i++)
            {
                if (gameData.CardPileState[pickupColumn][i] == CARD_STATE.REVEALED)
                {
                    int pickupIndex = gameData.CardPileIndices[pickupColumn][i];
                    int pickupNumber = (int)(pickupIndex % 13);
                    int pickupColor = SUITE_COLORS[pickupIndex / 13];

                    for (int dropColumn = 0; dropColumn < 7; dropColumn++)
                    {
                        if (i > 0 && gameData.CardPileCount[dropColumn] == 0 && pickupNumber == 12)
                        {
                            // move all of them
                            int numCards = gameData.CardPileCount[pickupColumn] - i;
                            for (int j = 0; j < numCards; j++)
                            {
                                byte index = gameData.CardPileIndices[pickupColumn][i];
                                KlondikeLogic.MoveCardFromColumnToColumn(ref gameData, index, pickupColumn, dropColumn);
                                replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_COLUMN, index, (byte)pickupColumn, (byte)dropColumn, gameData.GameTime);
                                KlondikeLogic.DebugPrint(gameData, "MoveCardFromColumnToColumn()");
                            }
                            return true;
                        }
                        else if (pickupColumn != dropColumn && gameData.CardPileCount[dropColumn] > 0)
                        {
                            int dropIndex = gameData.CardPileIndices[dropColumn][gameData.CardPileCount[dropColumn] - 1];
                            int dropNumber = (int)(dropIndex % 13);
                            int dropColor = SUITE_COLORS[dropIndex / 13];
                            if (pickupColumn != dropColumn && pickupNumber == dropNumber - 1 && pickupColor != dropColor)
                            {
                                // move all of them
                                int numCards = gameData.CardPileCount[pickupColumn] - i;
                                for (int j = 0; j < numCards; j++)
                                {
                                    byte index = gameData.CardPileIndices[pickupColumn][i];
                                    KlondikeLogic.MoveCardFromColumnToColumn(ref gameData, index, pickupColumn, dropColumn);
                                    replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_COLUMN, index, (byte)pickupColumn, (byte)dropColumn, gameData.GameTime);
                                    KlondikeLogic.DebugPrint(gameData, "MoveCardFromColumnToColumn()");
                                }
                                return true;
                            }
                        }
                    }
                    break;
                }
            }
        }
        return false;
    }

    static bool DoAIMoveFromColumnToStack(ref GameData gameData, ref ReplayData replayData)
    {
        for (int pickupColumn = 0; pickupColumn < 7; pickupColumn++)
        {
            if (gameData.CardPileCount[pickupColumn] > 0)
            {
                byte pickupIndex = gameData.CardPileIndices[pickupColumn][gameData.CardPileCount[pickupColumn] - 1];
                int pickupNumber = (int)(pickupIndex % 13);
                int pickupSuite = pickupIndex / 13;

                for (int stackIdx = 0; stackIdx < 4; stackIdx++)
                {
                    if (pickupNumber == 0 && gameData.CardStackCount[stackIdx] == 0)
                    {
                        KlondikeLogic.MoveCardFromColumnToStack(ref gameData, pickupIndex, pickupColumn, stackIdx);
                        replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_STACK, pickupIndex, (byte)pickupColumn, (byte)stackIdx, gameData.GameTime);
                        KlondikeLogic.DebugPrint(gameData, "MoveCardFromColumnToStack()");
                        return true;
                    }
                    else if (gameData.CardStackCount[stackIdx] > 0)
                    {
                        int stackCardIndex = gameData.CardStacks[stackIdx][gameData.CardStackCount[stackIdx] - 1];
                        int dropNumber = (int)(stackCardIndex % 13);
                        int dropSuite = stackCardIndex / 13;

                        if (pickupSuite == dropSuite && pickupNumber == dropNumber + 1)
                        {
                            KlondikeLogic.MoveCardFromColumnToStack(ref gameData, pickupIndex, pickupColumn, stackIdx);
                            replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.COLUMN_TO_STACK, pickupIndex, (byte)pickupColumn, (byte)stackIdx, gameData.GameTime);
                            KlondikeLogic.DebugPrint(gameData, "MoveCardFromColumnToStack()");
                            return true;
                        }

                    }
                }
            }
        }
        return false;
    }

    public static bool DoAIAutoComplete(ref GameData gameData)
    {
        for (int pickupColumn = 0; pickupColumn < 7; pickupColumn++)
        {
            if (gameData.CardPileCount[pickupColumn] > 0)
            {
                byte pickupIndex = gameData.CardPileIndices[pickupColumn][gameData.CardPileCount[pickupColumn] - 1];
                int pickupNumber = (int)(pickupIndex % 13);
                int pickupSuite = pickupIndex / 13;

                for (int stackIdx = 0; stackIdx < 4; stackIdx++)
                {
                    if (pickupNumber == 0 && gameData.CardStackCount[stackIdx] == 0)
                    {
                        KlondikeLogic.MoveCardFromColumnToStack(ref gameData, pickupIndex, pickupColumn, stackIdx);
                        return true;
                    }
                    else if (gameData.CardStackCount[stackIdx] > 0)
                    {
                        int stackCardIndex = gameData.CardStacks[stackIdx][gameData.CardStackCount[stackIdx] - 1];
                        int dropNumber = (int)(stackCardIndex % 13);
                        int dropSuite = stackCardIndex / 13;

                        if (pickupSuite == dropSuite && pickupNumber == dropNumber + 1)
                        {
                            KlondikeLogic.MoveCardFromColumnToStack(ref gameData, pickupIndex, pickupColumn, stackIdx);
                            return true;
                        }

                    }
                }
            }
        }
        return false;
    }

    static bool DoAIMoveFromWaste(ref GameData gameData, ref ReplayData replayData)
    {
        if (gameData.CardStockIndex >= 0 && gameData.CardStockIndex < gameData.CardStockCount)
        {
            byte wasteIndex = gameData.CardStockIndices[gameData.CardStockIndex];
            int pickupNumber = (int)(wasteIndex % 13);
            int pickupColor = SUITE_COLORS[wasteIndex / 13];
            int pickupSuite = wasteIndex / 13;

            for (int columnIdx = 0; columnIdx < 7; columnIdx++)
            {
                if (gameData.CardPileCount[columnIdx] == 0 && pickupNumber == 12)
                {
                    KlondikeLogic.MoveCardFromWasteToColumn(ref gameData, wasteIndex, columnIdx);
                    replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_COLUMN, wasteIndex, 0, (byte)columnIdx, gameData.GameTime);
                    KlondikeLogic.DebugPrint(gameData, "MoveCardFromWasteToColumn()");
                    return true;
                }
                else if (gameData.CardPileCount[columnIdx] > 0)
                {
                    int dropIndex = gameData.CardPileIndices[columnIdx][gameData.CardPileCount[columnIdx] - 1];
                    int dropNumber = (int)(dropIndex % 13);
                    int dropColor = SUITE_COLORS[dropIndex / 13];
                    if (pickupNumber == dropNumber - 1 && pickupColor != dropColor)
                    {
                        KlondikeLogic.MoveCardFromWasteToColumn(ref gameData, wasteIndex, columnIdx);
                        replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_COLUMN, wasteIndex, 0, (byte)columnIdx, gameData.GameTime);
                        KlondikeLogic.DebugPrint(gameData, "MoveCardFromWasteToColumn()");
                        return true;
                    }
                }
            }

            for (int stackIdx = 0; stackIdx < 4; stackIdx++)
            {
                if (pickupNumber == 0 && gameData.CardStackCount[stackIdx] == 0)
                {
                    KlondikeLogic.MoveCardFromWasteToStack(ref gameData, wasteIndex, stackIdx);
                    replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_STACK, wasteIndex, 0, (byte)stackIdx, gameData.GameTime);
                    KlondikeLogic.DebugPrint(gameData, "MoveCardFromWasteToStack()");
                    return true;
                }
                else if (gameData.CardStackCount[stackIdx] > 0)
                {
                    int stackCardIndex = gameData.CardStacks[stackIdx][gameData.CardStackCount[stackIdx] - 1];
                    int dropNumber = (int)(stackCardIndex % 13);
                    int dropSuite = stackCardIndex / 13;

                    if (pickupSuite == dropSuite && pickupNumber == dropNumber + 1)
                    {
                        KlondikeLogic.MoveCardFromWasteToStack(ref gameData, wasteIndex, stackIdx);
                        replayData.PlayerMoves[replayData.PlayerMoveCount++] = new PlayerMove(MOVE_TYPE.WASTE_TO_STACK, wasteIndex, 0, (byte)stackIdx, gameData.GameTime);
                        KlondikeLogic.DebugPrint(gameData, "MoveCardFromWasteToStack()");
                        return true;
                    }

                }
            }
        }
        return false;
    }
    #endregion // AI

    #region REPLAYS
    public void SaveReplay(int seed)
    {
        //Debug.LogFormat("SaveReplay()");

        if (!Directory.Exists("Assets/Resources/"))
            Directory.CreateDirectory("Assets/Resources/");

        string replayFileName = "Assets/Resources/Replay" + seed + ".bytes";
        using (FileStream fs = File.Create(replayFileName))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write(m_replayData.PlayerMoveCount);
            for (int i = 0; i < m_replayData.PlayerMoveCount; i++)
            {
                bw.Write((byte)m_replayData.PlayerMoves[i].MoveType);
                bw.Write((byte)m_replayData.PlayerMoves[i].PickupColumn);
                bw.Write((byte)m_replayData.PlayerMoves[i].PickupIndex);
                bw.Write((byte)m_replayData.PlayerMoves[i].DropColumn);
                bw.Write(m_replayData.PlayerMoves[i].Time);
            }
        }
    }

    void LoadReplay(int seed)
    {
        TextAsset asset = Resources.Load("Replay" + seed) as TextAsset;
        using (Stream s = new MemoryStream(asset.bytes))
        using (BinaryReader br = new BinaryReader(s))
        {
            m_replayData.PlayerMoveCount = br.ReadInt32();
            for (int i = 0; i < m_replayData.PlayerMoveCount; i++)
            {
                m_replayData.PlayerMoves[i].MoveType = (MOVE_TYPE)br.ReadByte();
                m_replayData.PlayerMoves[i].PickupColumn = br.ReadByte();
                m_replayData.PlayerMoves[i].PickupIndex = br.ReadByte();
                m_replayData.PlayerMoves[i].DropColumn = br.ReadByte();
                m_replayData.PlayerMoves[i].Time = br.ReadSingle();
            }
        }
    }

    public void SaveMove()
    {
        string path = Application.persistentDataPath + "/Replay/";
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string MoveFileName = path + "MoveData" + m_replayData.PlayerMoveCount + ".data";

        using (FileStream fs = File.Create(MoveFileName))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            for (int i = 0; i < 7; i++)
            {
                bw.Write(m_gameData.CardPileCount[i]);
                for (int j = 0; j < m_gameData.CardPileCount[i]; j++)
                {
                    bw.Write(m_gameData.CardPileIndices[i][j]);
                    bw.Write((byte)m_gameData.CardPileState[i][j]);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                bw.Write(m_gameData.CardStackCount[i]);
                for (int j = 0; j < m_gameData.CardStackCount[i]; j++)
                    bw.Write(m_gameData.CardStacks[i][j]);
            }

            bw.Write(m_gameData.CardStockCount);
            bw.Write(m_gameData.CardStockIndex);
            for (int i = 0; i < m_gameData.CardStockCount; i++)
                bw.Write(m_gameData.CardStockIndices[i]);
        }
    }

    public void LoadMove()
    {
        string path = Application.persistentDataPath + "/Replay/";
        if (!Directory.Exists(path))
            return;

        string MoveFileName = path + "MoveData" + m_replayData.PlayerMoveCount + ".data";

        KlondikeLogic.DebugPrint(m_gameData, "Before LoadMove()");

        using (BinaryReader br = new BinaryReader(File.Open(MoveFileName, FileMode.Open)))
        {
            for (int i = 0; i < 7; i++)
            {
                m_gameData.CardPileCount[i] = br.ReadByte();
                for (int j = 0; j < m_gameData.CardPileCount[i]; j++)
                {
                    m_gameData.CardPileIndices[i][j] = br.ReadByte();
                    m_gameData.CardPileState[i][j] = (CARD_STATE)br.ReadByte();
                }
            }

            for (int i = 0; i < 4; i++)
            {
                m_gameData.CardStackCount[i] = br.ReadByte();
                for (int j = 0; j < m_gameData.CardStackCount[i]; j++)
                    m_gameData.CardStacks[i][j] = br.ReadByte();
            }

            m_gameData.CardStockCount = br.ReadByte();
            m_gameData.CardStockIndex = br.ReadSByte();
            for (int i = 0; i < m_gameData.CardStockCount; i++)
                m_gameData.CardStockIndices[i] = br.ReadByte();
        }

        KlondikeLogic.DebugPrint(m_gameData, "After LoadMove()");
    }

    public static void DoReplayMove(ref GameData gameData, ref ReplayData replayData)
    {
        if (replayData.PlayerMoveIndex >= replayData.PlayerMoveCount)
            return;

        PlayerMove playerMove = replayData.PlayerMoves[replayData.PlayerMoveIndex++];

        Debug.LogFormat("DoReplayMove({0}) index {1}", playerMove.MoveType, replayData.PlayerMoveIndex);

        switch (playerMove.MoveType)
        {
            case MOVE_TYPE.COLUMN_TO_COLUMN:
                KlondikeLogic.MoveCardFromColumnToColumn(ref gameData, playerMove.PickupIndex, playerMove.PickupColumn, playerMove.DropColumn);
                break;

            case MOVE_TYPE.COLUMN_TO_STACK:
                KlondikeLogic.MoveCardFromColumnToStack(ref gameData, playerMove.PickupIndex, playerMove.PickupColumn, playerMove.DropColumn);
                break;

            case MOVE_TYPE.WASTE_TO_COLUMN:
                KlondikeLogic.MoveCardFromWasteToColumn(ref gameData, playerMove.PickupIndex, playerMove.DropColumn);
                break;

            case MOVE_TYPE.WASTE_TO_STACK:
                KlondikeLogic.MoveCardFromWasteToStack(ref gameData, playerMove.PickupIndex, playerMove.DropColumn);
                break;

            case MOVE_TYPE.MOVE_TO_WASTE:
                KlondikeLogic.MoveCardsToWaste(ref gameData);
                break;
        }

        KlondikeLogic.DebugPrint(gameData, "DoReplayMove()");
    }
    #endregion //REPLAYS
}
