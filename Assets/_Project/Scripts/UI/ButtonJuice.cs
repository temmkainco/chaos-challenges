using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Cartoon feel for buttons: grows a little on hover, squashes on press.</summary>
[RequireComponent(typeof(Selectable))]
public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _pressScale = 0.94f;

    private Selectable _selectable;
    private bool _hovered;

    private void Awake() => _selectable = GetComponent<Selectable>();

    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        _hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (_selectable.interactable) ScaleTo(_hoverScale, 0.12f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        ScaleTo(1f, 0.12f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_selectable.interactable) ScaleTo(_pressScale, 0.06f);
    }

    public void OnPointerUp(PointerEventData eventData) => ScaleTo(_hovered ? _hoverScale : 1f, 0.15f);

    private void ScaleTo(float scale, float duration)
    {
        transform.DOKill();
        transform.DOScale(scale, duration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
    }
}
