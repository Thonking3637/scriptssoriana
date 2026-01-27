using UnityEngine;

[CreateAssetMenu(fileName = "NewFruit", menuName = "Store/Fruit")]
public class Fruit : ScriptableObject
{
    public string fruitName;
    public float weight;
    public string code;
    public Sprite image;
    public int price;
    public int pricePerKilo = 30;

    private void OnEnable()
    {
        price = Random.Range(20, 41);
        weight = Mathf.Round(Random.Range(0.10f, 0.20f) * 100f) / 100f;
    }

}
