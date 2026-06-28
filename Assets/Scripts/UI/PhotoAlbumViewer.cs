// PhotoAlbumViewer.cs
// 기능: PhotoAlbumViewer.uxml 로 만든 갤러리/사진 상세 UI에 PhotoAlbum 사진 데이터를 바인딩한다.
// 사용: PhotoAlbumView GameObject 의 UIDocument Source Asset 을 PhotoAlbumViewer.uxml 로 지정하고,
//       같은 GameObject 에 이 컴포넌트를 붙인다. 앨범 모드에서만 표시된다.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PhotoAlbumViewer : MonoBehaviour
{
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;
    private const int SlotsPerPage = 9;

    private readonly List<VisualElement> _slots = new();
    private readonly List<Image> _slotImages = new();

    private VisualElement _root;
    private VisualElement _designRoot;
    private VisualElement _galleryPanel;
    private VisualElement _detailPanel;
    private Label _galleryCountLabel;
    private Label _detailCountLabel;
    private Image _detailImage;
    private Label _prevButton;
    private Label _nextButton;
    private Label _closeButton;

    private CameraController _controller;
    private PhotoAlbum _album;
    private int _selectedIndex = -1;

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (_album != null)
            _album.OnPhotoAdded -= OnPhotoAdded;

        if (_controller != null)
        {
            _controller.OnModeChanged -= OnModeChanged;
            _controller.OnViewModeChanged -= OnViewModeChanged;
        }

        CursorStateController.Release(this);
        CameraLookLock.Release(this);
    }

    private IEnumerator BindWhenReady()
    {
        yield return null;

        var uiRoot = GetComponent<UIDocument>().rootVisualElement;
        if (uiRoot == null)
        {
            Debug.LogError("[PhotoAlbumViewer] UIDocument rootVisualElement 가 없습니다.", this);
            yield break;
        }

        BindUxml(uiRoot);
        if (_root == null || _designRoot == null || _galleryPanel == null || _detailPanel == null)
        {
            Debug.LogError("[PhotoAlbumViewer] PhotoAlbumViewer.uxml 의 필수 요소(album-root/design-root/gallery-panel/detail-panel)를 찾지 못했습니다.", this);
            yield break;
        }

        _root.RegisterCallback<GeometryChangedEvent>(_ => FitDesignRoot());

        _album = PhotoAlbum.Current;
        if (_album != null)
            _album.OnPhotoAdded += OnPhotoAdded;
        else
            Debug.LogWarning("[PhotoAlbumViewer] 씬에서 PhotoAlbum 을 찾지 못했습니다. 빈 앨범 UI 만 표시합니다.", this);

        _controller = CameraController.Current;
        if (_controller != null)
        {
            _controller.OnModeChanged += OnModeChanged;
            _controller.OnViewModeChanged += OnViewModeChanged;
        }

        RefreshAll();
        UpdateVisibility();
    }

    private void BindUxml(VisualElement uiRoot)
    {
        _root = uiRoot.Q<VisualElement>("album-root");
        _designRoot = uiRoot.Q<VisualElement>("album-design-root");
        _galleryPanel = uiRoot.Q<VisualElement>("album-gallery-panel");
        _detailPanel = uiRoot.Q<VisualElement>("album-detail-panel");
        _galleryCountLabel = uiRoot.Q<Label>("gallery-count");
        _detailCountLabel = uiRoot.Q<Label>("detail-count");
        _detailImage = uiRoot.Q<Image>("album-detail-image");
        _prevButton = uiRoot.Q<Label>("detail-prev");
        _nextButton = uiRoot.Q<Label>("detail-next");
        _closeButton = uiRoot.Q<Label>("detail-close");

        _slots.Clear();
        _slotImages.Clear();
        for (int i = 0; i < SlotsPerPage; i++)
        {
            int index = i;
            var slot = uiRoot.Q<VisualElement>($"gallery-slot-{i}");
            var image = uiRoot.Q<Image>($"gallery-slot-image-{i}");
            if (slot == null || image == null) continue;

            image.scaleMode = ScaleMode.ScaleAndCrop;
            slot.RegisterCallback<ClickEvent>(_ => OpenDetail(index));
            slot.RegisterCallback<PointerUpEvent>(_ => OpenDetail(index));
            image.RegisterCallback<ClickEvent>(evt =>
            {
                OpenDetail(index);
                evt.StopPropagation();
            });
            image.RegisterCallback<PointerUpEvent>(evt =>
            {
                OpenDetail(index);
                evt.StopPropagation();
            });
            _slots.Add(slot);
            _slotImages.Add(image);
        }

        if (_detailImage != null)
            _detailImage.scaleMode = ScaleMode.ScaleAndCrop;

        _prevButton?.RegisterCallback<ClickEvent>(_ => SelectRelative(-1));
        _nextButton?.RegisterCallback<ClickEvent>(_ => SelectRelative(1));
        _closeButton?.RegisterCallback<ClickEvent>(_ => ShowGallery());
    }

    private void FitDesignRoot()
    {
        float width = _root.resolvedStyle.width;
        float height = _root.resolvedStyle.height;
        if (width <= 0f || height <= 0f) return;

        float scale = Mathf.Min(width / DesignWidth, height / DesignHeight);
        _designRoot.style.left = (width - DesignWidth * scale) * 0.5f;
        _designRoot.style.top = (height - DesignHeight * scale) * 0.5f;
        _designRoot.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    private void OnPhotoAdded(PhotoAlbum.Photo _)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshGallerySlots();
        RefreshDetail();
    }

    private void RefreshGallerySlots()
    {
        int count = PhotoCount;
        for (int i = 0; i < _slots.Count; i++)
        {
            bool hasPhoto = GetPhotoTexture(i) != null;
            _slotImages[i].image = hasPhoto ? GetPhotoTexture(i) : null;
            _slotImages[i].style.display = hasPhoto ? DisplayStyle.Flex : DisplayStyle.None;
            _slots[i].EnableInClassList("slot-empty", !hasPhoto);
            _slots[i].SetEnabled(hasPhoto);
            _slots[i].pickingMode = hasPhoto ? PickingMode.Position : PickingMode.Ignore;
        }

        if (_galleryCountLabel != null)
            _galleryCountLabel.text = $"{count}/{SlotsPerPage}";
    }

    private void RefreshDetail()
    {
        if (_detailImage == null) return;

        Texture2D image = GetPhotoTexture(_selectedIndex);
        _detailImage.image = image;
        _detailImage.EnableInClassList("slot-empty", image == null);

        if (_detailCountLabel != null)
        {
            int visibleIndex = image != null ? _selectedIndex + 1 : 0;
            _detailCountLabel.text = $"{visibleIndex}/{SlotsPerPage}";
        }
    }

    private void OpenDetail(int index)
    {
        if (GetPhotoTexture(index) == null) return;

        _selectedIndex = index;
        RefreshDetail();
        _galleryPanel.style.display = DisplayStyle.None;
        _detailPanel.style.display = DisplayStyle.Flex;
    }

    private void ShowGallery()
    {
        _detailPanel.style.display = DisplayStyle.None;
        _galleryPanel.style.display = DisplayStyle.Flex;
    }

    private void SelectRelative(int direction)
    {
        if (PhotoCount == 0) return;

        int start = Mathf.Clamp(_selectedIndex, 0, PhotoCount - 1);
        int next = Mathf.Clamp(start + direction, 0, PhotoCount - 1);
        if (next == _selectedIndex) return;

        _selectedIndex = next;
        RefreshDetail();
    }

    private Texture2D GetPhotoTexture(int index)
    {
        if (_album == null || index < 0 || index >= _album.Photos.Count || index >= SlotsPerPage) return null;
        return _album.Get(index).Image;
    }

    private int PhotoCount => _album != null ? Mathf.Min(_album.Photos.Count, SlotsPerPage) : 0;

    private void OnModeChanged(CameraMode _) => UpdateVisibility();
    private void OnViewModeChanged(CameraViewMode _) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (_root == null) return;

        bool show = _controller != null && _controller.IsAlbumView;
        _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (show) ShowGallery();
        ApplyCursorState(show);
    }

    private void ApplyCursorState(bool albumVisible)
    {
        if (albumVisible)
        {
            CursorStateController.RequestUnlocked(this);
            CameraLookLock.RequestLocked(this);
        }
        else
        {
            CursorStateController.Release(this);
            CameraLookLock.Release(this);
        }
    }
}
