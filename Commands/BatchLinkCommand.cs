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
                window.ShowDialog();

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
