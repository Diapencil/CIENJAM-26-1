// PhotoAlbum.cs
// 기능: 플레이어가 카메라로 촬영한 사진(Texture2D)을 저장/조회하는 인게임 사진첩.
//       인게임 도메인 스코프 싱글톤이라 인게임 GameObject 소멸 시 함께 비워진다.
// 사용법: 인게임 씬에 빈 GameObject 를 만들어 본 컴포넌트를 붙인다. 촬영 측(CameraSystem)은
//           PhotoAlbum.Current?.Add(texture);
//         로 저장하고, 열람 UI 는
//           PhotoAlbum.Current.Photos        // 전체 목록(읽기 전용)
//           PhotoAlbum.Current.Get(index)    // 단건 조회
//           PhotoAlbum.Current.OnPhotoAdded  // 추가 시 갱신 이벤트
//         로 접근한다.

using System;
using System.Collections.Generic;
using UnityEngine;

public class PhotoAlbum : DomainSingleton<PhotoAlbum>
{
    /// <summary>촬영된 사진 1장.</summary>
    public readonly struct Photo
    {
        public readonly Texture2D Image;
        public readonly float Time; // 촬영 시각(Time.time)

        public Photo(Texture2D image, float time)
        {
            Image = image;
            Time = time;
        }
    }

    private readonly List<Photo> _photos = new();

    /// <summary>저장된 사진 목록(읽기 전용).</summary>
    public IReadOnlyList<Photo> Photos => _photos;

    /// <summary>사진이 추가될 때 발행된다. 인자는 방금 추가된 사진.</summary>
    public event Action<Photo> OnPhotoAdded;

    /// <summary>사진을 추가하고 저장된 인덱스를 반환한다. image 가 null 이면 -1.</summary>
    public int Add(Texture2D image)
    {
        if (image == null) return -1;

        var photo = new Photo(image, Time.time);
        _photos.Add(photo);
        OnPhotoAdded?.Invoke(photo);
        return _photos.Count - 1;
    }

    /// <summary>인덱스로 사진을 조회한다. 범위를 벗어나면 빈 Photo.</summary>
    public Photo Get(int index)
    {
        if (index < 0 || index >= _photos.Count) return default;
        return _photos[index];
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 도메인 종료 시 텍스처 메모리 정리
        foreach (var p in _photos)
            if (p.Image != null) Destroy(p.Image);
        _photos.Clear();
    }
}
