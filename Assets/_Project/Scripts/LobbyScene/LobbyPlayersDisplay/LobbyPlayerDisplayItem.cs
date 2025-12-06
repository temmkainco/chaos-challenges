using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerDisplayItem : MonoBehaviour
{
    [field: SerializeField] public TMP_Text PlayerNickname_TMP { get; private set; }
    [field: SerializeField] public Image PlayerAvatar_Image {  get; private set; }
}
