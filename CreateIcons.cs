using System;
using System.Drawing;
using System.Drawing.Drawing2D;

class Program
{
    static void Main()
    {
        CreateRotationIcon("RotateIcon32.png", 32);
        CreateRotationIcon("RotateIcon16.png", 16);
        Console.WriteLine("Icons created: RotateIcon32.png and RotateIcon16.png");
    }
    
    static void CreateRotationIcon(string filePath, int size)
    {
        using (var bitmap = new Bitmap(size, size))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            
            float center = size / 2f;
            float radius = size * 0.35f;
            
            // Background circle
            using (var brush = new SolidBrush(Color.FromArgb(44, 90, 160)))
            {
                graphics.FillEllipse(brush, center - radius, center - radius, radius * 2, radius * 2);
            }
            
            // Rotation arrow
            using (var pen = new Pen(Color.White, Math.Max(1, size / 16f)))
            {
                var rect = new RectangleF(center - radius * 0.8f, center - radius * 0.8f, 
                                        radius * 1.6f, radius * 1.6f);
                graphics.DrawArc(pen, rect, -90, 270);
            }
            
            // Arrow head
            using (var brush = new SolidBrush(Color.White))
            {
                var arrowSize = size / 8f;
                var arrowX = center - radius * 0.8f;
                var arrowY = center - radius * 0.2f;
                
                PointF[] arrowPoints = {
                    new PointF(arrowX - arrowSize, arrowY),
                    new PointF(arrowX + arrowSize, arrowY - arrowSize),
                    new PointF(arrowX + arrowSize, arrowY + arrowSize)
                };
                graphics.FillPolygon(brush, arrowPoints);
            }
            
            // Center dot
            using (var brush = new SolidBrush(Color.FromArgb(74, 144, 226)))
            {
                var dotSize = size / 8f;
                graphics.FillEllipse(brush, center - dotSize/2, center - dotSize/2, dotSize, dotSize);
            }
            
            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
