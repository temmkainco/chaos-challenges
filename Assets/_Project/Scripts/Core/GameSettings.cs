using UnityEngine;

namespace Core
{
    public class GameSettings : ScriptableObject
    {
        public enum RuntimeMode { Local, Steam, Dev }
        public RuntimeMode CurrentMode = RuntimeMode.Local;
    }
}