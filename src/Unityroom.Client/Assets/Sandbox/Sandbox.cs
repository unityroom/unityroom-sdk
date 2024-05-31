using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unityroom.Client;

public class Sandbox : MonoBehaviour
{
    [SerializeField] InputField inputField;
    [SerializeField] Button button;
    [SerializeField] Text text;

    readonly UnityroomClient client = new()
    {
        HmacKey = "bqSYvXJo8oe1Pwk7JhB+E9EUABwvXHoku/YjZPJcvGpjXDNMrcaEXQiQrx/fcn1tPukdZzm4ZVZDNJW2jYq60A==",
    };

    void Start()
    {
        button.onClick.AddListener(async () =>
        {
            if (!float.TryParse(inputField.text, out var s)) return;
            await SendAsync(s, destroyCancellationToken);
        });
    }

    async Task SendAsync(float score, CancellationToken cancellationToken)
    {
        text.text = "";
        button.interactable = false;

        try
        {
            var response = await client.Scoreboards.SendAsync(new()
            {
                ScoreboardId = 1,
                Score = score,
            }, cancellationToken);

            text.text =
@$"status: {response.Status}
scoreUpdated: {response.ScoreUpdated}";
        }
        catch (UnityroomApiException ex)
        {
            text.text =
@$"code: {ex.ErrorCode}
type: {ex.ErrorType}
message: {ex.Message}";
        }
        finally
        {
            button.interactable = true;
        }
    }
}