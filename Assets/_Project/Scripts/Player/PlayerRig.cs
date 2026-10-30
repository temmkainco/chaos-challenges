using UnityEngine;

/// <summary>Cached bones of a character (humanoid rig): where held items and nameplates attach.</summary>
public class PlayerRig : MonoBehaviour
{
    public Transform RightHand { get; private set; }
    public Transform Head { get; private set; }

    public static PlayerRig For(GameObject character)
    {
        if (!character.TryGetComponent<PlayerRig>(out var rig))
            rig = character.AddComponent<PlayerRig>();
        return rig;
    }

    private void Awake()
    {
        var animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }
}
