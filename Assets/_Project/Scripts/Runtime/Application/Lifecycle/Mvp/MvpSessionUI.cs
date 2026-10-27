using System.Text;
using Project.Core.Match;
using Project.Core.Match.Zones;
using Project.Core.UI;
using PurrNet;
using TMPro;
using UnityEngine;

namespace Project.Game.Mvp
{
    public sealed class MvpSessionUI : InputBlockerUI
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private TMP_Text _hud;
        [SerializeField] private TMP_InputField _address;
        [SerializeField] private UnityEngine.UI.Button _host;
        [SerializeField] private UnityEngine.UI.Button _join;
        [SerializeField] private UnityEngine.UI.Button _deploy;
        [SerializeField] private UnityEngine.UI.Button _return;
        [SerializeField] private UnityEngine.UI.Button _explore;
        [SerializeField] private UnityEngine.UI.Button _disconnect;
        private MvpPhase _lastPhase = (MvpPhase)255;
        private float _nextRefresh;

        private void Awake()
        {
            _registerOnEnable = false;
            _host.onClick.AddListener(() => MvpSessionController.Instance.HostLocal());
            _join.onClick.AddListener(() => MvpSessionController.Instance.JoinLocal(_address.text));
            _deploy.onClick.AddListener(() => MvpSessionController.Instance.DeployToBank());
            _return.onClick.AddListener(() => MvpSessionController.Instance.ReturnToHub());
            _disconnect.onClick.AddListener(() => MvpSessionController.Instance.Disconnect());
            _explore.onClick.AddListener(Close);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UIInput.OnToggleInventory += ToggleHubMenu;
        }

        protected override void OnDisable()
        {
            UIInput.OnToggleInventory -= ToggleHubMenu;
            base.OnDisable();
        }

        private void ToggleHubMenu()
        {
            if (!MvpSessionController.Instance || MvpSessionController.Instance.Phase != MvpPhase.Hub) return;
            if (IsOpen) Close(); else Open();
        }

        public override void Open()
        {
            if (_panel) _panel.SetActive(true);
            base.Open();
        }

        public override void Close()
        {
            if (isActiveAndEnabled && MvpSessionController.Instance && MvpSessionController.Instance.Phase != MvpPhase.Hub)
                return;
            base.Close();
            if (_panel) _panel.SetActive(false);
        }

        private void Update()
        {
            var session = MvpSessionController.Instance;
            if (!session || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            var phase = session.Phase;
            bool host = NetworkManager.main && NetworkManager.main.isServer;
            if (_lastPhase != phase)
            {
                _lastPhase = phase;
                if (phase == MvpPhase.Raid)
                {
                    base.Close();
                    _panel.SetActive(false);
                }
                else Open();
            }

            _host.gameObject.SetActive(phase == MvpPhase.Offline);
            _join.gameObject.SetActive(phase == MvpPhase.Offline);
            _address.gameObject.SetActive(phase == MvpPhase.Offline);
            _host.interactable = _join.interactable = !session.IsBusy;
            //_deploy.gameObject.SetActive(phase == MvpPhase.Hub && host);
            _explore.gameObject.SetActive(phase == MvpPhase.Hub);
            _return.gameObject.SetActive(host && phase is MvpPhase.Results or MvpPhase.Error);
            _disconnect.gameObject.SetActive(phase != MvpPhase.Offline);
            _deploy.interactable = _return.interactable = !session.IsBusy;

            switch (phase)
            {
                case MvpPhase.Offline:
                    _title.text = "GET THE BAG LOCAL PLAYTEST";
                    _body.text = session.IsBusy ? "Connecting…" :
                        "Host a party or enter the host's LAN address and join.\nOn this computer, use 127.0.0.1. UDP port: 5000.\n\n";
                    _hud.text = "";
                    break;
                case MvpPhase.Hub:
                    _title.text = "PARTY HUB";
                    _body.text = $"Party: {session.PartySize}/4\nDestination: Bank\n\n" +
                        (host ? "Launch when your friends have joined." : "Waiting for the host to launch the Bank.") +
                        "\nExplore the hub, then press Tab to reopen this menu.\n\n" + BuildSummary(session, false);
                    _hud.text = $"HUB {session.PartySize}/4 PLAYERS TAB: PARTY / DEPLOY";
                    break;
                case MvpPhase.Loading:
                    _title.text = "TRAVELLING";
                    _body.text = "Waiting for the party to load and spawn…";
                    _hud.text = "";
                    break;
                case MvpPhase.Raid:
                    var raid = RaidManager.Instance;
                    var extraction = FindAnyObjectByType<ExtractionZone>();
                    int seconds = raid ? Mathf.CeilToInt(Mathf.Max(0, raid.TimeRemaining.value)) : 0;
                    _hud.text = $"BANK {seconds / 60:00}:{seconds % 60:00}\n" +
                        (extraction && extraction.IsCountdownActive.value
                            ? $"EXTRACTION: {Mathf.CeilToInt(extraction.CountdownRemaining.value)}s GET TO THE VAN"
                            : "Loot valuables Fight guards [F] extraction switch beside the van");
                    break;
                case MvpPhase.Results:
                    _title.text = $"RUN {session.Summary.RunId} {session.Summary.Outcome}";
                    _body.text = BuildSummary(session, true) +
                        (host ? "\nReturn to the hub when everyone has read the results." : "\nWaiting for the host to return the party to the hub.");
                    _hud.text = "";
                    break;
                case MvpPhase.Error:
                    _title.text = "TRAVEL FAILED";
                    _body.text = (session.LastError ?? "The party could not complete the transition.") + "\nReturn to the hub or disconnect to try again.";
                    _hud.text = "";
                    break;
            }
        }

        private static string BuildSummary(MvpSessionController session, bool detailed)
        {
            var summary = session.Summary;
            if (summary.Players == null) return "Loot bank starts empty. Banked loot lasts for this hosted session.";
            var text = new StringBuilder();
            foreach (var result in summary.Players)
            {
                bool local = NetworkManager.main && NetworkManager.main.localPlayer == result.Player;
                text.Append(local ? "YOU" : $"Player {result.Player}");
                if (detailed)
                {
                    text.Append(result.Extracted ? "  EXTRACTED" : "  LOST / LEFT BEHIND");
                    text.Append("\n").Append(result.Loot).Append("\n");
                    text.Append(result.Extracted ? "Kept: $" : "Lost: $").Append(result.LootValue).Append(" ");
                }
                else text.Append(" ");
                text.Append("Session bank: $").Append(result.BankedValue).Append("\n\n");
            }
            return text.ToString();
        }
    }
}
