using System;
using System.Collections;
using UnityEngine.Networking;

namespace BodyForge
{
    internal static class ReleaseUpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/LaisterDev/BodyForge/releases/latest";

        public static IEnumerator Check()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ApiUrl))
            {
                request.timeout = 10;
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("User-Agent", "BodyForge/" + PluginInfo.Version);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
                {
                    BodyForgePlugin.LogWarn("update check unavailable: " + request.error);
                    UpdateNotice.SetAvailable(false);
                    yield break;
                }

                try
                {
                    MiniJson.Node release = MiniJson.Parse(request.downloadHandler.text);
                    bool stable = !(release.Get("draft")?.AsBool(true) ?? true)
                        && !(release.Get("prerelease")?.AsBool(true) ?? true);
                    string tag = release.Get("tag_name")?.AsString("") ?? "";
                    UpdateNotice.SetAvailable(stable && IsNewer(tag, PluginInfo.Version));
                }
                catch (Exception error)
                {
                    BodyForgePlugin.LogWarn("update check response was invalid: " + error.Message);
                    UpdateNotice.SetAvailable(false);
                }
            }
        }

        internal static bool IsNewer(string candidate, string installed)
        {
            System.Version latest;
            System.Version current;
            return System.Version.TryParse(Normalize(candidate), out latest)
                && System.Version.TryParse(Normalize(installed), out current)
                && latest.CompareTo(current) > 0;
        }

        private static string Normalize(string version)
        {
            string normalized = (version ?? "").Trim();
            return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(1)
                : normalized;
        }
    }
}
