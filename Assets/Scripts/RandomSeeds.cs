using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class RandomSeeds
{
#if UNITY_EDITOR
    [MenuItem("Klondike/GenerateSeeds")]
    public static void GenerateSeeds()
    {

        ReplayData replayData = new ReplayData();
        replayData.PlayerMoves = new PlayerMove[1024];
        GameData gameData = new GameData();
        KlondikeLogic.AllocateGameData(ref gameData);

        //string seedString = "int[] m_seeds = {";
        float time = Time.realtimeSinceStartup;
        List<int> goodSeeds = new List<int>(16384);
        for (int seed = 0; seed < 500000; seed++)
        {
            replayData.PlayerMoveCount = 0;
            StartTestGame(ref gameData, seed, ref replayData);

            int loopCheck = 0;
            bool run = true;
            while (run)
            {
                if (KlondikeLogic.CheckWinCondition(gameData))
                    GameManager.SetGameState(ref gameData, GAME_STATE.VICTORY);
                else if (KlondikeLogic.CheckAutoCompleteCondition(gameData))
                    GameManager.DoAIAutoComplete(ref gameData);
                else if (!GameManager.DoAI(ref gameData, ref replayData))
                    loopCheck++;
                else
                    loopCheck = 0;

                if (gameData.GameState == GAME_STATE.VICTORY || loopCheck > 100)
                    break;
            }

            if (gameData.GameState == GAME_STATE.VICTORY)
            {
                goodSeeds.Add(seed);
                //SaveReplay(seed);
            }
        }

        WriteSeeds(goodSeeds);

        Debug.LogFormat("Seeds found {0} time {1}", goodSeeds.Count, Time.realtimeSinceStartup - time);
    }

    static void StartTestGame(ref GameData gameData, int seed, ref ReplayData replayData)
    {
        //Debug.LogFormat("StartGame seed {0}", seed);
        replayData.PlayerMoveCount = 0;

        KlondikeLogic.StartGame(ref gameData, seed, GameManager.TempCardsShuffled);

        GameManager.SetGameState(ref gameData, GAME_STATE.IN_GAME);
    }

    public static void WriteSeeds(List<int> seeds)
    {
        Debug.LogFormat("WriteSeeds() {0}", seeds.Count);

        if (!Directory.Exists("Assets/Resources/"))
            Directory.CreateDirectory("Assets/Resources/");

        string seedFileName = "Assets/Resources/Seeds.bytes";
        using (FileStream fs = File.Create(seedFileName))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int numSeeds = seeds.Count;
            bw.Write(numSeeds);
            for (int i = 0; i < numSeeds; i++)
                bw.Write(seeds[i]);
        }
    }
#endif

    public static int ReadRandomSeed()
    {
        TextAsset asset = Resources.Load("Seeds") as TextAsset;
        using (Stream s = new MemoryStream(asset.bytes))
        using (BinaryReader br = new BinaryReader(s))
        {
            int numSeeds = br.ReadInt32();
            int randomIndex = KlondikeLogic.CustomRandInt() % numSeeds;

            br.BaseStream.Seek((randomIndex + 1) * sizeof(int), SeekOrigin.Begin);
            return br.ReadInt32();
        }
    }
}
