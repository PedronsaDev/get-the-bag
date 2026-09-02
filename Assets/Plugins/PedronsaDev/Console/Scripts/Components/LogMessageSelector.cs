using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
public class LogMessageSelector : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ConsolePreferences m_Preferences;
    [SerializeField] private Image m_Background;

    private LogMessageSetup m_LogMessage;
    private static int _currentSelectedHash = 0;

    #region Events
    // Select event
    public static event Action<int> OnSelectLogMessage;
    public static event Action<int> OnLogMessageClicked;
    #endregion

    private Color m_BackgroundColor;
    public int Hash;
    private Color _currentColor;

    private void Start()
    {
        m_LogMessage = GetComponent<LogMessageSetup>();
        m_BackgroundColor = m_Background.color;
        _currentColor = m_BackgroundColor;
        Hash = gameObject.GetHashCode();
    }

    private void OnEnable()
    {
        OnSelectLogMessage += OnLogMessageSelected;
        OnLogMessageClicked += OnLogMessageSelected;
    }

    private void OnDisable()
    {
        OnSelectLogMessage -= OnLogMessageSelected;
        OnLogMessageClicked -= OnLogMessageSelected;
    }

    public static void LogMessageClicked(int hash)
    {
        OnLogMessageClicked?.Invoke(hash);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSelectLogMessage?.Invoke(Hash);
    }

    private void OnLogMessageSelected(int hash)
    {
        if (hash == this.Hash)
        {
            if (_currentSelectedHash == this.Hash)
            {
                m_Background.color = m_BackgroundColor;
                if (m_LogMessage.AltTextGameObject.activeSelf) 
                    m_LogMessage.DisableAltText();

                _currentSelectedHash = 0;
            }
            else
            {
                m_Background.color = m_Preferences.BackgroundSelectionColor;
                //m_LogMessage.EnableAltText();
                
                _currentSelectedHash = this.Hash;
            }
        }
        else if (m_Background.color == m_Preferences.BackgroundSelectionColor)
        {
            m_Background.color = m_BackgroundColor;
            if (m_LogMessage.AltTextGameObject.activeSelf) 
                m_LogMessage.DisableAltText();
        }

        _currentColor = m_Background.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        m_Background.color = m_Preferences.BackgroundHoverTextColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        m_Background.color = _currentColor;
    }
}
}