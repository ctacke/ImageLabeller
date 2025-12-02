namespace ImageLabeller.Models
{
    public class YoloAnnotation
    {
        public int ClassId { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public YoloAnnotation(int classId, double centerX, double centerY, double width, double height)
        {
            ClassId = classId;
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return $"{ClassId} {CenterX:F6} {CenterY:F6} {Width:F6} {Height:F6}";
        }

        public static YoloAnnotation? Parse(string line)
        {
            var parts = line.Trim().Split(' ');
            if (parts.Length >= 5 &&
                int.TryParse(parts[0], out int classId) &&
                double.TryParse(parts[1], out double cx) &&
                double.TryParse(parts[2], out double cy) &&
                double.TryParse(parts[3], out double w) &&
                double.TryParse(parts[4], out double h))
            {
                return new YoloAnnotation(classId, cx, cy, w, h);
            }
            return null;
        }
    }
}
