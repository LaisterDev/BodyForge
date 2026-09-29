using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BodyForge
{
    internal static class UpdateNotice
    {
        public const string Message = "A new version of BodyForge is available.";
        private const string LinkName = "BodyForgeUpdateLink";
        private const string ReleaseUrl = "https://github.com/LaisterDev/BodyForge/releases/latest";
        private const float DelayAfterSpawnSeconds = 2f;

        private static bool _shown;
        private static bool _available;
        private static float _playerDetectedAt = -1f;

        public static void Tick()
        {
            if (!_available || _shown || Player.m_localPlayer == null || MessageHud.instance == null) return;

            if (_playerDetectedAt < 0f)
            {
                _playerDetectedAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _playerDetectedAt < DelayAfterSpawnSeconds) return;

            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, Message);
            BodyForgePlugin.LogInfo("update notice displayed: " + Message);
            _shown = true;
        }

        public static void SetAvailable(bool available)
        {
            _available = available;
            FejdStartup menu = FejdStartup.instance;
            if (available)
            {
                AddToMainMenu(menu);
                return;
            }

            Transform parent = menu?.m_moddedText?.transform.parent;
            Transform existing = parent?.Find(LinkName);
            if (existing != null) Object.Destroy(existing.gameObject);
        }

        public static void AddToMainMenu(FejdStartup menu)
        {
            if (!_available) return;
            GameObject source = menu?.m_showChangelogButton;
            RectTransform moddedRect = menu?.m_moddedText?.transform as RectTransform;
            if (source == null || moddedRect == null || moddedRect.parent == null
                || moddedRect.parent.Find(LinkName) != null) return;

            TMP_Text sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
            Button sourceButton = source.GetComponent<Button>() ?? source.GetComponentInChildren<Button>(true);
            if (sourceLabel == null || sourceButton == null)
            {
                BodyForgePlugin.LogWarn("could not read the native Changelog button style");
                return;
            }

            GameObject link = Object.Instantiate(sourceLabel.gameObject, moddedRect.parent, false);
            link.name = LinkName;
            link.transform.SetSiblingIndex(moddedRect.GetSiblingIndex());

            foreach (Localize localize in link.GetComponentsInChildren<Localize>(true))
                Object.DestroyImmediate(localize);
            foreach (Button inheritedButton in link.GetComponents<Button>())
                Object.DestroyImmediate(inheritedButton);

            TMP_Text label = link.GetComponent<TMP_Text>();
            if (label == null)
            {
                Object.Destroy(link);
                BodyForgePlugin.LogWarn("could not clone the native Changelog label");
                return;
            }

            label.text = Message;
            label.enableAutoSizing = false;
            label.fontSize = sourceLabel.fontSize * 2f;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.raycastTarget = true;

            RectTransform linkRect = link.transform as RectTransform;
            if (linkRect == null)
            {
                Object.Destroy(link);
                BodyForgePlugin.LogWarn("the cloned update link has no RectTransform");
                return;
            }
            linkRect.anchorMin = moddedRect.anchorMin;
            linkRect.anchorMax = moddedRect.anchorMax;
            linkRect.pivot = new Vector2(1f, moddedRect.pivot.y);
            label.ForceMeshUpdate();
            float linkWidth = label.preferredWidth;
            float linkHeight = Mathf.Max(linkRect.rect.height, label.preferredHeight);
            linkRect.sizeDelta = new Vector2(linkWidth, linkHeight);
            float warningRight = moddedRect.anchoredPosition.x
                + moddedRect.rect.width * (1f - moddedRect.pivot.x);
            float warningTop = moddedRect.anchoredPosition.y
                + moddedRect.rect.height * (1f - moddedRect.pivot.y);
            linkRect.anchoredPosition = new Vector2(
                warningRight,
                warningTop + 12f + linkHeight * linkRect.pivot.y
            );

            Button button = link.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = label;
            button.colors = sourceButton.colors;
            button.onClick.AddListener(() => Application.OpenURL(ReleaseUrl));
            link.SetActive(true);
            BodyForgePlugin.LogInfo("clickable update notice added to main menu");
        }
    }

    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class UpdateNoticeMainMenuPatch
    {
        private static void Postfix(FejdStartup __instance)
        {
            UpdateNotice.AddToMainMenu(__instance);
        }
    }
}
