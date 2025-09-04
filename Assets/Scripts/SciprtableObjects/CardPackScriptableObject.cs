using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/CardPackScriptableObject", order = 2)]
public class CardPackScriptableObject : ScriptableObject
{
    public Sprite Back;
    public Sprite[] Cards;
    public Sprite[] HD;
    public Color TableColor;
}