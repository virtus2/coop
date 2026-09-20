using System;
using Coop.Audio;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Coop.UI
{
    /// <summary>
    /// Unity UI Button을 상속하여 마우스/키보드/게임패드 네비게이션과 완벽히 호환되며,
    /// PrimeTween 기반의 풍부한 반응 애니메이션(Game Juice)과 다국어 지원(Localization),
    /// 그리고 사운드 효과(SoundCue)를 지원하는 범용 버튼 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Common Button (Juicy)", 30)]
    public class CommonButton : Button
    {
        [Header("Visual References")]
        [Tooltip("크기 및 위치 애니메이션이 적용될 RectTransform입니다. 미지정 시 본 오브젝트의 RectTransform을 사용합니다.")]
        [SerializeField] private RectTransform _targetTransform;

        [Tooltip("버튼 텍스트를 표시할 TextMeshProUGUI 컴포넌트입니다.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Tooltip("다국어 텍스트 바인딩을 위한 LocalizeStringEvent 컴포넌트입니다.")]
        [SerializeField] private LocalizeStringEvent _localizeStringEvent;

        [Header("Animation Settings (Game Juice)")]
        [Tooltip("PrimeTween 기반 인터랙션 애니메이션을 활성화할지 여부입니다.")]
        [SerializeField] private bool _useAnimations = true;

        [Tooltip("일시정지(Time.timeScale == 0) 상태에서도 버튼 애니메이션이 정상 동작하도록 unscaledTime을 사용합니다.")]
        [SerializeField] private bool _useUnscaledTime = true;

        [Header("Hover / Select Animation")]
        [Tooltip("마우스 호버 또는 키보드/패드 포커스 시 확대 배율입니다.")]
        [Range(0.8f, 1.5f)]
        [SerializeField] private float _hoverScale = 1.06f;

        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private Ease _hoverEase = Ease.OutBack;
        [SerializeField] private Color _hoverColorTint = new Color(1.15f, 1.15f, 1.15f, 1f);

        [Header("Press / Down Animation")]
        [Tooltip("버튼을 누르고 있을 때 축소 배율입니다.")]
        [Range(0.7f, 1.2f)]
        [SerializeField] private float _pressedScale = 0.94f;

        [Tooltip("버튼을 누를 때 Y축 아래로 살짝 밀리는 거리입니다. (물리 버튼 느낌)")]
        [SerializeField] private float _pressedYOffset = -2.5f;

        [SerializeField] private float _pressedDuration = 0.08f;
        [SerializeField] private Ease _pressedEase = Ease.OutQuad;
        [SerializeField] private Color _pressedColorTint = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Click / Release Punch Animation")]
        [Tooltip("클릭 직후 튀어오르는 탄성 펀치 스케일 강도입니다.")]
        [SerializeField] private Vector3 _clickPunchScale = new Vector3(0.12f, -0.06f, 0f);

        [SerializeField] private float _clickPunchDuration = 0.22f;
        [SerializeField] private int _clickPunchFrequency = 10;

        [Header("Disabled State")]
        [SerializeField] private Color _disabledColorTint = new Color(0.55f, 0.55f, 0.55f, 0.6f);

        [Header("Sound Cues")]
        [Tooltip("버튼 호버 / 선택 시 재생할 사운드 에셋입니다.")]
        [SerializeField] private SoundCueAsset _hoverSoundAsset;
        [SerializeField] private SoundCue _hoverSoundInline = new SoundCue();

        [Tooltip("버튼 클릭 성공 시 재생할 사운드 에셋입니다.")]
        [SerializeField] private SoundCueAsset _clickSoundAsset;
        [SerializeField] private SoundCue _clickSoundInline = new SoundCue();

        [Tooltip("비활성화(interactable == false) 상태에서 클릭 시도 시 재생할 사운드 에셋입니다. (선택 사항)")]
        [SerializeField] private SoundCueAsset _disabledClickSoundAsset;
        [SerializeField] private SoundCue _disabledClickSoundInline = new SoundCue();

        // 런타임 상태 캐싱
        private Vector3 _originalScale = Vector3.one;
        private Vector3 _originalLocalPos = Vector3.zero;
        private Color _originalGraphicColor = Color.white;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isInitialized = false;

        // PrimeTween 활성 인스턴스 핸들 (중복 실행 방지 및 부드러운 전환)
        private Tween _scaleTween;
        private Tween _posTween;
        private Tween _colorTween;

        public TextMeshProUGUI Label => _label;
        public LocalizeStringEvent LocalizeEvent => _localizeStringEvent;
        public bool IsHovered => _isHovered;
        public bool IsPressed => _isPressed;

        public SoundCue ActiveHoverSound => (_hoverSoundAsset != null) ? _hoverSoundAsset.Cue : _hoverSoundInline;
        public SoundCue ActiveClickSound => (_clickSoundAsset != null) ? _clickSoundAsset.Cue : _clickSoundInline;
        public SoundCue ActiveDisabledClickSound => (_disabledClickSoundAsset != null) ? _disabledClickSoundAsset.Cue : _disabledClickSoundInline;

        protected override void Awake()
        {
            base.Awake();
            InitializeReferences();

            // PrimeTween과 기본 Unity ColorTint 간의 충돌 방지를 위해 Transition을 None으로 강제
            transition = Transition.None;
        }

        private void InitializeReferences()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_targetTransform == null)
            {
                _targetTransform = GetComponent<RectTransform>();
            }

            if (_targetTransform != null)
            {
                _originalScale = _targetTransform.localScale;
                _originalLocalPos = _targetTransform.localPosition;
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            if (targetGraphic != null)
            {
                _originalGraphicColor = targetGraphic.color;
            }

            if (_label == null)
            {
                _label = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_localizeStringEvent == null)
            {
                _localizeStringEvent = GetComponentInChildren<LocalizeStringEvent>(true);
            }

            _isInitialized = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResetVisualStateImmediate();
        }

        protected override void OnDisable()
        {
            StopAllRunningTweens();
            ResetVisualStateImmediate();
            base.OnDisable();
        }

        #region Pointer & Event Handlers

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (!interactable)
            {
                return;
            }

            _isHovered = true;
            PlayHoverSound();
            AnimateHover(true);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            _isHovered = false;
            _isPressed = false;

            if (interactable)
            {
                AnimateHover(false);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!interactable)
            {
                return;
            }

            _isPressed = true;
            AnimatePressed();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (!interactable)
            {
                return;
            }

            _isPressed = false;

            if (_isHovered)
            {
                AnimateHover(true);
            }
            else
            {
                AnimateHover(false);
            }
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable)
            {
                PlayDisabledClickSound();
                return;
            }

            base.OnPointerClick(eventData);
            PlayClickSound();
            AnimateClickPunch();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (!interactable)
            {
                return;
            }

            _isHovered = true;
            PlayHoverSound();
            AnimateHover(true);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            _isHovered = false;
            _isPressed = false;

            if (interactable)
            {
                AnimateHover(false);
            }
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!interactable)
            {
                PlayDisabledClickSound();
                return;
            }

            base.OnSubmit(eventData);
            PlayClickSound();
            AnimateClickPunch();
        }

        #endregion

        #region Game Juice Animations (PrimeTween)

        private void AnimateHover(bool isEntering)
        {
            if (!_useAnimations || _targetTransform == null)
            {
                return;
            }

            _scaleTween.Stop();
            _posTween.Stop();
            _colorTween.Stop();

            Vector3 targetScale = isEntering ? (_originalScale * _hoverScale) : _originalScale;
            Vector3 targetPos = _originalLocalPos;
            Color targetColor = isEntering ? (_originalGraphicColor * _hoverColorTint) : _originalGraphicColor;

            _scaleTween = Tween.Scale(_targetTransform, targetScale, _hoverDuration, _hoverEase, useUnscaledTime: _useUnscaledTime);
            _posTween = Tween.LocalPosition(_targetTransform, targetPos, _hoverDuration, Ease.OutQuad, useUnscaledTime: _useUnscaledTime);

            if (targetGraphic != null)
            {
                _colorTween = Tween.Color(targetGraphic, targetColor, _hoverDuration, Ease.OutQuad, useUnscaledTime: _useUnscaledTime);
            }
        }

        private void AnimatePressed()
        {
            if (!_useAnimations || _targetTransform == null)
            {
                return;
            }

            _scaleTween.Stop();
            _posTween.Stop();
            _colorTween.Stop();

            Vector3 targetScale = _originalScale * _pressedScale;
            Vector3 targetPos = _originalLocalPos + new Vector3(0f, _pressedYOffset, 0f);
            Color targetColor = _originalGraphicColor * _pressedColorTint;

            _scaleTween = Tween.Scale(_targetTransform, targetScale, _pressedDuration, _pressedEase, useUnscaledTime: _useUnscaledTime);
            _posTween = Tween.LocalPosition(_targetTransform, targetPos, _pressedDuration, _pressedEase, useUnscaledTime: _useUnscaledTime);

            if (targetGraphic != null)
            {
                _colorTween = Tween.Color(targetGraphic, targetColor, _pressedDuration, _pressedEase, useUnscaledTime: _useUnscaledTime);
            }
        }

        private void AnimateClickPunch()
        {
            if (!_useAnimations || _targetTransform == null)
            {
                return;
            }

            _scaleTween.Stop();
            _posTween.Stop();

            // Y 오프셋 원위치
            _posTween = Tween.LocalPosition(_targetTransform, _originalLocalPos, 0.1f, Ease.OutQuad, useUnscaledTime: _useUnscaledTime);

            // 경쾌한 펀치 바운스 애니메이션
            _scaleTween = Tween.PunchScale(
                _targetTransform,
                _clickPunchScale,
                _clickPunchDuration,
                _clickPunchFrequency,
                useUnscaledTime: _useUnscaledTime
            );
        }

        private void StopAllRunningTweens()
        {
            _scaleTween.Stop();
            _posTween.Stop();
            _colorTween.Stop();
        }

        private void ResetVisualStateImmediate()
        {
            if (_targetTransform != null)
            {
                _targetTransform.localScale = _originalScale;
                _targetTransform.localPosition = _originalLocalPos;
            }

            if (targetGraphic != null)
            {
                targetGraphic.color = interactable ? _originalGraphicColor : (_originalGraphicColor * _disabledColorTint);
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            // Unity 기본 Selectable의 색상 변환을 차단하고 interactable 변화 시 시각적 피드백 처리
            if (targetGraphic == null)
            {
                return;
            }

            if (state == SelectionState.Disabled)
            {
                StopAllRunningTweens();
                targetGraphic.color = _originalGraphicColor * _disabledColorTint;
                if (_targetTransform != null)
                {
                    _targetTransform.localScale = _originalScale;
                    _targetTransform.localPosition = _originalLocalPos;
                }
            }
            else if (!instant && state == SelectionState.Normal && !_isHovered)
            {
                targetGraphic.color = _originalGraphicColor;
            }
        }

        #endregion

        #region Sound Helpers

        private void PlayHoverSound()
        {
            ActiveHoverSound?.Play();
        }

        private void PlayClickSound()
        {
            ActiveClickSound?.Play();
        }

        private void PlayDisabledClickSound()
        {
            ActiveDisabledClickSound?.Play();
        }

        #endregion

        #region Localization & Text API

        /// <summary>
        /// 버튼에 표시될 텍스트를 직접 설정합니다.
        /// </summary>
        public void SetText(string text)
        {
            if (_label != null)
            {
                _label.text = text;
            }
        }

        /// <summary>
        /// Unity Localization 테이블 이름과 항목 키를 지정하여 버튼 텍스트를 다국어로 변경합니다.
        /// </summary>
        /// <param name="tableName">StringTableCollection 이름 (예: "UIStrings")</param>
        /// <param name="entryKey">문자열 항목 키 (예: "btn_confirm")</param>
        public void SetLocalizedKey(string tableName, string entryKey)
        {
            if (_localizeStringEvent == null)
            {
                _localizeStringEvent = GetComponentInChildren<LocalizeStringEvent>(true);
            }

            if (_localizeStringEvent != null)
            {
                _localizeStringEvent.StringReference.SetReference(tableName, entryKey);
                _localizeStringEvent.RefreshString();
            }
            else if (_label != null)
            {
                // LocalizeStringEvent가 없을 경우 직접 테이블에서 조회 시도
                var localizedStr = new LocalizedString(tableName, entryKey);
                _label.text = localizedStr.GetLocalizedString();
            }
        }

        /// <summary>
        /// 기존 StringTable을 유지한 채 키만 변경합니다.
        /// </summary>
        public void SetLocalizedKey(string entryKey)
        {
            if (_localizeStringEvent != null && _localizeStringEvent.StringReference.TableReference.ReferenceType != UnityEngine.Localization.Tables.TableReference.Type.Empty)
            {
                _localizeStringEvent.StringReference.TableEntryReference = entryKey;
                _localizeStringEvent.RefreshString();
            }
        }

        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            transition = Transition.None;

            if (_targetTransform == null)
            {
                _targetTransform = GetComponent<RectTransform>();
            }

            if (_label == null)
            {
                _label = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_localizeStringEvent == null)
            {
                _localizeStringEvent = GetComponentInChildren<LocalizeStringEvent>(true);
            }
        }
#endif
    }
}
