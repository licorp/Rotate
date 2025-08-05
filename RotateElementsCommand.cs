using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;
using System.Windows.Forms;

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

            Debug.WriteLine("[RotateAddin] === START EXECUTE ===");

            try
            {
                // Main loop to allow multiple rotations
                while (true)
                {
                    // Show interface to get rotation angle (form will check selection when Run is clicked)
                    Debug.WriteLine("[RotateAddin] Getting rotation angle from user");
                    double rotationAngle = GetRotationAngleFromUser();
                    Debug.WriteLine($"[RotateAddin] Rotation angle received: {rotationAngle} radians ({rotationAngle * 180 / Math.PI} degrees)");
                    
                    if (double.IsNaN(rotationAngle))
                    {
                        Debug.WriteLine("[RotateAddin] User cancelled - exiting tool");
                        return Result.Cancelled;
                    }

                    // Now check selected elements after user clicked Run
                    ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                    Debug.WriteLine($"[RotateAddin] Selected elements count: {selectedIds.Count}");
                    
                    if (selectedIds.Count == 0)
                    {
                        Debug.WriteLine("[RotateAddin] No elements selected, continuing to wait for selection");
                        continue; // Go back to show form again without any dialog
                    }

                // Check if selected elements can be rotated
                List<ElementId> rotatableIds = new List<ElementId>();
                List<string> nonRotatableTypes = new List<string>();
                
                foreach (ElementId id in selectedIds)
                {
                    Element element = doc.GetElement(id);
                    if (element != null)
                    {
                        // Check if element can be rotated (has location and is not pinned)
                        if (element.Location != null && !element.Pinned)
                        {
                            rotatableIds.Add(id);
                            Debug.WriteLine($"[RotateAddin] Element {element.Id} ({element.Category?.Name}) can be rotated");
                        }
                        else
                        {
                            string reason = element.Location == null ? "no location" : "pinned";
                            nonRotatableTypes.Add($"{element.Category?.Name} ({reason})");
                            Debug.WriteLine($"[RotateAddin] Element {element.Id} ({element.Category?.Name}) cannot be rotated: {reason}");
                        }
                    }
                }
                
                if (rotatableIds.Count == 0)
                {
                    Debug.WriteLine("[RotateAddin] No rotatable elements found");
                    TaskDialog.Show("Cannot Rotate / Không thể xoay", 
                        $"None of the selected elements can be rotated.\nKhông có đối tượng nào được chọn có thể xoay.\n\nNon-rotatable: {string.Join(", ", nonRotatableTypes)}");
                    continue; // Go back to form instead of exiting
                }
                
                if (nonRotatableTypes.Count > 0)
                {
                    Debug.WriteLine($"[RotateAddin] Some elements cannot be rotated: {string.Join(", ", nonRotatableTypes)}");
                    TaskDialogResult continueResult = TaskDialog.Show("Partial Selection / Chọn một phần", 
                        $"Some elements cannot be rotated and will be skipped:\n{string.Join(", ", nonRotatableTypes)}\n\nContinue with {rotatableIds.Count} rotatable elements?\nTiếp tục với {rotatableIds.Count} đối tượng có thể xoay?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                    if (continueResult != TaskDialogResult.Yes)
                    {
                        continue; // Go back to form instead of exiting
                    }
                }
                
                // Update selectedIds to only include rotatable elements
                selectedIds = rotatableIds;

                // Check if angle is effectively zero
                if (Math.Abs(rotationAngle) < 0.0001) // Less than ~0.006 degrees
                {
                    Debug.WriteLine("[RotateAddin] Rotation angle is too small, asking user");
                    TaskDialogResult zeroAngleResult = TaskDialog.Show("Small Angle / Góc nhỏ", 
                        "The rotation angle is very small (close to zero). Continue anyway?\nGóc xoay rất nhỏ (gần bằng 0). Vẫn tiếp tục?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                    if (zeroAngleResult != TaskDialogResult.Yes)
                    {
                        Debug.WriteLine("[RotateAddin] User declined small angle - continuing to next rotation");
                        continue; // Go back to start for next rotation
                    }
                }

                // 3. Select rotation axis (pipe or pipe fitting)
                Debug.WriteLine("[RotateAddin] Selecting rotation axis");
                Line rotationAxis = SelectRotationAxis(uiDoc);
                if (rotationAxis == null)
                {
                    Debug.WriteLine("[RotateAddin] User cancelled axis selection - continuing to next rotation");
                    continue; // Go back to start for next rotation
                }
                Debug.WriteLine($"[RotateAddin] Rotation axis: Start={rotationAxis.GetEndPoint(0)}, End={rotationAxis.GetEndPoint(1)}");

                // 4. Checkout elements if workshared
                if (doc.IsWorkshared)
                {
                    Debug.WriteLine("[RotateAddin] Document is workshared, checking out elements");
                    try
                    {
                        WorksharingUtils.CheckoutElements(doc, selectedIds);
                        Debug.WriteLine("[RotateAddin] Elements checked out successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RotateAddin] Checkout error: {ex.Message}");
                        TaskDialog.Show("Checkout Error", $"Cannot checkout elements: {ex.Message}");
                        return Result.Failed;
                    }
                }

                // 5. Perform rotation
                Debug.WriteLine("[RotateAddin] Starting rotation transaction");
                using (Transaction trans = new Transaction(doc, "Rotate objects around axis"))
                {
                    trans.Start();
                    Debug.WriteLine("[RotateAddin] Transaction started");
                    
                    try
                    {
                        ElementTransformUtils.RotateElements(doc, selectedIds, rotationAxis, rotationAngle);
                        Debug.WriteLine("[RotateAddin] RotateElements completed successfully");
                    }
                    catch (Exception rotateEx)
                    {
                        Debug.WriteLine($"[RotateAddin] RotateElements failed: {rotateEx.Message}");
                        trans.RollBack();
                        TaskDialog.Show("Rotation Error / Lỗi xoay", 
                            $"Failed to rotate elements: {rotateEx.Message}\nLỗi khi xoay đối tượng: {rotateEx.Message}");
                        return Result.Failed;
                    }
                    
                    trans.Commit();
                    Debug.WriteLine("[RotateAddin] Transaction committed");
                }

                // Ask user if direction is correct
                Debug.WriteLine("[RotateAddin] Asking user for direction confirmation");
                TaskDialogResult result = TaskDialog.Show(
                    "Confirmation / Xác nhận", 
                    "Objects have been rotated. Is the direction correct?\nCác đối tượng đã được xoay. Hướng có đúng không?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                Debug.WriteLine($"[RotateAddin] User confirmation result: {result}");

                if (result == TaskDialogResult.No)
                {
                    Debug.WriteLine("[RotateAddin] User wants to reverse rotation");
                    // Rotate back by rotating additional -2 * original angle
                    using (Transaction trans = new Transaction(doc, "Rotate reverse direction"))
                    {
                        trans.Start();
                        ElementTransformUtils.RotateElements(doc, selectedIds, rotationAxis, -2 * rotationAngle);
                        trans.Commit();
                        Debug.WriteLine("[RotateAddin] Reverse rotation completed");
                    }
                }

                // Rotation completed successfully - prepare for next rotation
                Debug.WriteLine("[RotateAddin] Rotation completed - preparing for next rotation");
                uiDoc.Selection.SetElementIds(new List<ElementId>());
                
                continue; // Go back to the start of the main loop
                } // End of main while loop
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Debug.WriteLine("[RotateAddin] Operation cancelled by user");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RotateAddin] ERROR: {ex.Message}");
                Debug.WriteLine($"[RotateAddin] Stack trace: {ex.StackTrace}");
                message = $"Error: {ex.Message}";
                return Result.Failed;
            }
        }

        // Static variables for form reuse
        private static RotateWindow _rotateWindow;
        private static bool _isWaitingForInput = false;
        private static double _receivedAngle = double.NaN;

        /// <summary>
        /// Get rotation angle from user using reusable WPF interface
        /// </summary>
        private double GetRotationAngleFromUser()
        {
            Debug.WriteLine("[RotateAddin] === GetRotationAngleFromUser START ===");
            
            // Tạo hoặc tái sử dụng window
            if (_rotateWindow == null)
            {
                Debug.WriteLine("[RotateAddin] Creating new RotateWindow");
                _rotateWindow = new RotateWindow();
                
                // Subscribe to rotation event
                _rotateWindow.RotationRequested += OnRotationRequested;
            }
            else
            {
                Debug.WriteLine("[RotateAddin] Reusing existing RotateWindow");
            }

            // Reset và show window
            _isWaitingForInput = true;
            _receivedAngle = double.NaN;
            
            Debug.WriteLine("[RotateAddin] Showing rotate window");
            Debug.WriteLine($"[RotateAddin] _isWaitingForInput reset to: {_isWaitingForInput}");
            Debug.WriteLine($"[RotateAddin] _receivedAngle reset to: {_receivedAngle}");
            _rotateWindow.ShowRotateWindow();

            // Wait for user input (simple polling)
            int timeout = 0;
            Debug.WriteLine("[RotateAddin] Starting polling loop");
            while (_isWaitingForInput && timeout < 300000) // 5 minutes timeout
            {
                System.Threading.Thread.Sleep(100);
                timeout += 100;
                
                // Process Windows messages
                System.Windows.Forms.Application.DoEvents();
                
                // Debug every 5 seconds
                if (timeout % 5000 == 0)
                {
                    Debug.WriteLine($"[RotateAddin] Polling... timeout: {timeout}ms, waiting: {_isWaitingForInput}, angle: {_receivedAngle}");
                }
            }

            Debug.WriteLine($"[RotateAddin] Polling ended - timeout: {timeout}ms, waiting: {_isWaitingForInput}, angle: {_receivedAngle}");

            if (!_isWaitingForInput && !double.IsNaN(_receivedAngle))
            {
                Debug.WriteLine($"[RotateAddin] Received angle: {_receivedAngle} degrees");
                
                // Show form again after processing
                _rotateWindow.ShowAgainAfterProcessing();
                
                // Convert to radians
                double radians = _receivedAngle * Math.PI / 180.0;
                Debug.WriteLine($"[RotateAddin] Returning angle: {radians} radians ({_receivedAngle} degrees)");
                return radians;
            }
            
            Debug.WriteLine("[RotateAddin] Timeout or cancelled");
            return double.NaN;
        }

        /// <summary>
        /// Event handler for rotation request
        /// </summary>
        private static void OnRotationRequested(object sender, RotationEventArgs e)
        {
            Debug.WriteLine($"[RotateAddin] === OnRotationRequested START ===");
            Debug.WriteLine($"[RotateAddin] OnRotationRequested: {e.AngleInDegrees} degrees");
            Debug.WriteLine($"[RotateAddin] Before update - _isWaitingForInput: {_isWaitingForInput}");
            Debug.WriteLine($"[RotateAddin] Before update - _receivedAngle: {_receivedAngle}");
            
            _receivedAngle = e.AngleInDegrees;
            _isWaitingForInput = false;
            
            Debug.WriteLine($"[RotateAddin] After update - _isWaitingForInput: {_isWaitingForInput}");
            Debug.WriteLine($"[RotateAddin] After update - _receivedAngle: {_receivedAngle}");
            Debug.WriteLine($"[RotateAddin] === OnRotationRequested END ===");
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

        /// <summary>
        /// Cleanup method để gọi khi application shutdown
        /// </summary>
        public static void Cleanup()
        {
            if (_rotateWindow != null)
            {
                _rotateWindow.Close();
                _rotateWindow = null;
            }
        }
    }
}