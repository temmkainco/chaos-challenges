using Fusion;
using System.Collections.Generic;
using UnityEngine;

public enum PlateState : byte
{
    Hidden,
    Safe,
    Mine,
}

/// <summary>
/// Square field of plates. Plate states are replicated; where the mines are is known only to the host until a
/// plate is pressed. Every peer builds the plate visuals locally from <see cref="Size"/> and
/// <see cref="FieldVersion"/>, and plays the reveal when a plate's state changes.
/// </summary>
public class Minefield : NetworkBehaviour
{
    public const int MIN_SIZE = 4;
    public const int MAX_SIZE = 7;

    [SerializeField] private ExplodeButton _buttonPrefab;
    [SerializeField] private float _spacing = 2.2f;
    [SerializeField] private Transform _buttonParent;
    [SerializeField, Range(0.1f, 0.6f)] private float _mineRatio = 0.3f;
    [SerializeField, Tooltip("Lit material with _BaseColor, used for the explosion puffs")] private Material _effectMaterial;

    [Networked] public int Size { get; private set; }
    [Networked] public int MineCount { get; private set; }
    [Networked] public int MinesFound { get; private set; }
    /// <summary>Bumped on every new field, so clients rebuild their plates.</summary>
    [Networked] public int FieldVersion { get; private set; }
    [Networked, Capacity(MAX_SIZE * MAX_SIZE)] private NetworkArray<byte> States { get; }

    public int MinesLeft => MineCount - MinesFound;
    public int PlateCount => Size * Size;

    private bool[] _mines = new bool[MAX_SIZE * MAX_SIZE];
    private readonly List<ExplodeButton> _plates = new();
    private PlateState[] _shown = new PlateState[MAX_SIZE * MAX_SIZE];
    private int _builtVersion;
    private MinesMinigameController _controller;

    public void Initialize(MinesMinigameController controller)
    {
        _controller = controller;
    }

    #region Host

    /// <summary>Deals a new field: the size grows with the players, the mine count is exact.</summary>
    public void Generate(int playerCount)
    {
        Size = Mathf.Clamp(playerCount + 2, MIN_SIZE, MAX_SIZE);
        int plates = PlateCount;
        MineCount = Mathf.Clamp(Mathf.RoundToInt(plates * _mineRatio), Mathf.Max(1, playerCount - 1), plates - 1);
        MinesFound = 0;

        var order = new List<int>(plates);
        for (int i = 0; i < plates; i++) order.Add(i);
        for (int i = plates - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        System.Array.Clear(_mines, 0, _mines.Length);
        for (int i = 0; i < MineCount; i++)
            _mines[order[i]] = true;

        for (int i = 0; i < States.Length; i++)
            States.Set(i, (byte)PlateState.Hidden);

        FieldVersion++;
        Debug.Log($"[Mines] New {Size}x{Size} field with {MineCount} mines");
    }

    /// <summary>Presses a hidden plate. Returns <see cref="PlateState.Hidden"/> if the plate was already pressed or does not exist.</summary>
    public PlateState Reveal(int index)
    {
        if (index < 0 || index >= PlateCount || GetState(index) != PlateState.Hidden)
            return PlateState.Hidden;

        var result = _mines[index] ? PlateState.Mine : PlateState.Safe;
        States.Set(index, (byte)result);
        if (result == PlateState.Mine)
            MinesFound++;
        return result;
    }

    public bool HasHiddenSafePlate()
    {
        for (int i = 0; i < PlateCount; i++)
            if (GetState(i) == PlateState.Hidden && !_mines[i])
                return true;
        return false;
    }

    public int PickRandomHiddenPlate()
    {
        var hidden = new List<int>();
        for (int i = 0; i < PlateCount; i++)
            if (GetState(i) == PlateState.Hidden)
                hidden.Add(i);
        return hidden.Count > 0 ? hidden[Random.Range(0, hidden.Count)] : -1;
    }

    #endregion

    public PlateState GetState(int index) => (PlateState)States[index];

    public Vector3 GetPlatePosition(int index)
    {
        float offset = (Size - 1) * _spacing * 0.5f;
        int x = index % Size, y = index / Size;
        return _buttonParent.TransformPoint(new Vector3(x * _spacing - offset, 0f, y * _spacing - offset));
    }

    public override void Render()
    {
        if (FieldVersion == 0)
            return;

        if (FieldVersion != _builtVersion)
            Rebuild();

        for (int i = 0; i < _plates.Count; i++)
        {
            var state = GetState(i);
            if (state == _shown[i])
                continue;

            _shown[i] = state;
            _plates[i].ShowState(state, animate: true);
            if (state == PlateState.Mine && _effectMaterial != null)
                LowPolyExplosion.Play(_plates[i].transform.position + Vector3.up * 0.6f, 2.2f, _effectMaterial);
        }
    }

    private void Rebuild()
    {
        bool replacing = _plates.Count > 0;
        foreach (var plate in _plates)
            plate.Remove();
        _plates.Clear();

        _builtVersion = FieldVersion;
        for (int i = 0; i < PlateCount; i++)
        {
            var plate = Instantiate(_buttonPrefab, GetPlatePosition(i), _buttonParent.rotation, _buttonParent);
            plate.Initialize(i, _controller);
            _shown[i] = GetState(i);
            plate.ShowState(_shown[i], animate: false);
            plate.PopIn(replacing ? 0.6f + i * 0.02f : i * 0.02f);
            _plates.Add(plate);
        }
    }
}
