using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RevitRotateAddin
{
    /// <summary>
    /// Filter to select elements that can be used as rotation axis (pipes and pipe fittings)
    /// </summary>
    public class AxisSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Check if element is a pipe with linear curve
            if (elem is Pipe pipe && pipe.Location is LocationCurve locationCurve && 
                locationCurve.Curve is Line)
            {
                return true;
            }
            
            // Check if element is a pipe fitting that has a linear connector axis
            if (elem.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
            {
                // Check if pipe fitting has connectors that can define an axis
                if (elem is FamilyInstance familyInstance)
                {
                    var connectorManager = familyInstance.MEPModel?.ConnectorManager;
                    if (connectorManager != null)
                    {
                        var connectors = connectorManager.Connectors.Cast<Connector>().ToList();
                        if (connectors.Count >= 2)
                        {
                            // If we have at least 2 connectors, we can create an axis
                            return true;
                        }
                    }
                }
            }
            
            // Check if element has Location.Curve that is a Line (for other linear elements)
            if (elem?.Location is LocationCurve locCurve && locCurve.Curve is Line)
            {
                return true;
            }
            
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Command to rotate selected objects around an axis
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RotateElementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Show interface to get rotation angle first
                double rotationAngle = GetRotationAngleFromUser();
                if (double.IsNaN(rotationAngle))
                {
                    return Result.Cancelled;
                }

                // 2. Check selected elements
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Notification / Thông báo", 
                        "Please select at least one object to rotate.\nVui lòng chọn ít nhất một đối tượng để xoay.");
                    return Result.Cancelled;
                }

                // 3. Select rotation axis (pipe or pipe fitting)
                Line rotationAxis = SelectRotationAxis(uiDoc);
                if (rotationAxis == null)
                {
                    return Result.Cancelled;
                }

                // 4. Checkout elements if workshared
                if (doc.IsWorkshared)
                {
                    try
                    {
                        WorksharingUtils.CheckoutElements(doc, selectedIds);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Checkout Error", $"Cannot checkout elements: {ex.Message}");
                        return Result.Failed;
                    }
                }

                // 5. Perform rotation
                using (Transaction trans = new Transaction(doc, "Rotate objects around axis"))
                {
                    trans.Start();
                    ElementTransformUtils.RotateElements(doc, selectedIds, rotationAxis, rotationAngle);
                    trans.Commit();
                }

                // 6. Ask user if direction is correct
                TaskDialogResult result = TaskDialog.Show(
                    "Confirmation / Xác nhận", 
                    "Objects have been rotated. Is the direction correct?\nCác đối tượng đã được xoay. Hướng có đúng không?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (result == TaskDialogResult.No)
                {
                    // Rotate back by rotating additional -2 * original angle
                    using (Transaction trans = new Transaction(doc, "Rotate reverse direction"))
                    {
                        trans.Start();
                        ElementTransformUtils.RotateElements(doc, selectedIds, rotationAxis, -2 * rotationAngle);
                        trans.Commit();
                    }
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"Error: {ex.Message}";
                return Result.Failed;
            }
        }

        /// <summary>
        /// Get rotation angle from user using WPF interface
        /// </summary>
        private double GetRotationAngleFromUser()
        {
            var window = new RotateWindow();
            if (window.ShowDialog() == true && window.IsOKClicked)
            {
                // Convert from degrees to radians
                return window.AngleInDegrees * Math.PI / 180.0;
            }
            return double.NaN;
        }

        /// <summary>
        /// Allow user to select rotation axis (pipes or pipe fittings)
        /// </summary>
        private Line SelectRotationAxis(UIDocument uiDoc)
        {
            try
            {
                Reference reference = uiDoc.Selection.PickObject(
                    ObjectType.Element, 
                    new AxisSelectionFilter(), 
                    "Select a pipe or pipe fitting as rotation axis / Chọn ống hoặc phụ kiện ống làm trục xoay");

                Element axisElement = uiDoc.Document.GetElement(reference);
                
                // Handle pipes with linear location curve
                if (axisElement is Pipe pipe && pipe.Location is LocationCurve locationCurve && 
                    locationCurve.Curve is Line line)
                {
                    return line;
                }
                
                // Handle pipe fittings - create axis from connectors
                if (axisElement.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                {
                    if (axisElement is FamilyInstance familyInstance)
                    {
                        var connectorManager = familyInstance.MEPModel?.ConnectorManager;
                        if (connectorManager != null)
                        {
                            var connectors = connectorManager.Connectors.Cast<Connector>().ToList();
                            if (connectors.Count >= 2)
                            {
                                // Create axis from first two connectors
                                XYZ point1 = connectors[0].Origin;
                                XYZ point2 = connectors[1].Origin;
                                
                                // Check if points are different enough to create a line
                                if (point1.DistanceTo(point2) > 0.001) // 1mm tolerance
                                {
                                    return Line.CreateBound(point1, point2);
                                }
                            }
                            
                            // If we can't create axis from connectors, try to use fitting's location
                            if (axisElement.Location is LocationPoint locationPoint)
                            {
                                // For fittings at location point, we'll create a vertical axis
                                XYZ center = locationPoint.Point;
                                XYZ point1 = new XYZ(center.X, center.Y, center.Z - 1);
                                XYZ point2 = new XYZ(center.X, center.Y, center.Z + 1);
                                return Line.CreateBound(point1, point2);
                            }
                        }
                    }
                }
                
                // Handle other linear elements
                if (axisElement?.Location is LocationCurve locCurve && locCurve.Curve is Line linearCurve)
                {
                    return linearCurve;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled
            }
            
            return null;
        }
    }
}