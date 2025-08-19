using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace RevitRotateAddin
{
    [Transaction(TransactionMode.Manual)]
    public class RibbonApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] OnStartup called");
                
                // Tìm tab "Licorp" có sẵn
                string tabName = "Licorp";
                System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Looking for tab: {tabName}");
                
                RibbonPanel panel = null;
                
                // Thử tìm tab "Licorp" và các panels trong đó
                try
                {
                    var panels = application.GetRibbonPanels(tabName);
                    System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Found {panels.Count} panels in {tabName} tab");
                    
                    foreach (var p in panels)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Panel in {tabName}: '{p.Name}'");
                    }
                    
                    // Sử dụng panel đầu tiên có sẵn hoặc tạo panel mới
                    if (panels.Count > 0)
                    {
                        panel = panels[0]; // Dùng panel đầu tiên
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Using existing panel: '{panel.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Error accessing Licorp tab: {ex.Message}");
                }
                
                // Nếu không tìm thấy panel hoặc tab, tạo panel mới trong tab Licorp
                if (panel == null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Creating new panel in {tabName} tab");
                        panel = application.CreateRibbonPanel(tabName, "Rotate Tools");
                        System.Diagnostics.Debug.WriteLine("[RibbonApplication] Panel 'Rotate Tools' created successfully");
                    }
                    catch (Exception ex)
                    {
                        // Nếu không tạo được trong tab Licorp, tạo tab mới
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Cannot create panel in Licorp tab: {ex.Message}");
                        tabName = "Licorp Tools";
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Creating new tab: {tabName}");
                        application.CreateRibbonTab(tabName);
                        panel = application.CreateRibbonPanel(tabName, "Rotate Tools");
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] New tab and panel created: {tabName}");
                    }
                }
                
                // Đường dẫn tới assembly hiện tại
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Assembly path: {assemblyPath}");
                
                // Tạo PushButtonData cho Rotate command
                PushButtonData buttonData = new PushButtonData(
                    "RotateElementsButton",
                    "Xoay đối tượng\nRotate Objects",
                    assemblyPath,
                    "RevitRotateAddin.RotateElementsCommand"
                );
                
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] PushButtonData created");
                
                // Thêm tooltip
                buttonData.ToolTip = "Xoay đối tượng quanh trục ống / Rotate objects around pipe axis";
                buttonData.LongDescription = "Chọn đối tượng trước, sau đó dùng công cụ này để xoay chúng quanh trục ống hoặc pipe fitting. " +
                                           "Bạn có thể chỉ định góc xoay và chọn trục xoay. / " +
                                           "Select objects first, then use this tool to rotate them around a pipe or pipe fitting axis. " +
                                           "You can specify the rotation angle and select the rotation axis.";
                
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] Tooltip and description set");
                
                // Thêm icon rotation
                try
                {
                    buttonData.LargeImage = CreateRotationIcon(32);
                    buttonData.Image = CreateRotationIcon(16);
                    System.Diagnostics.Debug.WriteLine("[RibbonApplication] Rotation icons created");
                }
                catch (Exception ex)
                {
                    // Fallback to text icon
                    try
                    {
                        buttonData.LargeImage = CreateTextIcon("ROT", 32);
                        buttonData.Image = CreateTextIcon("ROT", 16);
                        System.Diagnostics.Debug.WriteLine("[RibbonApplication] Fallback text icons created");
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine($"[RibbonApplication] All icon creation failed: {ex.Message}");
                    }
                }
                
                // Thêm button vào panel
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] Adding button to panel");
                PushButton button = panel.AddItem(buttonData) as PushButton;
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] Button added successfully");
                
                // Thêm help URL (optional)
                try
                {
                    button.SetContextualHelp(new ContextualHelp(
                        ContextualHelpType.Url, 
                        "https://licorp.com/help/rotate-elements"));
                    System.Diagnostics.Debug.WriteLine("[RibbonApplication] Help URL set");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Help URL failed: {ex.Message}");
                }
                
                System.Diagnostics.Debug.WriteLine("[RibbonApplication] OnStartup completed successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RibbonApplication] ERROR in OnStartup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[RibbonApplication] Stack trace: {ex.StackTrace}");
                TaskDialog.Show("Licorp Rotate Add-in Error", $"Failed to create ribbon: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
        
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        
        /// <summary>
        /// Tạo icon rotation với mũi tên xoay
        /// </summary>
        private BitmapSource CreateRotationIcon(int size)
        {
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Background
                var backgroundBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(44, 90, 160));
                drawingContext.DrawEllipse(backgroundBrush, null, 
                    new System.Windows.Point(size/2.0, size/2.0), size/2.5, size/2.5);
                
                // Rotation arrow
                var center = new System.Windows.Point(size/2.0, size/2.0);
                var radius = size/3.5;
                var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, size/16.0);
                
                // Draw arc
                var startAngle = -90;
                var sweepAngle = 270;
                var startRad = startAngle * Math.PI / 180;
                var endRad = (startAngle + sweepAngle) * Math.PI / 180;
                
                var startPoint = new System.Windows.Point(
                    center.X + radius * Math.Cos(startRad),
                    center.Y + radius * Math.Sin(startRad));
                var endPoint = new System.Windows.Point(
                    center.X + radius * Math.Cos(endRad),
                    center.Y + radius * Math.Sin(endRad));
                
                var geometry = new System.Windows.Media.PathGeometry();
                var figure = new System.Windows.Media.PathFigure();
                figure.StartPoint = startPoint;
                figure.Segments.Add(new System.Windows.Media.ArcSegment(
                    endPoint, new System.Windows.Size(radius, radius), 0, true, 
                    System.Windows.Media.SweepDirection.Clockwise, true));
                geometry.Figures.Add(figure);
                
                drawingContext.DrawGeometry(null, pen, geometry);
                
                // Arrow head
                var arrowSize = size / 8.0;
                var arrowBrush = System.Windows.Media.Brushes.White;
                var arrowPoints = new System.Windows.Media.PointCollection
                {
                    new System.Windows.Point(endPoint.X - arrowSize, endPoint.Y),
                    new System.Windows.Point(endPoint.X + arrowSize, endPoint.Y - arrowSize),
                    new System.Windows.Point(endPoint.X + arrowSize, endPoint.Y + arrowSize)
                };
                
                var arrowGeometry = new System.Windows.Media.PathGeometry();
                var arrowFigure = new System.Windows.Media.PathFigure();
                arrowFigure.StartPoint = arrowPoints[0];
                arrowFigure.Segments.Add(new System.Windows.Media.LineSegment(arrowPoints[1], true));
                arrowFigure.Segments.Add(new System.Windows.Media.LineSegment(arrowPoints[2], true));
                arrowFigure.IsClosed = true;
                arrowGeometry.Figures.Add(arrowFigure);
                
                drawingContext.DrawGeometry(arrowBrush, null, arrowGeometry);
                
                // Center dot
                var centerBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(74, 144, 226));
                drawingContext.DrawEllipse(centerBrush, null, center, size/16.0, size/16.0);
            }
            
            var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            return renderBitmap;
        }
        
        /// <summary>
        /// Tạo icon đơn giản từ text
        /// </summary>
        private BitmapSource CreateTextIcon(string text, int size)
        {
            var visual = new System.Windows.Controls.TextBlock()
            {
                Text = text,
                FontSize = size * 0.4,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Blue,
                Background = System.Windows.Media.Brushes.White,
                Width = size,
                Height = size,
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            
            visual.Measure(new System.Windows.Size(size, size));
            visual.Arrange(new System.Windows.Rect(0, 0, size, size));
            
            var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            
            return renderBitmap;
        }
    }
}
