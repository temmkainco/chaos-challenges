using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace Networking
{
    public class NetworkLobbyCodeGenerator
    {
        private const int CODE_SIZE = 6;
        private const int ATTEMPTS_COUNT = 100;

        private const string ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        
        public string GenerateCode(List<SessionInfo> existingSessions)
        {
            char[] buffer = new char[CODE_SIZE];

            for (int attempt = 0; attempt < ATTEMPTS_COUNT; attempt++)
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = ALPHABET[Random.Range(0, ALPHABET.Length)];

                string code = new string(buffer);

                bool exists = existingSessions.Exists(s => s.Properties.ContainsKey("code")
                                                 && (string)s.Properties["code"] == code);

                if (!exists)
                    return code;
            }

            Debug.LogError($"Failed to generate unique code after {ATTEMPTS_COUNT} attempts");
            return null;
        }
    }
}
