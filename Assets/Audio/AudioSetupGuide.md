# AudioMixer 및 SoundManager 설정 가이드

본 프로젝트에는 SFX 풀링, BGM 크로스페이드, 볼륨 제어를 담당하는 `SoundManager`가 구현되어 있습니다.
더욱 정밀한 사운드 제어와 오디오 이펙트(리버브, EQ 등) 확장을 위해 Unity `AudioMixer`를 연동하는 방법을 안내합니다.

---

## 1. AudioMixer 에셋 생성
1. Unity Editor의 `Project` 창에서 `Assets/Audio/` 폴더로 이동합니다.
2. 우클릭 -> **Create** -> **Audio** -> **Audio Mixer**를 선택합니다.
3. 에셋 이름을 `MainAudioMixer`로 지정합니다.

---

## 2. 오디오 믹서 그룹(Groups) 구성
1. 생성한 `MainAudioMixer`를 더블 클릭하여 **Audio Mixer** 창을 엽니다.
2. **Groups** 패널에서 기본 `Master` 그룹 아래에 2개의 자식 그룹을 추가합니다:
   - `Master` 선택 -> **+** 클릭 -> `BGM` 입력
   - `Master` 선택 -> **+** 클릭 -> `SFX` 입력

---

## 3. 볼륨 파라미터 노출 (Expose Parameters)
스크립트(`SoundManager`)에서 슬라이더 볼륨을 조절할 수 있도록 각 그룹의 볼륨을 노출해야 합니다:

1. `Master` 그룹 선택 -> Inspector 창의 **Attenuation** 헤더 아래 **Volume** 우클릭 -> **Expose 'Volume (of Master)' to script** 클릭.
2. `BGM` 그룹 선택 -> Inspector 창의 **Attenuation** 헤더 아래 **Volume** 우클릭 -> **Expose 'Volume (of BGM)' to script** 클릭.
3. `SFX` 그룹 선택 -> Inspector 창의 **Attenuation** 헤더 아래 **Volume** 우클릭 -> **Expose 'Volume (of SFX)' to script** 클릭.
4. Audio Mixer 창 우측 상단의 **Exposed Parameters** 드롭다운을 클릭합니다.
5. 노출된 파라미터 이름을 더블 클릭(또는 우클릭 Rename)하여 다음과 같이 변경합니다:
   - `MyExposedParam` (Master Volume) -> **`MasterVolume`**
   - `MyExposedParam 1` (BGM Volume) -> **`BgmVolume`**
   - `MyExposedParam 2` (SFX Volume) -> **`SfxVolume`**

---

## 4. SoundManager 컴포넌트 할당
1. 씬에 빈 GameObject를 생성하고 이름을 `SoundManager`로 지정합니다.
2. `SoundManager` 컴포넌트를 추가합니다.
3. Inspector에서 다음 항목을 연결합니다:
   - **Audio Mixer**: `MainAudioMixer`
   - **Master Group**: `Master` 그룹
   - **Bgm Group**: `BGM` 그룹
   - **Sfx Group**: `SFX` 그룹
   - (참고: `SoundManager`는 `DontDestroyOnLoad`로 유지되므로 씬 전환 시에도 파괴되지 않습니다.)

> **안내**: AudioMixer가 할당되지 않은 상태에서도 `SoundManager`는 자체 Fallback 로직을 통해 AudioSource 볼륨을 직접 제어하므로 에러 없이 정상 작동합니다.
