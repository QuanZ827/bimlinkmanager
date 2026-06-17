using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace BimLinkManager
{
    public class App : IExternalApplication
    {
        internal static App Instance { get; private set; }
        internal UIControlledApplication UiApp { get; private set; }

        // The batch-link ExternalEvent + handler MUST be created here in OnStartup (a valid
        // Revit API context), not in a window constructor — an ExternalEvent created off the
        // API context is never serviced, so Raise() silently no-ops and Execute never runs.
        // Mirrors WSPICT App.cs. Owned by App; the window raises BatchLinkExternalEvent.
        public static Execution.BatchLinkHandler BatchHandler { get; private set; }
        public static ExternalEvent BatchLinkExternalEvent { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Instance = this;
                UiApp = application;
                AppConstants.EnsureAppDataFolder();

                BatchHandler = new Execution.BatchLinkHandler();
                BatchLinkExternalEvent = ExternalEvent.Create(BatchHandler);

                try
                {
                    application.CreateRibbonTab(AppConstants.RibbonTabName);
                }
                catch
                {
                }

                var panel = application.CreateRibbonPanel(
                    AppConstants.RibbonTabName,
                    AppConstants.RibbonPanelName);

                var assembly = Assembly.GetExecutingAssembly();
                var assemblyPath = assembly.Location;

                var data = new PushButtonData(
                    AppConstants.BatchLinkButtonName,
                    AppConstants.BatchLinkButtonText,
                    assemblyPath,
                    typeof(Commands.BatchLinkCommand).FullName)
                {
                    ToolTip = AppConstants.BatchLinkTooltip,
                    LongDescription = "Discovers .rvt files in your ACC project and creates cloud links in a single batch operation."
                };

                TrySetIcons(data);

                panel.AddItem(data);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(AppConstants.ProductName, "Startup failed: " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            BatchLinkExternalEvent?.Dispose();
            return Result.Succeeded;
        }

        private static void TrySetIcons(PushButtonData data)
        {
            // LargeImage = the 96px source scaled to 32 DIPs; Image = the 32px source scaled to 16
            // DIPs. Mirrors WSPICT: load the EMBEDDED PNGs via GetManifestResourceStream — pack://
            // application URIs do not resolve during Revit add-in startup, which is why the icon
            // never showed. Failures are logged (see GetHiResRibbonIcon), not silently swallowed.
            var large = GetHiResRibbonIcon(AppConstants.IconResource96, 32);
            var small = GetHiResRibbonIcon(AppConstants.IconResource32, 16);
            if (large != null) data.LargeImage = large;
            if (small != null) data.Image = small;
        }

        /// <summary>
        /// Load an embedded PNG via the assembly manifest and recreate it at a DPI that makes WPF
        /// treat it as targetDip × targetDip device-independent pixels, keeping all source pixels for
        /// sharp rendering on high-DPI screens. Mirrors WSPICT.App.GetHiResRibbonIcon.
        /// </summary>
        private static BitmapSource GetHiResRibbonIcon(string resourceName, int targetDip)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Diagnostics.Log.Warn("Ribbon icon resource not found: " + resourceName);
                        return null;
                    }

                    var decoder = new PngBitmapDecoder(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];

                    int pixelW = frame.PixelWidth;
                    int pixelH = frame.PixelHeight;
                    double scaledDpi = pixelW * 96.0 / targetDip;

                    int stride = (pixelW * frame.Format.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[stride * pixelH];
                    frame.CopyPixels(pixels, stride, 0);

                    var result = BitmapSource.Create(
                        pixelW, pixelH,
                        scaledDpi, scaledDpi,
                        frame.Format, frame.Palette,
                        pixels, stride);
                    result.Freeze();
                    return result;
                }
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Warn("Ribbon icon load failed (" + resourceName + "): " + ex.Message);
                return null;
            }
        }
    }
}
