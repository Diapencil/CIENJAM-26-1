// LoadingScreenController.cs
// 기능 : 로딩 씬 전용 UIDocument 제어기. 인스펙터에 등록된 랜덤 메시지 목록 중 하나를
//        화면 하단 중앙에 표시하고, 인스펙터에 지정된 이미지를 전체 화면 배경으로 적용한다.
// 사용 : 로딩 씬에 UIDocument(Source Asset = LoadingScreen.uxml) 가 붙은 GameObject 를 만들고
//        같은 GameObject 에 본 컴포넌트를 추가한다. 그 후 인스펙터에서
//        randomMessages 리스트와 loadingImage 를 채우면 OnEnable 시점에 자동 적용된다.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LoadingScreenController : MonoBehaviour
{
    [Header("랜덤 메시지")]
    [Tooltip("로딩 중 화면 하단 중앙에 표시할 메시지 목록. 이 중 하나가 랜덤으로 선택됩니다.")]
    [SerializeField] private List<string> randomMessages = new();

    [Header("배경 이미지")]
    [Tooltip("로딩 화면 전체를 채울 배경 이미지. 비워두면 배경 없이 표시됩니다.")]
    [SerializeField] private Sprite loadingImage;

    // UXML 요소 이름 (LoadingScreen.uxml 과 일치해야 함)
    private const string ImageElementName = "loading-image";
    private const string MessageElementName = "loading-message";

    private void OnEnable()
    {
        StartCoroutine(ApplyWhenDocumentReady());
    }

    private System.Collections.IEnumerator ApplyWhenDocumentReady()
    {
        yield return null;

        Apply();
    }

    private void Apply()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[LoadingScreenController] UIDocument 의 rootVisualElement 가 없습니다. Source Asset 이 지정되었는지 확인하세요.", this);
            return;
        }

        ApplyImage(root);
        ApplyRandomMessage(root);
    }

    /// <summary>인스펙터에 지정된 이미지를 배경 요소에 적용합니다.</summary>
    private void ApplyImage(VisualElement root)
    {
        var imageElement = root.Q<VisualElement>(ImageElementName);
        if (imageElement == null)
        {
            Debug.LogWarning($"[LoadingScreenController] '{ImageElementName}' 요소를 찾을 수 없습니다.", this);
            return;
        }

        if (loadingImage != null)
            imageElement.style.backgroundImage = new StyleBackground(loadingImage);
    }

    /// <summary>메시지 목록 중 하나를 랜덤으로 골라 하단 라벨에 표시합니다.</summary>
    private void ApplyRandomMessage(VisualElement root)
    {
        var messageLabel = root.Q<Label>(MessageElementName);
        if (messageLabel == null)
        {
            Debug.LogWarning($"[LoadingScreenController] '{MessageElementName}' 요소를 찾을 수 없습니다.", this);
            return;
        }

        if (randomMessages == null || randomMessages.Count == 0)
        {
            messageLabel.text = string.Empty;
            return;
        }

        int index = Random.Range(0, randomMessages.Count);
        messageLabel.text = randomMessages[index];
    }
}
