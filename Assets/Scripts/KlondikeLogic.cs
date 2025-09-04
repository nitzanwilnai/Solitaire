using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class KlondikeLogic
{

    public static int seed;
    public static int CustomRandInt()
    {
        seed = (214013 * seed + 2531011);
        return (seed >> 16) & 0x7FFF;
    }

    public static void ShuffleCards(ref byte[] shuffledCards)
    {
        int numCards = shuffledCards.Length;

        for (int i = 0; i < numCards; i++)
            shuffledCards[i] = (byte)i;

        for (int i = 0; i < numCards; i++)
        {
            int randomIndex = CustomRandInt() % numCards;
            byte c = shuffledCards[randomIndex];
            shuffledCards[randomIndex] = shuffledCards[i];
            shuffledCards[i] = c;
        }
    }

    public static void AllocateGameData(ref GameData gameData)
    {
        gameData.CardStockIndices = new byte[52];

        gameData.CardPileIndices = new byte[7][];
        gameData.CardPileCount = new byte[7];
        gameData.CardPileState = new CARD_STATE[7][];
        for (int i = 0; i < 7; i++)
        {
            gameData.CardPileIndices[i] = new byte[20];
            gameData.CardPileState[i] = new CARD_STATE[20];
        }

        gameData.CardStacks = new byte[4][];
        gameData.CardStackCount = new byte[4];
        for (int i = 0; i < 4; i++)
            gameData.CardStacks[i] = new byte[13];
    }

    public static void ResetGameData(ref GameData gameData)
    {
        gameData.CardStockCount = 0;
        for (int i = 0; i < 52; i++)
            gameData.CardStockIndices[i] = 0;

        for (int i = 0; i < 7; i++)
        {
            gameData.CardPileCount[i] = 0;
            for (int j = 0; j < 20; j++)
            {
                gameData.CardPileIndices[i][j] = 0;
                gameData.CardPileState[i][j] = CARD_STATE.HIDDEN;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            gameData.CardStackCount[i] = 0;
            for (int j = 0; j < 13; j++)
            {
                gameData.CardStacks[i][j] = 0;
            }
        }

        gameData.HighlightedIndex = -1;
        gameData.GameTime = 0.0f;
    }

    public static void StartGame(ref GameData gameData, int newSeed, byte[] tempCardsShuffled)
    {
        ResetGameData(ref gameData);

        // 3, 5 ok
        seed = newSeed;

        KlondikeLogic.ShuffleCards(ref tempCardsShuffled);

        // needs to into klondike logic!
        int count = 0;
        for (int i = 0; i < 7; i++)
        {
            gameData.CardPileCount[i] = (byte)(i + 1);
            for (int j = 0; j < gameData.CardPileCount[i]; j++)
            {
                gameData.CardPileIndices[i][j] = tempCardsShuffled[count++];
            }

            gameData.CardPileState[i][gameData.CardPileCount[i] - 1] = CARD_STATE.REVEALED;
        }

        gameData.CardStockCount = (byte)(52 - count);
        for (int i = 0; i < gameData.CardStockCount; i++)
            gameData.CardStockIndices[i] = tempCardsShuffled[i + count];
        gameData.CardStockIndex = -1;

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 13; j++)
                gameData.CardStacks[i][j] = 0;
    }

    public static void DebugPrint(GameData gameData, string title)
    {
#if UNITY_EDITOR
        return;
        string debugString = title;
        for (int i = 0; i < 7; i++)
        {
            debugString += "\n Column " + i + " Count: " + gameData.CardPileCount[i] + " - ";
            for (int j = 0; j < gameData.CardPileCount[i]; j++)
            {
                int index = gameData.CardPileIndices[i][j];
                debugString += " " + index + " - " + gameData.CardPileState[i][j].ToString() + ",";
            }
        }
        for (int i = 0; i < 4; i++)
        {
            debugString += "\n Stack " + i + " Count: " + gameData.CardStackCount[i] + " - ";
            for (int j = 0; j < gameData.CardStackCount[i]; j++)
            {
                int index = gameData.CardStacks[i][j];
                debugString += " " + index + ",";
            }
        }

        debugString += "\n Stock count: " + gameData.CardStockCount + " - ";
        for (int i = 0; i < gameData.CardStockCount; i++)
            debugString += " " + gameData.CardStockIndices[i] + "(" + GetCardString((gameData.CardStockIndices[i] % 13)+1) + ")" + ",";
        debugString += "\n cardStockIndex " + gameData.CardStockIndex;

        Debug.Log(debugString);
#endif
    }

    public static string GetCardString(int index)
    {
        if (index == 1)
            return "A";
        if (index < 11)
            return index.ToString();
        if (index == 11)
            return "J";
        if (index == 12)
            return "Q";
        return "K";
    }

    public static void AddNewInteractableCards(ref GameData gameData, int column)
    {
        if (gameData.CardPileCount[column] > 0)
        {
            if (gameData.CardPileState[column][gameData.CardPileCount[column] - 1] == CARD_STATE.HIDDEN)
            {
                gameData.CardPileState[column][gameData.CardPileCount[column] - 1] = CARD_STATE.REVEALED;
                gameData.Score += 20;
            }
        }
    }

    public static void MoveCardFromColumnToColumn(ref GameData gameData, byte index, int oldColumn, int newColumn)
    {
        byte count = 0;
        for (int i = 0; i < gameData.CardPileCount[oldColumn]; i++)
        {
            if (gameData.CardPileIndices[oldColumn][i] != index)
            {
                gameData.CardPileIndices[oldColumn][count] = gameData.CardPileIndices[oldColumn][i];
                gameData.CardPileState[oldColumn][count] = gameData.CardPileState[oldColumn][i];
                count++;
            }
        }
        gameData.CardPileCount[oldColumn] = count;
        gameData.CardPileState[oldColumn][count + 1] = CARD_STATE.HIDDEN;

        gameData.CardPileIndices[newColumn][gameData.CardPileCount[newColumn]] = index;
        if (gameData.CardPileState[newColumn][gameData.CardPileCount[newColumn]] == CARD_STATE.HIDDEN)
        {
            gameData.CardPileState[newColumn][gameData.CardPileCount[newColumn]] = CARD_STATE.REVEALED;
            gameData.Score += 20;
        }

        gameData.CardPileCount[newColumn]++;

        AddNewInteractableCards(ref gameData, oldColumn);
    }

    public static void MoveCardFromColumnToStack(ref GameData gameData, byte index, int oldColumn, int newColumn)
    {
        byte count = 0;
        for (int i = 0; i < gameData.CardPileCount[oldColumn]; i++)
        {
            if (gameData.CardPileIndices[oldColumn][i] != index)
            {
                gameData.CardPileIndices[oldColumn][count] = gameData.CardPileIndices[oldColumn][i];
                gameData.CardPileState[oldColumn][count] = gameData.CardPileState[oldColumn][i];
                count++;
            }
        }
        gameData.CardPileCount[oldColumn] = count;
        gameData.CardPileState[oldColumn][count + 1] = CARD_STATE.HIDDEN;

        gameData.CardStacks[newColumn][gameData.CardStackCount[newColumn]++] = index;

        gameData.Score += 100;

        AddNewInteractableCards(ref gameData, oldColumn);
    }

    public static void MoveCardFromWasteToStack(ref GameData gameData, byte index, int newColumn)
    {
        byte count = 0;
        for (int i = 0; i < gameData.CardStockCount; i++)
            if (gameData.CardStockIndices[i] != index)
                gameData.CardStockIndices[count++] = gameData.CardStockIndices[i];
        gameData.CardStockCount = count;


        gameData.CardStockIndex--;

        gameData.CardStacks[newColumn][gameData.CardStackCount[newColumn]++] = index;

        gameData.Score += 100;
    }

    public static void MoveCardFromWasteToColumn(ref GameData gameData, byte index, int newColumn)
    {
        byte count = 0;
        for (int i = 0; i < gameData.CardStockCount; i++)
            if (gameData.CardStockIndices[i] != index)
                gameData.CardStockIndices[count++] = gameData.CardStockIndices[i];
        gameData.CardStockCount = count;

        gameData.CardStockIndex--;

        gameData.CardPileIndices[newColumn][gameData.CardPileCount[newColumn]] = index;
        if (gameData.CardPileState[newColumn][gameData.CardPileCount[newColumn]] == CARD_STATE.HIDDEN)
        {
            gameData.CardPileState[newColumn][gameData.CardPileCount[newColumn]] = CARD_STATE.REVEALED;
            gameData.Score += 20;
        }

        gameData.CardPileCount[newColumn]++;
    }

    public static void MoveCardsToWaste(ref GameData gameData)
    {
        if (gameData.CardStockIndex >= gameData.CardStockCount - 1)
        {
            gameData.CardStockIndex = -1;
            return;
        }
        gameData.CardStockIndex += 3;
        if (gameData.CardStockIndex > gameData.CardStockCount - 1)
            gameData.CardStockIndex = (sbyte)(gameData.CardStockCount - 1);
    }

    public static bool CanPlaceCardInStack(GameData gameData, int index, int stackIdx)
    {
        int myNumericValue = (int)(index % 13);
        if (myNumericValue == 0 && gameData.CardStackCount[stackIdx] == 0)
        {
            return true;
        }
        else if (gameData.CardStackCount[stackIdx] > 0)
        {
            int stackCardIndex = gameData.CardStacks[stackIdx][gameData.CardStackCount[stackIdx] - 1];
            int otherNumericValue = (int)(stackCardIndex % 13);
            int mySuite = index / 13;
            int otherSuite = stackCardIndex / 13;

            if (myNumericValue == otherNumericValue + 1 && mySuite == otherSuite)
                return true;
        }
        return false;
    }

    public static bool IndexInArray(byte index, byte[] array, int count)
    {
        for (int i = 0; i < count; i++)
            if (index == array[i])
                return true;
        return false;
    }

    public static bool CheckAutoCompleteCondition(GameData gameData)
    {
        for (int i = 0; i < 7; i++)
            if (gameData.CardPileCount[i] > 0)
                if (gameData.CardPileState[i][0] == CARD_STATE.HIDDEN)
                    return false;

        return (gameData.CardStockCount == 0);
    }


    public static bool CheckWinCondition(GameData gameData)
    {
        int totalStackCount = 0;
        for (int i = 0; i < 4; i++)
            totalStackCount += gameData.CardStackCount[i];

        return (totalStackCount == 52);
    }

    public static bool CanPlaceCardInEmptyColumn(GameData gameData, byte selectedCardIndex, int column)
    {
        if (gameData.CardPileCount[column] == 0)
        {
            int myNumericValue = (int)(selectedCardIndex % 13);
            if (myNumericValue == 12)
            {
                Debug.LogFormat("myValue {0} placed in pile {1}", myNumericValue, column);
                return true;
            }
        }
        return false;
    }

    public static bool CanPlaceCardInColumn(int selectedCardIndex, int index)
    {
        int myNumericValue = (int)(selectedCardIndex % 13);
        int otherNumericValue = (int)(index % 13);
        int myColor = GameManager.SUITE_COLORS[selectedCardIndex / 13];
        int otherColor = GameManager.SUITE_COLORS[index / 13];

        if (myColor != otherColor && myNumericValue == otherNumericValue - 1)
            return true;

        return false;
    }

    public static void UndoMoveCardFromColumnToColumn(ref GameData gameData, byte index, int oldColumn, int newColumn)
    {

    }
}
