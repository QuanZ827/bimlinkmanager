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
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var asmName = asm.GetName().Name;

                BitmapImage Load(string size)
                {
                    var uri = new Uri("pack://application:,,,/" + asmName + ";component/Resources/icon_" + size + ".png", UriKind.Absolute);
                    try
                    {
                        return new BitmapImage(uri);
                    }
                    catch
                    {
                        return null;
                    }
                }

                var img16 = Load("16");
                var img32 = Load("32");
                if (img16 != null) data.Image = img16;
                if (img32 != null) data.LargeImage = img32;
            }
            catch
            {
            }
        }
    }
}
