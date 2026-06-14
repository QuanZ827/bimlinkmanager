using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;

namespace BimLinkManager.Services
{
    /// <summary>
    /// Talks to the Autodesk Platform Services (APS) Data Management API
    /// over raw HTTPS. JSON parsed with System.Text.Json.JsonDocument — no third-party packages.
    /// </summary>
    public class AccDataService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly TokenService _tokens;
        private bool _disposed;

        public AccDataService(TokenService tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(AppConstants.HttpTimeoutSeconds),
                BaseAddress = new Uri(AppConstants.ApsBaseUrl + "/")
            };
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("BimLinkManager/" + AppConstants.Version);
        }

        // ============================================================
        // Public API
        // ============================================================

        public async Task<List<AccHub>> GetHubsAsync(CancellationToken ct = default)
        {
            var json = await GetJsonAsync("project/v1/hubs", ct).ConfigureAwait(false);
            var hubs = new List<AccHub>();
            if (json == null) return hubs;
            using (json)
            {
                if (!json.RootElement.TryGetProperty("data", out var data)) return hubs;
                foreach (var el in data.EnumerateArray())
                {
                    var hub = new AccHub
                    {
                        Id = GetStr(el, "id"),
                        Name = GetNestedStr(el, "attributes", "name"),
                        Region = GetNestedStr(el, "attributes", "region")
                    };
                    if (!string.IsNullOrEmpty(hub.Id)) hubs.Add(hub);
                }
            }
            return hubs;
        }

        public async Task<List<AccProject>> GetProjectsAsync(AccHub hub, CancellationToken ct = default)
        {
            if (hub == null || string.IsNullOrEmpty(hub.Id)) return new List<AccProject>();
            var projects = new List<AccProject>();
            var url = "project/v1/hubs/" + Uri.EscapeDataString(hub.Id) + "/projects";
            while (!string.IsNullOrEmpty(url))
            {
                var json = await GetJsonAsync(url, ct).ConfigureAwait(false);
                if (json == null) break;
                using (json)
                {
                    if (json.RootElement.TryGetProperty("data", out var data))
                    {
                        foreach (var el in data.EnumerateArray())
                        {
                            var proj = new AccProject
                            {
                                Id = GetStr(el, "id"),
                                Name = GetNestedStr(el, "attributes", "name"),
                                HubId = hub.Id,
                                Region = hub.Region
                            };
                            if (!string.IsNullOrEmpty(proj.Id)) projects.Add(proj);
                        }
                    }
                    url = GetNextUrl(json.RootElement);
                }
            }
            return projects;
        }

        public async Task<List<AccFolder>> GetTopFoldersAsync(AccProject project, CancellationToken ct = default)
        {
            var folders = new List<AccFolder>();
            if (project == null || string.IsNullOrEmpty(project.HubId) || string.IsNullOrEmpty(project.Id))
                return folders;
            var url = "project/v1/hubs/" + Uri.EscapeDataString(project.HubId)
                + "/projects/" + Uri.EscapeDataString(project.Id) + "/topFolders";
            var json = await GetJsonAsync(url, ct).ConfigureAwait(false);
            if (json == null) return folders;
            using (json)
            {
                if (!json.RootElement.TryGetProperty("data", out var data)) return folders;
                foreach (var el in data.EnumerateArray())
                {
                    var folder = new AccFolder
                    {
                        Id = GetStr(el, "id"),
                        Name = GetNestedStr(el, "attributes", "displayName")
                            ?? GetNestedStr(el, "attributes", "name"),
                        ProjectId = project.Id
                    };
                    if (!string.IsNullOrEmpty(folder.Id)) folders.Add(folder);
                }
            }
            return folders;
        }

        public async Task<AccFolderContents> GetFolderContentsAsync(string projectId, string folderId, string region, CancellationToken ct = default)
        {
            var result = new AccFolderContents();
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(folderId)) return result;

            // Versions reference by relationship id. We need to join items -> their tip version
            // in `included`. Build a lookup keyed by version urn so we can find GUIDs per item.
            var versionsByUrn = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var itemTipUrn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var url = "data/v1/projects/" + Uri.EscapeDataString(projectId)
                + "/folders/" + Uri.EscapeDataString(folderId) + "/contents";
            while (!string.IsNullOrEmpty(url))
            {
                var json = await GetJsonAsync(url, ct).ConfigureAwait(false);
                if (json == null) break;
                using (json)
                {
                    if (json.RootElement.TryGetProperty("included", out var inc) && inc.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in inc.EnumerateArray())
                        {
                            var type = GetStr(v, "type");
                            var id = GetStr(v, "id");
                            if (string.Equals(type, "versions", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(id))
                            {
                                versionsByUrn[id] = v.Clone();
                            }
                        }
                    }

                    if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in data.EnumerateArray())
                        {
                            var type = GetStr(el, "type");
                            var id = GetStr(el, "id");
                            if (string.IsNullOrEmpty(id)) continue;

                            if (string.Equals(type, "folders", StringComparison.OrdinalIgnoreCase))
                            {
                                var sub = new AccFolder
                                {
                                    Id = id,
                                    Name = GetNestedStr(el, "attributes", "displayName")
                                        ?? GetNestedStr(el, "attributes", "name"),
                                    ProjectId = projectId
                                };
                                result.Subfolders.Add(sub);
                            }
                            else if (string.Equals(type, "items", StringComparison.OrdinalIgnoreCase))
                            {
                                if (el.TryGetProperty("relationships", out var rel)
                                    && rel.TryGetProperty("tip", out var tip)
                                    && tip.TryGetProperty("data", out var tipData))
                                {
                                    var versionUrn = GetStr(tipData, "id");
                                    if (!string.IsNullOrEmpty(versionUrn))
                                    {
                                        itemTipUrn[id] = versionUrn;
                                    }
                                }
                            }
                        }
                    }

                    url = GetNextUrl(json.RootElement);
                }
            }

            // Project the item/version pairs into AccRvtFile entries (only .rvt with cloud GUIDs)
            foreach (var kv in itemTipUrn)
            {
                if (!versionsByUrn.TryGetValue(kv.Value, out var version)) continue;

                var attrs = version.TryGetProperty("attributes", out var a) ? a : default;
                if (attrs.ValueKind != JsonValueKind.Object) continue;

                var name = GetStr(attrs, "name") ?? GetStr(attrs, "displayName");
                if (string.IsNullOrEmpty(name)) continue;
                if (!name.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase)) continue;

                var file = new AccRvtFile
                {
                    ItemId = kv.Key,
                    VersionUrn = kv.Value,
                    Name = name,
                    DisplayName = GetStr(attrs, "displayName") ?? name,
                    ProjectId = projectId,
                    Region = region,
                    FolderId = folderId
                };

                if (attrs.TryGetProperty("storageSize", out var ss) && ss.ValueKind == JsonValueKind.Number
                    && ss.TryGetInt64(out var sizeVal))
                {
                    file.SizeBytes = sizeVal;
                }

                if (attrs.TryGetProperty("lastModifiedTime", out var lmt) && lmt.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(lmt.GetString(), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    file.ModifiedUtc = dt;
                }

                file.ModifiedBy = GetStr(attrs, "lastModifiedUserName");

                if (attrs.TryGetProperty("versionNumber", out var vn) && vn.ValueKind == JsonValueKind.Number
                    && vn.TryGetInt32(out var vnum))
                {
                    file.VersionNumber = vnum;
                }

                // Cloud GUIDs: attributes.extension.data.{projectGuid, modelGuid}
                if (attrs.TryGetProperty("extension", out var ext)
                    && ext.TryGetProperty("data", out var extData))
                {
                    file.ProjectGuid = GetStr(extData, "projectGuid");
                    file.ModelGuid = GetStr(extData, "modelGuid");
                }

                if (file.HasCloudGuids)
                {
                    result.Files.Add(file);
                }
                else
                {
                    Log.Info("Skipping non-cloud .rvt: " + file.Name + " (item " + kv.Key + ")");
                }
            }

            return result;
        }

        // ============================================================
        // Project matching from doc.PathName
        // ============================================================

        /// <summary>
        /// Extracts the hub name and project name from a Revit cloud path like
        /// "BIM 360://HubName/ProjectName/Folder/File.rvt".
        /// </summary>
        public static (string hubName, string projectName) ParseCloudPathName(string pathName)
        {
            if (string.IsNullOrEmpty(pathName)) return (null, null);
            var schemeIdx = pathName.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx < 0) return (null, null);
            var rest = pathName.Substring(schemeIdx + 3);
            var parts = rest.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return (null, null);
            if (parts.Length == 1) return (parts[0], null);
            return (parts[0], parts[1]);
        }

        // ============================================================
        // Internals
        // ============================================================

        private async Task<JsonDocument> GetJsonAsync(string urlOrPath, CancellationToken ct)
        {
            var token = _tokens.TryGetAccessToken(out var tokenError);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Cannot reach ACC: " + (tokenError ?? "no access token"));
            }

            using (var req = new HttpRequestMessage(HttpMethod.Get, urlOrPath))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await SafeReadAsync(resp).ConfigureAwait(false);
                        var msg = "APS " + (int)resp.StatusCode + " " + resp.ReasonPhrase + " for " + urlOrPath
                            + (string.IsNullOrEmpty(body) ? "" : (": " + Truncate(body, 500)));
                        Log.Warn(msg);
                        throw new HttpRequestException(msg);
                    }
                    var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    return await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);
                }
            }
        }

        private static async Task<string> SafeReadAsync(HttpResponseMessage resp)
        {
            try { return await resp.Content.ReadAsStringAsync().ConfigureAwait(false); }
            catch { return string.Empty; }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "...";
        }

        private static string GetStr(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(name, out var v)) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private static string GetNestedStr(JsonElement el, string first, string second)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(first, out var inner)) return null;
            return GetStr(inner, second);
        }

        private static string GetNextUrl(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("links", out var links)) return null;
            if (links.ValueKind != JsonValueKind.Object) return null;
            if (!links.TryGetProperty("next", out var next)) return null;
            if (next.ValueKind == JsonValueKind.String) return next.GetString();
            if (next.ValueKind == JsonValueKind.Object && next.TryGetProperty("href", out var href)
                && href.ValueKind == JsonValueKind.String)
            {
                return href.GetString();
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _http.Dispose(); } catch { }
        }
    }
}
