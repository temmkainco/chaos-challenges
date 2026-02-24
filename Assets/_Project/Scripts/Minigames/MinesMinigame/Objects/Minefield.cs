// Minefield.cs
using Fusion;
using UnityEngine;

public class Minefield : NetworkBehaviour
{
    [SerializeField] private ExplodeButton _buttonPrefab;
    [SerializeField] private float _spacing = 1.2f;
    [SerializeField] private Transform _buttonParent;

    [Networked] private int Size { get; set; }

    private bool[,] _mines;
    private ExplodeButton[,] _buttons;
    private MinesMinigameController _controller;

    public void Initialize(MinesMinigameController controller)
    {
        _controller = controller;
    }

    public void Generate(int playerCount)
    {
        Size = playerCount + 2;

        int totalTiles = Size * Size;
        int mineCount = Mathf.RoundToInt(totalTiles * 0.2f);

        _mines = new bool[Size, Size];
        for (int i = 0; i < mineCount; i++)
        {
            int x = Random.Range(0, Size);
            int y = Random.Range(0, Size);
            _mines[x, y] = true;
        }

        RPC_SpawnButtons(Size);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnButtons(int size)
    {
        SpawnButtonsLocally(size);
    }

    private void SpawnButtonsLocally(int size)
    {
        _buttons = new ExplodeButton[size, size];
        float offset = (size - 1) * _spacing * 0.5f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector3 position = _buttonParent.position + new Vector3(
                    x * _spacing - offset,
                    0,
                    y * _spacing - offset
                );

                ExplodeButton button = Instantiate(_buttonPrefab, position, Quaternion.identity, _buttonParent);
                button.Initialize(x, y, _controller);
                _buttons[x, y] = button;
            }
        }
    }

    public bool IsMine(int x, int y) => _mines[x, y];

    public void PressButton(int x, int y)
    {
        _buttons[x, y].Press();
    }
}