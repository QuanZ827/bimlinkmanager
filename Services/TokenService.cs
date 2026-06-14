using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BimLinkManager.Diagnostics;

namespace BimLinkManager.Services
{
    /// <summary>
    /// Extracts the user's OAuth2 access token from Revit's bundled SSONET.dll
    /// via reflection. SSONET.dll holds the token from the user's active
    /// Autodesk sign-in session. This is undocumented internal API — every
    /// reflection call is defensive.
    /// </summary>
    public class TokenService
    {
        private const string SsonetTypeName = "Autodesk.Revit.AdWebServicesBase";
        private const string GetInstanceMethod = "GetInstance";
        private const string GetTokenMethod = "GetOAuth2AccessToken";

        private static Assembly _cachedAssembly;
        private static readonly object _gate = new object();

        /// <summary>
        /// Returns a fresh OAuth2 access token for the active Autodesk session,
        /// or null if the user is not signed in or token retrieval failed.
        /// </summary>
        public string TryGetAccessToken(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                var asm = LoadSsonetAssembly();
                if (asm == null)
                {
                    errorMessage = "SSONET.dll not found in the Revit install directory.";
                    return null;
                }

                var type = asm.GetType(SsonetTypeName, throwOnError: false);
                if (type == null)
                {
                    errorMessage = "SSONET.dll loaded but type " + SsonetTypeName + " not found. This Revit version may not be supported.";
                    Log.Warn(errorMessage);
                    return null;
                }

                object instance = null;
                try
                {
                    var getInstance = type.GetMethod(
                        GetInstanceMethod,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    if (getInstance == null)
                    {
                        errorMessage = "Method " + GetInstanceMethod + " not found on " + SsonetTypeName + ".";
                        return null;
                    }
                    instance = getInstance.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    errorMessage = "Calling " + GetInstanceMethod + " threw: " + InnerMessage(ex);
                    Log.Error(errorMessage, ex);
                    return null;
                }

                if (instance == null)
                {
                    errorMessage = GetInstanceMethod + " returned null. User may not be signed in.";
                    return null;
                }

                try
                {
                    var getToken = type.GetMethod(
                        GetTokenMethod,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (getToken == null)
                    {
                        errorMessage = "Method " + GetTokenMethod + " not found on " + SsonetTypeName + ".";
                        return null;
                    }
                    var result = getToken.Invoke(instance, null);
                    var token = result as string;
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        errorMessage = "No access token available. Please sign in to Autodesk from the Revit File menu.";
                        return null;
                    }
                    return token;
                }
                catch (Exception ex)
                {
                    errorMessage = "Calling " + GetTokenMethod + " threw: " + InnerMessage(ex);
                    Log.Error(errorMessage, ex);
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Token extraction failed: " + InnerMessage(ex);
                Log.Error(errorMessage, ex);
                return null;
            }
        }

        private static Assembly LoadSsonetAssembly()
        {
            lock (_gate)
            {
                if (_cachedAssembly != null) return _cachedAssembly;

                var revitDir = GetRevitInstallDirectory();
                if (string.IsNullOrEmpty(revitDir)) return null;

                var ssonetPath = Path.Combine(revitDir, "SSONET.dll");
                if (!File.Exists(ssonetPath))
                {
                    Log.Warn("SSONET.dll not found at " + ssonetPath);
                    return null;
                }

                try
                {
                    _cachedAssembly = Assembly.LoadFrom(ssonetPath);
                    return _cachedAssembly;
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to load SSONET.dll from " + ssonetPath, ex);
                    return null;
                }
            }
        }

        private static string GetRevitInstallDirectory()
        {
            try
            {
                var module = Process.GetCurrentProcess().MainModule;
                if (module == null) return null;
                var exePath = module.FileName;
                if (string.IsNullOrEmpty(exePath)) return null;
                return Path.GetDirectoryName(exePath);
            }
            catch
            {
                return null;
            }
        }

        private static string InnerMessage(Exception ex)
        {
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            return inner.Message;
        }
    }
}
