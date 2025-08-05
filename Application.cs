using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace RevitRotateAddin
{
    /// <summary>
    /// Main application class for Revit Addin
    /// </summary>
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create Ribbon Panel
                RibbonPanel ribbonPanel = application.CreateRibbonPanel("Rotation Tools");

                // Create Push Button
                string thisAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                PushButtonData buttonData = new PushButtonData(
                    "RotateElements",
                    "Rotate 3D",
                    thisAssemblyPath,
                    "RevitRotateAddin.RotateElementsCommand");

                PushButton pushButton = ribbonPanel.AddItem(buttonData) as PushButton;
                pushButton.ToolTip = "Rotate selected objects around 3D axis";
                pushButton.LongDescription = "Select objects, input rotation angle and choose axis to rotate objects in 3D space";

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Startup Error", $"Cannot initialize application: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
