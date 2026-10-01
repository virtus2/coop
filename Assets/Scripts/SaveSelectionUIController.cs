using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CoopGame.SaveSystem;

public class SaveSelectionUIController : MonoBehaviour
{
    [System.Serializable]
    public class SaveSlotUI
    {
        public Button slotButton;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI infoText;
        public Button deleteButton;
        
        [HideInInspector]
        public string slotId; // e.g. "Slot_1"
    }

    [Header("UI References")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private SaveSlotUI[] _slots;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _errorPopup;
    [SerializeField] private TextMeshProUGUI _errorText;
    [SerializeField] private Button _errorCloseButton;

    private Action _onLobbyHostConfirmed;

    private void Start()
    {
        _panel.SetActive(false);
        _errorPopup.SetActive(false);

        _closeButton.onClick.AddListener(() => _panel.SetActive(false));
        _errorCloseButton.onClick.AddListener(() => _errorPopup.SetActive(false));

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            slot.slotId = $"Slot_{i + 1}";
            slot.slotButton.onClick.AddListener(() => OnSlotClicked(slot));
            if (slot.deleteButton != null)
            {
                slot.deleteButton.onClick.AddListener(() => OnDeleteClicked(slot));
            }
        }
    }

    public void Open(Action onLobbyHostConfirmed)
    {
        _onLobbyHostConfirmed = onLobbyHostConfirmed;
        RefreshSlots();
        _panel.SetActive(true);
    }

    private void RefreshSlots()
    {
        if (SaveLoadManager.Instance == null) return;

        foreach (var slot in _slots)
        {
            var (exists, isCorrupted, data) = SaveLoadManager.Instance.GetSaveFileInfo(slot.slotId);

            if (isCorrupted)
            {
                slot.titleText.text = $"{slot.slotId} - Corrupted";
                slot.infoText.text = "Save file is broken. Please delete.";
                if (slot.deleteButton != null) slot.deleteButton.gameObject.SetActive(true);
            }
            else if (exists && data != null)
            {
                slot.titleText.text = $"{slot.slotId}";
                // Display Date, Time, Days
                DateTime lastPlayed;
                if (!DateTime.TryParse(data.timestamp, out lastPlayed))
                {
                    lastPlayed = DateTime.MinValue;
                }
                
                string playTimeStr = TimeSpan.FromSeconds(data.playTime).ToString(@"hh\:mm");
                slot.infoText.text = $"Day {data.inGameDays} | Time: {playTimeStr} | {lastPlayed.ToString("yyyy-MM-dd HH:mm")}";
                if (slot.deleteButton != null) slot.deleteButton.gameObject.SetActive(true);
            }
            else
            {
                slot.titleText.text = $"{slot.slotId} - Empty";
                slot.infoText.text = "New Game";
                if (slot.deleteButton != null) slot.deleteButton.gameObject.SetActive(false);
            }
        }
    }

    private void OnSlotClicked(SaveSlotUI slot)
    {
        if (SaveLoadManager.Instance == null) return;

        var (exists, isCorrupted, data) = SaveLoadManager.Instance.GetSaveFileInfo(slot.slotId);

        if (isCorrupted)
        {
            ShowError("Save file is corrupted. Cannot start lobby with this save.");
            return;
        }

        if (exists)
        {
            SaveLoadManager.Instance.SetCurrentSlot(slot.slotId);
        }
        else
        {
            SaveLoadManager.Instance.CreateNewSave(slot.slotId);
        }

        _panel.SetActive(false);
        _onLobbyHostConfirmed?.Invoke();
    }

    private void OnDeleteClicked(SaveSlotUI slot)
    {
        if (SaveLoadManager.Instance == null) return;
        SaveLoadManager.Instance.DeleteSaveFile(slot.slotId);
        RefreshSlots();
    }

    private void ShowError(string message)
    {
        _errorText.text = message;
        _errorPopup.SetActive(true);
    }
}
