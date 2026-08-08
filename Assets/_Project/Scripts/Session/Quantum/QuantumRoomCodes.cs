namespace DiceGame.Session
{
    /// <summary>
    /// Short join codes for Quantum Photon rooms (host shares with client).
    /// </summary>
    public static class QuantumRoomCodes
    {
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Create()
        {
            var chars = new char[SessionConstants.QuantumRoomCodeLength];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
            }

            return new string(chars);
        }

        public static string Normalize(string code)
        {
            return string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : code.Trim().ToUpperInvariant();
        }
    }
}
