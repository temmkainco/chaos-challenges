using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Persistent popup for user-facing messages such as join failures and disconnect reasons.</summary>
public class MessagePanel : PanelBase
{
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _okButton;

    private bool _cursorWasVisible;
    private CursorLockMode _cursorLockMode;

    private void Awake()
    {
        _okButton.onClick.AddListener(Close);
    }

    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        _messageText.text = message;
        if (!gameObject.activeSelf)
        {
            _cursorWasVisible = Cursor.visible;
            _cursorLockMode = Cursor.lockState;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Open();
    }

    public override void Close()
    {
        if (!gameObject.activeSelf)
            return;

        Cursor.visible = _cursorWasVisible;
        Cursor.lockState = _cursorLockMode;
        base.Close();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            Close();
    }

    private void OnDestroy()
    {
        _okButton.onClick.RemoveListener(Close);
    }
}
