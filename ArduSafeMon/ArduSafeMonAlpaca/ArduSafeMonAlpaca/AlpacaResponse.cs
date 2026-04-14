namespace ArduSafeMonAlpaca;

/// <summary>
/// Réponse générique ASCOM Alpaca.
/// Tous les endpoints retournent ce type sérialisé en JSON.
/// </summary>
public record AlpacaResponse<T>(
    T Value,
    int ClientTransactionID,
    int ServerTransactionID,
    int ErrorNumber,
    string ErrorMessage);

/// <summary>Factory de réponses Alpaca avec compteur de transaction auto-incrémenté.</summary>
public static class AlpacaResult
{
    private static int _txId;

    public static AlpacaResponse<T> Ok<T>(T value, int clientTxId = 0) =>
        new(value, clientTxId, Interlocked.Increment(ref _txId), 0, "");

    public static AlpacaResponse<T> Fail<T>(int errorNumber, string message,
        T defaultValue = default!, int clientTxId = 0) =>
        new(defaultValue, clientTxId, Interlocked.Increment(ref _txId), errorNumber, message);
}
