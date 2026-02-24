// ExplodeButton.cs
using ButtonPack;
using Fusion;
using UnityEngine;

public class ExplodeButton : MonoBehaviour, IInteractable
{
    private int _x;
    private int _y;
    private MinesMinigameController _controller;

    [SerializeField] private PressController _pressController;

    public Outline Outline { get; private set; }
    public bool CanBeInteractedWith => true;

    public void Initialize(int x, int y, MinesMinigameController controller)
    {
        _x = x;
        _y = y;
        _controller = controller;
        Outline = GetComponent<Outline>();
        if (Outline != null) Outline.enabled = false;
    }

    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (_controller.CurrentTurn != player) return;
        _controller.RPC_ResolveTile(_x, _y);
    }

    public void Press()
    {
        _pressController?.Toggle1();
    }
}