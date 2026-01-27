using UnityEngine;

[System.Serializable]
public class Client : MonoBehaviour
{
    [Header("Client Information")]
    public string clientName;
    public bool requiresCardPayment;
    public int purchasePoints;
    public float patienceLevel;

    [Header("Recibo DATA")]
    public string address;
    public int paymentAmount;
    public string receiptType = "SERVICIO DE LUZ";

    [Header("Recarga Telefónica")]
    public string phoneNumber;
    public string phoneCompany;
    public int rechargeAmount;

    private void Start()
    {
        purchasePoints = Random.Range(50, 5001);
    }

    public void InitializeClient(string name, bool needsCard, int points, float patience)
    {
        clientName = name;
        requiresCardPayment = needsCard;
        patienceLevel = patience;
    }

    public void GenerateRechargeData()
    {
        phoneNumber = GeneratePhoneNumber();
        phoneCompany = GetRandomCompany();
        rechargeAmount = GetRandomAmount();
    }

    private string GeneratePhoneNumber()
    {
        return "55" + Random.Range(10000000, 99999999).ToString(); // Simula un número celular mexicano
    }

    private string GetRandomCompany()
    {
        string[] companies = new string[]
        {
            "SORIANA MOVIL",
            "TELCEL",
            "MOVISTAR",
            "IUSACELL",
            "AT&T",
        };

        return companies[Random.Range(0, companies.Length)];
    }

    private int GetRandomAmount()
    {
        int[] validAmounts = new int[] { 10, 20, 30, 50, 80, 100, 150, 200, 300, 500 };
        return validAmounts[Random.Range(0, validAmounts.Length)];
    }
}
