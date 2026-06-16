using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimLinkManager.Views;

namespace BimLinkManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BatchLinkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    message = "No active Revit document. Open a project first.";
                    return Result.Cancelled;
                }

                var doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                {
                    message = "BimLinkManager works on project documents only, not family documents.";
                    return Result.Cancelled;
                }

                var window = new MainWindow(commandData.Application);
                var helper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = Autodesk.Windows.ComponentManager.ApplicationWindow
                };

                // MUST be modeless (Show), NOT modal (ShowDialog). ShowDialog blocks the
                // Revit main thread in a nested WPF message pump until the window closes,
                // so Revit never returns to its own loop to service ExternalEvents — the
                // batch's App.BatchLinkExternalEvent.Raise() is then a silent no-op and
                // Execute never runs (the batch hangs forever). Mirrors WSPICT, which opens
                // its BatchLinkWindow with Show(). This was THE batch-hang bug — every
                // earlier fix chased ExternalEvent timing / the inner ProgressDialog, but
                // the real blocker was this outer window being modal.
                window.Show();

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Diagnostics.Log.Error("BatchLinkCommand failed", ex);
                return Result.Failed;
            }
        }
    }
}
