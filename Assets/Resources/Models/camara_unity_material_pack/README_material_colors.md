# camara_unity_low Material Color Map

OBJ 파일 안의 `usemtl` 이름과 동일하게 맞춘 색상표입니다.

| Material | HEX | 용도 | Unity 권장값 |
|---|---:|---|---|
| `fujiXT3metalBright` | `#73776F` | 밝은 금속 바디 / 실버 엣지 | Metallic 1.0, Smoothness 0.55, Alpha 1.0 |
| `fujiXT3metalDark` | `#171A18` | 어두운 금속 프레임 | Metallic 1.0, Smoothness 0.42, Alpha 1.0 |
| `fujiXT3metalGlossy` | `#222522` | 유광 블랙 금속 | Metallic 1.0, Smoothness 0.75, Alpha 1.0 |
| `fujiXT3metalGlossy.001` | `#2F312D` | 스트랩 연결부 유광 금속 | Metallic 1.0, Smoothness 0.68, Alpha 1.0 |
| `fujiXT3metalRubber` | `#101211` | 고무 섞인 어두운 금속부 | Metallic 0.35, Smoothness 0.35, Alpha 1.0 |
| `fujiXT3plasticBlack` | `#070807` | 무광 검정 플라스틱 / 렌즈 외장 | Metallic 0.0, Smoothness 0.28, Alpha 1.0 |
| `fujiXT3glass` | `#0A1519` | 렌즈 유리 | Metallic 0.0, Smoothness 0.95, Alpha 0.45 |
| `screen` | `#07100F` | 후면 LCD 화면 | Metallic 0.0, Smoothness 0.8, Alpha 1.0 |
| `fujiXT3leather` | `#12100E` | 카메라 바디 가죽 텍스처부 | Metallic 0.0, Smoothness 0.18, Alpha 1.0 |
| `fujiXT3selectors` | `#A39D8F` | 다이얼/선택자 표식 | Metallic 1.0, Smoothness 0.5, Alpha 1.0 |
| `leather` | `#3B2719` | 스트랩 가죽 밝은 면 | Metallic 0.0, Smoothness 0.22, Alpha 1.0 |
| `leather2` | `#21160F` | 스트랩 가죽 어두운 면 | Metallic 0.0, Smoothness 0.18, Alpha 1.0 |
| `rope` | `#25231F` | 스트랩 로프/직물 | Metallic 0.0, Smoothness 0.12, Alpha 1.0 |
| `rubber` | `#0D0D0C` | 스트랩 고무/패드 | Metallic 0.0, Smoothness 0.2, Alpha 1.0 |

## Unity 사용법
1. `camara_unity_low.obj`와 `camara_unity_low.mtl`을 같은 폴더에 둡니다.
2. Unity의 `Assets` 폴더로 둘 다 넣습니다.
3. 색이 아직 흐리거나 유리가 불투명하면 material을 열어 수동으로 Metallic/Smoothness/Alpha를 조정합니다.
4. URP/HDRP를 쓰는 경우 `CreateCameraMaterials.cs`를 `Assets/Editor/` 폴더에 넣고 실행해 material을 따로 생성한 뒤 연결해도 됩니다.
